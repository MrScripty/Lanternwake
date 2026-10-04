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
