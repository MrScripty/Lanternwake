using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lanternwake.Core;

/// <summary>A narrow authoring operation; stable IDs, gates and unknown fields are preserved.</summary>
public static class StoryTextEdit
{
    public static string Apply(string loadedJson, string currentJson, string beatId, string speaker, string text)
    {
        if (!string.Equals(loadedJson, currentJson, StringComparison.Ordinal))
            throw new InvalidOperationException("The story changed on disk. Reload before editing; your file has not been overwritten.");
        var root = JsonNode.Parse(currentJson) ?? throw new InvalidDataException("Empty story.");
        var beats = root["chapters"]!.AsArray().SelectMany(c => c!["scenes"]!.AsArray())
            .SelectMany(s => s!["beats"]!.AsArray());
        var beat = beats.SingleOrDefault(b => b!["id"]!.GetValue<string>() == beatId)
            ?? throw new InvalidDataException("The selected beat no longer exists.");
        beat["speaker"] = speaker;
        beat["text"] = text;
        var result = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
        Story.Parse(result);
        return result;
    }
}
