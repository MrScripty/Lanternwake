using Godot;

namespace Lanternwake.Presentation;

/// <summary>A reusable arrangement; all four stems share one continuous musical clock.</summary>
[GlobalClass, Tool]
public partial class MusicMix : Resource
{
    public enum SuiteKind { Saltmere, Undertow, NightLedger, OpenHorizon }
    [Export] public string Title { get; set; } = "";
    [Export] public SuiteKind Suite { get; set; }
    [Export(PropertyHint.Range, "0,1,0.01")] public float Ground { get; set; } = .6f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Theme { get; set; } = .5f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Bowed { get; set; } = .2f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Motion { get; set; }
    [ExportGroup("Story layers")]
    [Export(PropertyHint.Range, "0,1,0.01")] public float Environment { get; set; } = .85f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Character { get; set; } = .60f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Journey { get; set; } = .36f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ThemeUnderCharacter { get; set; } = .45f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float MoodUnderEnvironment { get; set; } = .3f;
    [Export] public bool SilenceStoryLayers { get; set; }

    public float[] Levels()
    {
        if ((int)Suite is < 0 or > 3) throw new InvalidOperationException("Music mix has an unknown suite: " + Title);
        var levels = new[] { Ground, Theme, Bowed, Motion };
        if (levels.Any(value => !float.IsFinite(value) || value is < 0 or > 1))
            throw new InvalidOperationException("Music mix stem levels must be finite and between zero and one: " + Title);
        if (new[] { Environment, Character, Journey, ThemeUnderCharacter, MoodUnderEnvironment }.Any(value => !float.IsFinite(value) || value is < 0 or > 1))
            throw new InvalidOperationException("Music story-layer levels must be finite and between zero and one: " + Title);
        return levels;
    }
}
