using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lanternwake.Core;

/// <summary>Writer-only dossier fields are never runtime context. Only dialogueStyle is sent.</summary>
public static class CharacterProfileEdit
{
    public static readonly string[] Fields = ["history", "personality", "motivations", "speakingStyle", "knowledgeNotes", "sources", "dialogueStyle"];
    public const int AuthorFieldLimit = 8000;
    public const int DialogueStyleLimit = 600;

    public static string Read(Character character, string field) => field switch
    {
        "history" => character.AuthoringProfile?.History ?? "",
        "personality" => character.AuthoringProfile?.Personality ?? "",
        "motivations" => character.AuthoringProfile?.Motivations ?? "",
        "speakingStyle" => character.AuthoringProfile?.SpeakingStyle ?? "",
        "knowledgeNotes" => character.AuthoringProfile?.KnowledgeNotes ?? "",
        "sources" => character.AuthoringProfile?.Sources ?? "",
        "dialogueStyle" => character.DialogueStyle ?? "",
        _ => throw new InvalidDataException("Unknown character profile field.")
    };

    public static void Validate(Character character)
    {
        foreach (var field in Fields)
            if (Read(character, field).Length > (field == "dialogueStyle" ? DialogueStyleLimit : AuthorFieldLimit))
                throw new InvalidDataException($"Character {character.Id}: {field} exceeds its authoring limit.");
    }

    public static string Apply(string loadedJson, string currentJson, string characterId, string field, string text)
    {
        if (!string.Equals(loadedJson, currentJson, StringComparison.Ordinal))
            throw new InvalidOperationException("The story changed on disk. Reload before editing; your file has not been overwritten.");
        if (!Fields.Contains(field)) throw new InvalidDataException("Unknown character profile field.");
        var root = JsonNode.Parse(currentJson) ?? throw new InvalidDataException("Empty story.");
        var character = root["characters"]!.AsArray().SingleOrDefault(c => c!["id"]!.GetValue<string>() == characterId)
            ?? throw new InvalidDataException("The selected character no longer exists.");
        if (field == "dialogueStyle") character[field] = text;
        else
        {
            character["authoringProfile"] ??= new JsonObject();
            character["authoringProfile"]![field] = text;
        }
        var result = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
        Story.Parse(result);
        return result;
    }
}
