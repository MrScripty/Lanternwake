using Godot;
using Lanternwake.Conversation;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private void ShowPumasDownloads()
    {
        if (_modal is not { } owner) return;
        StartPumasSetup(owner, "Reading Pumas download activity…", token => _setupPumas!.ListModelDownloadsAsync(token),
            result =>
            {
                if (!result.Success) { ShowPumasActivityFailure(result.ErrorCode, result.Message); return; }
                var downloads = result.Value!;
                ShowWindow("Pumas download activity", downloads.Count == 0 ?
                    "Pumas returned no tracked downloads. An empty list does not prove whether an earlier interrupted request was accepted. Check Pumas before requesting again." :
                    "These are all downloads currently tracked by Pumas. Choose the exact download ID to inspect it. This is a snapshot; refresh to see changes. Closing leaves Pumas activity running.\n\n" +
                    string.Join("\n\n", downloads.Select(download => $"{download.DownloadId}\n{download.RepoId ?? "Repository not returned"} · {StateText(download.State)}")),
                    downloads.Select(download => ("Inspect " + download.DownloadId, (Action)(() => RefreshPumasDownload(download.DownloadId))))
                        .Append(("Refresh activity", (Action)ShowPumasDownloads)).Append(("Back to setup", (Action)ShowPumasSetup)).ToArray());
            }, false);
    }

    private void RefreshPumasDownload(string downloadId)
    {
        if (_modal is not { } owner) return;
        StartPumasSetup(owner, "Reading the selected request from Pumas…",
            token => _setupPumas!.GetModelDownloadStatusAsync(downloadId, token), result =>
            {
                if (!result.Success) { ShowPumasActivityFailure(result.ErrorCode, result.Message); return; }
                ShowPumasDownload(result.Value!);
            }, false);
    }

    private void ShowPumasDownload(HfDownloadProgress download)
    {
        var terminal = download.State switch
        {
            HfDownloadState.Completed => "Pumas reports completion. This is not proof of a loaded model or dialogue readiness. Inspect artifact provenance and configure the runtime/profile in Pumas.",
            HfDownloadState.Cancelled => "Pumas reports terminal cancellation. Lanternwake did not restart the request.",
            HfDownloadState.Error => "Pumas reports failure. Inspect the failure and recovery options in Pumas. Lanternwake does not retry acquisition.",
            HfDownloadState.Paused => "Pumas reports a paused download. Use Pumas to resume or manage interrupted-transfer recovery.",
            HfDownloadState.Cancelling => "Pumas is cancelling. Refresh to observe the terminal result; acknowledgement alone does not confirm cancellation.",
            _ => "Pumas owns this transfer. Refresh to observe progress; closing this view leaves it running."
        };
        var actions = new List<(string, Action)> { ("Refresh this request", () => RefreshPumasDownload(download.DownloadId)) };
        if (CanCancel(download.State)) actions.Add(("Cancel this download", () => ConfirmPumasCancellation(download)));
        actions.Add(("All Pumas download activity", ShowPumasDownloads));
        actions.Add(("Back to setup", ShowPumasSetup));
        ShowWindow("Pumas download status", $"Download ID: {download.DownloadId}\nRepository: {download.RepoId ?? "not returned"}\nStatus: {StateText(download.State)}\nProgress: {(download.Progress.HasValue ? download.Progress.Value.ToString("P0") : "unknown")}\nTransferred: {SizeText(download.DownloadedBytes)} / {SizeText(download.TotalBytes)}\nLibrary model ID: {download.LibraryModelId ?? "not returned"}\nArtifact ID: {download.SelectedArtifactId ?? "not returned"}\n" +
            (download.Error is { } error ? $"Pumas error: {error}\n" : "") +
            (download.Retrying == true ? $"Pumas is retrying · attempt {download.RetryAttempt?.ToString() ?? "unknown"} / {download.RetryLimit?.ToString() ?? "unknown"}\n" : "") +
            $"\n{terminal}\n\nSnapshot from Pumas; refresh explicitly for current state. Progress percentages do not determine completion. Runtime and immutable-revision evidence remain Pumas-owned.", actions.ToArray());
    }

    private void ConfirmPumasCancellation(HfDownloadProgress download)
    {
        Window? confirmation = null;
        ShowWindow("Confirm Pumas cancellation", $"Download ID: {download.DownloadId}\nRepository: {download.RepoId ?? "not returned"}\nLast reported status: {StateText(download.State)}\n\nThis asks Pumas to cancel exactly this tracked download. Pumas owns transfer cleanup. Closing this panel sends no cancellation. The request may have changed since the last snapshot.",
            [("Send cancellation to Pumas", () =>
            {
                if (confirmation is null || _modal != confirmation) return;
                var owner = confirmation;
                StartPumasSetup(owner, "Sending cancellation to Pumas…",
                    token => _setupPumas!.CancelModelDownloadAsync(download.DownloadId, token), result =>
                    {
                        var outcome = result.Success ? "Pumas acknowledged cancellation. Refresh the request to observe its terminal status." :
                            result.ErrorCode == "pumas_rejected" ? $"Pumas did not acknowledge cancellation.\n{result.Message}" :
                            $"Cancellation outcome is unknown ({result.ErrorCode}). Refresh Pumas activity before sending another command. Lanternwake does not replay cancellation.";
                        ShowWindow("Pumas cancellation result", $"Download ID: {download.DownloadId}\n\n{outcome}",
                            [("Refresh this request", () => RefreshPumasDownload(download.DownloadId)), ("All Pumas download activity", ShowPumasDownloads)]);
                    }, false, "\n\nClosing stops waiting here. Pumas may already have accepted cancellation. Reopen activity and refresh its state before sending another command.");
            }), ("Keep this download running", () => RefreshPumasDownload(download.DownloadId))]);
        confirmation = _modal;
    }

    private void ShowPumasActivityFailure(string code, string? message) =>
        ShowWindow("Pumas activity unavailable", $"Pumas result: {code}\n{message ?? "No supported snapshot was returned."}\n\nThe current download state is unknown here. No acquisition or cancellation was sent by this lookup. Reopen or refresh Pumas activity to check its state.",
            [("Refresh activity", ShowPumasDownloads), ("Back to setup", ShowPumasSetup)]);
    private static bool CanCancel(HfDownloadState state) => state is HfDownloadState.Queued or HfDownloadState.Downloading or
        HfDownloadState.Pausing or HfDownloadState.Paused or HfDownloadState.Error;
    private static string StateText(HfDownloadState state) => state == HfDownloadState.Error ? "Failed" : state.ToString();
}
