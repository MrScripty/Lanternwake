#if TOOLS
using Godot;
using Lanternwake.Core;
using System.Diagnostics;
using File = System.IO.File;

public partial class StoryTextPlugin
{
    private Process? _playtestProcess;
    private Task<string>? _playtestOutput, _playtestErrors;

    private bool PlaySavedBeat(bool smoke = false)
    {
        if (HasDraft) { _status.Text = "Save or Reload your draft before starting a playtest."; return false; }
        try
        {
            if (!OS.IsDebugBuild()) throw new InvalidOperationException("Author playtest requires a debug editor build.");
            if (_playtestProcess is { HasExited: false }) { _status.Text = "A preview is already open. Close its game window before launching another."; return false; }
            _playtestProcess?.Dispose(); _playtestProcess = null;
            var current = File.ReadAllText(ProjectSettings.GlobalizePath(_storyPath));
            if (current != _loaded) throw new InvalidOperationException("The story changed on disk. Reload before launching saved content.");
            var beatId = _sceneData[_sceneIndex].Beats[_beatIndex].Id;
            // Validate before spawning. The child repeats validation and uses the same replay.
            StoryContextPreview.CreateSessionAtBeat(Story.Parse(current), beatId);
            var start = new ProcessStartInfo(OS.GetExecutablePath())
            {
                UseShellExecute = false,
                WorkingDirectory = ProjectSettings.GlobalizePath("res://"),
                RedirectStandardOutput = smoke,
                RedirectStandardError = smoke
            };
            start.ArgumentList.Add("--path"); start.ArgumentList.Add(start.WorkingDirectory);
            if (smoke) start.ArgumentList.Add("--headless");
            start.ArgumentList.Add("--"); start.ArgumentList.Add("--author-preview-beat"); start.ArgumentList.Add(beatId);
            if (smoke) start.ArgumentList.Add("--author-preview-smoke");
            _playtestProcess = Process.Start(start) ?? throw new InvalidOperationException("Could not start the author preview.");
            if (smoke)
            {
                _playtestOutput = _playtestProcess.StandardOutput.ReadToEndAsync();
                _playtestErrors = _playtestProcess.StandardError.ReadToEndAsync();
            }
            _status.Text = "Opened isolated author preview for " + beatId + ". Player saves are disabled; close the separate game window to finish.";
            return true;
        }
        catch (Exception error) { _status.Text = error.Message; return false; }
    }
}
#endif
