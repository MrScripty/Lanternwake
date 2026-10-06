using Godot;

namespace Lanternwake.Presentation;

/// <summary>Editable character prefab, with an optional additive breathing component.</summary>
[Tool, GlobalClass]
public partial class StageCharacter : Node3D
{
    [Export] public StageMotion? Breathing { get; set; }
}
