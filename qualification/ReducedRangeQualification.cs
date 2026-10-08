#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Real Main controls and native PCM, using only an owned deterministic tone.</summary>
public partial class ReducedRangeQualification : Node
{
    private GameView _game = null!;
    private int _checks;
    private T Observe<T>(string name) => (T)typeof(GameView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_game)!;
    private Window Modal => Observe<Window>("_modal");
    private VBoxContainer Controls => Modal.GetNode<VBoxContainer>("%ModalActions").GetNode<VBoxContainer>("AudioSettingsControls");
    private Control Menu => Observe<Control>("_mainMenu");
    private string Snapshot() => JsonSerializer.Serialize(Observe<StorySession>("_session").Snapshot(), Story.Json);
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Reduced range: " + claim);
        _checks++;
    }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Wait(double seconds)
    {
        var end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (Time.GetTicksMsec() < end) await Frame();
    }
    private async Task Keypress(Window window, Key key)
    {
        window.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        window.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Frame();
    }
    private void Close() => Modal.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
    private void Action(string text) => Modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == text).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task OpenSound(bool title)
    {
        if (title) Menu.GetNode<Button>("%MenuSound").EmitSignal(BaseButton.SignalName.Pressed);
        else { NativeGameControls.Press(_game, "SettingsButton"); Action("Sound settings"); }
        await Frame(); await Frame();
        Check(Modal.Title == "Sound settings" && Controls.IsVisibleInTree(), "actual Main sound settings exposes composed controls");
    }
    private async Task<(double Quiet, double Loud)> Measure(AudioStreamPlayer probe, AudioEffectCapture capture, bool reduced)
    {
        _game.Audio.SetReducedDynamicRange(reduced);
        async Task<double> Rms(float volume)
        {
            probe.VolumeLinear = volume;
            // Let the compressor envelope settle, including release after a loud sample.
            await Wait(1.5); capture.ClearBuffer(); await Wait(.25);
            var frames = capture.GetBuffer(capture.GetFramesAvailable());
            Check(frames.Length > 1000, "native output mixer supplies captured PCM");
            return Math.Sqrt(frames.Average(f => ((double)f.X * f.X + (double)f.Y * f.Y) / 2));
        }
        return (await Rms(.03f), await Rms(.8f));
    }
    public override async void _Ready()
    {
        AudioStreamPlayer? probe = null;
        AudioStreamWav? tone = null;
        AudioEffectCapture? capture = null;
        int captureIndex = -1;
        int master = AudioServer.GetBusIndex("Master");
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_RANGE_FIXTURE") ?? "";
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")), "owned debug fixture required");
            Check(ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "all userdata stays in private fixture");
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frame();
            Check(!_game.Audio.ReducedDynamicRange, "fresh process uses original uncompressed mix");
            await OpenSound(true);
            Check(!Controls.GetNode<CheckButton>("ReducedRange").ButtonPressed, "fresh visible control starts off");
            if (System.Environment.GetEnvironmentVariable("LANTERNWAKE_RANGE_MODE") == "fresh")
            {
                GD.Print($"LANTERNWAKE_REDUCED_RANGE_FRESH_OK checks={_checks}"); Close();
                _game._Notification((int)NotificationWMCloseRequest); return;
            }
            var state = Snapshot();
            var levels = new[] { "Music", "Ambience", "Effects", "Dialogue" }.ToDictionary(n => n, _game.Audio.GetLevel);
            var mute = Controls.GetNode<CheckButton>("Mute");
            Check(mute.HasFocus(), "initial sound focus remains on mute");
            await Keypress(Modal, Key.Tab);
            var toggle = Controls.GetNode<CheckButton>("ReducedRange");
            Check(toggle.HasFocus(), "Tab reaches reduced-range toggle");
            await Keypress(Modal, Key.Space);
            Check(toggle.ButtonPressed && _game.Audio.ReducedDynamicRange, "native keyboard Space enables master processing immediately");
            Check(levels.All(p => Math.Abs(_game.Audio.GetLevel(p.Key) - p.Value) < .0001) && !_game.Audio.Muted, "range selection preserves channel levels and mute");
            mute.ButtonPressed = true;
            Check(_game.Audio.Muted && _game.Audio.ReducedDynamicRange, "mute and range reduction coexist independently");
            mute.ButtonPressed = false;
            Controls.GetNode<HSlider>("Dialogue").Value = 65;
            Check(Math.Abs(_game.Audio.GetLevel("Dialogue") - .65) < .001 && _game.Audio.ReducedDynamicRange, "per-channel volume remains adjustable with reduction enabled");
            Check(Snapshot() == state, "sound choice leaves story, evidence and transcript intact");
            Close(); await OpenSound(true);
            Check(Controls.GetNode<CheckButton>("ReducedRange").ButtonPressed, "reopening reflects enabled session setting"); Close();
            Menu.GetNode<Button>("%MenuNewGame").EmitSignal(BaseButton.SignalName.Pressed); await Frame();
            Check(_game.InterfaceRoot.Visible && Observe<bool>("_started") && _game.Audio.ReducedDynamicRange, "actual New story retains sound choice for session");
            state = Snapshot(); await OpenSound(false);
            Check(Controls.GetNode<CheckButton>("ReducedRange").ButtonPressed, "in-story reading settings exposes same sound state");
            // Existing reading-size controls must also size the added label.
            Close(); NativeGameControls.Press(_game, "SettingsButton"); Action("Larger reading text"); Action("Larger reading text"); Action("Sound settings");
            await Frame(); await Frame();
            toggle = Controls.GetNode<CheckButton>("ReducedRange");
            var theme = GD.Load<Theme>("res://Scenes/UI/LanternwakeTheme.tres");
            Check(toggle.GetThemeFontSize("font_size") == (int)Math.Round(theme.DefaultFontSize * 1.5, MidpointRounding.AwayFromZero), "new control honors enlarged reading text");
            toggle.GrabFocus(); await Keypress(Modal, Key.Space);
            Check(!toggle.ButtonPressed && !_game.Audio.ReducedDynamicRange && Snapshot() == state, "keyboard can restore original mix without story mutation"); Close();
            Check(await _game.Audio.StopAndRetireAsync(), "real Main generated audio drains before isolated PCM measurement");
            var absentMusic = GD.Load<PackedScene>("res://Scenes/Audio/AudioDirector.tscn").Instantiate<AudioDirector>();
            absentMusic.Music.Stream = null; AddChild(absentMusic);
            absentMusic.SetReducedDynamicRange(true);
            Check(absentMusic.ReducedDynamicRange && !absentMusic.Music.HasStreamPlayback(), "range control remains available with missing optional music");
            absentMusic.SetReducedDynamicRange(false);
            Check(!absentMusic.ReducedDynamicRange && await absentMusic.StopAndRetireAsync(), "missing-music range toggle and shutdown restore original mix cleanly");
            absentMusic.Free();
            // Native capture after the master effect: no external audio or inference.
            var data = new byte[32000 * 2];
            for (int i = 0; i < 32000; i++)
            {
                short value = (short)(Math.Sin(i * Math.Tau * 440 / 32000) * 16000);
                data[i * 2] = (byte)value; data[i * 2 + 1] = (byte)(value >> 8);
            }
            tone = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 32000, Data = data,
                LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopBegin = 0, LoopEnd = 32000 };
            probe = new AudioStreamPlayer { Stream = tone, Bus = "Effects" }; AddChild(probe);
            _game.Audio.SetLevel("Effects", 1); probe.Play();
            capture = new AudioEffectCapture { BufferLength = 2 };
            captureIndex = AudioServer.GetBusEffectCount(master); AudioServer.AddBusEffect(master, capture);
            var original = await Measure(probe, capture, false);
            var reduced = await Measure(probe, capture, true);
            Check(original.Quiet > .0001 && original.Loud > original.Quiet * 20, "original mix preserves deterministic tone contrast");
            Check(reduced.Quiet > original.Quiet * .9 && reduced.Quiet < original.Quiet * 1.1, "quiet input retains its level without gain boost");
            Check(reduced.Loud < original.Loud * .6 && reduced.Loud > reduced.Quiet, "enabled master processing softens loud input");
            Check(reduced.Loud / reduced.Quiet < original.Loud / original.Quiet * .65, "measured loud/quiet ratio is reduced");
            var restored = await Measure(probe, capture, false);
            Check(Math.Abs(restored.Loud / original.Loud - 1) < .05 && Math.Abs(restored.Quiet / original.Quiet - 1) < .05, "disabling restores original measured output");
            var musicBus = AudioServer.GetBusIndex("Music");
            Check(AudioServer.GetBusEffect(musicBus, 1) is AudioEffectCompressor { Sidechain: var sidechain } && sidechain == "Dialogue" && AudioServer.IsBusEffectEnabled(musicBus, 1), "original speech sidechain stays enabled");
            _game.Audio.SetReducedDynamicRange(true);
            Check(_game.Audio.ReducedDynamicRange, "first process exits with reduction enabled, before fresh-process default check");
            GD.Print("LANTERNWAKE_REDUCED_RANGE_OK " + JsonSerializer.Serialize(new { checks = _checks,
                original = new { quiet = original.Quiet, loud = original.Loud },
                reduced = new { quiet = reduced.Quiet, loud = reduced.Loud },
                restored = new { quiet = restored.Quiet, loud = restored.Loud }, driver = AudioServer.GetDriverName() }));
            probe.Stop(); probe.Free(); probe = null; await Wait(.2);
            AudioServer.RemoveBusEffect(master, captureIndex); captureIndex = -1; capture.Dispose(); capture = null;
            tone.Dispose(); tone = null;
            _game._Notification((int)NotificationWMCloseRequest);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            if (_game is not null) await _game.Audio.StopAndRetireAsync();
            GetTree().Quit(1);
        }
        finally
        {
            if (probe is not null && GodotObject.IsInstanceValid(probe)) { probe.Stop(); probe.Free(); }
            if (captureIndex >= 0) AudioServer.RemoveBusEffect(master, captureIndex);
            capture?.Dispose(); tone?.Dispose();
        }
    }
}
#endif
