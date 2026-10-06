using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Lanternwake.Conversation;

public enum HfDownloadState { Queued, Downloading, Pausing, Paused, Cancelling, Completed, Cancelled, Error }
public sealed record HfDownloadProgress(string DownloadId, string? LibraryModelId, string? RepoId,
    string? SelectedArtifactId, string? ModelName, string? ModelType, HfDownloadState State,
    double? Progress, long? DownloadedBytes, long? TotalBytes, double? Speed, double? EtaSeconds,
    uint? RetryAttempt, uint? RetryLimit, bool? Retrying, double? NextRetryDelaySeconds, string? Error);

public sealed partial class PumasClient
{
    public Task<PumasResult<IReadOnlyList<HfDownloadProgress>>> ListModelDownloadsAsync(
        CancellationToken cancellationToken = default) =>
        SetupRpcAsync("list_model_downloads", new { }, result =>
        {
            var downloads = RequireArray(result, "downloads", 256).EnumerateArray().Select(ParseDownloadProgress).ToArray();
            if (downloads.Select(download => download.DownloadId).Distinct().Count() != downloads.Length) throw Contract();
            return (IReadOnlyList<HfDownloadProgress>)downloads;
        }, cancellationToken);

    public Task<PumasResult<HfDownloadProgress>> GetModelDownloadStatusAsync(string downloadId,
        CancellationToken cancellationToken = default)
    {
        if (!ValidText(downloadId, 512)) return Task.FromResult(PumasResult<HfDownloadProgress>.Unavailable("invalid_request"));
        return SetupRpcAsync("get_model_download_status", new { download_id = downloadId }, result =>
        {
            // Pumas95 flattens progress into result, rather than nesting it.
            var progress = ParseDownloadProgress(result);
            if (progress.DownloadId != downloadId) throw Contract();
            return progress;
        }, cancellationToken);
    }

    /// <summary>True acknowledges the command; only a later status query confirms terminal cancellation.</summary>
    public Task<PumasResult<bool>> CancelModelDownloadAsync(string downloadId,
        CancellationToken cancellationToken = default)
    {
        if (!ValidText(downloadId, 512)) return Task.FromResult(PumasResult<bool>.Unavailable("invalid_request"));
        return SetupRpcAsync("cancel_model_download", new { download_id = downloadId }, _ => true, cancellationToken);
    }

    private static HfDownloadProgress ParseDownloadProgress(JsonElement value)
    {
        var state = RequiredString(value, "status") switch
        {
            "queued" => HfDownloadState.Queued, "downloading" => HfDownloadState.Downloading,
            "pausing" => HfDownloadState.Pausing, "paused" => HfDownloadState.Paused,
            "cancelling" => HfDownloadState.Cancelling, "completed" => HfDownloadState.Completed,
            "cancelled" => HfDownloadState.Cancelled, "error" => HfDownloadState.Error,
            _ => throw Contract()
        };
        return new HfDownloadProgress(RequiredString(value, "downloadId"), OptionalString(value, "libraryModelId"),
            OptionalString(value, "repoId"), OptionalString(value, "selectedArtifactId"), OptionalString(value, "modelName"),
            OptionalString(value, "modelType"), state, OptionalNumber(value, "progress", 1),
            Size(value, "downloadedBytes"), Size(value, "totalBytes"), OptionalNumber(value, "speed"),
            OptionalNumber(value, "etaSeconds"), OptionalUnsigned(value, "retryAttempt"), OptionalUnsigned(value, "retryLimit"),
            OptionalBoolean(value, "retrying"), OptionalNumber(value, "nextRetryDelaySeconds"), OptionalString(value, "error", 2048));
    }

    private static double? OptionalNumber(JsonElement value, string key, double maximum = double.MaxValue)
    {
        if (!value.TryGetProperty(key, out var field) || field.ValueKind == JsonValueKind.Null) return null;
        if (field.ValueKind != JsonValueKind.Number || !field.TryGetDouble(out var number) ||
            !double.IsFinite(number) || number < 0 || number > maximum) throw Contract();
        return number;
    }
    private static uint? OptionalUnsigned(JsonElement value, string key)
    {
        if (!value.TryGetProperty(key, out var field) || field.ValueKind == JsonValueKind.Null) return null;
        if (field.ValueKind != JsonValueKind.Number || !field.TryGetUInt32(out var number)) throw Contract();
        return number;
    }
    private static bool? OptionalBoolean(JsonElement value, string key)
    {
        if (!value.TryGetProperty(key, out var field) || field.ValueKind == JsonValueKind.Null) return null;
        return field.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw Contract() };
    }
}
