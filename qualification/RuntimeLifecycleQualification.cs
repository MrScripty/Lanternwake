#if DEBUG
using Godot;
using Lanternwake.Audio;
using Lanternwake.Core;
using Lanternwake.Presentation;
using System.Reflection;

namespace Lanternwake.Qualification;

/// <summary>Exercises real game roots; reflection only selects canonical test checkpoints.</summary>
public partial class RuntimeLifecycleQualification : Node
{
    private GameView? _game;
    private int _checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private)!.GetValue(owner)!;
    private void Check(bool value, string claim)
    {
        if (!value) throw new InvalidOperationException("Runtime lifecycle: " + claim);
        _checks++;
    }
    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static int Tracked(AudioDirector audio) => Read<List<AudioStreamPlayback>>(audio, "_playbacks").Count;
    private static bool SettledHarbor(AudioStreamPlayer player, AudioStreamWav harbor) =>
        player.Playing && player.Stream is AudioStreamWav loop &&
        loop.MixRate == harbor.MixRate && loop.Format == harbor.Format && loop.Stereo == harbor.Stereo &&
        loop.Data.AsSpan().SequenceEqual(harbor.Data) && float.IsFinite(player.VolumeLinear) &&
        Math.Abs(player.VolumeLinear - 1) < .001;

    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_RUNTIME_FIXTURE") ?? "";
            Check(Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")) &&
                ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal),
                "owned disposable profile required before instantiating the real game");
            var scene = GD.Load<PackedScene>("res://Scenes/Main.tscn");
            var story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            var traversal = new StorySession(story);
            var checkpoints = new Dictionary<string, SaveData>();
            do
            {
                checkpoints.TryAdd(traversal.Scene.Location, traversal.Snapshot());
                if (traversal.Beat.Activity is { } activity) traversal.AnswerActivity(activity.CorrectIndex);
            } while (traversal.Advance());
            Check(checkpoints.Count == 5, "checkpoints derive from all five canonical locations");
            var eq = Enumerable.Range(0, AudioServer.GetBusEffectCount(AudioServer.GetBusIndex("Music")))
                .Select(i => AudioServer.GetBusEffect(AudioServer.GetBusIndex("Music"), i)).OfType<AudioEffectEQ>().Single();
            float baseline2k = eq.GetBandGainDb(6), baseline4k = eq.GetBandGainDb(7);

            for (int cycle = 0; cycle < 4; cycle++)
            {
                bool missing = cycle == 3;
                _game = scene.Instantiate<GameView>();
                if (missing) { _game.Audio.Music.Stream = null; _game.Audio.Effects.Stream = null; }
                AddChild(_game);
                await Frames(2);
                var menu = Read<Control>(_game, "_mainMenu");
                Check(menu.Visible && !_game.InterfaceRoot.Visible, "fresh game shows the authored startup menu");
                Check(Read<SessionStorage>(_game, "_storage").Mode == SessionMode.Normal, "real Main uses normal storage in the owned profile");
                var audio = _game.Audio;
                var synth = Read<BufferedMusicSynth?>(audio, "_musicSynth");
                Check(missing ? synth is null && !audio.Music.HasStreamPlayback() : synth?.WorkerRunning == true && audio.Music.HasStreamPlayback(),
                    "music acquisition matches available streams");
                ulong musicId = missing ? 0 : audio.Music.GetStreamPlayback().GetInstanceId();
                menu.GetNode<Button>("%MenuNewGame").EmitSignal(BaseButton.SignalName.Pressed);
                Check(!menu.Visible && _game.InterfaceRoot.Visible && Read<StorySession>(_game, "_session").Beat.Id == story.Chapters[0].Scenes[0].Beats[0].Id,
                    "the actual New story button starts the first authored beat");
                foreach (var location in new[] { "archive", "keeper_house", "lantern_room", "tide_cave", "harbor", "archive", "harbor" })
                {
                    Read<StorySession>(_game, "_session").Restore(checkpoints[location]);
                    typeof(GameView).GetMethod("RenderBeat", Private)!.Invoke(_game, [false]);
                    await Frames(1);
                    Check(audio.CurrentLocation == location && audio.CurrentMusicEnvironment == location,
                        "production render selects matching ambience and score: " + location);
                    Check(missing ? !audio.Music.HasStreamPlayback() : audio.Music.GetStreamPlayback().GetInstanceId() == musicId && synth!.WorkerRunning,
                        "scene changes preserve one music playback and live worker, or remain safely silent");
                }
                await ToSignal(GetTree().CreateTimer(.75), SceneTreeTimer.SignalName.Timeout);
                var active = audio.AmbienceA.Playing ? audio.AmbienceA : audio.AmbienceB;
                Check(audio.AmbienceA.Playing != audio.AmbienceB.Playing && SettledHarbor(active, audio.Harbor),
                    "rapid transitions settle to exactly one harbor loop at full local gain");
                if (cycle == 0)
                {
                    active.VolumeLinear = 0;
                    Check(active.Playing && !SettledHarbor(active, audio.Harbor), "settled assertion rejects a playing but silent harbor loop");
                    active.VolumeLinear = 1;
                    var wrong = new AudioStreamPlayer { Stream = audio.Archive, VolumeLinear = 1 };
                    AddChild(wrong);
                    wrong.Play();
                    Check(wrong.Playing && !SettledHarbor(wrong, audio.Harbor), "settled assertion rejects a full-gain wrong-location loop");
                    wrong.Stop();
                    wrong.Free();
                    GD.Print("LANTERNWAKE_RUNTIME_NEGATIVE_PROBES_OK silent=true wrong_stream=true");
                }
                audio.ApplyBeatCue("owned-exit-" + cycle, "bell_lowered", true);
                audio.SetDialogueActive(true);
                await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
                Check(audio.SpeechBlend > .8, "teardown begins during an active speech-ducking envelope");
                Check(Tracked(audio) >= 1, "ambience playback is owned before teardown");

                if (cycle is 0 or 3)
                {
                    Check(await audio.StopAndRetireAsync() && Tracked(audio) == 0, "graceful stop drains every native playback");
                    Check(await audio.StopAndRetireAsync(), "graceful stop is repeatable");
                    _game.Free();
                }
                else if (cycle == 1)
                {
                    _game.QueueFree();
                    await Frames(2);
                }
                else
                {
                    RemoveChild(_game);
                    Check(Tracked(audio) == 0 && !audio.MusicWorkerRunning, "tree removal releases playback ownership and joins synthesis");
                    _game.Free();
                }
                Check(!GodotObject.IsInstanceValid(_game) && synth?.WorkerRunning != true, "freed game leaves no synthesis worker");
                Check(Math.Abs(eq.GetBandGainDb(6) - baseline2k) < .001 && Math.Abs(eq.GetBandGainDb(7) - baseline4k) < .001,
                    "teardown restores shared music EQ before the next startup");
                _game = null;
                await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
                Check(!Directory.EnumerateFiles(ProjectSettings.GlobalizePath("user://"), "*.json").Any(),
                    "startup, checkpoint rendering and teardown write no saves or AI settings");
            }
            GD.Print($"LANTERNWAKE_RUNTIME_LIFECYCLE_OK cycles=4 checks={_checks} real_menu=4 locations=28 graceful=2 queued=1 detached=1 missing_streams=1");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            if (GodotObject.IsInstanceValid(_game))
            {
                await _game!.Audio.StopAndRetireAsync();
                _game.Free(); _game = null;
                await Frames(2);
            }
            GetTree().Quit(1);
        }
        finally { if (GodotObject.IsInstanceValid(_game)) _game!.Free(); }
    }
}
#endif
