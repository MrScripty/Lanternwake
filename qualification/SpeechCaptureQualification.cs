#if DEBUG
using Godot;
using Lanternwake.Conversation;
using Lanternwake.Core;
using Lanternwake.Presentation;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Lanternwake.Qualification;

/// <summary>Owned synthetic generator and controlled generic HTTP contract; never a microphone/Pumas qualification.</summary>
public partial class SpeechCaptureQualification : Node
{
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private)!.GetValue(owner)!;
    private static object? Call(object owner, string method, params object[] arguments) => owner.GetType().GetMethod(method, Private)!.Invoke(owner, arguments);
    private GameView? _game;
    private SpeechRecorder _recorder = null!;
    private FixtureContract _contract = new();
    private string _root = "";
    private int _checks;
    private bool _rendered, _deviceFailure;
    private ulong _lastTelemetry;
    private readonly List<(SpeechSampleBuffer Buffer, StereoSample[] Storage)> _buffers = new();
    private void Check(bool condition, string claim)
    {
        _checks++; if (!condition) throw new InvalidOperationException("Owned synthetic speech: " + claim);
    }
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> complete)
    {
        var end = Time.GetTicksMsec() + 5000;
        while (!complete() && Time.GetTicksMsec() < end) await Frames();
        Check(complete(), "bounded native fixture wait completes");
    }
    private void ObserveBuffer()
    {
        var buffer = Read<SpeechSampleBuffer?>(_recorder, "_samples");
        if (buffer is { Count: > 0 } && !_buffers.Any(pair => pair.Buffer == buffer))
            _buffers.Add((buffer, Read<StereoSample[]>(buffer, "_frames")));
    }
    private void PressMic() => Read<Button>(_game!, "_mic").EmitSignal(BaseButton.SignalName.Pressed);
    private void Return() => Call(_game!, "ReturnToStory");
    private void Open() => Call(_game!, "OpenConversation");
    private async Task Consent(bool start)
    {
        PressMic(); await Until(() => Read<ConfirmationDialog?>(_game!, "_microphoneConsent") is not null);
        var consent = Read<ConfirmationDialog>(_game!, "_microphoneConsent");
        consent.EmitSignal(start ? ConfirmationDialog.SignalName.Confirmed : ConfirmationDialog.SignalName.Canceled);
        if (start) await Until(() => _recorder.Recording);
        await Frames(2);
    }
    public override async void _Ready()
    {
        try
        {
            _root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_SPEECH_FIXTURE") ?? "";
            Check(Path.IsPathFullyQualified(_root) && File.Exists(Path.Combine(_root, "owned-fixture")) &&
                ProjectSettings.GlobalizePath("user://").StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "marked owned profile required");
            var admission = SpeechRuntimeAdmission.ForOwnedFixture(_root);
            Check(!admission.Allows(SpeechCaptureKind.Microphone), "owned permit can never authorize microphone input");
            var transcriber = new PumasSpeechTranscriber(admission, SpeechCaptureKind.OwnedSynthetic,
                settings => new(settings, new FixtureHandler(_contract), TimeSpan.FromSeconds(2)));
            _recorder = new(admission, SpeechCaptureKind.OwnedSynthetic, owner =>
            {
                if (_deviceFailure) throw new InvalidOperationException("Owned test device unavailable. No microphone was accessed.");
                return new GodotSpeechCaptureSource(owner, admission, SpeechCaptureKind.OwnedSynthetic);
            }, transcriber);
            var story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            var session = new StorySession(story);
            while (session.Beat.Conversation is null)
            {
                if (session.Beat.Activity is { } activity) session.AnswerActivity(activity.CorrectIndex);
                if (!session.Advance()) throw new InvalidOperationException("Conversation fixture unavailable.");
            }
            using (var storage = new SessionStorage(SessionMode.Normal, ProjectSettings.GlobalizePath("user://"), story)) storage.Write(false, session.Snapshot());
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>();
            Read<SpeechRecorder>(_game, "_speech").Dispose();
            typeof(GameView).GetField("_speech", Private)!.SetValue(_game, _recorder);
            AddChild(_game);
            _recorder.Configure(new(true, Model: "owned-synthetic-audio") { Profile = "fixture-audio", Language = "en" });
            _rendered = System.Environment.GetEnvironmentVariable("LANTERNWAKE_SPEECH_RENDERED") == "1";
            if (_rendered)
            {
                _game.AddChild(new Label { Text = "OWNED SYNTHETIC AUDIO · NO MICROPHONE · NO INFERENCE", Position = new(24, 4), ZIndex = 100 });
                GD.Print("LANTERNWAKE_OWNED_SPEECH_RENDERED_READY"); return;
            }
            Call(_game, "Load", false); Open(); await Frames(2);
            var buses = AudioServer.BusCount; var entry = Read<LineEdit>(_game, "_entry");
            entry.Text = "Keep this typed draft";
            GetWindow().Size = new(640, 480); await Frames(2);
            PressMic(); await Until(() => Read<ConfirmationDialog?>(_game, "_microphoneConsent") is not null);
            var narrowConsent = Read<ConfirmationDialog>(_game, "_microphoneConsent");
            Check(narrowConsent.Size.X <= 640 && narrowConsent.GetLabel().GetLineCount() > 1, "consent wraps and fits the actual 640px viewport");
            narrowConsent.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            GetWindow().Size = new(1280, 800); await Frames(2);
            Check(!_recorder.Recording && AudioServer.BusCount == buses && _contract.Posts == 0 && entry.Text == "Keep this typed draft", "consent cancellation captures/sends nothing and keeps draft");
            PressMic(); await Until(() => Read<ConfirmationDialog?>(_game, "_microphoneConsent") is not null);
            var stale = Read<ConfirmationDialog>(_game, "_microphoneConsent");
            Call(_game, "Load", false); stale.EmitSignal(ConfirmationDialog.SignalName.Confirmed); await Frames(3);
            Check(!_recorder.Recording && AudioServer.BusCount == buses && _contract.Posts == 0, "Load invalidates queued stale confirmation before resource acquisition");
            Open(); PressMic(); await Until(() => Read<ConfirmationDialog?>(_game, "_microphoneConsent") is not null);
            _contract.HoldGet = true;
            Read<ConfirmationDialog>(_game, "_microphoneConsent").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            await Until(() => _contract.HeldGet is not null);
            Call(_game, "Load", false); _contract.ReleaseGet();
            await Until(() => Read<OwnedOperations>(_game, "_operations").Count == 0);
            Check(!_recorder.Recording && AudioServer.BusCount == buses && _contract.Posts == 0, "Load during awaited post-consent recheck cannot start stale capture");
            Open(); await Consent(true); await Until(() => Read<SpeechSampleBuffer?>(_recorder, "_samples") is { Count: > 4096 }); ObserveBuffer();
            Check(AudioServer.BusCount == buses + 1 && _contract.Posts == 0, "consented generator uses real muted capture bus without audio submission");
            AudioServer.AddBus(1); AudioServer.SetBusName(1, "OwnedUnrelatedBus");
            Return(); await Frames(2);
            Check(AudioServer.GetBusIndex("OwnedUnrelatedBus") >= 0 && AudioServer.BusCount == buses + 1, "shifted-index cleanup preserves unrelated bus");
            AudioServer.RemoveBus(AudioServer.GetBusIndex("OwnedUnrelatedBus"));
            Check(_buffers.All(pair => pair.Buffer.Disposed && pair.Storage.All(f => f == default)) && !_recorder.HasRecording, "Return zeroes recorded backing arrays");
            Open(); await Consent(true); await Until(() => Read<SpeechSampleBuffer?>(_recorder, "_samples") is { Count: > 0 }); ObserveBuffer();
            typeof(GameView).GetField("_recordSeconds", Private)!.SetValue(_game, 30d);
            await Until(() => !_recorder.Recording);
            Check(_recorder.HasRecording && _contract.Posts == 0 && AudioServer.BusCount == buses && Read<Button>(_game, "_mic").Text == "Transcribe recording", "injected elapsed limit stops capture without sending and releases the bus");
            var history = JsonSerializer.Serialize(Read<StorySession>(_game, "_session").Snapshot());
            PressMic(); await Until(() => !_recorder.Pending && entry.Text == "OWNED SYNTHETIC TRANSCRIPT");
            Check(_contract.Posts == 1 && _contract.ValidStereo && entry.Editable && JsonSerializer.Serialize(Read<StorySession>(_game, "_session").Snapshot()) == history,
                "explicit action feeds actual generator PCM to generic adapter and only edits draft");
            Check(_buffers.All(pair => pair.Buffer.Disposed && pair.Storage.All(f => f == default)), "successful transcription zeroes transferred sample arrays");
            _deviceFailure = true; PressMic(); await Until(() => Read<ConfirmationDialog?>(_game, "_microphoneConsent") is not null);
            Read<ConfirmationDialog>(_game, "_microphoneConsent").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            await Until(() => !Read<Button>(_game, "_mic").Disabled);
            Check(!_recorder.Recording && AudioServer.BusCount == buses && entry.Editable && entry.Text == "OWNED SYNTHETIC TRANSCRIPT", "owned unavailable device restores typed draft with no bus leak");
            _deviceFailure = false;
            _contract.Available = false; PressMic(); await Until(() => !Read<Button>(_game, "_mic").Disabled);
            Check(!_recorder.Recording && Read<ConfirmationDialog?>(_game, "_microphoneConsent") is null, "unavailable selected capability blocks before consent/source acquisition");
            Call(_game, "CloseModal"); _contract.Available = true;
            await Consent(true); await Until(() => Read<SpeechSampleBuffer?>(_recorder, "_samples") is { Count: > 0 }); ObserveBuffer();
            var retained = Read<SpeechSampleBuffer>(_recorder, "_samples");
            _contract.HoldPost = true; PressMic(); await Until(() => _contract.HeldPost is not null);
            Return(); Open(); entry.Text = "New draft after close";
            Check(_recorder.Pending && !retained.Disposed, "close retains in-flight source buffer until original transport settles");
            PressMic(); await Frames(3); Check(_contract.Posts == 2 && !_recorder.Recording, "pending operation cannot be replaced after reopen");
            _contract.ReleasePost(); await Until(() => !_recorder.Pending);
            Check(retained.Disposed && entry.Text == "New draft after close", "late cancelled reply is discarded and original buffer is zeroed");
            Check(_recorder.Capability.Status == SpeechAvailability.Unsupported, "post-submission cancellation quarantines unknown producer outcome across reopen");
            PressMic(); await Frames(3); Check(_contract.Posts == 2 && !_recorder.Recording, "unknown outcome cannot restart capture or replay");
            await QualifyRecorderFailures(admission);
            Check(await _game.Audio.StopAndRetireAsync(), "graceful native fixture teardown drains original audio handles");
            _game.Free(); _game = null; await Frames(3);
            Check(AudioServer.BusCount == buses, "final tree teardown restores bus ownership");
            GD.Print($"LANTERNWAKE_SPEECH_CAPTURE_OK checks={_checks} source=owned_generator microphone=false inference=false injected_elapsed_limit=true posts={_contract.Posts}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    public override void _Process(double delta)
    {
        if (!_rendered || _game is null || Time.GetTicksMsec() - _lastTelemetry < 100) return;
        _lastTelemetry = Time.GetTicksMsec(); ObserveBuffer();
        var buffer = Read<SpeechSampleBuffer?>(_recorder, "_samples");
        var discarded = _buffers.Where(pair => pair.Buffer.Disposed).ToArray();
        File.WriteAllText(Path.Combine(_root, "observations.json"), JsonSerializer.Serialize(new
        {
            Source = "OWNED SYNTHETIC GENERATOR; NO MICROPHONE; NO INFERENCE", Recording = _recorder.Recording, Pending = _recorder.Pending,
            Frames = buffer?.Count ?? 0, HasRecording = _recorder.HasRecording, Posts = _contract.Posts, ValidStereo = _contract.ValidStereo,
            Consent = Read<ConfirmationDialog?>(_game, "_microphoneConsent") is not null, DisposedBuffers = discarded.Length,
            DisposedStorageZeroed = discarded.All(pair => pair.Storage.All(f => f == default)), BusCount = AudioServer.BusCount,
            Entry = Read<LineEdit>(_game, "_entry").Text, MicCaption = Read<Button>(_game, "_mic").Text
        }));
    }
    public override void _ExitTree() { if (GodotObject.IsInstanceValid(_game)) _game!.Free(); }

    private async Task QualifyRecorderFailures(SpeechRuntimeAdmission admission)
    {
        var changedContract = new FixtureContract();
        using (var selected = new PumasSpeechTranscriber(admission, SpeechCaptureKind.OwnedSynthetic,
            value => new(value, new FixtureHandler(changedContract), TimeSpan.FromSeconds(2))))
        {
            selected.Configure(new(true, Model: "owned-synthetic-audio"));
            Check((await selected.PrepareAsync(CancellationToken.None)).Status == SpeechAvailability.Ready, "alias selection prepared before consent");
            changedContract.Profile = "changed-profile";
            Check((await selected.PrepareAsync(CancellationToken.None, preserveSelection: true)).Status == SpeechAvailability.Unsupported && !selected.Prepared && changedContract.Posts == 0,
                "changed discovered profile requires fresh consent and sends no audio");
        }
        var settings = new SpeechServiceSettings(true, Model: "owned-synthetic-audio") { Profile = "fixture-audio" };
        foreach (var readFailure in new[] { false, true })
        {
            var contract = new FixtureContract(); var source = new OwnedTestSource(readFailure);
            using var transcriber = new PumasSpeechTranscriber(admission, SpeechCaptureKind.OwnedSynthetic,
                value => new(value, new FixtureHandler(contract), TimeSpan.FromSeconds(2)));
            using var recorder = new SpeechRecorder(admission, SpeechCaptureKind.OwnedSynthetic, _ => source, transcriber);
            recorder.Configure(settings); Check((await recorder.PrepareAsync(CancellationToken.None)).Status == SpeechAvailability.Ready, "owned source preflight ready");
            recorder.Start(this);
            try { recorder.Poll(3); throw new Exception("Source failure accepted."); }
            catch (InvalidOperationException) { Check(source.Disposed && !recorder.Recording && !recorder.HasRecording && contract.Posts == 0, "starvation/read failure discards source without submission"); }
        }
        var timeoutContract = new FixtureContract { HoldPost = true }; var testSource = new OwnedTestSource(false, emitFrame: true);
        using (var transcriber = new PumasSpeechTranscriber(admission, SpeechCaptureKind.OwnedSynthetic,
            value => new(value, new FixtureHandler(timeoutContract), TimeSpan.FromMilliseconds(100))))
        using (var recorder = new SpeechRecorder(admission, SpeechCaptureKind.OwnedSynthetic, _ => testSource, transcriber))
        {
            recorder.Configure(settings); await recorder.PrepareAsync(CancellationToken.None); recorder.Start(this); recorder.Poll();
            var buffer = Read<SpeechSampleBuffer>(recorder, "_samples");
            var operation = recorder.StopAndTranscribe(CancellationToken.None);
            await Until(() => timeoutContract.HeldPost is not null && timeoutContract.PostToken.IsCancellationRequested);
            Check(recorder.Pending && !buffer.Disposed, "deadline cannot release source buffer before held original transport settles");
            timeoutContract.ReleasePost();
            try { await operation; throw new Exception("Timeout produced transcript."); }
            catch (SpeechTranscriptionException error) { Check(error.Code == "timeout" && error.Outcome == AudioOperationOutcome.Unknown, "post-submission timeout preserves unknown outcome"); }
            Check(buffer.Disposed && recorder.Capability.Status == SpeechAvailability.Unsupported, "timeout zeroes retained PCM and blocks replay after observed transport settlement");
        }
        var activeContract = new FixtureContract();
        using var activeTranscriber = new PumasSpeechTranscriber(admission, SpeechCaptureKind.OwnedSynthetic,
            value => new(value, new FixtureHandler(activeContract), TimeSpan.FromSeconds(2)));
        var active = new SpeechRecorder(admission, SpeechCaptureKind.OwnedSynthetic, transcriber: activeTranscriber);
        active.Configure(settings); await active.PrepareAsync(CancellationToken.None);
        var buses = AudioServer.BusCount; active.Start(this);
        await Until(() => { active.Poll(); return active.HasRecording; });
        var captured = Read<SpeechSampleBuffer>(active, "_samples"); var storage = Read<StereoSample[]>(captured, "_frames");
        active.Dispose(); await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
        Check(captured.Disposed && storage.All(frame => frame == default) && AudioServer.BusCount == buses && activeContract.Posts == 0, "active-source terminal disposal stops generator, zeroes clip and releases bus without sending");
    }
    private sealed class OwnedTestSource(bool fail, bool emitFrame = false) : ISpeechCaptureSource
    {
        public bool Disposed;
        public int SampleRate => 48000;
        public StereoSample[] Read(int maximum) => fail ? throw new InvalidOperationException("Owned device read failure.") : emitFrame ? [new(.25f, -.5f)] : [];
        public void Dispose() => Disposed = true;
    }

    private sealed class FixtureContract
    {
        public bool Available = true, HoldPost, HoldGet, ValidStereo;
        public string Profile = "fixture-audio";
        public CancellationToken PostToken;
        public TaskCompletionSource? HeldGet;
        public void ReleaseGet() { HoldGet = false; HeldGet?.TrySetResult(); }
        public int Posts;
        public TaskCompletionSource? HeldPost;
        public void ReleasePost() { HoldPost = false; HeldPost?.TrySetResult(); }
        public async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken token)
        {
            string body;
            if (request.Method == HttpMethod.Get)
            {
                if (HoldGet) { HeldGet = new(TaskCreationOptions.RunContinuationsAsynchronously); await HeldGet.Task; token.ThrowIfCancellationRequested(); }
                var availability = Available ? "{\"state\":\"available\"}" : "{\"state\":\"unavailable\",\"reason\":\"unqualified_audio_runtime\"}";
                body = "{\"supported_contract_versions\":[1],\"model\":\"owned-synthetic-audio\",\"profile\":\"fixture-audio\",\"max_request_bytes\":33554432,\"capabilities\":[{\"semantic_task\":\"speech_to_text\",\"input_formats\":[\"pcm_f32le\"],\"output_formats\":[\"text\"],\"streaming\":false,\"availability\":" + availability + ",\"option_bounds\":[{\"option\":\"max_output_tokens\",\"minimum\":512,\"maximum\":512}]}]}";
                body = body.Replace("fixture-audio", Profile, StringComparison.Ordinal);
            }
            else
            {
                Posts++;
                using var json = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(token));
                var input = json.RootElement.GetProperty("input"); var count = input.GetProperty("sample_count").GetInt32();
                var pcm = Convert.FromBase64String(input.GetProperty("data_base64").GetString()!);
                ValidStereo = count > 0 && pcm.Length == count * 8 && input.GetProperty("channels").GetInt32() == 2 &&
                    input.GetProperty("sample_rate_hz").GetInt32() == (int)AudioServer.GetMixRate() && pcm.Any(value => value != 0) &&
                    request.RequestUri!.AbsolutePath == "/v1/model-operations" && !json.RootElement.TryGetProperty("capability", out _);
                Array.Clear(pcm);
                var id = json.RootElement.GetProperty("request_id").GetString();
                PostToken = token;
                if (HoldPost) { HeldPost = new(TaskCreationOptions.RunContinuationsAsynchronously); await HeldPost.Task; token.ThrowIfCancellationRequested(); }
                body = JsonSerializer.Serialize(new { contract_version = 1, request_id = id, result = new { kind = "text", text = "OWNED SYNTHETIC TRANSCRIPT", finish_reason = "stop" } });
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
    private sealed class FixtureHandler(FixtureContract contract) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation) => contract.Send(request, cancellation);
    }
}
#endif
