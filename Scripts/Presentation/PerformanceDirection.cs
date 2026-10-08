using Godot;

namespace Lanternwake.Presentation;

/// <summary>Explicit authored scene/beat direction, separate from canonical story/save state.</summary>
[Tool, GlobalClass]
public partial class PerformanceDirection : Resource
{
    [Export] public string[] SceneIds { get; set; } = [];
    [Export] public string[] RestingSceneIds { get; set; } = [];
    [Export] public string[] StillSceneIds { get; set; } = [];
    [Export] public Godot.Collections.Dictionary<string, string> WorkingBeatActors { get; set; } = new();
}
