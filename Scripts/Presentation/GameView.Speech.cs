using Godot;
using Lanternwake.Conversation;
namespace Lanternwake.Presentation;

public partial class GameView
{
    private bool CurrentSpeech(int generation) => generation == _generation && IsInsideTree() && !_closing && _chatPanel.Visible;
    private void CloseMicrophoneConsent()
    {
        var consent = _microphoneConsent; _microphoneConsent = null;
        if (GodotObject.IsInstanceValid(consent)) { consent!.Hide(); consent.QueueFree(); }
    }
    private void ResetSpeechControls()
    {
        _mic.Disabled = false; _mic.Text = "Use voice"; _send.Disabled = false; _entry.Editable = true;
    }
    private void PollMicrophone(double delta)
    {
        if (!_speech.Recording) return;
        try
        {
            _speech.Poll(delta); _recordSeconds += delta;
            if (_recordSeconds >= 30) _speech.StopCapture();
            _mic.Text = _speech.Recording ? $"Stop and transcribe ({_recordSeconds:0}s)" : "Transcribe recording";
            if (!_speech.Recording) SetStatusText("Recording stopped at its limit. Choose Transcribe recording or Return to story to discard it.");
        }
        catch (Exception error) { _speech.Discard(); ResetSpeechControls(); SetStatusText(error.Message); }
    }
    private void ToggleMicrophone()
    {
        if (!_busy && !_closing && _chatPanel.Visible && _microphoneConsent is null && !_speech.Pending)
            _operations.Track(ToggleMicrophoneAsync());
    }
    private async Task ToggleMicrophoneAsync()
    {
        var generation = _generation;
        _speechRequest?.Dispose(); _speechRequest = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = _speechRequest.Token;
        if (!_speech.Recording && !_speech.HasRecording)
        {
            _mic.Disabled = true;
            try
            {
                var capability = await _speech.PrepareAsync(token);
                if (!CurrentSpeech(generation) || _busy) return;
                if (capability.Status == SpeechAvailability.Unsupported) { ShowWindow("Cohere Transcribe via Pumas", capability.Message); return; }
                var consent = MicrophoneConsentScene.Instantiate<ConfirmationDialog>();
                _microphoneConsent = consent; AddChild(consent);
                void Cancel()
                {
                    if (_microphoneConsent != consent || !CurrentSpeech(generation)) return;
                    CloseMicrophoneConsent(); ResetSpeechControls(); _entry.GrabFocus();
                    SetStatusText("Recording cancelled. No audio was captured or sent.");
                }
                consent.Canceled += Cancel; consent.CloseRequested += Cancel;
                consent.Confirmed += () =>
                {
                    if (_microphoneConsent != consent || !CurrentSpeech(generation) || _busy) return;
                    CloseMicrophoneConsent(); _operations.Track(StartConsentedCapture(generation, token));
                };
                consent.PopupCentered();
            }
            catch (Exception error) { if (CurrentSpeech(generation)) SetStatusText(error is OperationCanceledException ? "Voice input cancelled." : error.Message); }
            finally { if (CurrentSpeech(generation) && _microphoneConsent is null) _mic.Disabled = false; }
            return;
        }
        _busy = true; _mic.Disabled = true; SetStatusText("Transcribing with local Pumas…");
        try
        {
            var text = await _speech.StopAndTranscribe(token);
            if (!CurrentSpeech(generation)) return;
            _entry.Text = text; _entry.GrabFocus();
            SetStatusText(_speech.FinishReason == "length" ? "Transcript reached its text limit. Review and edit it, then choose Say this." : "Review and edit your transcript, then choose Say this.");
        }
        catch (Exception error) { if (CurrentSpeech(generation)) SetStatusText(error is OperationCanceledException ? "Voice input cancelled." : error.Message); }
        finally { if (CurrentSpeech(generation)) { _busy = false; ResetSpeechControls(); } }
    }
    private async Task StartConsentedCapture(int generation, CancellationToken token)
    {
        try
        {
            // Recheck the consent-bound selection; a changed profile requires fresh consent.
            var capability = await _speech.RecheckAsync(token);
            if (!CurrentSpeech(generation) || _busy) return;
            if (capability.Status == SpeechAvailability.Unsupported) { SetStatusText(capability.Message); return; }
            _speech.Start(this); _recordSeconds = 0; _send.Disabled = true; _entry.Editable = false;
            _mic.Text = "Stop and transcribe (0s)"; SetStatusText("Recording. Return to story discards it; Stop and transcribe sends it to local Pumas.");
        }
        catch (Exception error) { if (CurrentSpeech(generation)) { _speech.Discard(); ResetSpeechControls(); SetStatusText(error is OperationCanceledException ? "Voice input cancelled." : error.Message); } }
        finally { if (CurrentSpeech(generation)) _mic.Disabled = false; }
    }
}
