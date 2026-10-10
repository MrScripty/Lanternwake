using Lanternwake.Conversation;

var transcriber = new PumasSpeechTranscriber();
if (transcriber.Capability.Status != SpeechAvailability.Unsupported)
    throw new Exception("Speech must default to disabled until deliberate local experiment selection.");
// Old settings cannot silently select a direct recognizer after the cutover.
Environment.SetEnvironmentVariable("LANTERNWAKE_WHISPER_CLI", "/bin/echo");
Environment.SetEnvironmentVariable("LANTERNWAKE_WHISPER_MODEL", "unused");
for (var attempt = 0; attempt < 2; attempt++)
{
    try
    {
        await transcriber.TranscribeAsync([new(.1f, .1f)], 48000, CancellationToken.None);
        throw new Exception("Unsupported speech returned a transcript.");
    }
    catch (NotSupportedException error) when (error.Message.Contains("Pumas") && error.Message.Contains("Cohere")) { }
}
using var cancel = new CancellationTokenSource();
cancel.Cancel();
try
{
    await transcriber.TranscribeAsync([], 48000, cancel.Token);
    throw new Exception("Cancelled speech was accepted.");
}
catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
Console.WriteLine("PASS default-disabled Pumas/Cohere capability, repeated rejection, no legacy fallback and pre-cancellation. No inference claim.");
await AudioModalityTests.RunAsync();

CaptureBufferTests.Run();

await InstalledRuntimeTests.RunAsync();
