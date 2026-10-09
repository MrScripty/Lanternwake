using System.Text.Json;
using Lanternwake.Core;

internal static class MissingMarginTests
{
    public const string Id = "ch2_s4_b019a";
    public static int Run(Story story)
    {
        int checks = 0;
        void Check(bool value, string claim) { if (!value) throw new Exception(claim); checks++; }
        var session = new StorySession(story);
        var fixtures = Environment.GetEnvironmentVariable("LANTERNWAKE_MARGIN_OUTPUT");
        do
        {
            if (fixtures is not null && session.Beat.Id is "ch2_s3a_evidence" or "ch2_s4_b001" or Id)
                SaveStore.Write(Path.Combine(fixtures, session.Beat.Id + ".json"), session.Snapshot());
            if (session.Beat.Id == Id) break;
            if (session.Beat.Activity is { } activity) session.AnswerActivity(activity.CorrectIndex);
            if (!session.Advance()) throw new Exception("Missing margin exchange not found");
        } while (true);
        var target = session.Snapshot(); var exchange = session.Beat.Exchange!;
        Check(session.Scene.Id == "ch2_s4" && session.KnownFacts.Any(f => f.Id == "f_west_omission") &&
            !session.KnownFacts.Any(f => f.Id == "f_full_testimony"), "Choice follows the omission evidence without revealing later testimony");
        Check(exchange.Options.Length == 3 && exchange.Options.Select(o => o.Reply).Distinct().Count() == 3,
            "Three supported intentions receive distinct authored responses");
        foreach (var index in Enumerable.Range(0, 3))
        {
            var branch = new StorySession(story); branch.Restore(target);
            var facts = branch.KnownFacts.ToArray(); var items = branch.Inventory.ToArray(); var cues = branch.ActiveStageCues;
            Check(branch.RecordExchange(index) && branch.ChosenExchangeIndex == index, "Explicit choice is retained");
            Check(branch.History.Count == target.History.Count + 2 && branch.History[^2].Text == exchange.Options[index].Label &&
                branch.History[^1].Text == exchange.Options[index].Reply && branch.History.TakeLast(2).All(l => !l.Generated && l.SceneId is null && l.ConversationCharacterId is null),
                "Exactly one authored pair is appended without pretending model generation");
            Check(!branch.RecordExchange((index + 1) % 3), "Repeated selection cannot rewrite the intention");
            var selected = branch.Snapshot(); var restored = new StorySession(story);
            restored.Restore(JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(selected, Story.Json), Story.Json)!);
            Check(restored.ChosenExchangeIndex == index && restored.History.SequenceEqual(selected.History), "JSON save/reload preserves the exact intention and reply");
            Check(restored.KnownFacts.SequenceEqual(facts) && restored.Inventory.SequenceEqual(items) && restored.ActiveStageCues.SequenceEqual(cues) &&
                restored.SolvedActivities.SetEquals(target.SolvedActivities), "No branch awards evidence, stage events, gates or invented relationships");
            Check(restored.Advance() && restored.Beat.Id == "ch2_s4_b020" && restored.Beat.Conversation is not null,
                "Every choice rejoins the original optional free-response conversation");
            Check(!restored.ConversationContext().Contains(exchange.Options[index].Reply), "Authored choice is not passed off as generated conversation memory");
        }
        var skipped = new StorySession(story); skipped.Restore(target);
        Check(skipped.Advance() && skipped.Beat.Id == "ch2_s4_b020" && skipped.History.Count == target.History.Count + 1,
            "Declining the local exchange preserves the fixed progression");
        // Remove only the added beat to model the old canonical reader and its v2 saves.
        var legacy = story with { Chapters = story.Chapters.Select(c => c with { Scenes = c.Scenes.Select(s => s with
            { Beats = s.Beats.Where(b => b.Id != Id).ToArray() }).ToArray() }).ToArray() };
        legacy.Validate(); var old = new StorySession(legacy);
        do
        {
            if (old.Beat.Id is "ch2_s4_b019" or "ch2_s4_b020" || old.IsEnding)
            {
                var save = old.Snapshot(); var restored = new StorySession(story); restored.Restore(save);
                Check(restored.Beat.Id == save.BeatId && restored.History.SequenceEqual(save.History) &&
                    restored.SolvedActivities.SetEquals(save.SolvedActivities), "Old before/after/completed v2 saves restore without backfilling or forcing a choice");
            }
            if (old.Beat.Activity is { } activity) old.AnswerActivity(activity.CorrectIndex);
        } while (old.Advance());
        Console.WriteLine($"Missing Margin core passed: {checks} assertions.");
        return checks;
    }
}
