using Godot;

namespace Lanternwake.Presentation;

/// <summary>A scene's opening mix or a persistent change starting at an authored beat.</summary>
[GlobalClass, Tool]
public partial class MusicSpot : Resource
{
    public enum FocusKind { Inherit, None, Ada, Nessa, Tomas, Sera, Ivo }
    [Export] public string SceneId { get; set; } = "";
    [Export] public string StartAtBeatId { get; set; } = "";
    [Export] public MusicMix Mix { get; set; } = null!;
    [Export] public FocusKind CharacterFocus { get; set; } = FocusKind.Inherit;
    [Export(PropertyHint.MultilineText)] public string Reason { get; set; } = "";
}
