using Godot;

namespace Lanternwake.Presentation;

/// <summary>Deterministic spotting, reconstructed on preview/load without replaying events.</summary>
[GlobalClass, Tool]
public partial class MusicScore : Resource
{
    [Export] public MusicMix TitleMix { get; set; } = null!;
    [Export] public Godot.Collections.Array<MusicSpot> Spots { get; set; } = new();

    public void Validate()
    {
        if (TitleMix is null) throw new InvalidOperationException("Assign a title music mix.");
        _ = TitleMix.Levels();
        var keys = new HashSet<(string, string)>();
        foreach (var spot in Spots)
        {
            if (spot is null || string.IsNullOrWhiteSpace(spot.SceneId) || spot.Mix is null)
                throw new InvalidOperationException("Every music spot needs a scene ID and a mix.");
            if (!keys.Add((spot.SceneId, spot.StartAtBeatId)))
                throw new InvalidOperationException("Duplicate music spot: " + spot.SceneId + "/" + spot.StartAtBeatId);
            _ = spot.Mix.Levels();
            if ((int)spot.CharacterFocus is < 0 or > 6)
                throw new InvalidOperationException("Unknown character music focus: " + spot.SceneId);
        }
    }
    public MusicSpot.FocusKind ResolveFocus(string sceneId, IReadOnlyList<string> reachedBeatIds)
    {
        var spots = Spots.Where(spot => spot.SceneId == sceneId).ToArray();
        var focus = spots.FirstOrDefault(spot => spot.StartAtBeatId.Length == 0)?.CharacterFocus ?? MusicSpot.FocusKind.None;
        foreach (var id in reachedBeatIds)
        {
            var change = spots.FirstOrDefault(spot => spot.StartAtBeatId == id);
            if (change is not null && change.CharacterFocus != MusicSpot.FocusKind.Inherit) focus = change.CharacterFocus;
        }
        return focus == MusicSpot.FocusKind.Inherit ? MusicSpot.FocusKind.None : focus;
    }

    public MusicMix Resolve(string sceneId, IReadOnlyList<string> reachedBeatIds)
    {
        var sceneSpots = Spots.Where(spot => spot.SceneId == sceneId).ToArray();
        var mix = sceneSpots.FirstOrDefault(spot => spot.StartAtBeatId.Length == 0)?.Mix ?? TitleMix;
        // Story order is authoritative; IDs are not timestamps or lexical sequence numbers.
        foreach (var id in reachedBeatIds)
        {
            var change = sceneSpots.FirstOrDefault(spot => spot.StartAtBeatId == id);
            if (change is not null) mix = change.Mix;
        }
        return mix;
    }
}
