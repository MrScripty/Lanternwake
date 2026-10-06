using Godot;
using Lanternwake.Core;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private async void RunAudioSmoke()
    {
        AudioEffectCapture? capture = null;
        var master = AudioServer.GetBusIndex("Master");
        var effectIndex = AudioServer.GetBusEffectCount(master);
        try
        {
            int count = 0;
            void Check(bool condition, string claim) { if (!condition) throw new InvalidOperationException("Audio smoke: " + claim); count++; }
            async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
            Check(_storage!.Mode == SessionMode.AutomatedTest, "audio tests own disposable saves");
            Check(Audio.CurrentLocation == "harbor", "title starts harbor ambience");
            Check(Audio.Music.Playing && Audio.Music.Bus == "Music", "music player is running on its authored bus");
            Check(Audio.Effects.Bus == "Effects" && Audio.AmbienceA.Bus == "Ambience" && Audio.AmbienceB.Bus == "Ambience", "all players use their independent routes");
            await Wait(.75);
            var current = Audio.AmbienceA.Playing ? Audio.AmbienceA : Audio.AmbienceB;
            var stream = (AudioStreamWav)current.Stream;
            Check(stream.LoopMode == AudioStreamWav.LoopModeEnum.Forward && stream.LoopEnd == 384000, "runtime loop spans the 16-second resource");
            Check(Audio.Harbor.LoopMode == AudioStreamWav.LoopModeEnum.Disabled, "shared imported resource is unchanged");
            var playback = current.GetStreamPlayback();
            Audio.ShowLocation("harbor");
            Check(current.GetStreamPlayback() == playback, "repeated location does not restart playback");
            foreach (var location in new[] { "archive", "keeper_house", "tide_cave", "lantern_room", "harbor" }) Audio.ShowLocation(location);
            await Wait(.75);
            Check(Audio.CurrentLocation == "harbor" && Audio.AmbienceA.Playing != Audio.AmbienceB.Playing, "rapid interrupted transitions settle to exactly one player");
            Check(Math.Abs((Audio.AmbienceA.Playing ? Audio.AmbienceA : Audio.AmbienceB).VolumeLinear - 1) < .001, "final transition reaches full local gain");
            Audio.SetLevel("Music", -.5f); Check(AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")), "zero music level is explicitly silent");
            Audio.SetLevel("Music", 2); Check(Audio.GetLevel("Music") == 1 && !AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")), "positive level unmutes and clamps");
            try { Audio.SetLevel("Music", float.NaN); throw new Exception("NaN admitted"); } catch (ArgumentOutOfRangeException) { count++; }
            try { Audio.SetLevel("Master", .5f); throw new Exception("unknown channel admitted"); } catch (ArgumentException) { count++; }
            Audio.SetMuted(true); Check(Audio.Muted, "master mute takes effect"); Audio.SetMuted(false); Check(!Audio.Muted, "master unmute takes effect");
            Audio.Effects.Stop(); Audio.ApplyBeatCue("loaded-cue", "bell_lowered", false);
            Check(!Audio.Effects.Playing, "loading a cue beat does not replay a historic event");
            Audio.ApplyBeatCue("new-cue", "bell_lowered", true); Check(Audio.Effects.Playing, "authored bell release plays");
            var effectPlayback = Audio.Effects.GetStreamPlayback();
            Audio.ApplyBeatCue("new-cue", "bell_lowered", true); Check(Audio.Effects.GetStreamPlayback() == effectPlayback, "rerendering a cue beat does not restart it");
            Audio.ApplyBeatCue("new-cue", "bell_lowered", false);
            Check(!Audio.Effects.Playing, "loading the same cue beat cancels its previous live one-shot");
            Audio.ApplyBeatCue("new-cue", "bell_lowered", true);
            Check(!Audio.Effects.Playing, "rerender after load does not revive the historical cue");
            Audio.ApplyBeatCue("later-cue", "bell_lowered", true);
            Audio.ApplyBeatCue("earlier-beat", null, false);
            Check(!Audio.Effects.Playing, "loading a different beat cancels the old timeline effect");
            SetReadingSize(150);
            ShowSettings();
            _modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(button => button.Text == "Sound settings").EmitSignal(Button.SignalName.Pressed);
            Check(_modal is not null && _modal.Title == "Sound settings", "reading settings retains the Sound settings action");
            var controls = _modal!.GetNode<VBoxContainer>("%ModalActions").GetNode<VBoxContainer>("AudioSettingsControls");
            Check(controls.IsVisibleInTree(), "sound controls are visible inside the modal");
            Check(_modal.GetNode<ScrollContainer>("%ModalActionsScroll").Visible, "sound settings reveals the composed modal scroll owner");
            Check(controls.GetNode<Label>("MusicLabel").GetThemeFontSize("font_size") == (int)Math.Round(GD.Load<Theme>("res://Scenes/UI/LanternwakeTheme.tres").DefaultFontSize * 1.5, MidpointRounding.AwayFromZero), "sound labels honor current reading size");
            controls.GetNode<HSlider>("Ambience").Value = 25;
            Check(Math.Abs(Audio.GetLevel("Ambience") - .25) < .001, "visible slider controls production ambience gain");
            controls.GetNode<CheckButton>("Mute").ButtonPressed = true;
            Check(Audio.Muted, "visible mute controls production master bus");
            CloseModal(); ShowAudioSettings();
            controls = _modal!.GetNode<VBoxContainer>("%ModalActions").GetNode<VBoxContainer>("AudioSettingsControls");
            Check(controls.GetNode<CheckButton>("Mute").ButtonPressed && controls.GetNode<HSlider>("Ambience").Value == 25, "reopening settings reflects live state");
            SetReadingSize(100); CloseModal(); Audio.SetMuted(false);
            Audio.SetLevel("Music", .35f); Audio.SetLevel("Ambience", .55f);
            capture = new AudioEffectCapture { BufferLength = 1 };
            AudioServer.AddBusEffect(master, capture);
            await Wait(.5);
            var frames = capture.GetBuffer(capture.GetFramesAvailable());
            // Dummy backend may expose player state without running an output mixer.
            if (frames.Length > 0)
            {
                var peak = frames.Max(frame => Math.Max(Math.Abs(frame.X), Math.Abs(frame.Y)));
                Check(peak > .00001 && peak < 1, "actual mixer produces bounded nonzero PCM");
                GD.Print($"LANTERNWAKE_AUDIO_PCM_OK frames={frames.Length} peak={peak:F6} driver={AudioServer.GetDriverName()}");
            }
            else GD.Print("LANTERNWAKE_AUDIO_PCM_UNAVAILABLE output backend produced no capture frames");
            Check(await Audio.StopAndRetireAsync(), "audio server releases all owned native playback handles");
            Check(!Audio.Music.Playing && !Audio.Effects.Playing && !Audio.AmbienceA.Playing && !Audio.AmbienceB.Playing, "shutdown stops every owned player");
            Audio.ShowLocation("archive"); Audio.ApplyBeatCue("late-cue", "bell_lowered", true);
            Check(!Audio.Effects.Playing && !Audio.AmbienceA.Playing && !Audio.AmbienceB.Playing, "late callbacks cannot restart stopped audio");
            GD.Print($"LANTERNWAKE_AUDIO_OK assertions={count}"); QuitAfterAudio();
        }
        catch (Exception error) { GD.PushError(error.ToString()); QuitAfterAudio(1); }
        finally { if (capture is not null) { AudioServer.RemoveBusEffect(master, effectIndex); capture.Dispose(); } }
    }
}
