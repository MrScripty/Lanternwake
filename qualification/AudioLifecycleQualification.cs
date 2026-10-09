#if DEBUG
using Godot;
using Lanternwake.Presentation;
using System.Reflection;

namespace Lanternwake.Qualification;

/// <summary>Native regression for absent streams and owned playback release.</summary>
public partial class AudioLifecycleQualification : Node
{
    private int _assertions;
    private AudioStreamWav _loop = null!;
    private readonly List<AudioStreamWav> _effects = new();
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Audio lifecycle: " + claim);
        _assertions++;
    }
    private static int Tracked(AudioDirector director) =>
        ((List<AudioStreamPlayback>)typeof(AudioDirector).GetField("_playbacks", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(director)!).Count;

    private AudioDirector Create(bool missingMusic = false, bool missingEffects = false)
    {
        AudioStreamWav? effect = null;
        if (!missingEffects)
        {
            effect = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 24000, Data = new byte[480] };
            _effects.Add(effect);
        }
        var director = GD.Load<PackedScene>("res://Scenes/Audio/AudioDirector.tscn").Instantiate<AudioDirector>();
        if (missingMusic) director.Music.Stream = null;
        director.Effects.Stream = effect;
        director.Harbor = _loop; director.Archive = _loop; director.KeeperHouse = _loop;
        director.LanternRoom = _loop; director.TideCave = _loop;
        director.FadeSeconds = .05;
        AddChild(director); // Runs the production _Ready, including music acquisition.
        return director;
    }

    public override async void _Ready()
    {
        AudioDirector? director = null;
        try
        {
            _loop = GD.Load<AudioStreamWav>("res://Assets/Audio/harbor.wav");
            director = Create(missingMusic: true, missingEffects: true);
            Check(!director.Music.HasStreamPlayback(), "missing music has no native playback");
            director._Process(0); // Before the fix this throws the owner's line 138 exception.
            Check(Tracked(director) == 0, "missing music never enters retirement ownership");
            director.ApplyBeatCue("missing-effect", "bell_lowered", true);
            director._Process(0);
            Check(!director.Effects.HasStreamPlayback() && Tracked(director) == 0, "missing effect never enters retirement ownership");
            director.Harbor = null!;
            try { director.ShowLocation("harbor"); throw new Exception("Missing ambience accepted"); }
            catch (InvalidOperationException error)
            {
                Check(error.Message.Contains("scripts/setup.sh") && error.Message.Contains("scripts/setup_audio.py"), "missing ambience explains fresh-checkout audio setup");
            }
            director.Harbor = _loop;
            director.ShowLocation("harbor");
            Check(Tracked(director) == 1, "valid ambience still plays with missing music and effects");
            Check(await director.StopAndRetireAsync(), "missing-stream shutdown drains valid ambience");
            Check(await director.StopAndRetireAsync(), "shutdown is repeatable");
            director.Free(); director = null;

            director = Create(missingMusic: true);
            director.ApplyBeatCue("effect-without-music", "bell_lowered", true);
            Check(Tracked(director) == 1 && director.Effects.HasStreamPlayback(), "missing music alone preserves valid effects");
            Check(await director.StopAndRetireAsync(), "missing-music-only shutdown drains effects");
            director.Free(); director = null;
            director = Create(missingEffects: true);
            director.ApplyBeatCue("music-without-effect", "bell_lowered", true);
            director._Process(0);
            Check(Tracked(director) == 1 && director.Music.HasStreamPlayback(), "missing effects alone preserve music ownership");
            Check(await director.StopAndRetireAsync(), "missing-effects-only shutdown drains music");
            director.Free(); director = null;

            director = Create();
            director.ShowLocation("harbor");
            Check(Tracked(director) == 2, "music and ambience each retain an owned playback");
            director._Process(0);
            Check(Tracked(director) == 2, "processing retains handles still owned by native players");
            director.ApplyBeatCue("short-effect", "bell_lowered", true);
            Check(Tracked(director) == 3, "one-shot acquires an owned playback");
            var deadline = Time.GetTicksMsec() + 2000;
            while (Tracked(director) > 2 && Time.GetTicksMsec() < deadline)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!director.Effects.Playing && Tracked(director) == 2, "naturally finished effect retires while loops remain owned");
            director.PlayDialogue(_effects[0]);
            Check(director.Dialogue.HasStreamPlayback() && Tracked(director) == 3, "dialogue acquires its own native playback");
            deadline = Time.GetTicksMsec() + 2000;
            while (Tracked(director) > 2 && Time.GetTicksMsec() < deadline)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!director.Dialogue.Playing && Tracked(director) == 2, "naturally finished dialogue retires without releasing active loops");
            director.PlayDialogue(_loop);
            director.StopDialogue();
            Check(director.Dialogue.Stream is null && !director.Dialogue.HasStreamPlayback(), "stopped dialogue clears its stream and native player ownership");
            director._Process(0);
            director.ApplyBeatCue("effect-before-stream-removal", "bell_lowered", true);
            director.Effects.Stream = null;
            director.ApplyBeatCue("effect-after-stream-removal", "bell_lowered", true);
            Check(!director.Effects.HasStreamPlayback(), "an effect whose stream was removed stays inactive on replay");
            director._Process(0);
            foreach (var location in new[] { "archive", "tide_cave", "harbor" }) director.ShowLocation(location);
            director.ApplyBeatCue("stopped-effect", "bell_lowered", true);
            director.ApplyBeatCue("loaded-effect", null, false);
            Check(await director.StopAndRetireAsync() && Tracked(director) == 0, "interrupted transitions and stopped effect drain every native handle");
            director.Free(); director = null;

            director = Create(missingMusic: true, missingEffects: true);
            director.ApplyBeatCue("missing-at-exit", "bell_lowered", true);
            director.Free();
            Check(!GodotObject.IsInstanceValid(director), "tree exit with absent streams completes without null disposal");
            director = null;
            director = Create();
            director.ShowLocation("harbor");
            director.ApplyBeatCue("active-at-exit", "bell_lowered", true);
            director.PlayDialogue(_loop);
            director.Free();
            Check(!GodotObject.IsInstanceValid(director), "tree exit stops and disposes active playback ownership");
            director = null;
            // Direct exit releases our handles immediately; the mixer cleans its
            // stopped handles on subsequent frames before the fixture quits.
            await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
            GD.Print($"LANTERNWAKE_AUDIO_LIFECYCLE_OK assertions={_assertions}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally
        {
            director?.Free();
            foreach (var effect in _effects) effect.Dispose();
            _loop?.Dispose();
        }
    }
}
#endif
