using System.Text.Json;
using Lanternwake.Core;

internal static class HistoryNavigationTests
{
    public static int Run(Story story)
    {
        var checks = 0;
        void Check(bool value, string claim) { if (!value) throw new Exception("History navigation: " + claim); checks++; }
        var session = new StorySession(story);
        var empty = new HistoryNavigation(story, []);
        Check(empty.Entries.Length == 0 && empty.Chapters.Length == 0 && empty.Search("anything").Length == 0, "empty transcript exposes no chapter");
        var initial = session.Snapshot();
        var early = new HistoryNavigation(story, session.History);
        Check(early.Entries.Single().Line.Text == session.Beat.Text && early.Chapters.Single().Id == story.Chapters[0].Id, "opening entry and authoritative reached chapter");
        Check(early.Search(story.Chapters[2].Scenes[0].Beats[0].Text).Length == 0, "future authored prose is never indexed");
        SaveData? earlier = null;
        while (!session.IsEnding)
        {
            if (session.Beat.Conversation is not null) session.RecordConversation("History fixture 語 [b] literal", "Recorded optional answer", true);
            if (session.Beat.Exchange is { } exchange)
            {
                session.RecordExchange(0);
                var selected = new HistoryNavigation(story, session.History);
                Check(selected.Search(exchange.Options[0].Reply).Length > 0, "selected exchange searchable");
                foreach (var option in exchange.Options.Skip(1)) Check(selected.Search(option.Reply).Length == 0, "unchosen authored reply excluded");
            }
            if (session.Beat.Activity is { } activity) session.AnswerActivity(activity.CorrectIndex);
            if (earlier is null && session.Chapter.Id == story.Chapters[1].Id) earlier = session.Snapshot();
            session.Advance();
        }
        var before = JsonSerializer.Serialize(session.Snapshot(), Story.Json);
        var facts = session.KnownFacts.Select(f => f.Id).ToArray(); var inventory = session.Inventory.Select(i => i.Id).ToArray();
        var complete = new HistoryNavigation(story, session.History);
        Check(complete.Chapters.Select(c => c.Id).SequenceEqual(story.Chapters.Select(c => c.Id)), "all reached chapter IDs preserve canonical order");
        Check(complete.Chapters.All(c => complete.Entries[c.FirstEntryIndex].ChapterId == c.Id), "chapter offsets reference recorded entries");
        Check(complete.Search("  ").Length == session.History.Count && complete.Search("unmatched fixture xyz").Length == 0, "blank and no results");
        Check(complete.Search("hIsToRy FiXtUrE 語 [b]").All(e => e.Line.Speaker == "you") && complete.Search("hIsToRy FiXtUrE 語 [b]").Length > 0, "literal Unicode case-insensitive search uses recorded lines");
        Check(complete.Search("You").Any(e => e.Line.Speaker == "you"), "displayed speaker names searchable");
        Check(before == JsonSerializer.Serialize(session.Snapshot(), Story.Json) && facts.SequenceEqual(session.KnownFacts.Select(f => f.Id)) && inventory.SequenceEqual(session.Inventory.Select(i => i.Id)), "projection preserves state and unlocks");
        session.Restore(earlier!);
        var restored = new HistoryNavigation(story, session.History);
        Check(restored.Chapters.Length == 2 && restored.Entries.Last().Line.BeatId == earlier!.BeatId, "earlier restore replaces latest and chapter boundary");
        Check(restored.Search(story.Chapters[2].Scenes[0].Beats[0].Text).Length == 0, "earlier restore removes later prose from search");
        var fresh = new StorySession(story); fresh.Restore(JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(earlier, Story.Json), Story.Json)!);
        Check(new HistoryNavigation(story, fresh.History).Entries.SequenceEqual(restored.Entries), "serialized fresh session has identical history navigation");
        session.Restore(initial);
        Check(new HistoryNavigation(story, session.History).Entries.Length == 1 && complete.Entries.Length > 1, "independent projections are snapshots");
        var sparse = new HistoryNavigation(story, [new("narrator", "Actual recorded text", BeatId: initial.BeatId), new("you", "Legacy unknown association")]);
        Check(sparse.Entries.Length == 2 && sparse.Chapters.Length == 1 && sparse.Search("Legacy").Length == 1, "missing metadata keeps actual prose without inventing chapter");
        return checks;
    }
}
