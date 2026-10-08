using System.Net;
using System.Text;
using System.Text.Json;
using Lanternwake.Core;
using Lanternwake.Conversation;

var passed = 0;
void Require(bool condition, string claim) { if (!condition) throw new Exception(claim); passed++; }
void Reject(Action action) { try { action(); } catch (InvalidDataException) { passed++; return; } throw new Exception("Invalid settings accepted"); }
var local = new AiSettings(DialogueProvider.Pumas, true, "http://127.0.0.1:8080/", "game-dialogue");
var hosted = new AiSettings(DialogueProvider.OpenRouter, true, "https://openrouter.ai/api/v1/", "test/dialogue");
var directory = Path.Combine(Path.GetTempPath(), "lanternwake-ai-test-" + Guid.NewGuid().ToString("N"));
var path = Path.Combine(directory, "ai-settings.json");
try
{
    Require(JsonSerializer.Serialize(AiSettings.Load(path, local)) == JsonSerializer.Serialize(local), "initial environment settings retained");
    hosted.Save(path);
    Require(JsonSerializer.Serialize(AiSettings.Load(path, local)) == JsonSerializer.Serialize(hosted), "provider URL model enabled preference persist over defaults");
    Require(!File.ReadAllText(path).Contains("ApiKey"), "settings do not contain a credential field");
    var saved = File.ReadAllBytes(path);
    Reject(() => (local with { Endpoint = "https://remote.invalid/" }).Save(path));
    Require(File.ReadAllBytes(path).SequenceEqual(saved), "invalid settings preserve previous preferences");
    Reject(() => (local with { Endpoint = "http://user:password@127.0.0.1/" }).Validate());
    Reject(() => (hosted with { Endpoint = "https://remote.invalid/api/v1/" }).Validate());
    Reject(() => (hosted with { Endpoint = "http://openrouter.ai/api/v1/" }).Validate());
    Reject(() => (local with { Model = "" }).Validate());
    Require((local with { Model = "", DialogueEnabled = false }).Validate().Model == "", "AI off does not require a model");
    Reject(() => (hosted with { Version = 2 }).Validate());
    var separate = hosted with
    {
        Transcription = new(true, "http://127.0.0.1:8081/", "speech-recognizer"),
        CharacterSpeech = new(true, "http://127.0.0.1:8082/", "speech-synthesizer"),
        CharacterVoices = new() { ["nessa"] = "voice-one", ["maren"] = "voice-two" }
    };
    separate.Save(path);
    var restored = AiSettings.Load(path, local);
    Require(restored.Model == hosted.Model && restored.Transcription == separate.Transcription &&
        restored.CharacterSpeech == separate.CharacterSpeech && restored.CharacterVoices["nessa"] == "voice-one" &&
        restored.CharacterVoices["maren"] == "voice-two", "dialogue, transcription, synthesis and character assignments persist independently");
    var hostedTranscription = separate with
    {
        Provider = DialogueProvider.Pumas, Endpoint = local.Endpoint,
        Transcription = new(true, hosted.Endpoint, "hosted/transcription") { Provider = DialogueProvider.OpenRouter }
    };
    hostedTranscription.Save(path);
    var hostedRestored = AiSettings.Load(path, local);
    Require(hostedRestored.Provider == DialogueProvider.Pumas &&
        hostedRestored.Transcription.Provider == DialogueProvider.OpenRouter &&
        hostedRestored.Transcription.Endpoint == hosted.Endpoint && hostedRestored.Transcription.Model == "hosted/transcription" &&
        hostedRestored.CharacterSpeech.Provider == DialogueProvider.Pumas && hostedRestored.CharacterSpeech.Endpoint == separate.CharacterSpeech.Endpoint,
        "transcription provider persists independently from dialogue and synthesis");
    var hostedVoices = hostedTranscription with
    {
        Transcription = separate.Transcription,
        CharacterSpeech = new(true, hosted.Endpoint, "hosted/synthesis") { Provider = DialogueProvider.OpenRouter }
    };
    hostedVoices.Save(path);
    hostedRestored = AiSettings.Load(path, local);
    Require(hostedRestored.Provider == DialogueProvider.Pumas && hostedRestored.Transcription.Provider == DialogueProvider.Pumas &&
        hostedRestored.CharacterSpeech.Provider == DialogueProvider.OpenRouter && hostedRestored.CharacterSpeech.Model == "hosted/synthesis",
        "character voice provider persists independently from dialogue and transcription");
    Reject(() => (hostedVoices with { CharacterSpeech = hostedVoices.CharacterSpeech with { Endpoint = "http://127.0.0.1:8082/" } }).Validate());
    Reject(() => (separate with { Transcription = separate.Transcription with { Provider = (DialogueProvider)99 } }).Validate());
    File.WriteAllText(path, JsonSerializer.Serialize(new { Version = 1, hosted.Provider, hosted.DialogueEnabled, hosted.Endpoint, hosted.Model,
        Transcription = new { Enabled = true, Endpoint = "http://127.0.0.1:8081/", Model = "legacy/transcription" },
        CharacterSpeech = new { Enabled = true, Endpoint = "http://127.0.0.1:8082/", Model = "legacy/voice" } }));
    var legacy = AiSettings.Load(path, local);
    Require(legacy.Transcription.Provider == DialogueProvider.Pumas && legacy.Transcription.Model == "legacy/transcription" &&
        legacy.CharacterSpeech.Provider == DialogueProvider.Pumas && legacy.CharacterSpeech.Model == "legacy/voice",
        "existing speech preferences default to Pumas while retaining URL and model");
    separate.Save(path);
    var previous = File.ReadAllBytes(path);
    Reject(() => (separate with { Transcription = new(Endpoint: "https://remote.invalid/") }).Save(path));
    Require(File.ReadAllBytes(path).SequenceEqual(previous), "invalid speech URL does not overwrite settings");
    Reject(() => (hosted with { Model = "sk-or-fixture-key" }).Save(path));
    Reject(() => (hosted with { Model = "  sk-or-fixture-key  " }).Save(path));
    Reject(() => (separate with { CharacterVoices = new() { ["nessa"] = "sk-or-fixture-key" } }).Save(path));
    File.WriteAllText(path, JsonSerializer.Serialize(new { Version = 1, hosted.Provider, hosted.DialogueEnabled, hosted.Endpoint, hosted.Model }));
    Require(AiSettings.Load(path, local).Transcription == new SpeechServiceSettings(), "existing dialogue settings load with independent speech defaults");
    File.WriteAllText(path, "{}"); Reject(() => AiSettings.Load(path, local));
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

const string status = """{"jsonrpc":"2.0","id":"lanternwake-status","result":{"success":true,"snapshot":{"schema_version":1,"served_models":[{"model_id":"library/model","model_alias":"game-dialogue","profile_id":"game-cpu","provider":"llama_cpp","load_state":"loaded"},{"model_id":"not-ready","profile_id":"other","provider":"llama_cpp","load_state":"loading"},{"model_id":"wrong-provider","profile_id":"other","provider":"torch","load_state":"loaded"}],"router_profiles":[]}}}""";
using (var handler = new Fixture(status))
using (var connection = new AiConnection(local, "test-key-must-not-go-to-pumas", handler))
{
    var models = await connection.ListModelsAsync();
    Require(models.Success && models.Models.Single().Id == "game-dialogue", "selector lists actual loaded compatible Pumas aliases only");
    Require(handler.Url == "http://127.0.0.1:8080/rpc" && handler.Body.Contains("get_serving_status"), "discovery uses existing Pumas read-only RPC");
    Require(handler.Authorization is null, "OpenRouter credentials never go to Pumas");
}
foreach (var malformed in new[] { "{}", status.Replace("\"schema_version\":1", "\"schema_version\":\"1\""), status.Replace("lanternwake-status", "wrong-id") })
{
    using var connection = new AiConnection(local, handler: new Fixture(malformed));
    Require(!(await connection.ListModelsAsync()).Success, "incompatible serving response is not readiness");
}
using (var connection = new AiConnection(local, handler: new Fixture(status.Replace("\"router_profiles\":[]", "\"router_profiles\":[{\"profile_id\":\"game-cpu\",\"observation_state\":\"stale\",\"catalog_state\":\"current\"}]"))))
    Require((await connection.ListModelsAsync()).Models.Length == 0, "stale router models cannot be chosen");
using (var connection = new AiConnection(local, handler: new Fixture(status.Replace("wrong-provider", "game-dialogue"))))
    Require((await connection.ListModelsAsync()).Models.Length == 0, "an alias colliding with another loaded model identity cannot be chosen");

const string catalog = """{"data":[{"id":"test/dialogue","name":"Dialogue test","architecture":{"input_modalities":["text"],"output_modalities":["text"]}},{"id":"test/images","name":"Images","architecture":{"input_modalities":["text"],"output_modalities":["image"]}}]}""";
using (var handler = new Fixture(catalog))
using (var connection = new AiConnection(hosted, "fixture-key", handler))
{
    var models = await connection.ListModelsAsync();
    Require(models.Success && models.Models.Single().Id == "test/dialogue", "OpenRouter selector contains text conversation models");
    Require(handler.Url == "https://openrouter.ai/api/v1/models" && handler.Authorization == "Bearer fixture-key", "catalog uses documented OpenRouter endpoint and authentication");
}
const string completion = """{"choices":[{"message":{"content":"The ferry stays in harbor."}}]}""";
using (var handler = new Fixture(completion))
using (var connection = new AiConnection(hosted, "fixture-key", handler))
{
    var reply = await connection.GenerateOpenRouterAsync("nessa", "Only the storm is known.", "Can the ferry leave?");
    Require(reply.Success && reply.Text == "The ferry stays in harbor.", "OpenRouter reply reaches the common text boundary");
    using var payload = JsonDocument.Parse(handler.Body);
    Require(handler.Url == "https://openrouter.ai/api/v1/chat/completions" && payload.RootElement.GetProperty("model").GetString() == hosted.Model, "selected provider model is used for generation");
    Require(!payload.RootElement.GetProperty("stream").GetBoolean(), "bounded non-streaming reply");
    Require(!handler.Body.Contains("fixture-key"), "credential remains out of dialogue body");
}
foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.PaymentRequired, HttpStatusCode.TooManyRequests })
{
    using var connection = new AiConnection(hosted, "fixture-key", new Fixture("{}", code));
    var reply = await connection.GenerateOpenRouterAsync("nessa", "Storm.", "Hello");
    Require(!reply.Success && reply.ErrorCode == (code == HttpStatusCode.Unauthorized ? "authentication_failed" : code == HttpStatusCode.PaymentRequired ? "credits_required" : "rate_limited"), "provider failure is explicit and not fabricated dialogue");
}
using (var handler = new Fixture(completion))
using (var connection = new AiConnection(hosted, handler: handler))
{
    Require((await connection.GenerateOpenRouterAsync("nessa", "Storm.", "Hello")).ErrorCode == "api_key_missing", "missing key is actionable");
    Require(handler.Url is null, "missing key does not contact a service");
}
foreach (var malformed in new[] { "{}", "{\"choices\":[null]}", "{\"choices\":[{\"message\":{\"content\":null}}]}", completion.Replace("\"choices\":", "\"choices\":[],\"choices\":"), completion.Replace("The ferry stays in harbor.", new string('x', 4001)) })
{
    using var connection = new AiConnection(hosted, "fixture-key", new Fixture(malformed));
    Require((await connection.GenerateOpenRouterAsync("nessa", "Storm.", "Hello")).ErrorCode == "invalid_response", "malformed and oversized text is rejected");
}
using (var cancellation = new CancellationTokenSource())
using (var connection = new AiConnection(hosted, "fixture-key", new Fixture(completion)))
{
    cancellation.Cancel();
    Require((await connection.ListModelsAsync(cancellation.Token)).ErrorCode == "cancelled", "catalog cancellation terminates");
    Require((await connection.GenerateOpenRouterAsync("nessa", "Storm.", "Hello", cancellation.Token)).ErrorCode == "cancelled", "generation cancellation terminates");
}
// These checks never access the player's desktop credentials.
var disabledStore = new DisabledCredentialStore();
Require(await disabledStore.LoadAsync(CancellationToken.None) is null, "automated checks cannot load user credentials");
try { await disabledStore.SaveAsync("fixture-only-key", CancellationToken.None); throw new Exception("Unexpected secret persistence"); }
catch (IOException error) { Require(!error.Message.Contains("fixture-only-key"), "disabled storage rejects persistence without echoing the key"); }
try { await new DesktopCredentialStore().SaveAsync("fixture\nkey", CancellationToken.None); throw new Exception("Invalid key accepted"); }
catch (InvalidDataException) { passed++; }

