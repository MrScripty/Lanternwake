using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lanternwake.Conversation;
using Lanternwake.Core;
using System.Diagnostics;

var passed = 0;
await Check("LiveSmoke uses production character identity and exact wire context", async () =>
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
    var gameSource = File.ReadAllText(Path.Combine(root, "Scripts/Presentation/GameView.cs"));
    Require(gameSource.Contains("_pumas.GenerateAsync(chat.CharacterId, _session.ConversationContext(), input,"), "production identity/context call");
    var storyPath = Path.Combine(root, "Content/story.json");
    var story = Story.Parse(File.ReadAllText(storyPath));
    var session = new StorySession(story);
    while (session.Beat.Conversation is null)
    {
        if (session.Beat.Activity is { } activity) session.AnswerActivity(activity.CorrectIndex);
        Require(session.Advance(), "conversation reached");
    }
    var chat = session.Beat.Conversation!;
    Require(story.Characters.Single(c => c.Id == chat.CharacterId).Name != chat.CharacterId, "fixture distinguishes display name from ID");
    await using var production = new FakePumas();
    using var client = new PumasClient(production.Uri, "game-dialogue");
    Require((await client.GenerateAsync(chat.CharacterId, session.ConversationContext(), chat.Suggestions[0])).Success, "production-equivalent request");
    await using var smoke = new FakePumas();
    var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root };
    start.ArgumentList.Add("run"); start.ArgumentList.Add("--project"); start.ArgumentList.Add("integration/pumas/LiveSmoke/LiveSmoke.csproj");
    start.ArgumentList.Add("--no-build"); start.ArgumentList.Add("--"); start.ArgumentList.Add(storyPath); start.ArgumentList.Add(session.Beat.Id); start.ArgumentList.Add(chat.Suggestions[0]);
    start.Environment["LANTERNWAKE_PUMAS_URL"] = smoke.Uri.AbsoluteUri;
    start.Environment["LANTERNWAKE_PUMAS_MODEL"] = "game-dialogue";
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
    try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
    finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    Require(process.ExitCode == 0, "LiveSmoke succeeds: " + await stderr);
    using var receipt = JsonDocument.Parse(await stdout);
    Require(receipt.RootElement.GetProperty("canonicalUnchanged").GetBoolean(), "helper preserves canonical session");
    Require(smoke.Paths.SequenceEqual(production.Paths) && smoke.Bodies.SequenceEqual(production.Bodies), "identical serialized identity, context and user turn");
});
await Check("direct Pumas status and generation", async () =>
{
    await using var server = new FakePumas();
    using var client = new PumasClient(server.Uri, "game-dialogue");
    var reply = await client.GenerateAsync("Mara", "The harbor light is out.", "What happened?");
    Require(reply.Success && reply.Text == "Mind the old pier.", "text result");
    Require(server.Paths.SequenceEqual(new[] { "/rpc", "/v1/chat/completions" }), "real API routes");
    using var rpc = JsonDocument.Parse(server.Bodies.First());
    Require(rpc.RootElement.GetProperty("method").GetString() == "get_serving_status", "RPC method");
    using var chat = JsonDocument.Parse(server.Bodies.Last());
    Require(chat.RootElement.GetProperty("model").GetString() == "game-dialogue", "model identity");
    Require(!chat.RootElement.GetProperty("stream").GetBoolean(), "nonstream contract");
});
await Check("non-llama provider rejected before generation", async () =>
{
    await using var server = new FakePumas { Status = FakePumas.StatusJson.Replace("llama_cpp", "ollama") };
    using var client = new PumasClient(server.Uri, "game-dialogue");
    Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).ErrorCode == "wrong_provider", "provider");
    Require(server.Paths.Count == 1, "no generation");
});
await Check("library-only RPC is unavailable", async () =>
{
    await using var server = new FakePumas { Status = """{"jsonrpc":"2.0","id":"lanternwake-status","error":{"code":-32601}}""" };
    using var client = new PumasClient(server.Uri, "game-dialogue");
    Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).ErrorCode == "pumas_contract", "capability");
});
await Check("wrong schema and wrong type are rejected", async () =>
{
    foreach (var value in new[] { "2", "\"1\"", "null" })
    {
        await using var server = new FakePumas { Status = FakePumas.StatusJson.Replace("\"schema_version\":1", "\"schema_version\":" + value) };
        using var client = new PumasClient(server.Uri, "game-dialogue");
        Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).ErrorCode == "pumas_contract", "schema type");
    }
});
await Check("missing model is not silently substituted", async () =>
{
    await using var server = new FakePumas();
    using var client = new PumasClient(server.Uri, "missing");
    Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).ErrorCode == "model_unavailable", "model");
});
await Check("Pumas95 current and pending router catalogs permit generation", async () =>
{
    foreach (var state in new[] { "current", "pending" })
        await WithStatus(EditStatus(s => s["router_profiles"]![0]!["catalog_state"] = state), "", true);
});
await Check("stale selected router fails before generation", async () =>
{
    foreach (var state in new[] { "connecting", "unavailable", "unknown" })
        await WithStatus(EditStatus(s => s["router_profiles"]![0]!["observation_state"] = state), "model_unavailable");
    await WithStatus(EditStatus(s => s["router_profiles"]![0]!["catalog_state"] = "uncertain"), "model_unavailable");
});
await Check("dedicated profiles need no router observation", async () =>
{
    await WithStatus(EditStatus(s => s["router_profiles"] = new JsonArray()), "", true);
    await WithStatus(EditStatus(s => s.AsObject().Remove("router_profiles")), "", true);
});
await Check("unrelated stale router does not block selected profile", async () =>
{
    await WithStatus(EditStatus(s => s["router_profiles"]!.AsArray().Add(new JsonObject
        { ["profile_id"] = "other", ["observation_state"] = "unavailable", ["catalog_state"] = "uncertain" })), "", true);
    await WithStatus(EditStatus(s => {
        s["router_profiles"]!.AsArray().Add(new JsonObject { ["profile_id"] = "other", ["observation_state"] = 42 });
        s["router_profiles"]!.AsArray().Add(new JsonObject { ["profile_id"] = "other" });
        s["router_profiles"]!.AsArray().Add(new JsonObject { ["profile_id"] = 42 });
    }), "", true);
});
await Check("alias takes precedence over matching library ID as in Pumas95 gateway", async () =>
{
    await WithStatus(EditStatus(s => {
        var other = s["served_models"]![0]!.DeepClone();
        other["model_id"] = "game-dialogue"; other["model_alias"] = "other-alias"; other["provider"] = "ollama";
        s["served_models"]!.AsArray().Insert(0, other);
    }), "", true);
});
await Check("duplicate aliases and ambiguous base IDs never generate", async () =>
{
    await WithStatus(EditStatus(s => s["served_models"]!.AsArray().Add(s["served_models"]![0]!.DeepClone())), "model_unavailable");
    await using var server = new FakePumas { Status = EditStatus(s => {
        s["served_models"]![0]!["model_alias"] = "alias-one";
        var other = s["served_models"]![0]!.DeepClone(); other["model_alias"] = "alias-two";
        s["served_models"]!.AsArray().Add(other);
    }) };
    using var client = new PumasClient(server.Uri, "library/model");
    Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).ErrorCode == "model_unavailable", "ambiguous base ID");
    Require(server.Paths.Count == 1, "no generation");
});
await Check("base ID selects one loaded model", async () =>
{
    await using var server = new FakePumas();
    using var client = new PumasClient(server.Uri, "library/model");
    Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).Success, "base ID");
});
await Check("nonloaded states never generate", async () =>
{
    foreach (var state in new[] { "requested", "loading", "unloading", "unloaded", "failed" })
        await WithStatus(EditStatus(s => s["served_models"]![0]!["load_state"] = state), "model_unavailable");
});
await Check("router collection shape and selected profile duplicates fail closed", async () =>
{
    await WithStatus(EditStatus(s => s["router_profiles"] = new JsonObject()), "pumas_contract");
    await WithStatus(EditStatus(s => s["router_profiles"]!.AsArray().Add(1)), "pumas_contract");
    await WithStatus(EditStatus(s => s["router_profiles"]!.AsArray().Add(s["router_profiles"]![0]!.DeepClone())), "pumas_contract");
    await WithStatus(EditStatus(s => s["served_models"]![0]!["profile_id"] = " "), "pumas_contract");
});
await Check("completion must be assistant dialogue", async () =>
{
    foreach (var role in new[] { "user", "tool", "system", "" })
    {
        await using var server = new FakePumas { Completion = JsonSerializer.Serialize(new
            { choices = new[] { new { message = new { role, content = "Not an assistant reply." } } } }) };
        using var client = new PumasClient(server.Uri, "game-dialogue");
        Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).ErrorCode == "invalid_response", "message role");
    }
});
await Check("malformed completion is not fabricated", async () =>
{
    await using var server = new FakePumas { Completion = """{"choices":[{"message":{"role":"assistant","content":null}}]}""" };
    using var client = new PumasClient(server.Uri, "game-dialogue");
    Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).ErrorCode == "invalid_response", "null reply");
});
await Check("duplicate JSON fields rejected", async () =>
{
    const string valid = """{"choices":[{"message":{"role":"assistant","content":"Mind the old pier."}}]}""";
    await using var control = new FakePumas { Completion = valid };
    using var controlClient = new PumasClient(control.Uri, "game-dialogue");
    Require((await controlClient.GenerateAsync("Mara", "Harbor.", "Hello")).Success, "same response without a duplicated field is valid");
    await using var server = new FakePumas { Completion = """{"choices":[{"message":{"role":"assistant","content":"Mind the old pier."}}],"choices":[{"message":{"role":"assistant","content":"Mind the old pier."}}]}""" };
    using var client = new PumasClient(server.Uri, "game-dialogue");
    Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).ErrorCode == "invalid_response", "duplicate");
});
await Check("caller cancellation observed", async () =>
{
    await using var server = new FakePumas();
    using var client = new PumasClient(server.Uri, "game-dialogue");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    Require((await client.GenerateAsync("Mara", "Harbor.", "Hello", cancellation.Token)).ErrorCode == "cancelled", "cancelled");
});
await Check("in-flight cancellation releases single-generation ownership without replay", async () =>
{
    await using var server = new FakePumas { HoldCompletion = true };
    using var client = new PumasClient(server.Uri, "game-dialogue");
    using var cancellation = new CancellationTokenSource();
    var pending = client.GenerateAsync("Mara", "Harbor.", "Hello", cancellation.Token);
    await server.CompletionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Require((await client.GenerateAsync("Mara", "Harbor.", "Again")).ErrorCode == "busy", "one generation owner");
    cancellation.Cancel();
    Require((await pending.WaitAsync(TimeSpan.FromSeconds(5))).ErrorCode == "cancelled", "cancelled during response");
    Require(server.Paths.Count == 2, "cancelled request never replayed");
    server.CompletionReleased.TrySetResult();
    server.HoldCompletion = false;
    Require((await client.GenerateAsync("Mara", "Harbor.", "New turn")).Success, "ownership released for explicit new turn");
    Require(server.Paths.Count == 4, "exactly two explicit turns");
});
await Check("HTTP error is unavailable", async () =>
{
    await using var server = new FakePumas { HttpStatus = 503 };
    using var client = new PumasClient(server.Uri, "game-dialogue");
    Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).ErrorCode == "pumas_unavailable", "503");
});
await Check("oversized dialogue rejected locally", async () =>
{
    await using var server = new FakePumas();
    using var client = new PumasClient(server.Uri, "game-dialogue");
    Require((await client.GenerateAsync("Mara", "Harbor.", new string('x', 2001))).ErrorCode == "invalid_request", "bounds");
    Require(server.Paths.Count == 0, "no request");
});
await Check("remote endpoint cannot receive dialogue", () =>
{
    try { using var client = new PumasClient(new Uri("http://example.com:8080"), "game"); }
    catch (ArgumentException) { return Task.CompletedTask; }
    throw new Exception("remote endpoint accepted");
});
Console.WriteLine($"PASS: {passed} direct-client contract scenarios (simulated Pumas; no live inference claim).");

