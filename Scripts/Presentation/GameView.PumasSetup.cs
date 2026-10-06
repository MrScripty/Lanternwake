using Godot;
using Lanternwake.Conversation;

namespace Lanternwake.Presentation;

public partial class GameView
{
    [Export] public PackedScene PumasSearchControlsScene { get; set; } = null!;
    private PumasClient? _setupPumas;
    private CancellationTokenSource? _setupRequest;

    private void ShowPumasSetup()
    {
        if (_busy || _closing) return;
        ShowWindow("Local conversation setup", "The story is fully playable without a model.\n\nSearch asks your local Pumas service for Hugging Face metadata. Pumas owns the external requests. You can inspect an option before explicitly asking Pumas to download it.\n\nPumas also manages verification and runtimes. After acquisition, configure and load a llama.cpp profile in Pumas, then start Lanternwake with its served alias in LANTERNWAKE_PUMAS_MODEL. A download request does not enable conversation.");
        var window = _modal!;
        var controls = PumasSearchControlsScene.Instantiate<VBoxContainer>();
        var rows = window.GetNode<VBoxContainer>("%ModalActions");
        rows.AddChild(controls);
        window.GetNode<ScrollContainer>("%ModalActionsScroll").Visible = true;
        var query = controls.GetNode<LineEdit>("Query");
        var search = controls.GetNode<Button>("Search");
        foreach (var control in controls.GetChildren().OfType<Control>()) _readingText.Register(control);
        void Search()
        {
            var text = query.Text.Trim();
            StartPumasSetup(window, "Searching through Pumas…", token => _setupPumas!.SearchHfModelsAsync(text, cancellationToken: token),
                result => ShowHfResults(result), false);
        }
        search.Pressed += Search;
        query.TextSubmitted += _ => Search();
        FitModalActions(window);
        query.GrabFocus();
    }

    private void ShowHfResults(PumasResult<IReadOnlyList<HfModel>> result)
    {
        if (!result.Success) { ShowPumasFailure(result.ErrorCode, result.Message, false); return; }
        var models = result.Value!;
        var compatible = models.Where(model => model.Formats.Contains("gguf", StringComparer.OrdinalIgnoreCase)).ToArray();
        ShowWindow("Pumas search results", models.Count == 0 ? "Pumas returned no models." :
            "Choose a GGUF repository to inspect its Pumas download options. Search results are previews; compatibility and model quality still need qualification.\n\n" +
            string.Join("\n\n", models.Select(model => $"{model.RepoId}\nFormats: {string.Join(", ", model.Formats)} · License: {model.License ?? "unknown"}")),
            compatible.Select(model => ("Inspect " + model.RepoId, (Action)(() =>
            {
                var owner = _modal!;
                StartPumasSetup(owner, "Inspecting download options through Pumas…",
                    token => _setupPumas!.GetHfDownloadDetailsAsync(model.RepoId, cancellationToken: token),
                    result => ShowHfOptions(model, result), false);
            }))).Append(("New search", (Action)ShowPumasSetup)).ToArray());
    }

    private void ShowHfOptions(HfModel model, PumasResult<HfDownloadDetails> result)
    {
        if (!result.Success) { ShowPumasFailure(result.ErrorCode, result.Message, false); return; }
        var details = result.Value!;
        ShowWindow("Pumas download options", $"Repository: {details.RepoId}\nLicense: {model.License ?? "unknown"}\nPreview reference: main (mutable)\nImmutable revision: unknown in this preview\n\nPumas resolves the revision during acquisition. A preview is not a verified artifact receipt.\n\nChoose an option to review the request.",
            details.DownloadOptions.Select(option => ($"Review {OptionName(option)} · {SizeText(option.SizeBytes)}",
                (Action)(() => ShowHfRequest(model, option)))).Append(("New search", (Action)ShowPumasSetup)).ToArray());
    }

