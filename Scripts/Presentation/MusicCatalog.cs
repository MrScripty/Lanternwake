using Godot;
using System.Text.Json;

namespace Lanternwake.Presentation;

/// <summary>The ordering contract between authored MIDI voices and their mix gains.</summary>
public sealed class MusicCatalog
{
    public sealed record Stem(string Name, int[] Channels);
    public sealed record Layer(string Id, string Role, string Title, string Midi, Stem[] Stems);
    private sealed record Document(int Format, Layer[] Layers);
    public Layer[] Layers { get; }
    public Stem[] Stems { get; }
    private readonly Dictionary<(string Role, string Id), int[]> _slots = new();

    private MusicCatalog(Document document)
    {
        Layers = document.Layers;
        Stems = Layers.SelectMany(layer => layer.Stems).ToArray();
        if (document.Format != 2 || Stems.Length < 1 || Stems.Select(stem => stem.Name).Distinct().Count() != Stems.Length)
            throw new InvalidDataException("Music catalog must identify unique score voices.");
        int offset = 0;
        foreach (var layer in Layers)
        {
            if (layer.Midi != System.IO.Path.GetFileName(layer.Midi) || !layer.Midi.EndsWith(".mid", StringComparison.Ordinal) ||
                layer.Role is not ("mood" or "environment" or "character" or "journey") || layer.Stems.Length == 0 ||
                !_slots.TryAdd((layer.Role, layer.Id), Enumerable.Range(offset, layer.Stems.Length).ToArray()))
                throw new InvalidDataException("Duplicate or invalid music layer: " + layer.Role + "/" + layer.Id);
            offset += layer.Stems.Length;
        }
    }
    public static MusicCatalog Load(string path) => new(JsonSerializer.Deserialize<Document>(Godot.FileAccess.GetFileAsString(path),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Empty music catalog."));
    public int[] Slots(string role, string id) => _slots.TryGetValue((role, id), out var slots)
        ? slots : throw new InvalidDataException("Missing music layer: " + role + "/" + id);
    public string[] Ids(string role) => Layers.Where(layer => layer.Role == role).Select(layer => layer.Id).ToArray();
}
