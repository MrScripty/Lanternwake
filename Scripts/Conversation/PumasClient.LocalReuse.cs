using System.Text.Json;

namespace Lanternwake.Conversation;

public sealed record LocalModelCandidate(string ModelId, string State);

public sealed partial class PumasClient
{
    /// <summary>Existing typed local-only intent query; never get/ensure or upstream acquisition.</summary>
    public async Task<PumasResult<IReadOnlyList<LocalModelCandidate>>> QueryExistingHfModelsAsync(HfDownloadRequest selected,
        CancellationToken cancellation = default)
    {
        if (!ValidText(selected.RepoId, 512) || !OptionalInput(selected.Quant))
            return PumasResult<IReadOnlyList<LocalModelCandidate>>.Unavailable("invalid_request");
        if (!_setup.Wait(0)) return PumasResult<IReadOnlyList<LocalModelCandidate>>.Unavailable("busy");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var id = "lanternwake-local-" + Guid.NewGuid().ToString("N");
            var artifact = new Dictionary<string, string> { ["format"] = "gguf" };
            if (selected.Quant is { Length: > 0 } quant) artifact["quantization"] = quant;
            // Preview filenames are not durable typed artifact IDs. File groups use a conservative repository/format query.
            using var document = await PostJsonAsync("rpc", new { jsonrpc = "2.0", id, method = "intent_query_models",
                @params = new { requirement = new { selector = new { kind = "upstream_repository", repository_id = selected.RepoId },
                    artifact, acquisition_policy = "local_only" } } }, deadline.Token).ConfigureAwait(false);
            var envelope = document.RootElement;
            if (StringField(envelope, "jsonrpc") != "2.0" || StringField(envelope, "id") != id || envelope.TryGetProperty("error", out _) ||
                !envelope.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object) throw Contract();
            var outcome = RequiredString(result, "outcome");
            if (outcome is "invalid_requirement" or "unsupported" or "unavailable")
                return PumasResult<IReadOnlyList<LocalModelCandidate>>.Unavailable("local_query_" + outcome);
            if (outcome != "matches" || !result.EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(new[] { "outcome", "candidates" })) throw Contract();
            var candidates = RequireArray(result, "candidates", 1000).EnumerateArray().Select(candidate =>
            {
                var state = RequiredString(candidate, "state");
                if (state is not ("ready" or "incomplete" or "unsatisfied")) throw Contract();
                var model = candidate.GetProperty("identity").GetProperty("model_ref");
                if (!model.GetProperty("model_ref_contract_version").TryGetInt32(out var version) || version != 1 ||
                    model.TryGetProperty("migration_diagnostics", out var diagnostics) &&
                    (diagnostics.ValueKind != JsonValueKind.Array || diagnostics.GetArrayLength() != 0)) throw Contract();
                return new LocalModelCandidate(RequiredString(model, "model_id"), state);
            }).ToArray();
            if (candidates.Select(candidate => candidate.ModelId).Distinct().Count() != candidates.Length)
                throw Contract(); // Keep ambiguous observations blocked rather than selecting a winner.
            return new(true, candidates, "");
        }
        catch (PumasProtocolException error) { return PumasResult<IReadOnlyList<LocalModelCandidate>>.Unavailable(error.Code); }
        catch (OperationCanceledException) { return PumasResult<IReadOnlyList<LocalModelCandidate>>.Unavailable(cancellation.IsCancellationRequested ? "cancelled" : "timeout"); }
        catch (HttpRequestException) { return PumasResult<IReadOnlyList<LocalModelCandidate>>.Unavailable("pumas_unavailable"); }
        catch (IOException) { return PumasResult<IReadOnlyList<LocalModelCandidate>>.Unavailable("pumas_unavailable"); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
        { return PumasResult<IReadOnlyList<LocalModelCandidate>>.Unavailable("pumas_contract"); }
        finally { _setup.Release(); }
    }
}
