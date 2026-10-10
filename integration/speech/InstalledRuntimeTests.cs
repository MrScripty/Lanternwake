using System.Net;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Lanternwake.Core;
using Lanternwake.Conversation;

internal static class InstalledRuntimeTests
{
    public static async Task RunAsync()
    {
        var checks = 0; void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        foreach (var host in new[] { (false, Architecture.X64, 6L), (true, Architecture.Arm64, 6L), (true, Architecture.X64, 5L), (true, Architecture.X64, -1L) })
        {
            var refused = SpeechRuntimeAdmission.EvaluateHost(host.Item1, host.Item2, host.Item3);
            Check(!refused.Allows(SpeechCaptureKind.Microphone) && refused.Refusal.Length > 0, "Unsupported host must report why it refused.");
        }
        var supported = SpeechRuntimeAdmission.EvaluateHost(true, Architecture.X64, 6);
        Check(supported.Allows(SpeechCaptureKind.Microphone) && !supported.Allows(SpeechCaptureKind.OwnedSynthetic), "Host preflight only admits its capture kind.");
        var requests = 0;
        using var transcriber = new PumasSpeechTranscriber(supported, SpeechCaptureKind.Microphone,
            settings => new(settings, new Handler(request =>
            {
                requests++;
                var available = new { supported_contract_versions = new[] { 1 }, model = settings.Model, profile = settings.Profile, max_request_bytes = 33554432,
                    capabilities = new[] { new { capability = "audio_transcription", semantic_task = "speech_to_text", input_formats = new[] { "pcm_f32le", "pcm_s16le" },
                        output_formats = new[] { "text" }, streaming = false, availability = new { state = "available" },
                        option_bounds = new[] { new { option = "max_output_tokens", minimum = 1, maximum = 2048 } } } } };
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(available), Encoding.UTF8, "application/json") };
            }), TimeSpan.FromSeconds(2)));
        var selection = new SpeechServiceSettings(true, Model: "local-cohere-model") { Profile = "cohere-local" };
        transcriber.Configure(selection);
        Check((await transcriber.PrepareAsync(default)).Status == SpeechAvailability.Unsupported && requests == 0, "Saved enabled/model flags cannot opt into the experiment.");
        transcriber.Configure(selection with { Runtime = SpeechRuntimeMode.ExperimentalLocalCohere, Profile = "" });
        Check((await transcriber.PrepareAsync(default)).Status == SpeechAvailability.Unsupported && requests == 0, "Explicit experiment still requires a profile.");
        transcriber.Configure(selection with { Runtime = SpeechRuntimeMode.ExperimentalLocalCohere, Provider = DialogueProvider.OpenRouter, Endpoint = AiSettings.DefaultEndpoint(DialogueProvider.OpenRouter) });
        Check((await transcriber.PrepareAsync(default)).Status == SpeechAvailability.Unsupported && requests == 0, "Hosted preferences never admit microphone capture.");
        transcriber.Configure(selection with { Runtime = SpeechRuntimeMode.ExperimentalLocalCohere });
        Check((await transcriber.PrepareAsync(default)).Status == SpeechAvailability.Ready && transcriber.Prepared && requests == 1, "Explicit local selection checks the real consumer contract before consent.");
        transcriber.Configure(selection with { Runtime = SpeechRuntimeMode.ExperimentalLocalCohere, Model = "other-model" });
        Check(!transcriber.Prepared && (await transcriber.PrepareAsync(default, preserveSelection: true)).Status == SpeechAvailability.Unsupported && requests == 1,
            "Selection changes invalidate prepared consent and cannot silently rebind.");
        var path = Path.Combine(Path.GetTempPath(), "speech-settings-" + Guid.NewGuid() + ".json");
        try
        {
            var settings = AiSettings.FromEnvironment() with { Transcription = selection with { Runtime = SpeechRuntimeMode.ExperimentalLocalCohere } };
            settings.Save(path); Check(AiSettings.Load(path, AiSettings.FromEnvironment()).Transcription == settings.Transcription, "Runtime/model/profile persist independently of the story.");
            File.WriteAllText(path, "{\"Provider\":0,\"DialogueEnabled\":false,\"Endpoint\":\"http://127.0.0.1:8080/\",\"Model\":\"\",\"Transcription\":{\"Enabled\":true,\"Model\":\"legacy\"}}");
            Check(AiSettings.Load(path, AiSettings.FromEnvironment()).Transcription.Runtime == SpeechRuntimeMode.Disabled, "Old preferences migrate to disabled admission.");
        }
        finally { File.Delete(path); }
        Console.WriteLine($"PASS explicit local Cohere admission: {checks} checks; simulated platform branches and controlled capabilities, no physical microphone or inference.");
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request));
    }
}
