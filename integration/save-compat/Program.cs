using System.Text.Json;
using Lanternwake.Core;

if (args.Length != 2) throw new ArgumentException("Pass the frozen story.json and a directory containing choice-0.json, choice-1.json and choice-2.json.");
var story = Story.Parse(File.ReadAllText(args[0]));
for (var choice = 0; choice < 3; choice++)
{
    var save = SaveStore.Read(Path.Combine(args[1], "choice-" + choice + ".json"));
    var session = new StorySession(story); session.Restore(save);
    if (JsonSerializer.Serialize(session.Snapshot(), Story.Json) != JsonSerializer.Serialize(save, Story.Json))
        throw new Exception("Frozen reader changed selected state: " + choice);
    Console.WriteLine("PASS actual frozen reader preserves authored choice " + choice);
}
