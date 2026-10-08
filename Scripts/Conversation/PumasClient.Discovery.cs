using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Lanternwake.Conversation;

public sealed record PumasResult<T>(bool Success, T? Value, string ErrorCode, string? Message = null)
{
    public static PumasResult<T> Unavailable(string code, string? message = null) => new(false, default, code, message);
}
public sealed record HfFileGroup(IReadOnlyList<string> Filenames, uint ShardCount, string Label);
public sealed record HfDownloadOption(string Quant, long? SizeBytes, HfFileGroup? FileGroup);
public sealed record HfModel(string RepoId, string Name, string Developer, string Kind,
    IReadOnlyList<string> Formats, IReadOnlyList<string> Quants, string? License, string? ReleaseDate);
// Pumas95 preview metadata has no immutable revision. Do not infer one from main or a URL.
public sealed record HfDownloadDetails(string RepoId, IReadOnlyList<HfDownloadOption> DownloadOptions, long? TotalSizeBytes);
public sealed record HfDownloadRequest(string RepoId, string Family, string OfficialName, string? Quant,
    IReadOnlyList<string>? Filenames = null, string? PipelineTag = null, string? LicenseStatus = null,
    string? ReleaseDate = null);
public sealed record HfDownloadStarted(string DownloadId, string? SelectedArtifactId);

public sealed partial class PumasClient
{
    private readonly SemaphoreSlim _setup = new(1, 1);
    private const long MaxSafeInteger = 9_007_199_254_740_991;

    public Task<PumasResult<IReadOnlyList<HfModel>>> SearchHfModelsAsync(string query, int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (!ValidText(query, 200) || limit is < 1 or > 25)
            return Task.FromResult(PumasResult<IReadOnlyList<HfModel>>.Unavailable("invalid_request"));
        return SetupRpcAsync("search_hf_models", new { query, kind = "text-generation", limit, hydrate_limit = limit },
            result =>
            {
                var models = RequireArray(result, "models", 25).EnumerateArray().Select(model => new HfModel(
                    RequiredString(model, "repoId"), RequiredString(model, "name"),
                    OptionalString(model, "developer") ?? "", OptionalString(model, "kind") ?? "unknown",
                    Strings(model, "formats"), Strings(model, "quants"), OptionalString(model, "license"),
                    OptionalString(model, "releaseDate"))).ToArray();
                if (models.Select(m => m.RepoId).Distinct().Count() != models.Length) throw Contract();
                return (IReadOnlyList<HfModel>)models;
            }, cancellationToken);
    }

    public Task<PumasResult<HfDownloadDetails>> GetHfDownloadDetailsAsync(string repoId,
        IReadOnlyList<string>? quants = null, CancellationToken cancellationToken = default)
    {
        if (!ValidText(repoId, 512) || quants is not null &&
            (quants.Count > 100 || quants.Any(q => !ValidText(q, 512))))
            return Task.FromResult(PumasResult<HfDownloadDetails>.Unavailable("invalid_request"));
        return SetupRpcAsync("get_hf_download_details", new { repo_id = repoId, quants = quants ?? Array.Empty<string>() },
            result =>
            {
                if (!result.TryGetProperty("details", out var details) || details.ValueKind != JsonValueKind.Object ||
                    RequiredString(details, "repoId") != repoId) throw Contract();
                var options = RequireArray(details, "downloadOptions", 100).EnumerateArray().Select(option =>
                {
                    HfFileGroup? group = null;
                    if (option.TryGetProperty("fileGroup", out var files) && files.ValueKind != JsonValueKind.Null)
                    {
                        var names = Strings(files, "filenames", required: true);
                        if (names.Count == 0 || names.Distinct().Count() != names.Count ||
                            !files.TryGetProperty("shardCount", out var shards) || !shards.TryGetUInt32(out var count) || count == 0)
                            throw Contract();
                        group = new HfFileGroup(names, count, RequiredString(files, "label"));
                    }
                    if (!option.TryGetProperty("quant", out var quant) || quant.ValueKind != JsonValueKind.String ||
                        group is null && !ValidText(quant.GetString(), 512)) throw Contract();
                    return new HfDownloadOption(OptionalString(option, "quant") ?? "", Size(option, "sizeBytes"), group);
                }).ToArray();
                return new HfDownloadDetails(repoId, options, Size(details, "totalSizeBytes"));
            }, cancellationToken);
    }

