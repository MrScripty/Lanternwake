using System.Runtime.InteropServices;

namespace Lanternwake.Conversation;

internal enum SpeechCaptureKind { Microphone, OwnedSynthetic }

/// <summary>Host preflight only. Pumas owns runtime/model admission and confinement.</summary>
public sealed class SpeechRuntimeAdmission
{
    private readonly SpeechCaptureKind? _source;
    public string Refusal { get; }
    private SpeechRuntimeAdmission(SpeechCaptureKind? source, string refusal = "") { _source = source; Refusal = refusal; }
    public static SpeechRuntimeAdmission Installed { get; } = InspectHost();
    private static SpeechRuntimeAdmission InspectHost()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return EvaluateHost(false, RuntimeInformation.ProcessArchitecture, -1);
        try
        {
            // Read-only VERSION query. Never create a ruleset or relax Pumas confinement.
            var abi = LandlockVersion(444, IntPtr.Zero, 0, 1);
            return EvaluateHost(true, Architecture.X64, abi);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        { return new(null, "Cannot check Landlock support on this host. Experimental local Cohere is unavailable. No audio was recorded."); }
    }
    internal static SpeechRuntimeAdmission EvaluateHost(bool linux, Architecture architecture, long abi) =>
        !linux || architecture != Architecture.X64
            ? new(null, "Experimental local Cohere requires Linux x86_64 and Landlock ABI 6 or newer. No audio was recorded.")
            : abi >= 6 ? new(SpeechCaptureKind.Microphone) : new(null,
                "Experimental local Cohere requires Landlock ABI 6 or newer; the host query returned " + abi + ". No audio was recorded.");
    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
    private static extern long LandlockVersion(long number, IntPtr attributes, nuint size, uint flags);
    internal bool Allows(SpeechCaptureKind source) => _source == source;
#if DEBUG
    internal static SpeechRuntimeAdmission ForOwnedFixture(string root)
    {
        if (!Path.IsPathFullyQualified(root) || !File.Exists(Path.Combine(root, "owned-fixture")))
            throw new InvalidOperationException("Marked owned speech fixture required.");
        return new(SpeechCaptureKind.OwnedSynthetic);
    }
#endif
}
