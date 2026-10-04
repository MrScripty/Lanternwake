using Lanternwake.Core;

internal static class SessionLaunchTests
{
    public static int Run(Story story)
    {
        var count = 0;
        void Check(bool condition, string claim) { if (!condition) throw new Exception(claim); count++; }
        void Reject(string[] args, bool debug, string claim)
        {
            try { SessionLaunch.Parse(args, debug); } catch (InvalidDataException) { count++; return; }
            throw new Exception(claim);
        }
        Check(SessionLaunch.Parse([], true).Mode == SessionMode.Normal, "Ordinary launch keeps normal persistence");
        Check(SessionLaunch.Parse(["--audio-smoke"], true).Mode == SessionMode.AutomatedTest, "Audio smoke selects disposable persistence");
        Reject(["--stage-preview", "--audio-smoke"], true, "Audio smoke cannot mix with preview");
        Reject(["--audio-smoke=1"], true, "Malformed audio test cannot select real saves");
        Check(SessionLaunch.Parse(["--ui-smoke"], true).Mode == SessionMode.AutomatedTest, "UI smoke selects disposable persistence");
        Check(SessionLaunch.Parse(["--live-ui-preview"], true).Mode == SessionMode.AutomatedTest, "Live qualification isolates saves too");
        Check(SessionLaunch.Parse(["--stage-preview"], true).Mode == SessionMode.AuthorPreview, "Stage preview cannot select real slots");
        var selected = SessionLaunch.Parse(["--author-preview-beat", "chosen"], true);
        Check(selected.Mode == SessionMode.AuthorPreview && selected.BeatId == "chosen" && !selected.StagePreview, "Selected beat is a distinct author-preview entry");
        Reject(["--author-preview-beat"], true, "Missing beat rejected");
        Reject(["--author-preview-beat", "--ui-smoke"], true, "A flag cannot substitute for a beat ID");
        Reject(["--author-preview-beat", "a", "--author-preview-beat", "b"], true, "Duplicate beat selector rejected");
        Reject(["--stage-preview", "--author-preview-beat", "a"], true, "Conflicting preview types rejected");
        Reject(["--stage-preview", "--ui-smoke"], true, "Preview and normal test cannot mix");
        Reject(["--smoke", "--ui-smoke"], true, "Multiple test modes rejected");
        Reject(["--author-preview-smoke"], true, "Selected-beat smoke requires selection");
        Reject(["--author-preview-beat", "a"], false, "Release preview rejected instead of silently using real saves");
        Reject(["--stage-preview"], false, "Release stage preview rejected");
        foreach (var malformed in new[] { "--author-preview-beat=some-id", "--stage-preview=true", "--author-preview-unknown", "--ui-smoke=1", "--save-isolation-smoke=yes", "--live-ui-preview=true", "--smoke=1", "--AUTHOR-PREVIEW-BEAT", "--Stage-Preview" })
            Reject([malformed], true, "Malformed preview/test option never selects normal saves: " + malformed);
        Check(SessionLaunch.Parse(["--unrelated-option"], true).Mode == SessionMode.Normal, "Unrelated option policy remains scoped");
        var all = story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).ToArray();
        foreach (var target in all.Where(b => b.Activity is not null))
        {
            var preview = StoryContextPreview.CreateSessionAtBeat(story, target.Id);
            Check(preview.Beat.Id == target.Id && !preview.CanAdvance, "Selected current activity remains unsolved: " + target.Id);
            var prior = all.TakeWhile(b => b.Id != target.Id).Where(b => b.Activity is not null).Select(b => b.Id).ToHashSet();
            Check(preview.SolvedActivities.SetEquals(prior), "Only prior mandatory gates are solved");
        }
        var ending = StoryContextPreview.CreateSessionAtBeat(story, all[^1].Id);
        Check(ending.IsEnding && ending.ActiveStageCues.Contains("bell_lowered"), "Late preview replays authored stage cues");
        return count;
    }
}
