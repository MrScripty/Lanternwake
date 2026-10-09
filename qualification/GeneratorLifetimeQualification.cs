#if DEBUG
using Godot;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Exercises generator custody without taking an extra native stream reference.</summary>
public partial class GeneratorLifetimeQualification : Node
{
    private int _checks;
    private static bool Alive(WeakRef reference)
    {
        // Inspect the native Variant type without recreating a managed stream
        // wrapper. IsInstanceIdValid calls InstanceFromId in C# and takes a ref.
        using var value = reference.GetRef();
        return value.VariantType != Variant.Type.Nil;
    }
    private void Check(bool value, string claim)
    {
        if (!value) throw new InvalidOperationException("Generator lifetime: " + claim);
        _checks++;
    }
    public override async void _Ready()
    {
        AudioDirector? director = null;
        bool quittingTree = false;
        try
        {
            AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), true);
            for (int cycle = 0; cycle < 12; cycle++)
            {
                director = GD.Load<PackedScene>("res://Scenes/Audio/AudioDirector.tscn").Instantiate<AudioDirector>();
                AddChild(director);
                var stream = director.Music.Stream!;
                using var reference = GodotObject.WeakRef(stream) ?? throw new InvalidOperationException("Missing generator weak reference");
                Check(stream is AudioStreamGenerator && director.Music.HasStreamPlayback(), "real generator starts");
                director.ShowLocation("harbor");
                director.ShowLocation("archive");
                director.ShowLocation("tide_cave");
                if (OS.GetCmdlineUserArgs().Contains("--quit-whole-tree"))
                {
                    GD.Print("LANTERNWAKE_GENERATOR_TREE_QUIT_READY");
                    quittingTree = true;
                    GetTree().Quit(); return;
                }
                if (cycle % 3 == 0)
                {
                    director.StopPlayback();
                    Check(Alive(reference), "Stop preserves native generator through final mix");
                    var settlement = director.StopAndRetireAsync();
                    if (cycle == 3)
                    {
                        Check(!settlement.IsCompleted, "release is pending before forced teardown");
                        director.Free();
                    }
                    Check(await settlement, "native release drains within existing two seconds, including pending-stop teardown");
                    var releaseDeadline = Time.GetTicksMsec() + 2000;
                    while (Alive(reference) && Time.GetTicksMsec() < releaseDeadline)
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Check(!Alive(reference), "graceful retirement releases native generator");
                    if (GodotObject.IsInstanceValid(director))
                        Check(await director.StopAndRetireAsync(), "repeated retirement succeeds");
                    else Check(!director.MusicWorkerRunning, "pending-stop teardown joins synthesis");
                    if (GodotObject.IsInstanceValid(director)) director.Free();
                }
                else
                {
                    if (cycle % 3 == 1) director.Free();
                    else { RemoveChild(director); director.Free(); }
                    Check(Alive(reference), "forced exit retains native generator beyond node lifetime");
                    var deadline = Time.GetTicksMsec() + 2000;
                    while (Alive(reference) && Time.GetTicksMsec() < deadline)
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Check(!Alive(reference), "native playback retirement releases forced-exit generator");
                }
                director = null;
            }
            GD.Print($"LANTERNWAKE_GENERATOR_LIFETIME_OK cycles=12 checks={_checks} graceful=3 pending_stop_exit=1 freed=4 detached=4");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { if (!quittingTree && GodotObject.IsInstanceValid(director)) director!.Free(); }
    }
}
#endif
