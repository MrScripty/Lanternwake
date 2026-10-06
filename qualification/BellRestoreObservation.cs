#if DEBUG
using System.Reflection;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

// Passive debug wrapper for real Main, normal storage and external X11 input.
// It neither restores state nor reads/writes player slots.
public partial class BellRestoreObservation : Node
{
    private GameView _game = null!;
    private string _path = "";
    private string _last = "";
    public override void _Ready()
    {
        var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_BELL_OBSERVATION_FIXTURE") ?? "";
        if (!Path.IsPathFullyQualified(root) || !File.Exists(Path.Combine(root, "owned-fixture")) ||
            !ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Owned normal observation fixture required.");
        _path = Path.Combine(root, "runtime-observation.json");
        _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game);
        var storage = (SessionStorage)typeof(GameView).GetField("_storage", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_game)!;
        if (storage.Mode != SessionMode.Normal) throw new InvalidOperationException("Normal mode required.");
    }
    public override void _Process(double delta)
    {
        var session = (StorySession)typeof(GameView).GetField("_session", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_game)!;
        var state = BellRuntimeState.Read(session);
        if (state == _last) return;
        File.WriteAllText(_path + ".tmp", state);
        File.Move(_path + ".tmp", _path, true);
        _last = state;
    }
}
#endif
