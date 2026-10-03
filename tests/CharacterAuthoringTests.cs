using Lanternwake.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class CharacterAuthoringTests
{
    public static int Run(Story story, string json)
    {
        var count = 0;
        void Check(bool condition, string claim) { if (!condition) throw new Exception(claim); count++; }
        void Reject(Action action, string claim)
        {
            try { action(); } catch (InvalidDataException) { count++; return; }
            throw new Exception(claim);
        }
        var legacyDocument = JsonNode.Parse(json)!;
        foreach (var character in legacyDocument["characters"]!.AsArray())
        {
            character!.AsObject().Remove("authoringProfile"); character.AsObject().Remove("dialogueStyle");
        }
        var legacyJson = legacyDocument.ToJsonString();
        var legacy = Story.Parse(legacyJson);
        Check(legacy.Characters.All(c => c.AuthoringProfile is null && c.DialogueStyle is null), "Legacy characters need no profile fields");
        var legacySave = new StorySession(legacy).Snapshot();
        var restored = new StorySession(story); restored.Restore(legacySave);
        Check(restored.Beat.Id == legacySave.BeatId, "Old saves restore after dossier additions");
        foreach (var field in CharacterProfileEdit.Fields)
        {
            var edited = CharacterProfileEdit.Apply(json, json, "nessa", field, "Author edit 語");
            var expected = JsonNode.Parse(json)!;
            var target = expected["characters"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == "nessa")!;
            if (field == "dialogueStyle") target[field] = "Author edit 語";
            else { target["authoringProfile"] ??= new JsonObject(); target["authoringProfile"]![field] = "Author edit 語"; }
            Check(JsonNode.DeepEquals(expected, JsonNode.Parse(edited)), "Profile save changes only selected field: " + field);
        }
        var optional = CharacterProfileEdit.Apply(legacyJson, legacyJson, "nessa", "history", "Only in author tools.");
        Check(Story.Parse(optional).Characters.Single(c => c.Id == "nessa").AuthoringProfile!.History == "Only in author tools.", "First edit creates optional profile");
        var unknown = JsonNode.Parse(json)!;
        unknown["futureDocumentField"] = "keep-root";
        var nessa = unknown["characters"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == "nessa")!;
        nessa["futureCharacterField"] = "keep-character";
        nessa["authoringProfile"] ??= new JsonObject();
        nessa["authoringProfile"]!["futureProfileField"] = "keep-profile";
        var unknownJson = unknown.ToJsonString();
        var preserved = CharacterProfileEdit.Apply(unknownJson, unknownJson, "nessa", "history", "Changed history");
        Check(preserved.Contains("keep-root") && preserved.Contains("keep-character") && preserved.Contains("keep-profile"), "Unknown nested JSON survives dossier save");
        Reject(() => CharacterProfileEdit.Apply(json, json, "nessa", "knowledge", "f_authorship"), "Profile edit cannot change knowledge ceiling");
        Reject(() => CharacterProfileEdit.Apply(json, json, "missing", "history", "text"), "Missing character rejected");
        Reject(() => CharacterProfileEdit.Apply(json, json, "nessa", "history", new string('x', 8001)), "Author field bound enforced");
        Reject(() => CharacterProfileEdit.Apply(json, json, "nessa", "dialogueStyle", new string('x', 601)), "Runtime style bound enforced");
        try { CharacterProfileEdit.Apply(json, json + "\n", "nessa", "history", "stale"); throw new Exception("Stale dossier save accepted"); }
        catch (InvalidOperationException) { count++; }
        Reject(() => StoryContextPreview.AtBeat(story, "missing-beat"), "Unknown preview beat rejected");
        Check(StoryContextPreview.AtBeat(story, story.Chapters[0].Scenes[0].Beats[0].Id) is null, "No prompt for non-conversation beat");

        var secretStory = story with
        {
            Characters = story.Characters.Select(c => c with
            {
                AuthoringProfile = new("SECRET_HISTORY_" + c.Id, "SECRET_PERSONALITY_" + c.Id,
                    "SECRET_MOTIVATIONS_" + c.Id, "SECRET_STYLE_" + c.Id, "SECRET_KNOWLEDGE_" + c.Id, "SECRET_SOURCES_" + c.Id)
            }).ToArray()
        };
        secretStory.Validate();
        var session = new StorySession(secretStory);
        var conversations = 0;
        while (true)
        {
            if (session.Beat.Conversation is { } chat)
            {
                conversations++;
                var before = JsonSerializer.Serialize(session.Snapshot());
                var context = session.ConversationContext();
                var preview = StoryContextPreview.AtBeat(secretStory, session.Beat.Id);
                Check(preview == context, "Preview uses identical production context at " + session.Beat.Id);
                Check(JsonSerializer.Serialize(session.Snapshot()) == before, "Preview cannot mutate existing session");
                Check(!context.Contains("SECRET_"), "All author-only fields excluded from " + session.Beat.Id);
                Check(context.Length <= 12000, "Context stays within unchanged budget");
                var person = secretStory.Characters.Single(c => c.Id == chat.CharacterId);
                if (!string.IsNullOrWhiteSpace(person.DialogueStyle)) Check(context.Contains(person.DialogueStyle), "Explicit runtime style is included");
                var visible = session.KnownFacts.Where(f => chat.AllowedFacts.Contains(f.Id) && person.Knowledge.Contains(f.Id)).Select(f => f.Id).ToHashSet();
                foreach (var fact in secretStory.Facts)
                    Check(context.Contains(fact.Text) == visible.Contains(fact.Id), "Exact fact intersection in context: " + fact.Id);
                var oldContext = StoryContextPreview.AtBeat(legacy, session.Beat.Id)!;
                Check(!oldContext.Contains("Dialogue behavior"), "Absent runtime style keeps legacy context shape");
                Check(StoryContextPreview.AtBeat(story, session.Beat.Id) == context, "Changing dossiers cannot alter runtime context");
            }
            if (session.Beat.Activity is { } activity) session.AnswerActivity(activity.CorrectIndex);
            if (!session.Advance()) break;
        }
        Check(conversations == 28, "All 28 authored conversations covered by secret-sentinel regression");
        var completed = new StorySession(legacy); completed.Restore(session.Snapshot());
        Check(completed.IsEnding, "Completed dossier-era saves remain compatible with legacy profiles");
        foreach (var recorded in new[] { "ada", "ivo", "operator", "clerk" })
        {
            var invalid = JsonNode.Parse(json)!;
            var beat = invalid["chapters"]![0]!["scenes"]![0]!["beats"]!.AsArray().First(b => b!["conversation"] is not null)!;
            beat["conversation"]!["characterId"] = recorded;
            beat["conversation"]!["allowedFacts"] = new JsonArray();
            Reject(() => Story.Parse(invalid.ToJsonString()), "Viewpoint/recorded characters never become live targets: " + recorded);
        }
        return count;
    }
}
