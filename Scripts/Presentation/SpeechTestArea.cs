using Godot;
using Lanternwake.Conversation;
using Lanternwake.Core;

namespace Lanternwake.Presentation;

/// <summary>A settings-only view of the shared speech pipeline; never writes a story draft or save.</summary>
internal sealed class SpeechTestArea : IDisposable
{
    private readonly Node _owner;
    private readonly SpeechRecorder _recorder;
    private readonly Func<SpeechServiceSettings> _selection;
    private readonly Func<bool> _active, _configurationBusy;
    private readonly Action<Task> _track;
    private readonly Action<bool> _lockSettings;
    private readonly PackedScene _consentScene;
    private readonly Button _record, _stop, _transcribe, _cancel;
    private readonly Label _status;
    private readonly TextEdit _result;
    private CancellationTokenSource? _request;
    private ConfirmationDialog? _consent;
    private bool _running, _disposed;
    private int _generation;
    private double _seconds;
    public bool InUse => _running || _consent is not null || _recorder.Recording || _recorder.HasRecording || _recorder.Pending;

    public SpeechTestArea(Node owner, VBoxContainer form, SpeechRecorder recorder, PackedScene consentScene,
        Func<SpeechServiceSettings> selection, Func<bool> active, Func<bool> configurationBusy,
        Action<Task> track, Action<bool> lockSettings)
    {
        _owner = owner; _recorder = recorder; _consentScene = consentScene;
        _selection = selection; _active = active; _configurationBusy = configurationBusy; _track = track; _lockSettings = lockSettings;
        _record = form.GetNode<Button>("TestRecord"); _stop = form.GetNode<Button>("TestStop");
        _transcribe = form.GetNode<Button>("TestTranscribe"); _cancel = form.GetNode<Button>("TestCancel");
        _status = form.GetNode<Label>("TestStatus"); _result = form.GetNode<TextEdit>("TestResult");
        _record.Pressed += () => { if (CanBegin()) _track(RecordAsync()); };
        _stop.Pressed += () =>
        {
            if (!Current() || _running || !_recorder.Recording) return;
            try { _recorder.Poll(); _recorder.StopCapture(); _status.Text = "Stopped. Choose Transcribe to send this clip to local Pumas, or Cancel to discard it."; }
            catch (Exception error) { _recorder.Discard(); _status.Text = error.Message; }
            Refresh();
        };
        _transcribe.Pressed += () =>
        { if (Current() && !_configurationBusy() && !_running && !_recorder.Pending && !_recorder.Recording && _recorder.HasRecording) _track(TranscribeAsync()); };
        _cancel.Pressed += Cancel;
        Refresh();
    }
    private bool Current() => !_disposed && _active();
    private bool Current(int generation) => Current() && generation == _generation;
    private bool CanBegin() => Current() && !_configurationBusy() && !InUse;
    public void Refresh()
    {
        if (!Current()) return;
        _record.Disabled = _configurationBusy() || InUse;
        _stop.Disabled = !_recorder.Recording || _running;
        _transcribe.Disabled = _configurationBusy() || _running || _recorder.Pending || _recorder.Recording || !_recorder.HasRecording;
        _cancel.Disabled = !InUse;
        _result.Editable = !_running && !_recorder.Recording;
        _lockSettings(InUse);
    }
    private CancellationToken NewRequest()
    {
        _request?.Cancel(); _request?.Dispose();
        _request = new(TimeSpan.FromSeconds(90)); return _request.Token;
    }
    private async Task RecordAsync()
    {
        var generation = ++_generation;
        _running = true; Refresh(); _status.Text = "Checking the selected local Pumas model…";
        var token = NewRequest();
        try
        {
            _recorder.Configure(_selection());
            var ready = await _recorder.PrepareAsync(token);
            if (!Current(generation)) return;
            if (ready.Status == SpeechAvailability.Unsupported) { _status.Text = ready.Message; return; }
            var consent = _consentScene.Instantiate<ConfirmationDialog>(); _consent = consent; _record.GetWindow().AddChild(consent);
            consent.DialogText = "Record up to 30 seconds for this settings test. Stop releases the microphone. Transcribe sends the clip only to the selected local Pumas model. Cancel, changing tabs or closing settings discards it. The editable test result stays out of the game.";
            consent.Canceled += Cancel; consent.CloseRequested += Cancel;
            consent.Confirmed += () =>
            {
                if (!Current(generation) || _consent != consent || _running) return;
                CloseConsent(); _track(StartAsync(generation));
            };
            consent.PopupCentered();
        }
        catch (Exception error) { if (Current(generation)) _status.Text = Error(error); }
        finally { FinishOperation(generation); }
    }
    private async Task StartAsync(int generation)
    {
        _running = true; Refresh(); _status.Text = "Rechecking the consented selection…";
        var token = NewRequest();
        try
        {
            var ready = await _recorder.RecheckAsync(token);
            if (!Current(generation)) return;
            if (ready.Status == SpeechAvailability.Unsupported) { _status.Text = ready.Message; return; }
            _recorder.Start(_owner); _seconds = 0;
            _status.Text = "Recording (0s). Stop releases the microphone; Transcribe sends only after you choose it.";
        }
        catch (Exception error) { if (Current(generation)) { _recorder.Discard(); _status.Text = Error(error); } }
        finally { FinishOperation(generation); }
    }
    private async Task TranscribeAsync()
    {
        var generation = ++_generation;
        _running = true; Refresh(); _status.Text = "Transcribing with local Pumas…";
        var token = NewRequest();
        try
        {
            var text = await _recorder.StopAndTranscribe(token);
            if (!Current(generation)) return;
            _result.Text = text;
            _status.Text = _recorder.FinishReason == "length" ? "The transcript reached its text limit. Review and edit the test result." :
                text.Length == 0 ? "Pumas returned an empty transcript. Try a short, clearly spoken clip." : "Transcribed. You can edit this test result; it is kept out of the game.";
        }
        catch (Exception error) { if (Current(generation)) _status.Text = Error(error); }
        finally { FinishOperation(generation); }
    }
    private void FinishOperation(int generation)
    {
        _running = false;
        if (Current() && generation != _generation)
            _status.Text = _recorder.Capability.Status == SpeechAvailability.Unsupported ? _recorder.Capability.Message :
                "Cancelled. The local request has settled; no result was applied.";
        Refresh();
    }
    public void Poll(double delta)
    {
        if (!Current()) return;
        if (!_recorder.Recording) { Refresh(); return; }
        try
        {
            _recorder.Poll(delta); _seconds += delta;
            if (_seconds >= 30) _recorder.StopCapture();
            _status.Text = _recorder.Recording ? $"Recording ({_seconds:0}s). Choose Stop when ready." :
                "Stopped at the 30-second or sample limit. Choose Transcribe or Cancel. No audio has been sent.";
        }
        catch (Exception error) { _recorder.Discard(); _status.Text = error.Message; }
        Refresh();
    }
    public void Cancel()
    {
        if (!Current()) return;
        _generation++; _request?.Cancel(); CloseConsent(); _recorder.Discard();
        _status.Text = _running || _recorder.Pending ? "Cancelled. Waiting for the original request to settle; no transcript will be applied." :
            "Recording discarded. No test transcript was submitted to the game.";
        Refresh();
    }
    private void CloseConsent()
    {
        var consent = _consent; _consent = null;
        if (GodotObject.IsInstanceValid(consent)) { consent!.Hide(); consent.QueueFree(); }
    }
    private static string Error(Exception error) => error is OperationCanceledException ? "Transcription test cancelled." : error.Message;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _generation++; _request?.Cancel(); _request?.Dispose(); _request = null;
        CloseConsent(); _recorder.Discard();
    }
}
