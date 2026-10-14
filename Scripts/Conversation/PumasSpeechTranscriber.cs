using Lanternwake.Core;

namespace Lanternwake.Conversation;

public readonly record struct StereoSample(float Left, float Right);
public enum SpeechAvailability { Unsupported, Ready }
public sealed record SpeechCapability(SpeechAvailability Status, string Message);
public sealed class SpeechTranscriptionException(string code, AudioOperationOutcome outcome, string message) : Exception(message)
{
    public string Code { get; } = code;
    public AudioOperationOutcome Outcome { get; } = outcome;
}

/// <summary>Independent runtime admission plus selected generic Pumas contract; unknown outcomes stay quarantined.</summary>
public sealed class PumasSpeechTranscriber : IDisposable
{
    private readonly object _sync = new();
    private SpeechServiceSettings _settings = new();
    private readonly SpeechRuntimeAdmission _admission;
    private readonly SpeechCaptureKind _source;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Func<SpeechServiceSettings, PumasAudioTextClient> _factory;
    private PumasAudioTextClient? _client;
    private SpeechServiceSettings? _clientSettings;
    private bool _unknown, _pending, _preparing, _prepared, _disposed;
    public string FinishReason { get; private set; } = "";
    public PumasSpeechTranscriber() : this(SpeechRuntimeAdmission.Installed, SpeechCaptureKind.Microphone) { }
    internal PumasSpeechTranscriber(SpeechRuntimeAdmission admission, SpeechCaptureKind source,
        Func<SpeechServiceSettings, PumasAudioTextClient>? factory = null)
    { _admission = admission; _source = source; _factory = factory ?? (settings => new(settings)); }
    public bool Pending { get { lock (_sync) return _pending || _preparing; } }
    public bool Prepared { get { lock (_sync) return _prepared && !_disposed && !_unknown && !_pending && !_preparing; } }
    public void Configure(SpeechServiceSettings settings)
    {
        settings = settings.Validate();
        lock (_sync) { if (_settings != settings) _prepared = false; _settings = settings; }
    }
    private bool RuntimeSelected => _source == SpeechCaptureKind.OwnedSynthetic ||
        (_settings.Runtime == SpeechRuntimeMode.ExperimentalLocalCohere && _settings.Provider == DialogueProvider.Pumas &&
         _settings.Profile.Length > 0);
    public SpeechCapability Capability
    {
        get
        {
            lock (_sync) return new(!_disposed && _admission.Allows(_source) && RuntimeSelected && !_unknown ? SpeechAvailability.Ready : SpeechAvailability.Unsupported,
                _unknown ? "Pumas may still be working on the previous audio request. Voice input is paused until its owner confirms settlement. Typed replies remain available."
                : !RuntimeSelected ? "Select Experimental local Cohere through Pumas, an indexed model ID and an explicit profile in AI setup → Transcription. No audio has been recorded."
                : !_admission.Allows(_source) ? _admission.Refusal
                : "Voice capture requires an available selected local runtime and explicit consent.");
        }
    }
    public async Task<AudioTextCapability> InspectSelectedModelAsync(CancellationToken cancellation)
    {
        SpeechServiceSettings settings; lock (_sync) settings = _settings;
        using var client = _factory(settings);
        return await client.DiscoverAsync(cancellation).ConfigureAwait(false);
    }
    public async Task<SpeechCapability> PrepareAsync(CancellationToken cancellation, bool preserveSelection = false)
    {
        cancellation.ThrowIfCancellationRequested();
        SpeechServiceSettings candidate;
        SpeechServiceSettings? expected;
        lock (_sync)
        {
            if (Capability.Status == SpeechAvailability.Unsupported) return Capability;
            if (_pending || _preparing) return new(SpeechAvailability.Unsupported, "An audio request is still settling. Typed replies remain available.");
            if (preserveSelection && !_prepared) return new(SpeechAvailability.Unsupported, "Speech selection changed. Choose Use voice again for fresh consent.");
            expected = preserveSelection ? _clientSettings : null;
            _preparing = true; _prepared = false; candidate = _settings;
        }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
        try
        {
            using var probe = _factory(candidate);
            var selected = await probe.DiscoverAsync(lifetime.Token).ConfigureAwait(false);
            lifetime.Token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (_settings != candidate || _disposed) return new(SpeechAvailability.Unsupported, "Speech settings changed. Choose Use voice again.");
                if (!selected.Advertised) return new(SpeechAvailability.Unsupported, Message(selected.Code));
                var pinned = candidate with { Profile = selected.Profile };
                if (expected is not null && expected != pinned) return new(SpeechAvailability.Unsupported, "The selected speech profile changed. Choose Use voice again for fresh consent.");
                if (_clientSettings != pinned) { _client?.Dispose(); _client = _factory(pinned); _clientSettings = pinned; }
                _prepared = true;
            }
            return new(SpeechAvailability.Ready, "Record only after choosing Start recording. Audio goes only to local Pumas when you choose to transcribe.");
        }
        finally { lock (_sync) { _preparing = false; if (_disposed) _lifetime.Dispose(); } }
    }
    public async Task<string> TranscribeAsync(IReadOnlyList<StereoSample> samples, int sampleRate, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        PumasAudioTextClient client;
        lock (_sync)
        {
            if (Capability.Status == SpeechAvailability.Unsupported) throw new NotSupportedException(Capability.Message);
            if (!_prepared || _client is null || _pending || _preparing) throw new InvalidOperationException("The selected speech operation is unavailable or still settling.");
            _pending = true; _prepared = false; client = _client; FinishReason = "";
        }
        try
        {
            var reply = await client.GenerateAsync(samples, sampleRate, cancellation).ConfigureAwait(false);
            lock (_sync) { if (reply.Outcome == AudioOperationOutcome.Unknown) _unknown = true; FinishReason = reply.FinishReason; }
            if (!reply.Success) throw new SpeechTranscriptionException(reply.Code, reply.Outcome, Message(reply.Code, reply.Outcome));
            return reply.Text;
        }
        finally { lock (_sync) _pending = false; }
    }
    private static string Message(string code, AudioOperationOutcome outcome = AudioOperationOutcome.NotAdmitted) =>
        outcome == AudioOperationOutcome.Unknown ? "The audio request ended without confirmed Pumas settlement. Voice input is paused; typed replies remain available."
        : code switch
        {
            "speech_disabled" => "Voice input is off. Typed replies remain available.",
            "model_not_selected" => "Enter the indexed model ID returned by Pumas import_local_cohere. No audio has been recorded.",
            "model_not_found" => "Pumas has no loaded model with this ID/profile. Load it with serve_experimental_local_cohere, then try again.",
            "unsupported_provider" => "Transcription currently supports local Pumas only. Hosted preferences cannot receive microphone audio.",
            "unqualified_audio_runtime" or "capability_unavailable" => "Pumas refused this runtime. Check the experimental Cohere load result and platform support. No audio has been recorded.",
            "pumas_unavailable" => "Could not reach local Pumas. Check the transcription URL and start its owner.",
            "unsupported_contract" or "invalid_response" => "Pumas returned an incompatible audio contract. Use the documented Pumas version.",
            "cancelled" => "Voice input cancelled. No transcript was sent as dialogue.",
            "timeout" => "Pumas did not respond in time. Typed replies remain available.",
            "invalid_audio" => "No valid audio frames were captured. Typed replies remain available.",
            _ => "The selected local speech runtime is unavailable. Typed replies remain available."
        };
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return; _disposed = true; _prepared = false;
            _lifetime.Cancel(); _client?.Dispose();
            if (!_preparing) _lifetime.Dispose();
        }
    }
}
