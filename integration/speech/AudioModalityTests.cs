using System.Buffers.Binary;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lanternwake.Core;
using Lanternwake.Conversation;

internal static class AudioModalityTests
{
    private static int _checks;
    private static readonly SpeechServiceSettings Settings = new(true, Model: "fixture/audio+=") { Profile = "controlled-audio-private", Language = "en" };
    private static readonly StereoSample[] Samples = [new(.25f, -.5f), new(0, .75f)];
    private static void Check(bool condition, string message) { _checks++; if (!condition) throw new Exception(message); }
    /// <summary>GET /v1/served-models with exactly one served (model, profile) entry.</summary>
    private static JsonObject Capabilities(SpeechServiceSettings? settings = null)
    {
        settings ??= Settings;
        return JsonNode.Parse(JsonSerializer.Serialize(new
        {
            supported_contract_versions = new[] { 1 },
            max_request_bytes = 32 * 1024 * 1024, max_response_bytes = 32 * 1024 * 1024, max_stream_event_bytes = 262144,
            models = new[] { new { model = settings.Model, profile = settings.Profile, model_id = "library/" + settings.Profile,
                provider = "fixture", load_state = "loaded", profile_required = false,
                capabilities = new[] { new { capability = "audio_transcription", semantic_task = "speech_to_text",
                    input_formats = new[] { "pcm_f32le", "pcm_s16le" }, output_formats = new[] { "text" }, streaming = false,
                    availability = new { state = "available" }, option_bounds = new[] { new { option = "max_output_tokens", minimum = 512, maximum = 512 } } } } } }
        }))!.AsObject();
    }
    private static JsonObject Entry(JsonObject root) => root["models"]![0]!.AsObject();
    private static JsonObject Descriptor(JsonObject root) => Entry(root)["capabilities"]![0]!.AsObject();
    private static string ErrorBody(string? id, string code, string outcome, string message = "") =>
        JsonSerializer.Serialize(new { contract_version = 1, request_id = id, error = new { code, outcome, message } });
    private static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK, string type = "application/json") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, type) };
    private static string Terminal(string id, string text = "Synthetic transcript", string finish = "stop") =>
        JsonSerializer.Serialize(new { contract_version = 1, request_id = id, result = new { kind = "text", text, finish_reason = finish } });
    private static byte[]? Body(PumasAudioTextClient client) => (byte[]?)typeof(PumasAudioTextClient).GetField("_audioBody", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client);
    private static async Task<string> RequestId(HttpRequestMessage request)
    {
        using var json = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync());
        return json.RootElement.GetProperty("request_id").GetString()!;
    }

    public static async Task RunAsync()
    {
        await WireAndSnapshot();
        EncodingAllocationBound();
        EncodingBufferCustody();
        await DiscoveryRefusals();
        await InvalidInputAndConfig();
        await TerminalRefusals();
        await NonAdmissionDoesNotLatch();
        await AdditiveGrowthIgnored();
        await ServedModelSelection();
        await CancelAndDisposal();
        await RealLoopbackTransport();
        Console.WriteLine($"PASS Current Pumas selected-model audio/text controlled contract: {_checks} assertions; served-model discovery, additive-tolerant decoding, outcome-authoritative errors, PCM bounds, cancellation/timeout/disposal and unknown-outcome quarantine. No model inference qualification.");
    }

    private static async Task WireAndSnapshot()
    {
        var original = Samples.ToList(); byte[]? held = null; PumasAudioTextClient? client = null;
        var ids = new HashSet<string>();
        var handler = new FakeHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                Check(request.RequestUri!.AbsolutePath == "/v1/served-models" && request.RequestUri.Query.Length == 0, "Discovery reads the served-model list; selection never travels in a query.");
                original.Clear(); // Original caller collection may change while discovery awaits.
                return Response(Capabilities().ToJsonString());
            }
            Check(request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/v1/model-operations", "Only generic operation endpoint is allowed.");
            held = Body(client!); Check(held is not null, "Own request bytes through transport settlement.");
            using var json = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(token)); var root = json.RootElement;
            Check(root.GetProperty("capability").GetString() == "audio_transcription" && root.GetProperty("contract_version").GetInt32() == 1 &&
                root.GetProperty("output").GetString() == "text" && !root.TryGetProperty("semantic_task", out _) && !root.GetProperty("stream").GetBoolean(), "Current closed selected-model audio grammar.");
            Check(root.GetProperty("model").GetString() == Settings.Model && root.GetProperty("profile").GetString() == Settings.Profile, "Pin selected model and discovered profile.");
            var input = root.GetProperty("input"); var pcm = Convert.FromBase64String(input.GetProperty("data_base64").GetString()!);
            Check(input.GetProperty("kind").GetString() == "audio" && input.GetProperty("encoding").GetString() == "pcm_f32le" &&
                input.GetProperty("channels").GetInt32() == 2 && input.GetProperty("sample_rate_hz").GetInt32() == 48000 &&
                input.GetProperty("sample_count").GetInt32() == 2 && pcm.Length == 16, "Preserve actual rate/channels and frozen frame count.");
            Check(BinaryPrimitives.ReadSingleLittleEndian(pcm.AsSpan(0, 4)) == .25f && BinaryPrimitives.ReadSingleLittleEndian(pcm.AsSpan(4, 4)) == -.5f &&
                BinaryPrimitives.ReadSingleLittleEndian(pcm.AsSpan(12, 4)) == .75f, "Exact synthetic stereo little-endian PCM.");
            var options = root.GetProperty("options");
            Check(options.GetProperty("kind").GetString() == "audio" && options.GetProperty("language").GetString() == "en" && options.GetProperty("max_output_tokens").GetInt32() == 512, "Explicit closed language and declared token bound.");
            var id = root.GetProperty("request_id").GetString()!; Check(ids.Add(id), "Never reuse a request correlation ID.");
            return Response(Terminal(id, "Synthetic transcript", ids.Count == 1 ? "stop" : "length"));
        });
        using (client = new(Settings, handler, TimeSpan.FromSeconds(5)))
        {
            var reply = await client.GenerateAsync(original, 48000);
            Check(reply.Success && reply.Text == "Synthetic transcript" && reply.Outcome == AudioOperationOutcome.Completed && reply.FinishReason == "stop", "Typed terminal success preserved.");
            Check(held!.All(b => b == 0) && Body(client) is null, "PCM/base64 request cleared after terminal settlement.");
            original.AddRange(Samples);
            reply = await client.GenerateAsync(original, 48000);
            Check(reply.Success && reply.FinishReason == "length", "Length remains explicit, not silently relabelled stop.");
            Check(handler.Requests == 4 && held!.All(b => b == 0), "New explicit operation re-discovers and clears owned body.");
        }
        Check(handler.Disposed, "Owned HTTP handler disposed.");
    }

    private static async Task DiscoveryRefusals()
    {
        Action<JsonObject>[] mutate = [
            root => root["supported_contract_versions"] = new JsonArray(2),
            root => Entry(root)["model"] = "another-model",
            root => Entry(root)["profile"] = "wrong-profile",
            root => root["models"] = new JsonArray(),
            root => root["models"]!.AsArray().Add(Entry(root).DeepClone()),
            root => root["max_request_bytes"] = "bad",
            root => Descriptor(root)["availability"] = new JsonObject { ["state"] = "unavailable", ["reason"] = "unqualified_audio_runtime" },
            root => Descriptor(root)["availability"] = new JsonObject { ["state"] = "unavailable", ["reason"] = "a_future_reason" },
            root => Descriptor(root)["availability"] = new JsonObject { ["state"] = "a_future_state" },
            root => Descriptor(root)["capability"] = "audio_classification",
            root => Descriptor(root)["semantic_task"] = "audio_classification",
            root => Descriptor(root)["input_formats"] = new JsonArray("text"),
            root => Descriptor(root)["output_formats"] = new JsonArray("labels"),
            root => Entry(root)["capabilities"]!.AsArray().Add(Descriptor(root).DeepClone()),
            root => Descriptor(root)["option_bounds"]![0]!["maximum"] = 256,
            root => Descriptor(root)["streaming"] = true,
            root => root["max_request_bytes"] = 1
        ];
        foreach (var change in mutate)
        {
            var root = Capabilities(); change(root);
            var handler = new FakeHandler((request, _) => Task.FromResult(Response(root.ToJsonString())));
            using var client = new PumasAudioTextClient(Settings, handler, TimeSpan.FromSeconds(5));
            var reply = await client.GenerateAsync(Samples, 48000);
            Check(!reply.Success && reply.Outcome == AudioOperationOutcome.NotAdmitted && handler.Requests == 1 && Body(client) is null,
                "Bad/ambiguous/unavailable discovery or admission bound must never submit audio.");
        }
        foreach (var code in new[] { "model_not_found", "ambiguous_model", "capability_unavailable", "unsupported_contract" })
        {
            var handler = new FakeHandler((request, _) => Task.FromResult(Response(JsonSerializer.Serialize(new
                { contract_version = 1, request_id = (string?)null, error = new { code, outcome = "not_admitted", message = "fixture reason for " + code } }), HttpStatusCode.ServiceUnavailable)));
            using var client = new PumasAudioTextClient(Settings, handler, TimeSpan.FromSeconds(5));
            var discovery = await client.DiscoverAsync();
            Check(!discovery.Advertised && discovery.Code == code && discovery.Detail == "fixture reason for " + code && handler.Requests == 1, "Typed discovery error and Pumas message preserved without dispatch.");
        }
        var denied = new FakeHandler((_, _) => throw new Exception("Must not send"));
        using var unavailable = new PumasAudioTextClient(Settings with { Model = "" }, denied, TimeSpan.FromSeconds(5));
        Check((await unavailable.DiscoverAsync()).Code == "model_not_selected" && denied.Requests == 0, "Missing explicit model never silently substitutes.");
        var transcriber = new PumasSpeechTranscriber(); transcriber.Configure(Settings);
        Check(transcriber.Capability.Status == SpeechAvailability.Unsupported, "Installed microphone remains gated despite configured model.");
        try { await transcriber.TranscribeAsync(Samples, 48000, default); throw new Exception("Gate bypassed"); }
        catch (NotSupportedException) { Check(true, "No fake qualification of installed audio."); }
    }

    private static void EncodingAllocationBound()
    {
        var escaped = Settings with { Model = new string('"', 256), Profile = new string('a', 128), Language = "zh" };
        using var client = new PumasAudioTextClient(escaped, new FakeHandler((_, _) => throw new Exception("Encoding must not use transport")), TimeSpan.FromSeconds(5));
        var encode = typeof(PumasAudioTextClient).GetMethod("Encode", BindingFlags.Instance | BindingFlags.NonPublic)!;
        // Warm reflection/JIT before measuring only the synchronous production encoder.
        var warm = (byte[])encode.Invoke(client, [new byte[8], 48000, 1, "owned-encoding", Settings.Model, Settings.Profile])!;
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(warm);
        foreach (var length in new[] { 1, 2, 3, 8, 1024 * 1024, 24 * 1024 * 1024 - 16384 })
        {
            var pcm = new byte[length]; Array.Fill(pcm, (byte)0x4b);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var body = (byte[])encode.Invoke(client, [pcm, 192000, pcm.Length / 8, "owned-encoding", escaped.Model, escaped.Profile])!;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            try
            {
                Console.WriteLine($"Synthetic encoding allocation: body={body.Length} allocated={allocated}");
                Check(allocated <= body.Length * 3L + 16384, "Encoding allocates only owned base64 token, JSON staging and transport body, without discarded growth or hidden audio buffers.");
                using var json = JsonDocument.Parse(body);
                Check(json.RootElement.GetProperty("input").GetProperty("data_base64").GetBytesFromBase64().SequenceEqual(pcm), "Synthetic PCM wire bytes survive bounded encoding at base64 and near-limit sizes.");
                Check(json.RootElement.GetProperty("model").GetString() == escaped.Model && json.RootElement.GetProperty("profile").GetString() == escaped.Profile, "Worst-case escaped metadata retains exact selected identity.");
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(pcm); System.Security.Cryptography.CryptographicOperations.ZeroMemory(body); }
        }
    }

    private static void EncodingBufferCustody()
    {
        foreach (var failure in new[] { false, true })
        {
            var buffer = new PumasAudioTextClient.AudioEncodingBuffer(8192);
            Check(MemoryMarshal.TryGetArray<byte>(buffer.GetMemory(1), out var first), "Encoding buffer owns an inspectable managed array.");
            try
            {
                using var writer = new Utf8JsonWriter(buffer);
                writer.WriteStartObject(); writer.WriteBase64String("data", new byte[] { 0x4b, 0x4b, 0x4b }); writer.Flush();
                Check(MemoryMarshal.TryGetArray<byte>(buffer.GetMemory(4096), out var next) && ReferenceEquals(first.Array, next.Array), "Writer reservations keep the same owned array after encoded audio.");
                Check(first.Array!.Any(b => b != 0), "Synthetic base64 is present before owned buffer cleanup.");
                if (failure) writer.WriteEndArray(); // Invalid JSON state after audio was staged.
                else writer.WriteEndObject();
            }
            catch (InvalidOperationException) when (failure) { Check(true, "Writer failure after audio staging propagates."); }
            finally { buffer.Dispose(); }
            Check(first.Array!.All(b => b == 0), "Every byte, including uncommitted writer reservations, clears on success or exception.");
            try { buffer.GetMemory(); throw new Exception("Disposed encoding storage reused"); }
            catch (ObjectDisposedException) { Check(true, "Disposed encoding buffer cannot accept new bytes."); }
        }
        using var bounded = new PumasAudioTextClient.AudioEncodingBuffer(16);
        Check(MemoryMarshal.TryGetArray<byte>(bounded.GetMemory(), out var owned), "Bounded buffer owns its sole array.");
        bounded.GetSpan().Fill(0x4b); bounded.Advance(16);
        try { bounded.GetMemory(1); throw new Exception("Encoding buffer grew beyond fixed capacity"); }
        catch (Exception error) when (error.GetType().Name == "ProtocolException") { Check(true, "Exhausted capacity fails closed instead of allocating another audio array."); }
        bounded.Dispose(); Check(owned.Array!.All(b => b == 0), "Capacity failure leaves no retained encoded bytes after cleanup.");
    }

    private static async Task InvalidInputAndConfig()
    {
        foreach (var rate in new[] { 0, 7999, 192001 })
        {
            var h = new FakeHandler((_, _) => throw new Exception("Unexpected transport")); using var c = new PumasAudioTextClient(Settings, h, TimeSpan.FromSeconds(5));
            Check((await c.GenerateAsync(Samples, rate)).Code == "invalid_audio" && h.Requests == 0, "Invalid actual sample rate rejected before wire.");
        }
        foreach (var samples in new StereoSample[][] { [], [new(float.NaN, 0)], [new(0, float.PositiveInfinity)], [new(1.01f, 0)], new StereoSample[8000 * 30 + 1] })
        {
            var h = new FakeHandler((_, _) => throw new Exception("Unexpected transport")); using var c = new PumasAudioTextClient(Settings, h, TimeSpan.FromSeconds(5));
            Check((await c.GenerateAsync(samples, 8000)).Code == "invalid_audio" && h.Requests == 0, "Empty/nonfinite/out-of-range/over30-second PCM rejected.");
        }
        foreach (var settings in new[] { Settings with { Enabled = false }, Settings with { Provider = DialogueProvider.OpenRouter, Endpoint = AiSettings.DefaultEndpoint(DialogueProvider.OpenRouter) } })
        {
            var h = new FakeHandler((_, _) => throw new Exception("Unexpected transport")); using var c = new PumasAudioTextClient(settings, h, TimeSpan.FromSeconds(5));
            Check(!(await c.GenerateAsync(Samples, 48000)).Success && h.Requests == 0, "Disabled or hosted preferences cannot reroute local audio.");
        }
        foreach (var bad in new[] { Settings with { Language = "xx" }, Settings with { Profile = "../path" }, Settings with { Endpoint = "http://localhost:8080/" }, Settings with { Endpoint = "https://huggingface.co/" } })
        {
            try { bad.Validate(); throw new Exception("Invalid configuration accepted"); }
            catch (InvalidDataException) { Check(true, "Configuration rejects invented language/profile/remote transport."); }
        }
        var tooLongHandler = new FakeHandler((_, _) => throw new Exception("Long model reached transport"));
        using (var tooLong = new PumasAudioTextClient(Settings with { Model = new string('x', 257) }, tooLongHandler, TimeSpan.FromSeconds(5)))
            Check((await tooLong.DiscoverAsync()).Code == "invalid_model" && tooLongHandler.Requests == 0, "Pinned producer model byte bound applies without rewriting existing preferences.");
        var old = JsonSerializer.Deserialize<SpeechServiceSettings>("{\"Enabled\":true,\"Endpoint\":\"http://127.0.0.1:8080/\",\"Model\":\"speech\"}")!.Validate();
        Check(old.Language == "en" && old.Profile == "", "Existing owner configuration stays backward compatible.");
        var roundTrip = JsonSerializer.Deserialize<SpeechServiceSettings>(JsonSerializer.Serialize(Settings))!.Validate();
        Check(roundTrip == Settings, "Independent profile/language persist without dialogue settings.");
    }

    private static async Task TerminalRefusals()
    {
        Func<string, HttpResponseMessage>[] failures = [
            id => Response(Terminal("wrong-id")),
            id => Response(Terminal(id).Replace("\"contract_version\":1", "\"contract_version\":2")),
            id => Response(Terminal(id, finish: "")),
            id => Response(Terminal(id).Replace(",\"finish_reason\":\"stop\"", "")),
            id => Response(Terminal(id).Replace("\"kind\":\"text\"", "\"kind\":\"embeddings\"")),
            id => Response(Terminal(id, "\0")),
            id => Response(Terminal(id, new string('x', 16001))),
            id => Response(Terminal(id).Replace("\"contract_version\":1", "\"contract_version\":1,\"contract_version\":1")),
            id => Response("[]"), id => Response("not json"),
            id => Response(Terminal(id), type: "text/html"),
            id => Response(new string('x', 131073)),
            id => Response(ErrorBody(id, "provider_failure", "unknown", "provider dropped the request"), HttpStatusCode.BadGateway),
            id => Response(ErrorBody(null, "transport_lost", "unknown"), HttpStatusCode.BadGateway),
            id => Response(ErrorBody(id, "transport_lost", "invented"), HttpStatusCode.BadGateway),
            id => Response(JsonSerializer.Serialize(new { contract_version = 1, request_id = id, error = new { code = "provider_failure" } }), HttpStatusCode.BadGateway),
            id => Response(ErrorBody("another-request", "invalid_request", "not_admitted"), HttpStatusCode.BadRequest),
            id => Response(JsonSerializer.Serialize(new { contract_version = 1, request_id = id }), HttpStatusCode.BadGateway),
            id => Response("<html>bad gateway</html>", HttpStatusCode.BadGateway, "text/html")
        ];
        foreach (var response in failures)
        {
            PumasAudioTextClient? client = null; byte[]? held = null;
            var handler = new FakeHandler(async (request, token) =>
            {
                if (request.Method == HttpMethod.Get) return Response(Capabilities().ToJsonString());
                held = Body(client!); return response(await RequestId(request));
            });
            using (client = new(Settings, handler, TimeSpan.FromSeconds(5)))
            {
                var reply = await client.GenerateAsync(Samples, 48000);
                Check(!reply.Success && reply.Outcome == AudioOperationOutcome.Unknown && held!.All(b => b == 0) && Body(client) is null, "Invalid/lost/provider terminal cannot publish text or retain consumer PCM.");
                Check((await client.GenerateAsync(Samples, 48000)).Code == "producer_outcome_unknown" && handler.Requests == 2, "Never auto-replay uncertain producer outcome.");
            }
        }
        var calls = 0;
        var refusalHandler = new FakeHandler(async (request, _) =>
        {
            if (request.Method == HttpMethod.Get) return Response(Capabilities().ToJsonString());
            calls++; return Response(JsonSerializer.Serialize(new { contract_version = 1, request_id = await RequestId(request), error = new { code = "capability_unavailable", outcome = "not_admitted" } }), HttpStatusCode.ServiceUnavailable);
        });
        using var retry = new PumasAudioTextClient(Settings, refusalHandler, TimeSpan.FromSeconds(5));
        Check((await retry.GenerateAsync(Samples, 48000)).Outcome == AudioOperationOutcome.NotAdmitted && (await retry.GenerateAsync(Samples, 48000)).Outcome == AudioOperationOutcome.NotAdmitted && calls == 2, "Only explicit correlated non-admission allows a later explicit request.");
    }

    private static async Task NonAdmissionDoesNotLatch()
    {
        // Pumas returns request_id null when it could not read the body; outcome is still
        // authoritative. New error codes pass through. Neither may quarantine the client.
        foreach (var (echo, code) in new[] { (false, "invalid_request"), (true, "request_limit"), (true, "a_future_refusal") })
        {
            var posts = 0;
            var handler = new FakeHandler(async (request, _) =>
            {
                if (request.Method == HttpMethod.Get) return Response(Capabilities().ToJsonString());
                posts++;
                return Response(ErrorBody(echo ? await RequestId(request) : null, code, "not_admitted", $"rejected: {code}\u0007 by fixture"), HttpStatusCode.BadRequest);
            });
            using var client = new PumasAudioTextClient(Settings, handler, TimeSpan.FromSeconds(5));
            var reply = await client.GenerateAsync(Samples, 48000);
            Check(!reply.Success && reply.Code == code && reply.Outcome == AudioOperationOutcome.NotAdmitted, "Not-admitted error decodes by outcome, including a null request_id or new code.");
            Check(reply.Detail == $"rejected: {code}  by fixture", "Pumas error.message surfaces with control characters removed.");
            var again = await client.GenerateAsync(Samples, 48000);
            Check(again.Code == code && again.Outcome == AudioOperationOutcome.NotAdmitted && posts == 2, "Plain non-admission never latches producer uncertainty.");
        }
        var host = SpeechRuntimeAdmission.EvaluateHost(true, System.Runtime.InteropServices.Architecture.X64, 6);
        var transcriber = new PumasSpeechTranscriber(host, SpeechCaptureKind.Microphone, settings => new(settings, new FakeHandler((request, _) =>
            Task.FromResult(request.Method == HttpMethod.Get ? Response(Capabilities().ToJsonString())
                : Response(ErrorBody(null, "invalid_request", "not_admitted", "request body is not valid JSON"), HttpStatusCode.BadRequest))), TimeSpan.FromSeconds(5)));
        transcriber.Configure(Settings with { Runtime = SpeechRuntimeMode.ExperimentalLocalCohere });
        Check((await transcriber.PrepareAsync(default)).Status == SpeechAvailability.Ready, "Explicit selection prepares against served-model discovery.");
        try { await transcriber.TranscribeAsync(Samples, 48000, default); throw new Exception("Rejected request produced a transcript"); }
        catch (SpeechTranscriptionException error)
        {
            Check(error.Outcome == AudioOperationOutcome.NotAdmitted && error.Message.Contains("Pumas: request body is not valid JSON") &&
                error.Detail == "request body is not valid JSON", "Transcriber surfaces Pumas's message to the UI.");
        }
        Check(transcriber.Capability.Status == SpeechAvailability.Ready && (await transcriber.PrepareAsync(default)).Status == SpeechAvailability.Ready,
            "Voice input stays available after a not_admitted rejection.");
        transcriber.Dispose();
    }

    private static async Task AdditiveGrowthIgnored()
    {
        // Contract v1 grows additively: new fields, capabilities, formats, option names,
        // availability reasons, list entries and finish reasons must not break selection.
        var root = Capabilities();
        root["build"] = new JsonObject { ["future"] = true };
        root["supported_contract_versions"] = new JsonArray(1, 2);
        var entry = Entry(root);
        entry["context_tokens"] = 8192; entry["future_field"] = new JsonArray("x");
        var descriptor = Descriptor(root); // audio_transcription, about to sit between new descriptors
        var descriptors = entry["capabilities"]!.AsArray();
        descriptors.Insert(0, JsonNode.Parse("""{"capability":"multimodal_embedding","semantic_task":"multimodal_embedding","input_formats":["png_base64","content_batch"],"output_formats":["embeddings_float32"],"streaming":false,"availability":{"state":"unavailable","reason":"a_future_reason"},"option_bounds":[{"option":"dimensions","minimum":1,"maximum":768}]}"""));
        descriptors.Add(JsonNode.Parse("""{"capability":"decision","semantic_task":"decision","input_formats":["decision"],"output_formats":["decisions"],"streaming":false,"availability":{"state":"available"},"option_bounds":[],"future":{}}"""));
        descriptors.Add(JsonNode.Parse("""{"capability":"a_future_capability","semantic_task":"a_future_task","input_formats":["a_future_format"],"output_formats":["a_future_output"],"streaming":true,"availability":{"state":"a_future_state"},"option_bounds":[{"option":"a_future_option","minimum":0,"maximum":1}]}"""));
        Check(ReferenceEquals(descriptor, descriptors[1]), "Fixture keeps audio_transcription between new descriptors.");
        descriptor["input_formats"]!.AsArray().Add("a_future_pcm"); descriptor["output_formats"]!.AsArray().Add("labels");
        descriptor["availability"]!["observed_at"] = "now"; descriptor["future_hint"] = 1;
        descriptor["option_bounds"]!.AsArray().Insert(0, JsonNode.Parse("""{"option":"a_future_option","minimum":0,"maximum":1}"""));
        root["models"]!.AsArray().Insert(0, JsonNode.Parse("""{"model":"chat-alias","profile":"chat","model_id":"library/chat","provider":"llama_cpp","load_state":"loaded","profile_required":false,"capabilities":[{"capability":"chat_generation"}]}"""));
        root["models"]!.AsArray().Add("a future non-object entry");
        foreach (var finish in new[] { "stop", "content_filter", "a_future_finish" })
        {
            var handler = new FakeHandler(async (request, _) =>
            {
                if (request.Method == HttpMethod.Get) return Response(root.ToJsonString());
                var id = await RequestId(request);
                return Response(JsonSerializer.Serialize(new { contract_version = 1, request_id = id, usage = new { input_tokens = 3 },
                    result = new { kind = "text", text = "Tolerant transcript", finish_reason = finish, language = "en" } }));
            });
            using var client = new PumasAudioTextClient(Settings, handler, TimeSpan.FromSeconds(5));
            var discovery = await client.DiscoverAsync();
            Check(discovery.Advertised && discovery.Model == Settings.Model && discovery.Profile == Settings.Profile, "Unknown fields, descriptors, values and entries are ignored during selection.");
            var reply = await client.GenerateAsync(Samples, 48000);
            Check(reply.Success && reply.Text == "Tolerant transcript" && reply.FinishReason == finish && handler.Requests == 3,
                "Unknown result fields and new finish reasons do not reject a completed transcript.");
        }
    }

    private static async Task ServedModelSelection()
    {
        // The configured identity may be the indexed library model ID; the served alias is sent.
        var root = Capabilities(Settings with { Model = "served-alias" });
        Entry(root)["model_id"] = Settings.Model;
        string? sent = null;
        var handler = new FakeHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Get) return Response(root.ToJsonString());
            using var json = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(token));
            sent = json.RootElement.GetProperty("model").GetString();
            return Response(Terminal(json.RootElement.GetProperty("request_id").GetString()!));
        });
        using (var client = new PumasAudioTextClient(Settings, handler, TimeSpan.FromSeconds(5)))
            Check((await client.GenerateAsync(Samples, 48000)).Success && sent == "served-alias", "Library model ID resolves to its served alias.");

        // Several profiles serve one alias: an empty profile is ambiguous; an exact one selects.
        var multi = Capabilities();
        var second = Entry(multi).DeepClone().AsObject(); second["profile"] = "second-profile";
        multi["models"]!.AsArray().Add(second);
        foreach (var model in multi["models"]!.AsArray()) model!["profile_required"] = true;
        var none = new FakeHandler((_, _) => Task.FromResult(Response(multi.ToJsonString())));
        using (var client = new PumasAudioTextClient(Settings with { Profile = "" }, none, TimeSpan.FromSeconds(5)))
        {
            var refused = await client.DiscoverAsync();
            Check(!refused.Advertised && refused.Code == "ambiguous_model" && refused.Detail.Contains("second-profile"), "Ambiguous served alias names its profiles instead of guessing.");
        }
        using (var client = new PumasAudioTextClient(Settings with { Profile = "second-profile" }, none, TimeSpan.FromSeconds(5)))
            Check((await client.DiscoverAsync()) is { Advertised: true, Profile: "second-profile" }, "Exact profile selects one served entry.");
        using (var client = new PumasAudioTextClient(Settings with { Profile = "absent-profile" }, none, TimeSpan.FromSeconds(5)))
        {
            var missing = await client.DiscoverAsync();
            Check(!missing.Advertised && missing.Code == "model_not_found" && missing.Detail.Contains("controlled-audio-private"), "Missing profile lists the served profiles.");
        }
        var unavailable = Capabilities();
        Descriptor(unavailable)["availability"] = new JsonObject { ["state"] = "unavailable", ["reason"] = "a_future_reason" };
        using (var client = new PumasAudioTextClient(Settings, new FakeHandler((_, _) => Task.FromResult(Response(unavailable.ToJsonString()))), TimeSpan.FromSeconds(5)))
        {
            var refused = await client.DiscoverAsync();
            Check(refused is { Advertised: false, Code: "capability_unavailable", Detail: "a_future_reason" }, "Unknown availability reason is unavailable and reported verbatim.");
        }
    }

    private static async Task CancelAndDisposal()
    {
        foreach (var mode in new[] { "cancel", "timeout", "dispose" })
        {
            var admitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            PumasAudioTextClient? client = null; byte[]? held = null; CancellationToken wireToken = default;
            var handler = new FakeHandler(async (request, token) =>
            {
                if (request.Method == HttpMethod.Get) return Response(Capabilities().ToJsonString());
                held = Body(client!); wireToken = token; admitted.SetResult();
                // Deliberately hold this transport after caller cancellation. It
                // models no producer cessation and must retain consumer custody.
                await release.Task; token.ThrowIfCancellationRequested(); return Response(Terminal(await RequestId(request)));
            });
            using var cancel = new CancellationTokenSource();
            using (client = new(Settings, handler, mode == "timeout" ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromSeconds(5)))
            {
                var pending = client.GenerateAsync(Samples, 48000, cancel.Token); await admitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Check((await client.GenerateAsync(Samples, 48000)).Code == "busy", "Concurrent request refused, no audio replay.");
                if (mode == "dispose") client.Dispose(); else if (mode == "cancel") cancel.Cancel();
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!wireToken.IsCancellationRequested && DateTime.UtcNow < deadline) await Task.Delay(2);
                Check(wireToken.IsCancellationRequested && !pending.IsCompleted && held!.Any(b => b != 0), "Cancellation is requested while owned transport/body remains unsettled.");
                release.SetResult(); var reply = await pending;
                Check(!reply.Success && reply.Code == (mode == "timeout" ? "timeout" : "cancelled") && reply.Outcome == AudioOperationOutcome.Unknown, "Cancel/timeout/dispose after wire retains unknown producer outcome.");
                Check(held!.All(b => b == 0) && Body(client) is null, "Clear owned PCM/base64 only after original transport settles.");
                Check((await client.GenerateAsync(Samples, 48000)).Code == (mode == "dispose" ? "disposed" : "producer_outcome_unknown") && handler.Requests == 2, "No cancelled-generation replay.");
            }
            Check(handler.Disposed, "Handler released after transport settlement, including delayed Dispose.");
        }
        var h = new FakeHandler((_, _) => throw new Exception("Pre-cancelled request reached transport"));
        using var c = new PumasAudioTextClient(Settings, h, TimeSpan.FromSeconds(5)); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Check((await c.GenerateAsync(Samples, 48000, cancelled.Token)).Outcome == AudioOperationOutcome.NotAdmitted && h.Requests == 0, "Pre-cancellation makes no wire effect.");
    }

    private static async Task RealLoopbackTransport()
    {
        using var fixture = new LoopbackFixture(Capabilities());
        using var client = new PumasAudioTextClient(Settings with { Endpoint = fixture.Endpoint });
        var reply = await client.GenerateAsync(Samples, 48000);
        await fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Check(reply.Success && reply.Text == "Synthetic TCP transcript" && fixture.Paths.SequenceEqual(new[] { "/v1/served-models", "/v1/model-operations" }), "Actual loopback HTTP generic consumer succeeds against synthetic transport only.");
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Requests; public bool Disposed;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Requests++; return send(request, cancellationToken); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private sealed class LoopbackFixture : IDisposable
    {
        private readonly System.Net.Sockets.TcpListener _listener = new(IPAddress.Loopback, 0);
        public string Endpoint { get; }
        public Task Completion { get; }
        public List<string> Paths { get; } = [];
        public LoopbackFixture(JsonObject capabilities)
        {
            _listener.Start(); Endpoint = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/";
            Completion = Serve(capabilities);
        }
        private async Task Serve(JsonObject capabilities)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var tcp = await _listener.AcceptTcpClientAsync(); await using var stream = tcp.GetStream();
                var header = new List<byte>(); var single = new byte[1];
                while (header.Count < 16384)
                {
                    if (await stream.ReadAsync(single) != 1) throw new Exception("Fixture HTTP closed");
                    header.Add(single[0]); if (header.Count >= 4 && header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
                }
                var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
                var path = lines[0].Split(' ')[1].Split('?')[0]; Paths.Add(path);
                var length = lines.Where(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Select(l => int.Parse(l.Split(':')[1])).SingleOrDefault();
                var body = new byte[length]; await stream.ReadExactlyAsync(body);
                string response;
                if (path == "/v1/served-models") response = capabilities.ToJsonString();
                else
                {
                    using var request = JsonDocument.Parse(body); var root = request.RootElement;
                    Check(root.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] {
                        "contract_version", "request_id", "model", "profile", "capability", "input", "output", "options", "stream" }.Order()),
                        "TCP interoperability: exact current Pumas OperationRequest fields, no unknown producer fields.");
                    Check(root.GetProperty("capability").GetString() == "audio_transcription" && root.GetProperty("input").GetProperty("encoding").GetString() == "pcm_f32le",
                        "TCP interoperability: producer's declared capability and supported PCM encoding.");
                    response = Terminal(root.GetProperty("request_id").GetString()!, "Synthetic TCP transcript");
                }
                var bytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"));
                await stream.WriteAsync(bytes);
            }
        }
        public void Dispose() => _listener.Stop();
    }
}
