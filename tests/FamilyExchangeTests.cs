using System.Text.Json.Nodes;
using Lanternwake.Core;

internal static class FamilyExchangeTests
{
    public static int Run(Story story, string json, string storyPath)
    {
        var count = 0;
        void Check(bool value, string claim) { if (!value) throw new Exception(claim); count++; }
        void Reject(Action action, string claim) { try { action(); } catch (InvalidDataException) { count++; return; } throw new Exception(claim); }
        const string id = "ch4_s1a_b035";
        var exchange = story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).Single(b => b.Id == id).Exchange!;
        var fixtures = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storyPath))!, "..", "tests", "Fixtures", "family-exchange");
        var target = SaveStore.Read(Path.Combine(fixtures, "target.json"));
        foreach (var name in new[] { "target", "after", "completed" })
        {
            var legacy = SaveStore.Read(Path.Combine(fixtures, name + ".json"));
            var game = new StorySession(story); game.Restore(legacy);
            Check(game.Snapshot().Version == 2 && game.Beat.Id == legacy.BeatId && game.History.SequenceEqual(legacy.History) && game.SolvedActivities.SetEquals(legacy.SolvedActivities), "Old v2 position/history/gates remain exact without migration");
        }
        Check(exchange.Options.Length == 3 && exchange.Options.Select(o => o.Reply).Distinct().Count() == 3, "Three intentions have distinct authored responses");
        for (var choice = 0; choice < 3; choice++)
        {
            var game = new StorySession(story); game.Restore(target);
            var facts = game.KnownFacts.ToArray(); var items = game.Inventory.ToArray(); var cues = game.ActiveStageCues;
            Check(game.CanAdvance && game.ChosenExchangeIndex is null, "Exchange is optional and initially unspoken");
            Check(game.RecordExchange(choice) && game.ChosenExchangeIndex == choice, "Explicit choice records its matching response");
            Check(game.History.Count == target.History.Count + 2 && game.History[^2].Text == exchange.Options[choice].Label && game.History[^1].Text == exchange.Options[choice].Reply, "Only exact authored user/reply pair is appended");
            Check(!game.History[^1].Generated && game.History[^1].ConversationCharacterId is null && game.History[^1].SceneId is null, "Authored result is not a model reply or model memory");
            Check(game.Beat.Id == id && game.CanAdvance && facts.SequenceEqual(game.KnownFacts) && items.SequenceEqual(game.Inventory) && cues.SequenceEqual(game.ActiveStageCues) && game.SolvedActivities.SetEquals(target.SolvedActivities), "Relational emphasis grants no fact, item, score, stage event or progress");
            Check(!game.RecordExchange((choice + 1) % 3) && game.History.Count == target.History.Count + 2, "Repeated callbacks cannot change or duplicate a choice");
            var saved = game.Snapshot(); var restored = new StorySession(story); restored.Restore(saved);
            Check(restored.ChosenExchangeIndex == choice && restored.History.SequenceEqual(saved.History), "Chosen branch survives save/reload");
            Reject(() => restored.Restore(saved with { History = [.. saved.History, .. saved.History.TakeLast(2)] }), "Duplicate exchange is rejected transactionally");
            Reject(() => restored.Restore(saved with { History = saved.History.Take(saved.History.Count - 1).ToList() }), "Partial choice is rejected");
            Reject(() => restored.Restore(saved with { History = saved.History.Select((line, index) => index == saved.History.Count - 1 ? line with { Text = "Changed reply" } : line).ToList() }), "Mismatched reply is rejected");
            Check(restored.ChosenExchangeIndex == choice && restored.History.SequenceEqual(saved.History), "Bad restores preserve selected state");
            var export = Environment.GetEnvironmentVariable("LANTERNWAKE_FAMILY_COMPAT_OUTPUT");
            if (export is not null) SaveStore.Write(Path.Combine(export, "choice-" + choice + ".json"), saved);
            Check(game.Advance() && game.Beat.Id == "ch4_s1a_b036", "All choices rejoin the same existing successor");
        }
        var skip = new StorySession(story); skip.Restore(target); Check(skip.Advance() && skip.Beat.Id == "ch4_s1a_b036", "Leaving the exchange unspoken never blocks the story");
        void Invalid(Action<JsonObject> edit, string claim)
        {
            var document = JsonNode.Parse(json)!;
            var beat = document["chapters"]!.AsArray().SelectMany(c => c!["scenes"]!.AsArray()).SelectMany(s => s!["beats"]!.AsArray()).Single(b => b!["id"]!.GetValue<string>() == id)!;
            edit(beat["exchange"]!.AsObject()); Reject(() => Story.Parse(document.ToJsonString()), claim);
        }
        Invalid(e => e["characterId"] = "ivo", "Recorded voices cannot join authored live exchange");
        Invalid(e => e["characterId"] = "sera", "Exchange target must be present in scene");
        Invalid(e => e["options"] = null, "Missing options rejected");
        Invalid(e => e["options"]![1]!["label"] = e["options"]![0]!["label"]!.GetValue<string>(), "Duplicate labels rejected");
        Invalid(e => e["options"]![0]!["reply"] = " ", "Blank response rejected");
        var author = new StoryAuthoringIndex(story).Entries.Single(e => e.Beat.Id == id);
        Check(author.Context.Contains(exchange.Options[2].Reply), "Author view exposes exact source response");
        var edited = StoryTextEdit.Apply(json, json, id, "narrator", "Edited scene line.");
        Check(Story.Parse(edited).Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).Single(b => b.Id == id).Exchange!.Options.SequenceEqual(exchange.Options), "Beat editing preserves exchange choices");
        return count;
    }
}