if (args.Contains("--credential-store-smoke"))
{
    // Opt-in: use a unique account, never read or modify the real OpenRouter entry.
    var store = new DesktopCredentialStore("verification-" + Guid.NewGuid().ToString("N"));
    using var token = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        Require(await store.LoadAsync(token.Token) is null, "isolated desktop credential entry starts absent");
        await store.SaveAsync("lanternwake-synthetic-fixture-one", token.Token);
        Require(await store.LoadAsync(token.Token) == "lanternwake-synthetic-fixture-one", "desktop keyring save and reload");
        await store.SaveAsync("lanternwake-synthetic-fixture-two", token.Token);
        Require(await store.LoadAsync(token.Token) == "lanternwake-synthetic-fixture-two", "desktop keyring replaces its own entry");
        await store.ForgetAsync(token.Token);
        Require(await store.LoadAsync(token.Token) is null, "desktop keyring forget removes its own entry");
        Console.WriteLine("PASS desktop keyring lifecycle using synthetic credentials; no user key accessed.");
    }
    finally { using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await store.ForgetAsync(cleanup.Token); }
}
Console.WriteLine($"PASS {passed} AI configuration and provider assertions (HTTP fixtures; no live inference claim).");

sealed class Fixture(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public string? Url, Authorization;
    public string Body = "";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Url = request.RequestUri!.AbsoluteUri; Authorization = request.Headers.Authorization?.ToString();
        Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
