using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Lanternwake.Conversation;

var passed = 0;
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
await Check("malformed completion is not fabricated", async () =>
{
    await using var server = new FakePumas { Completion = """{"choices":[{"message":{"content":null}}]}""" };
    using var client = new PumasClient(server.Uri, "game-dialogue");
    Require((await client.GenerateAsync("Mara", "Harbor.", "Hello")).ErrorCode == "invalid_response", "null reply");
});
await Check("duplicate JSON fields rejected", async () =>
{
    await using var server = new FakePumas { Completion = """{"choices":[],"choices":[{"message":{"content":"fake"}}]}""" };
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

sealed class FakePumas : IAsyncDisposable
{
    public const string StatusJson = """{"jsonrpc":"2.0","id":"lanternwake-status","result":{"success":true,"snapshot":{"schema_version":1,"served_models":[{"model_id":"library/model","model_alias":"game-dialogue","profile_id":"game-cpu","provider":"llama_cpp","load_state":"loaded"}],"router_profiles":[]}}}""";
    public string Status { get; set; } = StatusJson;
    public string Completion { get; set; } = """{"choices":[{"message":{"role":"assistant","content":"Mind the old pier."}}]}""";
    public int HttpStatus { get; set; } = 200;
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
            var raw = Encoding.UTF8.GetBytes(context.Request.RawUrl == "/rpc" ? Status : Completion);
            context.Response.StatusCode = HttpStatus;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = raw.Length;
            await context.Response.OutputStream.WriteAsync(raw);
            context.Response.Close();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Close();
        await _loop;
    }
}
