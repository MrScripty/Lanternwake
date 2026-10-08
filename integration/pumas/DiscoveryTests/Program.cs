using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Lanternwake.Conversation;

var count = 0;
await Check("search is typed and sends only source-owned Pumas RPC fields", async () =>
{
    await using var fixture = new RpcFixture();
    using var client = new PumasClient(fixture.Uri);
    var result = await client.SearchHfModelsAsync("Dialogue", 10);
    Require(result.Success && result.Value!.Single().RepoId == "Fixture/Dialogue-GGUF", "typed search");
    var body = fixture.Bodies.Single();
    Require(body["method"]!.GetValue<string>() == "search_hf_models" &&
        body["params"]!["kind"]!.GetValue<string>() == "text-generation" &&
        body["params"]!["hydrate_limit"]!.GetValue<int>() == 10, "source parameters");
});
await Check("details preserve unknown size and grouped filenames; never synthesize revision", async () =>
{
    await using var fixture = new RpcFixture();
    using var client = new PumasClient(fixture.Uri);
    var result = await client.GetHfDownloadDetailsAsync("Fixture/Dialogue-GGUF");
    Require(result.Success && result.Value!.TotalSizeBytes is null && result.Value.DownloadOptions[0].SizeBytes == 491000000 &&
        result.Value.DownloadOptions[1].FileGroup!.Filenames.SequenceEqual(new[] { "model-1.gguf", "model-2.gguf" }), "metadata projection");
    Require(fixture.Bodies.Count == 1, "inspection never starts acquisition");
});
await Check("explicit quant request returns acceptance receipt without inference call", async () =>
{
    await using var fixture = new RpcFixture();
    using var client = new PumasClient(fixture.Uri);
    var result = await client.StartModelDownloadFromHfAsync(Request());
    Require(result.Success && result.Value!.DownloadId == "fixture-download-001" && result.Value.SelectedArtifactId == "fixture-artifact-001", "receipt");
    var body = fixture.Bodies.Single();
    Require(body["method"]!.GetValue<string>() == "start_model_download_from_hf" && body["params"]!["repo_id"]!.GetValue<string>() == Request().RepoId &&
        body["params"]!["quant"]!.GetValue<string>() == "Q4_K_M" && body["params"]!["model_type"]!.GetValue<string>() == "llm" &&
        body["params"]!["filenames"] is null && body["params"]!["revision"] is null, "no invented pin field");
});
await Check("group selection uses filenames rather than competing quant selection", async () =>
{
    await using var fixture = new RpcFixture();
    using var client = new PumasClient(fixture.Uri);
    Require((await client.StartModelDownloadFromHfAsync(Request() with { Quant = null, Filenames = new[] { "a.gguf", "b.gguf" } })).Success, "group request");
    Require(fixture.Bodies.Single()["params"]!["filenames"]!.AsArray().Count == 2, "group fields");
});
await Check("bad local inputs never reach Pumas", async () =>
{
    await using var fixture = new RpcFixture();
    using var client = new PumasClient(fixture.Uri);
    foreach (var query in new[] { "", " ", new string('x', 201), "x\0" }) Require((await client.SearchHfModelsAsync(query)).ErrorCode == "invalid_request", "query");
    foreach (var limit in new[] { -1, 0, 26 }) Require((await client.SearchHfModelsAsync("x", limit)).ErrorCode == "invalid_request", "limit");
    Require((await client.GetHfDownloadDetailsAsync("", new[] { "Q4" })).ErrorCode == "invalid_request", "repo");
    foreach (var request in new[] { Request() with { Quant = null }, Request() with { Family = "" }, Request() with { Filenames = new[] { "a", "a" } } })
        Require((await client.StartModelDownloadFromHfAsync(request)).ErrorCode == "invalid_request", "request selection");
    Require(fixture.Bodies.Count == 0, "local validation");
});
await Check("Pumas rejection preserves its public message", async () =>
{
    await using var fixture = new RpcFixture { Result = """{"success":false,"error":"repository unavailable"}""" };
    using var client = new PumasClient(fixture.Uri);
    var result = await client.StartModelDownloadFromHfAsync(Request());
    Require(!result.Success && result.ErrorCode == "pumas_rejected" && result.Message == "repository unavailable", "domain rejection");
});
await Check("wrong envelope identity, error, and success shape fail closed", async () =>
{
    foreach (var mutation in new Action<JsonObject>[] {
        o => o["id"] = "wrong", o => o["jsonrpc"] = "1.0", o => o["error"] = new JsonObject { ["code"] = -32601 },
        o => o["result"]!["success"] = "true", o => o.Remove("result") })
    {
        await using var fixture = new RpcFixture { EditEnvelope = mutation };
        using var client = new PumasClient(fixture.Uri);
        Require((await client.SearchHfModelsAsync("x")).ErrorCode == "pumas_contract", "envelope");
    }
});
await Check("uncorrelated details and unsafe sizes are rejected", async () =>
{
    foreach (var mutation in new Action<JsonNode>[] { o => o["details"]!["repoId"] = "Other/Repo", o => o["details"]!["totalSizeBytes"] = -1,
        o => o["details"]!["totalSizeBytes"] = 9007199254740992L, o => o["details"]!["downloadOptions"] = "array",
        o => o["details"]!["downloadOptions"]![1]!["fileGroup"]!["filenames"] = new JsonArray("a", "a") })
    {
        var data = JsonNode.Parse(RpcFixture.Read("details"))!; mutation(data);
        await using var fixture = new RpcFixture { Result = data.ToJsonString() };
        using var client = new PumasClient(fixture.Uri);
        Require((await client.GetHfDownloadDetailsAsync("Fixture/Dialogue-GGUF")).ErrorCode == "pumas_contract", "details rejection");
    }
});
await Check("malformed receipt never certifies acceptance", async () =>
{
    foreach (var data in new[] { """{"success":true,"download_id":""}""", """{"success":true,"download_id":"d","selectedArtifactId":"a","artifactId":"b"}""" })
    {
        await using var fixture = new RpcFixture { Result = data };
        using var client = new PumasClient(fixture.Uri);
        Require((await client.StartModelDownloadFromHfAsync(Request())).ErrorCode == "pumas_contract", "receipt rejection");
    }
});
await Check("HTTP, redirect and content-type failures have no automatic replay", async () =>
{
    foreach (var (status, contentType, code) in new[] { (503, "application/json", "pumas_unavailable"), (302, "application/json", "pumas_unavailable"), (200, "text/html", "invalid_response") })
    {
        await using var fixture = new RpcFixture { Status = status, ContentType = contentType };
        using var client = new PumasClient(fixture.Uri);
        Require((await client.StartModelDownloadFromHfAsync(Request())).ErrorCode == code && fixture.Bodies.Count == 1, "transport rejection without retry");
    }
});
await Check("bounded JSON rejects duplicate keys and oversized payload", async () =>
{
    foreach (var body in new[] { """{"id":"wrong","id":"also-wrong"}""", new string('x', 262145), "{" })
    {
        await using var fixture = new RpcFixture { Raw = body };
        using var client = new PumasClient(fixture.Uri);
        Require((await client.SearchHfModelsAsync("x")).ErrorCode == "invalid_response", "bounded JSON");
    }
});
await Check("in-flight cancellation releases setup ownership; acquisition is never replayed", async () =>
{
    await using var fixture = new RpcFixture { Hold = true };
    using var client = new PumasClient(fixture.Uri);
    using var cancellation = new CancellationTokenSource();
    var pending = client.StartModelDownloadFromHfAsync(Request(), cancellation.Token);
    await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Require((await client.SearchHfModelsAsync("x")).ErrorCode == "busy", "single setup ownership");
    cancellation.Cancel();
    Require((await pending.WaitAsync(TimeSpan.FromSeconds(5))).ErrorCode == "cancelled" && fixture.Bodies.Count == 1, "cancel without replay");
    fixture.Hold = false; fixture.Released.TrySetResult();
    Require((await client.SearchHfModelsAsync("x")).Success && fixture.Bodies.Count == 2, "explicit next lookup");
});
await ActivityContractTests.Run(Check);
Console.WriteLine($"PASS {count} controlled discovery/acquisition/activity scenarios; no external network or real acquisition.");
async Task Check(string name, Func<Task> test) { await test(); count++; Console.WriteLine("PASS " + name); }
static void Require(bool condition, string claim) { if (!condition) throw new Exception(claim); }
static HfDownloadRequest Request() => new("Fixture/Dialogue-GGUF", "Dialogue", "Dialogue", "Q4_K_M");

