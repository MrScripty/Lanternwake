using System.Text.Json;
using System.Text.Json.Nodes;
using Lanternwake.Core;

internal static class FirstRouteFeedbackTests
{
    public static int Run(Story story, string json)
    {
        var count = 0;
        void Check(bool value, string claim) { if (!value) throw new Exception(claim); count++; }
        const string gate = "ch1_s1a_evidence";
        var activity = story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats)
            .Single(b => b.Id == gate).Activity!;
        Check(activity.OptionFeedback is { Length: 3 } &&
            activity.OptionFeedback[0] != activity.OptionFeedback[2], "First route mistakes have distinct authored feedback");

        // Removing feedback models a pre-feature story; actual core traversal
        // produces a compatible unanswered checkpoint with the old transcript.
        var previous = JsonNode.Parse(json)!;
        previous["chapters"]!.AsArray().SelectMany(c => c!["scenes"]!.AsArray())
            .SelectMany(s => s!["beats"]!.AsArray()).Single(b => b!["id"]!.GetValue<string>() == gate)!
            ["activity"]!.AsObject().Remove("optionFeedback");
        var oldStory = Story.Parse(previous.ToJsonString());
        var oldSession = StoryContextPreview.CreateSessionAtBeat(oldStory, gate);
        var saved = oldSession.Snapshot();
        var game = new StorySession(story); game.Restore(saved);
        string State() => JsonSerializer.Serialize(new
        {
            snapshot = game.Snapshot(), facts = game.KnownFacts.ToArray(),
            inventory = game.Inventory.ToArray(), cues = game.ActiveStageCues,
            game.CanAdvance, game.Progress
        }, Story.Json);
        var unanswered = State();
        Check(game.Beat.Id == gate && !game.CanAdvance &&
            game.History.SequenceEqual(saved.History), "Pre-feedback save restores the exact unanswered question and transcript");
        foreach (var wrong in new[] { 0, 2, 0 })
        {
            Check(!game.AnswerActivity(wrong) && !game.Advance(), "Wrong route and repeated retry cannot unlock Continue");
            Check(State() == unanswered, "Wrong route preserves the complete snapshot and derived facts, items, cues and progress");
        }
        Check(game.AnswerActivity(activity.CorrectIndex) && game.CanAdvance && game.Beat.Id == gate,
            "Correct route unlocks Continue without automatically advancing");
        Check(game.History.SequenceEqual(saved.History) && game.SolvedActivities.SetEquals(saved.SolvedActivities.Concat([gate])),
            "Correct answer changes only the current solved gate, preserving the record");
        var restored = new StorySession(story); restored.Restore(game.Snapshot());
        Check(restored.CanAdvance && restored.History.SequenceEqual(saved.History), "Solved route and exact record survive save restoration");
        Check(game.Advance() && game.Beat.Id == "ch1_s2_b001", "Explicit Continue rejoins the existing inventory scene");
        var author = new StoryAuthoringIndex(story).Entries.Single(e => e.Beat.Id == gate);
        Check(activity.OptionFeedback!.All(author.Context.Contains), "Author context exposes every saved route rationale");
        var edited = StoryTextEdit.Apply(json, json, gate, "narrator", "Edited question introduction.");
        Check(Story.Parse(edited).Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats)
            .Single(b => b.Id == gate).Activity!.OptionFeedback!.SequenceEqual(activity.OptionFeedback!),
            "Story Text editing preserves all option feedback");
        var output = Environment.GetEnvironmentVariable("LANTERNWAKE_FIRST_ROUTE_FIXTURE_OUTPUT");
        if (output is not null) SaveStore.Write(Path.Combine(output, gate + ".json"), saved);
        return count;
    }
}
