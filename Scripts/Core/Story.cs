using System.Text.Json;

namespace Lanternwake.Core;

public sealed record Character(string Id, string Name, string Role, string Voice, string Color, string[] Knowledge);
public sealed record Fact(string Id, string Text);
public sealed record Item(string Id, string Name, string Description);
public sealed record Conversation(string CharacterId, string Prompt, string[] Suggestions, string Fallback, string[] AllowedFacts);
public sealed record EvidenceActivity(string Prompt, string[] Options, int CorrectIndex, string Explanation);
public sealed record Beat(string Id, string Speaker, string Text, string[]? UnlockFacts = null, string[]? UnlockItems = null, Conversation? Conversation = null, EvidenceActivity? Activity = null, string? StageCue = null);
public sealed record Scene(string Id, string Title, string Location, string TimeOfDay, string[] CharacterIds, Beat[] Beats);
public sealed record Chapter(string Id, string Title, Scene[] Scenes);
public sealed record Story(int SchemaVersion, string Title, Character[] Characters, Fact[] Facts, Chapter[] Chapters, Item[] Items)
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static Story Parse(string json)
    {
        var story = JsonSerializer.Deserialize<Story>(json, Json) ?? throw new InvalidDataException("Empty story.");
        story.Validate();
        return story;
    }
    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException("Unsupported story version.");
        if (Chapters is null || Chapters.Length != 5) throw new InvalidDataException("The story requires five chapters.");
        static HashSet<string> Unique(IEnumerable<string> ids, string label)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids) if (string.IsNullOrWhiteSpace(id) || !set.Add(id)) throw new InvalidDataException($"Invalid or duplicate {label}: {id}");
            return set;
        }
        var characters = Unique(Characters.Select(c => c.Id), "character");
        if (!characters.Contains("ada")) throw new InvalidDataException("Lanternwake requires Ada (ada) as its player character.");
        var facts = Unique(Facts.Select(f => f.Id), "fact");
        var items = Unique(Items.Select(i => i.Id), "item");
        Unique(Chapters.Select(c => c.Id), "chapter");
        Unique(Chapters.SelectMany(c => c.Scenes).Select(s => s.Id), "scene");
        Unique(Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).Select(b => b.Id), "beat");
        foreach (var character in Characters)
            if ((character.Knowledge ?? []).Any(f => !facts.Contains(f))) throw new InvalidDataException("Unknown character fact.");
        if (Chapters.Any(c => c.Scenes is null || c.Scenes.Length == 0)) throw new InvalidDataException("Every chapter requires scenes.");
        var unlocked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scene in Chapters.SelectMany(c => c.Scenes))
        {
            if (scene.Location is not ("harbor" or "keeper_house" or "archive" or "lantern_room" or "tide_cave")) throw new InvalidDataException($"Unknown location {scene.Location}.");
            if (scene.Beats.Length == 0 || scene.CharacterIds.Any(c => !characters.Contains(c))) throw new InvalidDataException($"Invalid scene {scene.Id}.");
            foreach (var beat in scene.Beats)
            {
                if (string.IsNullOrWhiteSpace(beat.Text) || (beat.Speaker != "narrator" && !characters.Contains(beat.Speaker))) throw new InvalidDataException($"Invalid beat {beat.Id}.");
                if ((beat.UnlockFacts ?? []).Any(f => !facts.Contains(f)) || (beat.UnlockItems ?? []).Any(i => !items.Contains(i))) throw new InvalidDataException($"Unknown unlock at {beat.Id}.");
                unlocked.UnionWith(beat.UnlockFacts ?? []);
                if (beat.StageCue is not (null or "bell_lowered")) throw new InvalidDataException($"Unknown stage cue at {beat.Id}.");
                if (beat.Activity is { } activity && (activity.Options.Length < 2 || activity.CorrectIndex < 0 || activity.CorrectIndex >= activity.Options.Length || string.IsNullOrWhiteSpace(activity.Explanation))) throw new InvalidDataException($"Invalid activity at {beat.Id}.");
                if (beat.Conversation is { } chat && (!characters.Contains(chat.CharacterId) || chat.AllowedFacts.Any(f => !facts.Contains(f)) || string.IsNullOrWhiteSpace(chat.Fallback))) throw new InvalidDataException($"Invalid conversation at {beat.Id}.");
                if (beat.Conversation is { } bounded && bounded.AllowedFacts.Any(f => !unlocked.Contains(f) || !Characters.Single(c => c.Id == bounded.CharacterId).Knowledge.Contains(f)))
                    throw new InvalidDataException($"Conversation exceeds unlocked character knowledge at {beat.Id}.");
            }
        }
    }
}
