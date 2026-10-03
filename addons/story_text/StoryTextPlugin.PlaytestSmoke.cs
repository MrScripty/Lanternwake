#if TOOLS
using Godot;

public partial class StoryTextPlugin
{
    private async Task RunPlaytestSmoke()
    {
        void Check(bool condition, string claim) { if (!condition) throw new InvalidOperationException("Playtest launch smoke: " + claim); }
        var settings = System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("res://project.godot"));
        var source = System.IO.File.ReadAllText(ProjectSettings.GlobalizePath(_storyPath));
        _dirty = true;
        Check(!PlaySavedBeat(true) && _playtestProcess is null, "dirty beat refuses launch");
        _dirty = false; _profileDirty = true;
        Check(!PlaySavedBeat(true) && _playtestProcess is null, "dirty dossier refuses launch");
        _profileDirty = false;
        var loaded = _loaded; _loaded += "\n";
        Check(!PlaySavedBeat(true) && _playtestProcess is null, "disk conflict refuses launch");
        _loaded = loaded;
        var target = _index!.Entries.Last(e => e.Beat.Activity is not null);
        _sceneIndex = target.SceneIndex; _beatIndex = target.BeatIndex; _scenes.Select(_sceneIndex); PopulateBeats();
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Check(PlaySavedBeat(true), "launch saved selected beat");
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try { await _playtestProcess!.WaitForExitAsync(timeout.Token); }
                catch
                {
                    // This child is a dedicated headless smoke, never an author's interactive preview.
                    if (!_playtestProcess!.HasExited) _playtestProcess.Kill(true);
                    await _playtestProcess.WaitForExitAsync();
                    throw;
                }
                var output = await _playtestOutput!;
                var errors = await _playtestErrors!;
                Check(_playtestProcess.ExitCode == 0 && output.Contains("LANTERNWAKE_SELECTED_BEAT_OK " + target.Beat.Id) && !errors.Contains("ERROR:"),
                    "native selected-beat child passes and exits cleanly: " + errors);
                _playtestProcess.Dispose(); _playtestProcess = null;
            }
            Check(System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("res://project.godot")) == settings, "launch never changes persistent run settings");
            Check(System.IO.File.ReadAllText(ProjectSettings.GlobalizePath(_storyPath)) == source, "launch never edits canonical story");
            GD.Print("LANTERNWAKE_PLAYTEST_LAUNCH_OK dirty/conflict rejection; repeated native selected-beat launch/exit; settings and story unchanged");
        }
        finally { _playtestProcess?.Dispose(); _playtestProcess = null; }
    }
}
#endif
