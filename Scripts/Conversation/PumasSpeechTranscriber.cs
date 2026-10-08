using Lanternwake.Core;

namespace Lanternwake.Conversation;

public readonly record struct StereoSample(float Left, float Right);
public enum SpeechAvailability { Unsupported }
public sealed record SpeechCapability(SpeechAvailability Status, string Message);

/// <summary>
/// Keeps microphone capture gated while installed Pumas audio is unqualified.
/// The generic consumer is PumasAudioTextClient; no available advertisement alone
/// re-enables this production boundary. See the pinned contract in docs/SPEECH.md.
/// No vendor endpoint, local CLI, or fabricated transcription is a substitute.
/// </summary>
public sealed class PumasSpeechTranscriber
{
    private SpeechServiceSettings _settings = new();
    public void Configure(SpeechServiceSettings settings) => _settings = settings.Validate();

    public async Task<AudioTextCapability> InspectSelectedModelAsync(CancellationToken cancellation)
    {
        using var client = new PumasAudioTextClient(_settings);
        return await client.DiscoverAsync(cancellation).ConfigureAwait(false);
    }

    public SpeechCapability Capability => new(SpeechAvailability.Unsupported,
        "Cohere Transcribe through Pumas Library is not available in this build yet. Typed replies and editable suggestions remain available. No audio has been recorded.");

    public Task<string> TranscribeAsync(IReadOnlyList<StereoSample> samples, int sampleRate, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        return Task.FromException<string>(new NotSupportedException(Capability.Message));
    }
}
