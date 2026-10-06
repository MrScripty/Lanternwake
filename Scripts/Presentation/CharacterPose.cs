using Godot;

namespace Lanternwake.Presentation;

/// <summary>Editor-authored additive joint angles, in degrees; no artwork resource mutation.</summary>
[Tool, GlobalClass]
public partial class CharacterPose : Resource
{
    [Export] public Vector3 TorsoDegrees { get; set; }
    [Export] public Vector3 HeadDegrees { get; set; }
    [Export] public Vector3 LeftShoulderDegrees { get; set; }
    [Export] public Vector3 LeftElbowDegrees { get; set; }
    [Export] public Vector3 RightShoulderDegrees { get; set; }
    [Export] public Vector3 RightElbowDegrees { get; set; }
}
