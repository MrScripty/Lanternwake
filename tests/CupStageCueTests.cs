using System.Text.Json.Nodes;
using Lanternwake.Core;

internal static class CupStageCueTests
{
    public static int Run(Story story, string json)
    {
        int checks = 0;
        void Check(bool condition, string claim)
        {
            if (!condition) throw new InvalidOperationException("Cup cue contract: " + claim);
            checks++;
        }
        var expected = new Dictionary<string, string>
        {
            ["ch2_s5_b012"] = "cup_broken", ["ch2_s5_b026"] = "cup_boxed", ["ch3_s5_b001"] = "steel_mug"
        };
        var entries = story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats.Select(b => (Scene: s, Beat: b))).ToArray();
        foreach (var entry in entries.Where(e => expected.ContainsKey(e.Beat.Id)))
            Check(entry.Scene.Location == "keeper_house" && entry.Beat.StageCue == expected[entry.Beat.Id], "canonical event cue and location");
        Check(entries.Count(e => e.Beat.StageCue is "cup_broken" or "cup_boxed" or "steel_mug") == 3, "only three authored cup transitions");
        var session = new StorySession(story);
        var saves = new Dictionary<string, SaveData>();
        do
        {
            if (expected.ContainsKey(session.Beat.Id) || session.Beat.Id == "ch2_s5_b011") saves.Add(session.Beat.Id, session.Snapshot());
            if (session.Beat.Activity is { } activity) session.AnswerActivity(activity.CorrectIndex);
        } while (session.Advance());
        foreach (var id in new[] { "ch3_s5_b001", "ch2_s5_b026", "ch2_s5_b012", "ch2_s5_b011", "ch3_s5_b001" })
        {
            session.Restore(saves[id]);
            var index = Array.FindIndex(entries, e => e.Beat.Id == id);
            Check(session.ActiveStageCues.SequenceEqual(entries.Take(index + 1).Select(e => e.Beat.StageCue).OfType<string>().Distinct()), "earlier/later restore derives cues from chronology");
            Check(session.Beat.Id == id && session.History.SequenceEqual(saves[id].History), "cue replay does not add narrative history");
        }
        var invalid = JsonNode.Parse(json)!;
        invalid["chapters"]![0]!["scenes"]![0]!["beats"]![0]!["stageCue"] = "cup_exploded";
        try { Story.Parse(invalid.ToJsonString()); throw new InvalidOperationException("Unknown cue accepted."); }
        catch (InvalidDataException) { checks++; }
        return checks;
    }
}
