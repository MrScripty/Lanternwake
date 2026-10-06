using System.Text.Json;
using System.Text.Json.Nodes;
using Lanternwake.Core;

var path = args.Length > 0 ? args[0] : "Content/story.json";
var source = File.ReadAllText(path);
var story = Story.Parse(source);
var count = 0;
var failures = new List<string>();
void Check(bool condition, string claim)
{
    count++;
    if (!condition) failures.Add(claim);
}
JsonNode Chapter(JsonNode root) => root["chapters"]![0]!;
JsonNode Scene(JsonNode root) => Chapter(root)["scenes"]![0]!;
JsonNode WithConversation(JsonNode root) => root["chapters"]!.AsArray()
    .SelectMany(c => c!["scenes"]!.AsArray()).SelectMany(s => s!["beats"]!.AsArray())
    .First(b => b!["conversation"] is not null)!["conversation"]!;
JsonNode WithActivity(JsonNode root) => root["chapters"]!.AsArray()
    .SelectMany(c => c!["scenes"]!.AsArray()).SelectMany(s => s!["beats"]!.AsArray())
    .First(b => b!["activity"] is not null)!["activity"]!;

var collections = new (string Name, Func<JsonNode, JsonNode> Owner, string Field)[]
{
    ("characters", r => r, "characters"),
    ("facts", r => r, "facts"),
    ("items", r => r, "items"),
    ("chapters", r => r, "chapters"),
    ("chapter scenes", Chapter, "scenes"),
    ("scene beats", Scene, "beats"),
    ("scene cast", Scene, "characterIds"),
    ("character knowledge", r => r["characters"]![0]!, "knowledge"),
    ("conversation suggestions", WithConversation, "suggestions"),
    ("conversation facts", WithConversation, "allowedFacts"),
    ("activity options", WithActivity, "options"),
};
foreach (var (name, owner, field) in collections)
{
    foreach (var mutation in new[] { "missing", "null", "null entry" })
    {
        var root = JsonNode.Parse(source)!;
        var target = owner(root);
        if (mutation == "missing") target.AsObject().Remove(field);
        else if (mutation == "null") target[field] = null;
        else
        {
            var entries = target[field]!.AsArray();
            if (entries.Count == 0) entries.Add((JsonNode?)null);
            else entries[0] = null;
        }
        try
        {
            Story.Parse(root.ToJsonString());
            Check(false, $"{name}: {mutation} was accepted");
        }
        catch (InvalidDataException error) { Check(!string.IsNullOrWhiteSpace(error.Message), $"{name}: diagnostic missing"); }
        catch (Exception error) { Check(false, $"{name}: {mutation} threw {error.GetType().Name}"); }
    }
}

var requiredText = new (string Field, Func<JsonNode, JsonNode> Owner, string Key, string Id)[]
{
    ("title", Chapter, "title", story.Chapters[0].Id),
    ("timeOfDay", Scene, "timeOfDay", story.Chapters[0].Scenes[0].Id),
    ("activity.prompt", WithActivity, "prompt", story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).First(b => b.Activity is not null).Id),
};
void RejectText(JsonNode root, string field, string id)
{
    try { Story.Parse(root.ToJsonString()); Check(false, $"Invalid {field} was accepted"); }
    catch (InvalidDataException error)
    {
        Check(error.Message.Contains(field, StringComparison.Ordinal) && error.Message.Contains(id, StringComparison.Ordinal),
            $"Diagnostic must identify {field} and {id}: {error.Message}");
    }
    catch (Exception error) { Check(false, $"Invalid {field} threw {error.GetType().Name}"); }
}
foreach (var (field, owner, key, id) in requiredText)
{
    foreach (var value in new string?[] { null, "", " \t\r\n\u2003" })
    {
        var root = JsonNode.Parse(source)!;
        owner(root)[key] = value;
        RejectText(root, field, id);
    }
    var missing = JsonNode.Parse(source)!;
    owner(missing).AsObject().Remove(key);
    RejectText(missing, field, id);
    var valid = JsonNode.Parse(source)!;
    const string label = "  Authored label — 語  ";
    owner(valid)[key] = label;
    var reparsed = JsonNode.Parse(JsonSerializer.Serialize(Story.Parse(valid.ToJsonString()), Story.Json))!;
    Check(owner(reparsed)[key]!.GetValue<string>() == label, $"{field} retains valid Unicode and surrounding whitespace");
}
var evidenceBeat = story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).First(b => b.Activity is not null);
for (var index = 0; index < evidenceBeat.Activity!.Options.Length; index++)
{
    foreach (var value in new string?[] { null, "", " \t\u2003" })
    {
        var root = JsonNode.Parse(source)!;
        WithActivity(root)["options"]![index] = value;
        RejectText(root, $"activity.options[{index}]", evidenceBeat.Id);
    }
}
var blankChoices = JsonNode.Parse(source)!;
var options = WithActivity(blankChoices)["options"]!.AsArray();
for (var index = 0; index < options.Count; index++) options[index] = " ";
RejectText(blankChoices, "activity.options[0]", evidenceBeat.Id);
var validChoice = JsonNode.Parse(source)!;
WithActivity(validChoice)["options"]![0] = "  Read the record — 語  ";
var parsedChoice = Story.Parse(validChoice.ToJsonString()).Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).First(b => b.Activity is not null).Activity!;
Check(parsedChoice.Options[0] == "  Read the record — 語  " && parsedChoice.CorrectIndex == evidenceBeat.Activity.CorrectIndex,
    "Valid choice text and answer index remain unchanged");

// Empty collections are allowed where no minimum is part of the content contract.
var empty = JsonNode.Parse(source)!;
Scene(empty)["characterIds"] = new JsonArray();
empty["characters"]![0]!["knowledge"] = new JsonArray();
WithConversation(empty)["suggestions"] = new JsonArray();
WithConversation(empty)["allowedFacts"] = new JsonArray();
Check(Story.Parse(empty.ToJsonString()).SchemaVersion == 1, "Valid empty collections remain accepted");
// Use the last beat so removing unlocks cannot affect later conversation validation.
var optional = JsonNode.Parse(source)!;
var last = optional["chapters"]!.AsArray().Last()!["scenes"]!.AsArray().Last()!["beats"]!.AsArray().Last()!;
last["unlockFacts"] = null;
last["unlockItems"] = null;
Check(Story.Parse(optional.ToJsonString()).SchemaVersion == 1, "Optional null unlock arrays remain accepted");
var session = new StorySession(story);
var snapshot = session.Snapshot();
var restored = new StorySession(Story.Parse(JsonSerializer.Serialize(story, Story.Json)));
restored.Restore(snapshot);
Check(JsonSerializer.Serialize(restored.Snapshot()) == JsonSerializer.Serialize(snapshot), "Schema-1 snapshot roundtrip unchanged");
Check(File.ReadAllText(path) == source, "Canonical source remains unchanged");
foreach (var failure in failures) Console.Error.WriteLine("FAIL " + failure);
Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")} {count} story structure checks; {failures.Count} failures.");
return failures.Count == 0 ? 0 : 1;