async Task Check(string name, Func<Task> action)
{
    await action();
    passed++;
    Console.WriteLine("PASS " + name);
}
static void Require(bool condition, string message)
{
    if (!condition) throw new Exception("Assertion failed: " + message);
}

static string EditStatus(Action<JsonNode> edit)
{
    var root = JsonNode.Parse(FakePumas.StatusJson)!;
    edit(root["result"]!["snapshot"]!);
    return root.ToJsonString();
}
static async Task WithStatus(string status, string error, bool success = false)
{
    await using var server = new FakePumas { Status = status };
    using var client = new PumasClient(server.Uri, "game-dialogue");
    var reply = await client.GenerateAsync("Mara", "Harbor.", "Hello");
    Require(reply.Success == success && reply.ErrorCode == error, "status outcome");
    Require(server.Paths.Count == (success ? 2 : 1), "generation admission");
}

sealed class FakePumas : IAsyncDisposable
{
    // Source-derived fixture, never represented as a real served model capture.
    public static readonly string StatusJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pumas95-loaded.json"));
    public string Status { get; set; } = StatusJson;
    public string Completion { get; set; } = """{"choices":[{"message":{"role":"assistant","content":"Mind the old pier."}}]}""";
    public int HttpStatus { get; set; } = 200;
    public bool HoldCompletion { get; set; }
    public TaskCompletionSource CompletionEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource CompletionReleased { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ConcurrentQueue<string> Paths { get; } = new();
    public ConcurrentQueue<string> Bodies { get; } = new();
    public Uri Uri { get; }
    private readonly HttpListener _listener = new();
    private readonly Task _loop;

    public FakePumas()
    {
        // Each test owns one ephemeral fixture port; no persistent service is changed.
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        Uri = new Uri($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add(Uri.AbsoluteUri);
        _listener.Start();
        _loop = Serve();
    }

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            Paths.Enqueue(context.Request.RawUrl ?? "");
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            Bodies.Enqueue(await reader.ReadToEndAsync());
            if (context.Request.RawUrl == "/v1/chat/completions")
            {
                CompletionEntered.TrySetResult();
                if (HoldCompletion) await CompletionReleased.Task;
            }
            var raw = Encoding.UTF8.GetBytes(context.Request.RawUrl == "/rpc" ? Status : Completion);
            context.Response.StatusCode = HttpStatus;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = raw.Length;
            try { await context.Response.OutputStream.WriteAsync(raw); context.Response.Close(); }
            catch (HttpListenerException) { } // Caller cancellation can close this fixture connection.
            catch (IOException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        CompletionReleased.TrySetResult();
        _listener.Close();
        await _loop;
    }
}
