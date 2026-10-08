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
            Check(Audio.Dialogue.Bus == "Dialogue", "future character voices have their own route");
            Check(Audio.Music.Stream is AudioStreamGenerator generator && generator.MixRate == 32000,
                "music is generated from MIDI rather than imported WAVs");
            Check(Audio.MusicWorkerRunning && Audio.StemCount == 31, "the bundled synth advances all score voices on its worker");
            var musicPlayback = Audio.Music.GetStreamPlayback();
            await Wait(.5);
            int baselineUnderruns = Audio.MusicUnderruns;
            long framesBefore = Audio.MusicFramesRendered;
            var musicFade = Audio.MusicFadeSeconds;
            Audio.MusicFadeSeconds = .05;
            int localMusicBus = AudioServer.GetBusIndex("Music");
            int localCaptureIndex = AudioServer.GetBusEffectCount(localMusicBus);
            var previewCatalog = MusicCatalog.Load(Audio.MusicCatalogPath);
            using (var localCapture = new AudioEffectCapture { BufferLength = .5f })
            {
                AudioServer.AddBusEffect(localMusicBus, localCapture);
                try
                {
                    foreach (var location in new[] { "harbor", "keeper_house", "archive", "lantern_room", "tide_cave" })
                    {
                        var scene = _story.Chapters.SelectMany(chapter => chapter.Scenes).First(scene => scene.Location == location);
                        _session = StoryContextPreview.CreateSessionAtBeat(_story, scene.Beats[0].Id);
                        _started = true; RenderBeat(false);
                        GD.Print("LANTERNWAKE_MUSIC_PREVIEW " + System.Text.Json.JsonSerializer.Serialize(new
                        {
                            location, scene_id = scene.Id, mix_title = Audio.CurrentMusicMix!.Title,
                            stems = Enumerable.Range(0, Audio.StemCount).ToDictionary(index => previewCatalog.Stems[index].Name, index => Audio.GetTargetStemLevel(index))
                        }));
                        // Isolate each environment using normal authored mix controls.
                        var spot = Audio.Score.Spots.First(spot => spot.SceneId == scene.Id && spot.StartAtBeatId.Length == 0);
                        var original = spot.Mix;
                        using var solo = new MusicMix { Ground = 0, Theme = 0, Bowed = 0, Motion = 0, Environment = .85f, Character = 0, Journey = 0 };
                        try
                        {
                            spot.Mix = solo; RenderBeat(false);
                            await Wait(1.2); localCapture.ClearBuffer(); await Wait(.25);
                            var pcm = localCapture.GetBuffer(localCapture.GetFramesAvailable());
                            Check(pcm.Length > 0 && Math.Sqrt(pcm.Average(frame => (frame.X * frame.X + frame.Y * frame.Y) / 2.0)) > .0001,
                                "the live synth audibly renders the selected location: " + location);
                        }
                        finally { spot.Mix = original; }
                    }
                    Audio.ApplyStoryMusic("ch1_s4", "archive", "ch1", new[] { "ch1_s4_b001", "ch1_s4_b005" });
                    await Wait(1.2); localCapture.ClearBuffer(); await Wait(.2);
                    var silence = localCapture.GetBuffer(localCapture.GetFramesAvailable());
                    Check(silence.Length > 0 && silence.Max(frame => Math.Max(Math.Abs(frame.X), Math.Abs(frame.Y))) < .00005,
                        "recording-space silence reaches the actual generator output after the fade and reflection tails");
                }
                finally { AudioServer.RemoveBusEffect(localMusicBus, localCaptureIndex); }
            }
            Check(Audio.MusicFramesRendered > framesBefore && Audio.Music.GetStreamPlayback() == musicPlayback,
                "all location changes preserve one live synthesis sample clock");
            Audio.ShowTitleMusic(immediate: true); Audio.ShowLocation("harbor");
            Audio.MusicFadeSeconds = musicFade;
            var scenes = _story.Chapters.SelectMany(chapter => chapter.Scenes).ToDictionary(scene => scene.Id);
            Check(Audio.Score.Spots.Count(spot => spot.StartAtBeatId.Length == 0) == scenes.Count, "every authored scene has an opening mix");
            foreach (var spot in Audio.Score.Spots)
                Check(scenes.TryGetValue(spot.SceneId, out var scene) && (spot.StartAtBeatId.Length == 0 || scene.Beats.Any(beat => beat.Id == spot.StartAtBeatId)),
                    "music spotting references a real scene and beat: " + spot.SceneId + "/" + spot.StartAtBeatId);
            Check(Audio.Score.Resolve("ch5_s3", new[] { "ch5_s3_b001", "ch5_s3_b006", "ch5_s3_b010" }).Title == "Receiving ends",
                "release mix persists beyond its starting beat, including load/preview");
            Check(Audio.Score.Resolve("ch5_s3", new[] { "ch5_s3_b001", "ch5_s3_b006", "ch5_s3_b013", "ch5_s3_b020" }).Suite == MusicMix.SuiteKind.OpenHorizon,
                "a later authored change supersedes the release mix");
            Check(Audio.Score.Resolve("ch5_s3", new[] { "ch5_s3_b001" }).Suite == MusicMix.SuiteKind.NightLedger,
                "rewinding reconstructs the earlier mix without retaining later cues");
            Check(Audio.Score.Resolve("future-unconfigured-scene", Array.Empty<string>()) == Audio.Score.TitleMix, "new unspotted scenes have a safe musical fallback");
            Check(Audio.Score.ResolveFocus("ch1_s4", new[] { "ch1_s4_b001", "ch1_s4_b015", "ch1_s4_b020" }) == MusicSpot.FocusKind.Tomas,
                "character focus persists across beats rather than following the current speaker");
            Check(Audio.Score.ResolveFocus("ch1_s4", new[] { "ch1_s4_b001" }) == MusicSpot.FocusKind.Ivo,
                "rewinding reconstructs Ivo's earlier recorded-presence focus");
            foreach (var chapter in _story.Chapters)
                foreach (var scene in chapter.Scenes)
                {
                    Audio.ApplyStoryMusic(scene.Id, scene.Location, chapter.Id, new[] { scene.Beats[0].Id });
                    Check(Audio.GetTargetStemLevel(Audio.GetLayerSlot("environment", scene.Location)) > 0,
                        "every scene selects its actual environment theme: " + scene.Id);
                    Check(Audio.GetTargetStemLevel(Audio.GetLayerSlot("journey", chapter.Id)) > 0,
                        "every scene selects its chapter's melodic development: " + scene.Id);
                }
            Audio.ApplyStoryMusic("ch1_s2a", "keeper_house", "ch1", new[] { "ch1_s2a_b001" });
            Check(Audio.CurrentCharacterTheme == "ada" && Audio.GetTargetStemLevel(Audio.GetLayerSlot("character", "ada")) > 0,
                "a character-led scene brings its authored motif into the mix");
            int houseSlot = Audio.GetLayerSlot("environment", "keeper_house"), adaSlot = Audio.GetLayerSlot("character", "ada");
            float houseLevel = Audio.GetTargetStemLevel(houseSlot), adaLevel = Audio.GetTargetStemLevel(adaSlot);
            Audio.ApplyStoryMusic("ch5_s5", "keeper_house", "ch5", new[] { "ch5_s5_b001" });
            Check(Audio.GetTargetStemLevel(houseSlot) == houseLevel && Audio.GetTargetStemLevel(adaSlot) == adaLevel &&
                  Audio.GetTargetStemLevel(Audio.GetLayerSlot("journey", "ch1")) == 0 && Audio.GetTargetStemLevel(Audio.GetLayerSlot("journey", "ch5")) > 0,
                "returning to the same place and person changes the journey rather than losing their identity");
            Audio.ApplyStoryMusic("ch1_s4", "archive", "ch1", new[] { "ch1_s4_b001", "ch1_s4_b005" });
            Check(Enumerable.Range(0, Audio.StemCount).All(index => Audio.GetTargetStemLevel(index) == 0),
                "recording-space spots silence every layer, including new character and journey music");
            Check(Audio.Music.GetStreamPlayback() == musicPlayback, "all environment, character and chapter changes preserve the native music clock");
            musicFade = Audio.MusicFadeSeconds;
            Audio.MusicFadeSeconds = .15;
            Audio.ApplyStoryMusic("ch5_s3", "lantern_room", "ch5", new[] { "ch5_s3_b001" });
            await Wait(.2);
            Check(Audio.GetStemLevel(8) > 0 && Math.Abs(Audio.GetStemLevel(8) - Audio.GetTargetStemLevel(8)) < .001 && Audio.GetStemLevel(0) == 0,
                "a suite transition settles to the authored stem levels");
            Audio.ApplyStoryMusic("ch5_s3", "lantern_room", "ch5", new[] { "ch5_s3_b001", "ch5_s3_b006" });
            Audio.ShowTitleMusic();
            Audio.ApplyStoryMusic("ch5_s3", "lantern_room", "ch5", new[] { "ch5_s3_b001", "ch5_s3_b006", "ch5_s3_b013" });
            await Wait(.2);
            Check(Audio.GetStemLevel(12) > 0 && Math.Abs(Audio.GetStemLevel(12) - Audio.GetTargetStemLevel(12)) < .001 && Audio.GetStemLevel(8) == 0 && Audio.GetStemLevel(0) == 0,
                "rapid mix requests settle to only the latest arrangement");
            Check(Audio.CurrentCharacterTheme == "ada" && Audio.GetStemLevel(Audio.GetLayerSlot("character", "ada")) > .5,
                "the latest beat's character focus also survives interrupted fades");
            Audio.ApplyStoryMusic("ch5_s3", "lantern_room", "ch5", new[] { "ch5_s3_b001", "ch5_s3_b006", "ch5_s3_b013" });
            Check(Audio.Music.GetStreamPlayback() == musicPlayback, "mix changes, repeated beats and loads retain one uninterrupted music playback");
            Audio.ShowTitleMusic(immediate: true); Audio.MusicFadeSeconds = musicFade;
            var musicBus = AudioServer.GetBusIndex("Music");
            var eq = (AudioEffectEQ10)AudioServer.GetBusEffect(musicBus, 0);
            var compressor = (AudioEffectCompressor)AudioServer.GetBusEffect(musicBus, 1);
            float baselinePresence = eq.GetBandGainDb(6), baselineGain = Audio.Music.VolumeDb, busLevel = Audio.GetLevel("Music");
            Check(compressor.Sidechain == "Dialogue" && compressor.Ratio > 1, "native compressor listens to the Dialogue bus");
            Check(eq.GetBandCount() == 10 && baselinePresence == -2, "editable music EQ has a speech-presence pocket");
            // Synthetic voiced-band fixture only; no character recording or microphone is needed.
            var voiceBytes = new byte[32000 * 2 * 3];
            for (int i = 0; i < voiceBytes.Length / 2; i++)
            {
                double t = i / 32000.0;
                short sample = (short)(32767 * .3 * Math.Sin(Math.Tau * 180 * t) * (.7 + .3 * Math.Sin(Math.Tau * 4 * t)));
                voiceBytes[2 * i] = (byte)(sample & 255); voiceBytes[2 * i + 1] = (byte)((sample >> 8) & 255);
            }
            using var voice = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 32000, Data = voiceBytes };
            var probeBytes = new byte[32000 * 2 * 3];
            for (int i = 0; i < probeBytes.Length / 2; i++)
            {
                short sample = (short)(32767 * .12 * Math.Sin(Math.Tau * 440 * i / 32000));
                probeBytes[2 * i] = (byte)(sample & 255); probeBytes[2 * i + 1] = (byte)((sample >> 8) & 255);
            }
            using var probeClip = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 32000, Data = probeBytes };
            var probe = new AudioStreamPlayer { Stream = probeClip, Bus = "Music" };
            using var musicCapture = new AudioEffectCapture { BufferLength = 1 };
            int musicCaptureIndex = AudioServer.GetBusEffectCount(musicBus);
            AudioServer.AddBusEffect(musicBus, musicCapture);
            AddChild(probe); Audio.Music.StreamPaused = true; probe.Play();
            try
            {
                await Wait(.3);
                var beforeVoice = musicCapture.GetBuffer(musicCapture.GetFramesAvailable());
                musicCapture.ClearBuffer();
                Audio.PlayDialogue(voice);
                await Wait(.4);
                var withVoice = musicCapture.GetBuffer(musicCapture.GetFramesAvailable());
                if (beforeVoice.Length > 0 && withVoice.Length > 0)
                {
                    double Rms(Vector2[] pcm) => Math.Sqrt(pcm.Average(frame => (frame.X * frame.X + frame.Y * frame.Y) / 2.0));
                    double beforeRms = Rms(beforeVoice), duringRms = Rms(withVoice);
                    Check(beforeRms > .001 && duringRms < beforeRms * .75, "real Dialogue-bus samples trigger native sidechain reduction on the Music bus");
                    GD.Print($"LANTERNWAKE_DIALOGUE_SIDECHAIN_PCM_OK before_rms={beforeRms:F6} during_rms={duringRms:F6}");
                }
                else GD.Print("LANTERNWAKE_DIALOGUE_SIDECHAIN_PCM_UNAVAILABLE output backend produced no capture frames");
            }
            finally
            {
                probe.Stop(); probe.QueueFree(); Audio.StopDialogue(); Audio.Music.StreamPaused = false;
                AudioServer.RemoveBusEffect(musicBus, musicCaptureIndex);
            }
            Audio.PlayDialogue(voice);
            await Wait(.25);
            Check(Audio.Dialogue.Playing && Audio.SpeechBlend > .9 && Audio.Music.VolumeDb < baselineGain - 2, "actual Dialogue playback smoothly ducks music");
            Check(eq.GetBandGainDb(6) < baselinePresence - 2 && Audio.GetLevel("Music") == busLevel, "speech opens an EQ pocket without changing the user's music setting");
            Audio.ApplyBeatCue("voice-timeline-load", null, false);
            Check(!Audio.Dialogue.Playing, "loading another beat cancels stale dialogue playback");
            await Wait(1.6);
            Check(Audio.SpeechBlend < .1 && Math.Abs(eq.GetBandGainDb(6) - baselinePresence) < .3, "music EQ and gain recover after speech");
            Audio.SetLevel("Dialogue", 0); Audio.SetDialogueActive(true);
            await Wait(.15);
            Check(Audio.SpeechBlend < .1, "muted voices do not duck music");
            Audio.SetDialogueActive(false); Audio.SetLevel("Dialogue", 1);
            GD.Print("LANTERNWAKE_ADAPTIVE_MUSIC_OK arrangements=19 stems=31 scenes=41 spots=" + Audio.Score.Spots.Count);
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
            controls.GetNode<HSlider>("Dialogue").Value = 65;
            Check(Math.Abs(Audio.GetLevel("Dialogue") - .65) < .001, "voice volume control is already wired for future playback");
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
            GD.Print($"LANTERNWAKE_LIVE_MUSIC_OK voices={Audio.StemCount} frames={Audio.MusicFramesRendered} underruns={Audio.MusicUnderruns - baselineUnderruns}");
            Check(Audio.MusicUnderruns == baselineUnderruns, "live synthesis maintains a full output buffer during scene and UI changes");
            Check(await Audio.StopAndRetireAsync(), "audio server releases all owned native playback handles");
            Check(!Audio.MusicWorkerRunning, "shutdown joins the synthesis worker");
            Check(!Audio.Music.Playing && !Audio.Effects.Playing && !Audio.AmbienceA.Playing && !Audio.AmbienceB.Playing && !Audio.Dialogue.Playing, "shutdown stops every owned player");
            Audio.ShowLocation("archive"); Audio.ApplyBeatCue("late-cue", "bell_lowered", true);
            Audio.PlayDialogue(voice); Audio.ShowTitleMusic(); Audio.SetDialogueActive(true);
            Check(!Audio.Effects.Playing && !Audio.AmbienceA.Playing && !Audio.AmbienceB.Playing && !Audio.Dialogue.Playing && !Audio.Music.Playing, "late callbacks cannot restart stopped audio");
            GD.Print($"LANTERNWAKE_AUDIO_OK assertions={count}"); QuitAfterAudio();
        }
        catch (Exception error) { GD.PushError(error.ToString()); QuitAfterAudio(1); }
        finally { if (capture is not null) { AudioServer.RemoveBusEffect(master, effectIndex); capture.Dispose(); } }
    }
}
