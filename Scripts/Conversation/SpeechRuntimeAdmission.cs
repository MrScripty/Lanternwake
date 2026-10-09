namespace Lanternwake.Conversation;

internal enum SpeechCaptureKind { Microphone, OwnedSynthetic }

/// <summary>Independent installed-runtime gate. A capability JSON cannot issue admission.</summary>
public sealed class SpeechRuntimeAdmission
{
    private readonly SpeechCaptureKind? _source;
    private SpeechRuntimeAdmission(SpeechCaptureKind? source) => _source = source;
    // The shipping Pumas runtime remains unqualified. Only the future owning
    // runtime qualification factory may replace this refusal, not preferences.
    public static SpeechRuntimeAdmission Installed { get; } = new(null);
    internal bool Allows(SpeechCaptureKind source) => _source == source;
#if DEBUG
    internal static SpeechRuntimeAdmission ForOwnedFixture(string root)
    {
        if (!Path.IsPathFullyQualified(root) || !File.Exists(Path.Combine(root, "owned-fixture")))
            throw new InvalidOperationException("Marked owned speech fixture required.");
        // This permit can never authorize AudioStreamMicrophone.
        return new(SpeechCaptureKind.OwnedSynthetic);
    }
#endif
}