    private void ShowHfRequest(HfModel model, HfDownloadOption option)
    {
        var family = string.IsNullOrWhiteSpace(model.Developer) ? model.RepoId.Split('/')[0] : model.Developer;
        var request = new HfDownloadRequest(model.RepoId, family, model.Name,
            option.FileGroup is null ? option.Quant : null, option.FileGroup?.Filenames,
            model.Kind, model.License, model.ReleaseDate);
        ShowWindow("Review Pumas download request", $"Repository: {model.RepoId}\nOption: {OptionName(option)}\nPreview size: {SizeText(option.SizeBytes)}\nLicense: {model.License ?? "unknown — check terms in Pumas"}\nPreview reference: main (mutable)\nImmutable revision: unknown in this preview\n" +
            (option.FileGroup is { } group ? "Files:\n" + string.Join("\n", group.Filenames) + "\n" : "") +
            "\nRequesting this option asks Pumas to acquire model files. Check the license and available storage first. Pumas owns the transfer, verification, revision resolution and runtime acquisition. No files are downloaded by Lanternwake.",
            [("Request download through Pumas", () =>
            {
                var owner = _modal!;
                StartPumasSetup(owner, "Requesting acquisition through Pumas…",
                    token => _setupPumas!.StartModelDownloadFromHfAsync(request, token),
                    result =>
                    {
                        if (!result.Success) { ShowPumasFailure(result.ErrorCode, result.Message, true); return; }
                        var receipt = result.Value!;
                        ShowWindow("Pumas accepted the request", $"Download ID: {receipt.DownloadId}\nArtifact ID: {receipt.SelectedArtifactId ?? "not returned"}\n\nThis is an acceptance receipt, not proof of installation, verification or readiness. Check acquisition progress and the stored upstream_revision in Pumas. Then configure and load the model there. Lanternwake will use its explicitly configured served alias.",
                            [("Back to setup", ShowPumasSetup)]);
                    }, true);
            }), ("Back to setup", ShowPumasSetup)]);
    }

    private void StartPumasSetup<T>(Window window, string pending, Func<CancellationToken, Task<PumasResult<T>>> request,
        Action<PumasResult<T>> render, bool acquisition)
    {
        if (_closing || _modal != window || _setupRequest is not null) return;
        _operations.Track(RunPumasSetupAsync(window, pending, request, render, acquisition));
    }

    private async Task RunPumasSetupAsync<T>(Window window, string pending, Func<CancellationToken, Task<PumasResult<T>>> request,
        Action<PumasResult<T>> render, bool acquisition)
    {
        using var cancellation = new CancellationTokenSource();
        _setupRequest = cancellation;
        window.GetNode<RichTextLabel>("%ModalText").Text = pending +
            (acquisition ? "\n\nClosing cancels waiting here. Pumas may already have accepted the request; check Pumas before requesting again." : "\n\nClose or Escape cancels this lookup.");
        foreach (var control in DescendantControls(window.GetNode<VBoxContainer>("%ModalActions")))
        {
            if (control is BaseButton button) button.Disabled = true;
            if (control is LineEdit entry) entry.Editable = false;
        }
        window.GetNode<Button>("%ModalCloseButton").GrabFocus();
        try
        {
            _setupPumas ??= new PumasClient();
            var result = await request(cancellation.Token);
            if (!_closing && IsInsideTree() && _modal == window && !cancellation.IsCancellationRequested) render(result);
        }
        catch (Exception error) when (error is ArgumentException or UriFormatException)
        {
            if (!_closing && _modal == window) ShowPumasFailure("invalid_request", "Check LANTERNWAKE_PUMAS_URL; use http://127.0.0.1:PORT/.", acquisition);
        }
        finally { if (_setupRequest == cancellation) _setupRequest = null; }
    }

    private void ShowPumasFailure(string code, string? message, bool acquisition) =>
        ShowWindow("Pumas setup unavailable", $"Pumas result: {code}\n{message ?? "No supported result was returned."}\n\n" +
            (acquisition && code != "pumas_rejected" && code != "invalid_request" ?
                "Acceptance is unknown. Check Pumas before sending another acquisition request. Lanternwake does not replay it." :
                "No successful acquisition receipt was returned. You can continue the authored story."),
            [("Back to setup", ShowPumasSetup)]);
    private static string OptionName(HfDownloadOption option) => option.FileGroup?.Label ?? option.Quant;
    private static string SizeText(long? size) => size.HasValue ? $"{size.Value:N0} bytes" : "size unknown";
    private static IEnumerable<Control> DescendantControls(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Control control) yield return control;
            foreach (var descendant in DescendantControls(child)) yield return descendant;
        }
    }
}
