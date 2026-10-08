using System.Text.Json.Nodes;
using Lanternwake.Core;

internal static class SourceReconstructionTests
{
    public static int Run(Story story, string json, string storyPath)
    {
        var count = 0;
        void Check(bool value, string claim) { if (!value) throw new Exception(claim); count++; }
        void Reject(Action action, string claim)
        {
            try { action(); } catch (InvalidDataException) { count++; return; }
            throw new Exception(claim);
        }
        const string gate = "ch4_s2_reconstruction_evidence";
        var beat = story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).Single(b => b.Id == gate);
        var activity = beat.Activity!;
        Check(activity.OptionFeedback is { Length: 3 } && activity.OptionFeedback[1] != activity.OptionFeedback[2], "Source mistakes have distinct authored rationales");
        var fixture = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storyPath))!, "..", "tests", "Fixtures", "source-reconstruction");
        var before = SaveStore.Read(Path.Combine(fixture, "before.json"));
        var after = SaveStore.Read(Path.Combine(fixture, "after.json"));
        var completed = SaveStore.Read(Path.Combine(fixture, "completed.json"));
        foreach (var legacy in new[] { before, after, completed })
        {
            Check(legacy.Version == 1 && !legacy.SolvedActivities.Contains(gate), "Frozen fixtures predate the new gate");
            var game = new StorySession(story); game.Restore(legacy);
            Check(game.Beat.Id == legacy.BeatId && game.History.SequenceEqual(legacy.History), "Legacy position and exact transcript survive insertion");
            Check(game.SolvedActivities.SetEquals(legacy.SolvedActivities.Concat(legacy == before ? [] : new[] { gate })), "Only the known passed insertion receives legacy carry-forward");
            Check(game.Snapshot().Version == 2, "Next snapshot uses current version");
            var reloaded = new StorySession(story); reloaded.Restore(game.Snapshot());
            Check(reloaded.Beat.Id == legacy.BeatId && reloaded.SolvedActivities.SetEquals(game.SolvedActivities), "Migrated snapshot loads without a second carry-forward");
        }
        var session = new StorySession(story); session.Restore(before);
        Check(session.Advance() && session.Beat.Id == gate && !session.CanAdvance, "Legacy save before insertion reaches the required source comparison");
        var unsolved = session.Snapshot();
        foreach (var wrong in new[] { 1, 2 })
        {
            Check(!session.AnswerActivity(wrong) && !session.Advance(), "Unsupported claim cannot advance");
            Check(session.History.SequenceEqual(unsolved.History) && session.SolvedActivities.SetEquals(unsolved.SolvedActivities), "Retry changes no canonical history or solved state");
        }
        var fresh = new StorySession(story); fresh.Restore(unsolved);
        Check(!fresh.CanAdvance, "Saved unanswered new gate remains unanswered");
        Check(session.AnswerActivity(0) && session.Advance() && session.Beat.Id == after.BeatId, "Supported account rejoins the existing authored payoff");
        Reject(() => fresh.Restore(after with { Version = 2 }), "Current saves cannot skip the new gate");
        Reject(() => fresh.Restore(after with { History = [.. after.History, new("narrator", beat.Text, BeatId: gate)] }), "A v1 save that reached but did not solve the new gate is not grandfathered");
        var missingOriginal = new HashSet<string>(completed.SolvedActivities);
        missingOriginal.Remove("ch1_s1a_evidence");
        Reject(() => fresh.Restore(completed with { SolvedActivities = missingOriginal }), "Legacy migration cannot waive any original gate");
        Check(fresh.Beat.Id == gate && !fresh.CanAdvance, "Rejected restores leave the current state intact");
        var temp = Path.Combine(Path.GetTempPath(), "lanternwake-legacy-source-" + Guid.NewGuid() + ".json");
        try
        {
            SaveStore.Write(temp, completed); var bytes = File.ReadAllBytes(temp);
            var candidate = SaveRecovery.Inspect(temp, story, false, false);
            Check(candidate.Availability == SaveAvailability.Available, "Normal recovery selection accepts a genuine completed v1 save");
            Check(bytes.SequenceEqual(File.ReadAllBytes(temp)), "Inspecting legacy recovery preserves original bytes");
        }
        finally { File.Delete(temp); }
        void InvalidFeedback(Action<JsonArray> edit, string claim)
        {
            var document = JsonNode.Parse(json)!;
            var b = document["chapters"]!.AsArray().SelectMany(c => c!["scenes"]!.AsArray())
                .SelectMany(s => s!["beats"]!.AsArray()).Single(b => b!["id"]!.GetValue<string>() == gate)!;
            edit(b["activity"]!["optionFeedback"]!.AsArray());
            Reject(() => Story.Parse(document.ToJsonString()), claim);
        }
        InvalidFeedback(a => a.RemoveAt(2), "Feedback must match the option count");
        InvalidFeedback(a => a[1] = "  ", "Every wrong option requires a nonblank rationale");
        InvalidFeedback(a => a[2] = null, "Null wrong-option rationale is invalid");
        var authored = new StoryAuthoringIndex(story).Entries.Single(e => e.Beat.Id == gate);
        Check(authored.Context.Contains(activity.OptionFeedback![1]) && authored.Context.Contains(activity.OptionFeedback[2]), "Author context exposes both saved rationales");
        return count;
    }
}
