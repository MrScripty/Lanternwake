using Lanternwake.Conversation;
using System.Text.Json.Nodes;

static class ActivityContractTests
{
    private const string Id = "fixture-download-001";
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("activity list recovers exact backend IDs without requesting acquisition", async () =>
        {
            await using var fixture = new RpcFixture();
            using var client = new PumasClient(fixture.Uri);
            var result = await client.ListModelDownloadsAsync();
            Require(result.Success && result.Value!.Single().DownloadId == Id, "list projection");
            var request = fixture.Bodies.Single();
            Require(request["method"]!.GetValue<string>() == "list_model_downloads" && request["params"]!.AsObject().Count == 0, "read-only exact list parameters");
        });
        await check("flattened Pumas status preserves progress, identity and retry evidence", async () =>
        {
            await using var fixture = new RpcFixture();
            using var client = new PumasClient(fixture.Uri);
            var result = await client.GetModelDownloadStatusAsync(Id);
            var progress = result.Value!;
            Require(result.Success && progress.State == HfDownloadState.Downloading && progress.Progress == .5 &&
                progress.DownloadedBytes == 500 && progress.TotalBytes == 1000 && progress.RetryAttempt == 1 && progress.RetryLimit == 3 &&
                progress.Retrying == false && progress.Speed == 10.5 && progress.LibraryModelId == "llm/Fixture/Dialogue-GGUF-Q4" &&
                progress.SelectedArtifactId == "fixture-artifact-001", "typed flat status");
            Require(fixture.Bodies.Single()["params"]!["download_id"]!.GetValue<string>() == Id, "exact status ID");
        });
        await check("all eight source states remain distinct even at 100 percent", async () =>
        {
            foreach (var (wire, state) in new[] { ("queued", HfDownloadState.Queued), ("downloading", HfDownloadState.Downloading),
                ("pausing", HfDownloadState.Pausing), ("paused", HfDownloadState.Paused), ("cancelling", HfDownloadState.Cancelling),
                ("completed", HfDownloadState.Completed), ("cancelled", HfDownloadState.Cancelled), ("error", HfDownloadState.Error) })
            {
                await using var fixture = new RpcFixture { Result = Edit(o => { o["status"] = wire; o["progress"] = 1; }) };
                using var client = new PumasClient(fixture.Uri);
                var result = await client.GetModelDownloadStatusAsync(Id);
                Require(result.Success && result.Value!.State == state && fixture.Bodies.Count == 1, "no inferred completion or lifecycle command");
            }
        });
        await check("unknown nullable progress and absent library association are preserved", async () =>
        {
            await using var fixture = new RpcFixture { Result = """{"success":true,"downloadId":"fixture-download-001","status":"completed"}""" };
            using var client = new PumasClient(fixture.Uri);
            var result = await client.GetModelDownloadStatusAsync(Id);
            Require(result.Success && result.Value!.Progress is null && result.Value.LibraryModelId is null && result.Value.TotalBytes is null,
                "completion is backend state; metadata not fabricated");
        });
        await check("explicit cancellation returns acknowledgement only with one exact-ID command", async () =>
        {
            await using var fixture = new RpcFixture();
            using var client = new PumasClient(fixture.Uri);
            var result = await client.CancelModelDownloadAsync(Id);
            Require(result.Success && result.Value, "command acknowledgement");
            var request = fixture.Bodies.Single();
            Require(request["method"]!.GetValue<string>() == "cancel_model_download" && request["params"]!.AsObject().Count == 1 &&
                request["params"]!["download_id"]!.GetValue<string>() == Id, "exact target, no implicit refresh/replay");
        });
        await check("missing status and rejected cancellation preserve Pumas failure", async () =>
        {
            await using var fixture = new RpcFixture { Result = """{"success":false,"error":"Download not found"}""" };
            using var client = new PumasClient(fixture.Uri);
            var status = await client.GetModelDownloadStatusAsync(Id);
            var cancel = await client.CancelModelDownloadAsync(Id);
            Require(status.ErrorCode == "pumas_rejected" && status.Message == "Download not found" &&
                !cancel.Success && cancel.ErrorCode == "pumas_rejected" && fixture.Bodies.Count == 2, "no false completion/cancellation");
        });
        await check("status response must match selected download ID", async () =>
        {
            await using var fixture = new RpcFixture { Result = Edit(o => o["downloadId"] = "another-request") };
            using var client = new PumasClient(fixture.Uri);
            Require((await client.GetModelDownloadStatusAsync(Id)).ErrorCode == "pumas_contract", "correlation");
        });
        await check("duplicate activity IDs fail before presenting ambiguous commands", async () =>
        {
            var progress = JsonNode.Parse(RpcFixture.Read("progress"))!;
            await using var fixture = new RpcFixture { Result = new JsonObject { ["success"] = true,
                ["downloads"] = new JsonArray(progress.DeepClone(), progress.DeepClone()) }.ToJsonString() };
            using var client = new PumasClient(fixture.Uri);
            Require((await client.ListModelDownloadsAsync()).ErrorCode == "pumas_contract", "ambiguous list");
        });
        await check("unknown or malformed lifecycle shape fails closed", async () =>
        {
            foreach (var result in new[] { Edit(o => o["status"] = "ready"), Edit(o => o["status"] = 1),
                """{"success":true,"progress":{"downloadId":"fixture-download-001","status":"downloading"}}""",
                Edit(o => o["libraryModelId"] = new JsonObject()) })
            {
                await using var fixture = new RpcFixture { Result = result };
                using var client = new PumasClient(fixture.Uri);
                Require((await client.GetModelDownloadStatusAsync(Id)).ErrorCode == "pumas_contract", "shape rejection");
            }
        });
        await check("source numeric ranges are enforced without guessing missing values", async () =>
        {
            foreach (var edit in new Action<JsonObject>[] { o => o["progress"] = -.1, o => o["progress"] = 1.1, o => o["speed"] = -1,
                o => o["etaSeconds"] = -1, o => o["nextRetryDelaySeconds"] = -1, o => o["downloadedBytes"] = 9007199254740992L,
                o => o["totalBytes"] = -1, o => o["retryAttempt"] = -1, o => o["retryLimit"] = 4294967296L, o => o["retrying"] = "true" })
            {
                await using var fixture = new RpcFixture { Result = Edit(edit) };
                using var client = new PumasClient(fixture.Uri);
                Require((await client.GetModelDownloadStatusAsync(Id)).ErrorCode == "pumas_contract", "numeric/boolean range");
            }
            await using var overflow = new RpcFixture { Result = RpcFixture.Read("progress").Replace("10.5", "1e400") };
            using var overflowClient = new PumasClient(overflow.Uri);
            Require((await overflowClient.GetModelDownloadStatusAsync(Id)).ErrorCode == "pumas_contract", "nonfinite number");
        });
        await check("invalid IDs never reach Pumas lifecycle mutations or lookups", async () =>
        {
            await using var fixture = new RpcFixture();
            using var client = new PumasClient(fixture.Uri);
            foreach (var id in new[] { "", " ", new string('x', 513), "bad\0" })
            {
                Require((await client.GetModelDownloadStatusAsync(id)).ErrorCode == "invalid_request", "status input");
                Require((await client.CancelModelDownloadAsync(id)).ErrorCode == "invalid_request", "cancel input");
            }
            Require(fixture.Bodies.IsEmpty, "no requests");
        });
        await check("closing a pending read releases ownership and sends no cancellation", async () =>
        {
            await using var fixture = new RpcFixture { Hold = true };
            using var client = new PumasClient(fixture.Uri);
            using var cancellation = new CancellationTokenSource();
            var pending = client.GetModelDownloadStatusAsync(Id, cancellation.Token);
            await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Require((await pending.WaitAsync(TimeSpan.FromSeconds(5))).ErrorCode == "cancelled" && fixture.Bodies.Count == 1, "read cancellation only");
            fixture.Hold = false; fixture.Released.TrySetResult();
            Require((await client.ListModelDownloadsAsync()).Success && fixture.Bodies.Count == 2, "recovery reads Pumas instead of reacquiring");
        });
        await check("cancel acknowledgement transport failure is unknown and never replayed", async () =>
        {
            foreach (var fixture in new[] { new RpcFixture { Status = 503 }, new RpcFixture { EditEnvelope = o => o["id"] = "wrong" },
                new RpcFixture { Result = """{"success":"true"}""" } })
            {
                await using var owned = fixture;
                using var client = new PumasClient(fixture.Uri);
                Require(!(await client.CancelModelDownloadAsync(Id)).Success && fixture.Bodies.Count == 1, "one explicit command; no terminal cancellation fabricated");
            }
        });
        await check("interrupted cancellation wait is not an undo or implicit replay", async () =>
        {
            await using var fixture = new RpcFixture { Hold = true };
            using var client = new PumasClient(fixture.Uri);
            using var cancellation = new CancellationTokenSource();
            var pending = client.CancelModelDownloadAsync(Id, cancellation.Token);
            await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Require((await pending.WaitAsync(TimeSpan.FromSeconds(5))).ErrorCode == "cancelled" && fixture.Bodies.Count == 1, "cancel wait interrupted without replay");
            fixture.Hold = false; fixture.Released.TrySetResult();
            Require((await client.GetModelDownloadStatusAsync(Id)).Success && fixture.Bodies.Count == 2, "explicit status recheck allowed");
        });
    }
    private static string Edit(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(RpcFixture.Read("progress"))!.AsObject(); edit(root); return root.ToJsonString();
    }
    private static void Require(bool condition, string claim) { if (!condition) throw new Exception(claim); }
}