sealed class RpcFixture : IAsyncDisposable
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".json"));
    public Uri Uri { get; }
    public string? Result { get; init; }
    public string? Raw { get; init; }
    public Action<JsonObject>? EditEnvelope { get; init; }
    public int Status { get; init; } = 200;
    public string ContentType { get; init; } = "application/json";
    public bool Hold { get; set; }
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ConcurrentQueue<JsonNode> Bodies { get; } = new();
    private readonly HttpListener _listener = new();
    private readonly Task _loop;
    public RpcFixture()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        Uri = new($"http://127.0.0.1:{port}/"); _listener.Prefixes.Add(Uri.AbsoluteUri); _listener.Start(); _loop = Serve();
    }
    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); } catch (HttpListenerException) { break; } catch (ObjectDisposedException) { break; }
            using var reader = new StreamReader(context.Request.InputStream);
            var request = JsonNode.Parse(await reader.ReadToEndAsync())!; Bodies.Enqueue(request); Entered.TrySetResult();
            if (Hold) await Released.Task;
            var method = request["method"]!.GetValue<string>();
            var name = method switch { "search_hf_models" => "search", "get_hf_download_details" => "details", "start_model_download_from_hf" => "started",
                "list_model_downloads" => "downloads", "get_model_download_status" => "progress", "cancel_model_download" => "cancel-ack", _ => throw new Exception("Unexpected method") };
            var envelope = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = request["id"]!.DeepClone(), ["result"] = JsonNode.Parse(Result ?? Read(name)) };
            EditEnvelope?.Invoke(envelope);
            var bytes = Encoding.UTF8.GetBytes(Raw ?? envelope.ToJsonString());
            context.Response.StatusCode = Status; context.Response.ContentType = ContentType; context.Response.ContentLength64 = bytes.Length;
            if (Status == 302) context.Response.RedirectLocation = "http://127.0.0.1:1/never-follow";
            try { await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close(); } catch (HttpListenerException) { } catch (IOException) { }
        }
    }
    public async ValueTask DisposeAsync() { Released.TrySetResult(); _listener.Close(); await _loop; }
}
