#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Canonical cup events through production advance, recovery, location changes and replay.</summary>
public partial class CupStateQualification : Node
{
    private GameView _game = null!;
    private Beat[] _ordered = [];
    private string _root = "";
    private int _checks, _beats, _houses, _resumes;
    private readonly Dictionary<string, string> _events = new()
    {
        ["ch2_s5_b012"] = "broken", ["ch2_s5_b026"] = "boxed", ["ch3_s5_b001"] = "mug"
    };
    private T Observe<T>(string name) => (T)typeof(GameView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_game)!;
    private StorySession Session => Observe<StorySession>("_session");
    private Window? Modal => Observe<Window?>("_modal");
    private StageScene Stage => Observe<StageDirector>("_stage").GetChildren().OfType<StageScene>().Single();
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Cup states: " + claim);
        _checks++;
    }
    private void Press(string name) => _game.InterfaceRoot.GetNode<Button>("%" + name).EmitSignal(BaseButton.SignalName.Pressed);
    private void Action(string text) => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == text).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private static string Snapshot(SaveData data) => JsonSerializer.Serialize(data, Story.Json);
    private Dictionary<string, byte[]> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json").ToDictionary(p => Path.GetFileName(p), File.ReadAllBytes);
    private string Expected()
    {
        var index = Array.FindIndex(_ordered, b => b.Id == Session.Beat.Id);
        // Oracle is authored chronology, independent of runtime cues/exports.
        if (index >= Array.FindIndex(_ordered, b => b.Id == "ch3_s5_b001")) return "mug";
        if (index >= Array.FindIndex(_ordered, b => b.Id == "ch2_s5_b026")) return "boxed";
        return index >= Array.FindIndex(_ordered, b => b.Id == "ch2_s5_b012") ? "broken" : "intact";
    }
    private void CheckStage()
    {
        if (Session.Scene.Location != "keeper_house") return;
        var expected = Expected(); _houses++;
        var original = Stage.FindChild("Cylinder009", true, false) as MeshInstance3D;
        Check(original is not null && original.IsVisibleInTree() == (expected == "intact"), "original blue cup follows authored boundary at " + Session.Beat.Id);
        foreach (var name in new[] { "CupIntact", "CupFragments", "CupFloorHandle", "CupBoxed", "SteelMug" })
        {
            var node = Stage.GetNodeOrNull<Node3D>("Scenery/" + name);
            var visible = name switch
            {
                "CupIntact" => expected == "intact", "CupFragments" => expected == "broken",
                "CupFloorHandle" => expected is "broken" or "boxed", "CupBoxed" => expected is "boxed" or "mug",
                _ => expected == "mug"
            };
            Check(node is not null && node.IsVisibleInTree() == visible, name + " visibility at " + Session.Beat.Id);
        }
        Check(Stage.FindChildren("*", "CollisionObject3D", true, false).Count == 0, "cup state has no player-triggered physics or interaction");
        Check(!Observe<StageDirector>("_stage").MotionEnabled, "canonical change is independent of environmental motion");
        var camera = Session.Beat.Id == "ch2_s5_b012" ? Stage.CupFloorCamera : Stage.StoryCamera;
        Check(camera is not null && Stage.GetViewport().GetCamera3D() == camera,
            "current-beat camera follows advance/recovery/replay rather than cumulative break history");
        if (expected == "broken")
        {
            // The original floorboards top at 0.095; old fallen meshes were below it.
            var board = Stage.GetNode<MeshInstance3D>("Scenery/Panel058");
            var floorTop = (board.GlobalTransform * board.GetAabb()).End.Y;
            var fallen = Stage.CupFragments!.GetChildren().OfType<MeshInstance3D>()
                .Concat(Stage.CupFloorHandle!.GetChildren().OfType<MeshInstance3D>());
            foreach (var mesh in fallen)
                Check((mesh.GlobalTransform * mesh.GetAabb()).Position.Y >= floorTop - 0.0001f,
                    mesh.Name + " rests above the existing floorboard surface");
        }
    }
    private void Capture(string state)
    {
        var user = ProjectSettings.GlobalizePath("user://");
        var output = Path.Combine(_root, "saved", state); Directory.CreateDirectory(output);
        foreach (var file in Directory.GetFiles(user, "*.json")) File.Copy(file, Path.Combine(output, Path.GetFileName(file)), true);
        File.WriteAllText(Path.Combine(_root, "userdata-relative.txt"), Path.GetRelativePath(_root, user));
    }
    private async Task Load(bool previous, SaveData expected)
    {
        var files = Files(); Press("LoadButton");
        Action((previous ? "Recover previous manual save · " : "Load current manual save · ") + expected.BeatId);
        await Frame();
        Check(Snapshot(Session.Snapshot()) == Snapshot(expected), "actual Load restores exact beat/transcript/gates");
        var after = Files();
        Check(files.Count == after.Count && files.All(p => after[p.Key].SequenceEqual(p.Value)), "load/visual derivation preserves every save byte");
        CheckStage(); _resumes++;
    }
    private async Task Complete()
    {
        Press("AdvanceButton");
        while (true)
        {
            CheckStage();
            if (Session.Beat.Id == "ch2_s5_b011") { Press("SaveButton"); Capture("prebreak"); }
            if (_events.TryGetValue(Session.Beat.Id, out var state))
            {
                var current = Session.Snapshot(); Press("SaveButton"); Capture(state);
                var earlier = Observe<SessionStorage>("_storage").Inspect(false, true).Snapshot!;
                await Load(true, earlier); await Load(false, current);
                var before = Snapshot(Session.Snapshot()); var stage = Stage;
                Press("ContentNoteButton"); Press("SettingsButton"); Action("Content note");
                Modal!.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
                Modal!.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
                Check(Stage == stage && Snapshot(Session.Snapshot()) == before, "reading note does not cause another stage event"); CheckStage();
            }
            if (Session.Beat.Activity is { } activity && !Session.CanAdvance) { Press("AdvanceButton"); Action(activity.Options[activity.CorrectIndex]); }
            if (Session.IsEnding) break;
            var previous = Session.Beat.Id; Press("AdvanceButton");
            Check(Session.Beat.Id != previous, "production Advance progresses required path"); _beats++; await Frame();
        }
        Press("AdvanceButton"); Action("Start a new watch"); Action("Start new watch");
        Check(Session.Beat.Id == _ordered[0].Id && Session.ActiveStageCues.Length == 0, "new watch resets cumulative stage state");
        while (Session.Scene.Location != "keeper_house")
        {
            if (Session.Beat.Activity is { } activity && !Session.CanAdvance) { Press("AdvanceButton"); Action(activity.Options[activity.CorrectIndex]); }
            var previous = Session.Beat.Id; Press("AdvanceButton");
            Check(Session.Beat.Id != previous, "new-watch Advance obeys existing evidence gates"); _beats++; await Frame();
        }
        CheckStage();
        Check(Expected() == "intact", "new watch returns to original intact cup");
    }
    public override async void _Ready()
    {
        try
        {
            _root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_CUP_FIXTURE") ?? "";
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(_root) && File.Exists(Path.Combine(_root, "owned-fixture")), "owned debug fixture required");
            Check(ProjectSettings.GlobalizePath("user://").StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "private userdata required");
            var story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            _ordered = story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).ToArray();
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frame();
            var mode = System.Environment.GetEnvironmentVariable("LANTERNWAKE_CUP_MODE") ?? "complete";
            _game.Audio.SetMuted(true);
            if (mode == "baseline")
            {
                Check(Session.Beat.Id == "ch2_s5_b012", "baseline exact authored break");
                var original = (MeshInstance3D)Stage.FindChild("Cylinder009", true, false);
                Check(!original.IsVisibleInTree(), "original blue cup must retire at authored break; baseline still shows it intact");
            }
            else
            {
                Press("SettingsButton"); Action("Toggle instant text"); Press("SettingsButton"); Action("Toggle reduced motion");
                if (mode == "complete") await Complete();
                else if (mode == "resume")
                {
                    var expected = Observe<SessionStorage>("_storage").Inspect(false, false).Snapshot!;
                    await Load(false, expected); CheckStage();
                }
                else CheckStage();
            }
            GD.Print("LANTERNWAKE_CUP_STATES_OK " + JsonSerializer.Serialize(new { mode, checks = _checks, nativeAdvances = _beats, keeperBeats = _houses, resumes = _resumes, beat = Session.Beat.Id }));
            Press("SettingsButton"); Action("Quit game");
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString()); if (_game is not null) await _game.Audio.StopAndRetireAsync(); GetTree().Quit(1);
        }
    }
}
#endif
