namespace Lanternwake.Conversation;

public readonly record struct StereoSample(float Left, float Right);
public enum SpeechAvailability { Unsupported }
public sealed record SpeechCapability(SpeechAvailability Status, string Message);

/// <summary>
/// Owns the temporarily unavailable Pumas/Cohere boundary. Re-enable only against
/// the implemented producer contract and lifecycle evidence in docs/SPEECH.md.
/// No vendor endpoint, local CLI, or fabricated transcription is a substitute.
/// </summary>
public sealed class PumasSpeechTranscriber
{
    public SpeechCapability Capability => new(SpeechAvailability.Unsupported,
        "Cohere Transcribe through Pumas Library is not available yet. Pumas needs a transcription adapter. Typed replies and editable suggestions remain available. No audio has been recorded.");

    public Task<string> TranscribeAsync(IReadOnlyList<StereoSample> samples, int sampleRate, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        return Task.FromException<string>(new NotSupportedException(Capability.Message));
    }
}