    // This asks Pumas to acquire the selected artifact. It does not transfer files,
    // choose a revision, verify bytes, install a runtime, or certify a loaded model.
    public Task<PumasResult<HfDownloadStarted>> StartModelDownloadFromHfAsync(HfDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !ValidText(request.RepoId, 512) || !ValidText(request.Family, 512) ||
            !ValidText(request.OfficialName, 512) || !OptionalInput(request.Quant) ||
            !OptionalInput(request.PipelineTag) || !OptionalInput(request.LicenseStatus) || !OptionalInput(request.ReleaseDate) ||
            request.Filenames is { } files && (files.Count is < 1 or > 100 || files.Any(f => !ValidText(f, 512)) || files.Distinct().Count() != files.Count) ||
            request.Filenames is null && string.IsNullOrWhiteSpace(request.Quant))
            return Task.FromResult(PumasResult<HfDownloadStarted>.Unavailable("invalid_request"));
        return SetupRpcAsync("start_model_download_from_hf", new
        {
            repo_id = request.RepoId, family = request.Family, official_name = request.OfficialName,
            model_type = "llm", quant = request.Quant, filenames = request.Filenames,
            pipeline_tag = request.PipelineTag, license_status = request.LicenseStatus, release_date = request.ReleaseDate
        }, result =>
        {
            var selected = OptionalString(result, "selectedArtifactId");
            if (OptionalString(result, "artifactId") != selected) throw Contract();
            return new HfDownloadStarted(RequiredString(result, "download_id"), selected);
        }, cancellationToken);
    }

    private async Task<PumasResult<T>> SetupRpcAsync<T>(string method, object parameters, Func<JsonElement, T> parse,
        CancellationToken cancellationToken)
    {
        if (!_setup.Wait(0)) return PumasResult<T>.Unavailable("busy");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var id = "lanternwake-setup-" + Guid.NewGuid().ToString("N");
            using var document = await PostJsonAsync("rpc", new { jsonrpc = "2.0", id, method, @params = parameters }, deadline.Token)
                .ConfigureAwait(false);
            var envelope = document.RootElement;
            if (StringField(envelope, "jsonrpc") != "2.0" || StringField(envelope, "id") != id ||
                envelope.TryGetProperty("error", out _) || !envelope.TryGetProperty("result", out var result) ||
                result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("success", out var success) ||
                success.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Contract();
            if (success.ValueKind == JsonValueKind.False)
                return PumasResult<T>.Unavailable("pumas_rejected", RequiredString(result, "error", 2048));
            return new PumasResult<T>(true, parse(result), "");
        }
        catch (PumasProtocolException error) { return PumasResult<T>.Unavailable(error.Code); }
        catch (OperationCanceledException) { return PumasResult<T>.Unavailable(cancellationToken.IsCancellationRequested ? "cancelled" : "timeout"); }
        catch (HttpRequestException) { return PumasResult<T>.Unavailable("pumas_unavailable"); }
        catch (IOException) { return PumasResult<T>.Unavailable("pumas_unavailable"); }
        catch (JsonException) { return PumasResult<T>.Unavailable("invalid_response"); }
        catch (InvalidOperationException) { return PumasResult<T>.Unavailable("pumas_contract"); }
        finally { _setup.Release(); }
    }

    private static PumasProtocolException Contract() => new("pumas_contract");
    private static bool OptionalInput(string? value) => value is null || ValidText(value, 512);
    private static string RequiredString(JsonElement value, string key, int limit = 512) =>
        OptionalString(value, key, limit) is { } text && ValidText(text, limit) ? text : throw Contract();
    private static string? OptionalString(JsonElement value, string key, int limit = 512)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Contract();
        if (!value.TryGetProperty(key, out var field) || field.ValueKind == JsonValueKind.Null) return null;
        if (field.ValueKind != JsonValueKind.String) throw Contract();
        var text = field.GetString()!;
        if (text.Length > 0 && !ValidText(text, limit)) throw Contract();
        return text.Length == 0 ? null : text;
    }
    private static JsonElement RequireArray(JsonElement value, string key, int limit)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var array) ||
            array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > limit) throw Contract();
        return array;
    }
    private static IReadOnlyList<string> Strings(JsonElement value, string key, bool required = false)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Contract();
        if (!required && !value.TryGetProperty(key, out _)) return Array.Empty<string>();
        return RequireArray(value, key, 100).EnumerateArray().Select(item =>
            item.ValueKind == JsonValueKind.String && ValidText(item.GetString(), 512) ? item.GetString()! : throw Contract()).ToArray();
    }
    private static long? Size(JsonElement value, string key)
    {
        if (!value.TryGetProperty(key, out var field) || field.ValueKind == JsonValueKind.Null) return null;
        if (!field.TryGetInt64(out var size) || size < 0 || size > MaxSafeInteger) throw Contract();
        return size;
    }
}
