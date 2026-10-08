#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

public partial class BellDescentQualification : Node
{
    private GameView _game = null!;
    private int _checks;
    private T Observe<T>(string name) => (T)typeof(GameView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_game)!;
    private StorySession Session => Observe<StorySession>("_session");
    private StageScene Stage => Observe<StageDirector>("_stage").GetChildren().OfType<StageScene>().Single();
    private Window? Modal => Observe<Window?>("_modal");
    private void Check(bool value, string claim) { if (!value) throw new Exception(claim); _checks++; }
    private void Press(string name) => NativeGameControls.Press(_game, name);
    private void Action(string caption) => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == caption).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private string Snapshot() => BellRuntimeState.Read(Session);
    private void CheckMarks()
    {
        var rim = Stage.BellBody!.GetNode<MeshInstance3D>("Cylinder002");
        var halfRim = rim.Mesh.GetAabb().Size.Y * .5f;
        var marks = new[] { "MarkHigh", "MarkMiddle", "MarkLow" }
            .Select(name => Stage.GetNode<MeshInstance3D>("Scenery/CounterweightInstallation/GuideLeft/" + name)).ToArray();
        var heights = marks.Select(mark => mark.Position.Y + mark.GetParent<Node3D>().Position.Y).ToArray();
        Check(heights.SequenceEqual(new[] { 1.95f, 1.55f, .8f }, new ApproximateFloatComparer()), "three painted marks retain descending order inside the authored travel");
        foreach (var mark in marks)
        {
            var centre = mark.Position.Y + mark.GetParent<Node3D>().Position.Y;
            var halfMark = mark.Mesh.GetAabb().Size.Y * .5f;
            Check(centre + halfMark < rim.Position.Y - halfRim &&
                  centre - halfMark > rim.Position.Y + Stage.BellLoweredOffset.Y + halfRim,
                  "entire painted mark is passed by the dark rim between frozen endpoints: " + mark.Name);
        }
        Check(heights.Take(2).All(y => y - .0275f > rim.Position.Y - 1.05f + halfRim) &&
              heights[2] + .0275f < rim.Position.Y - 1.05f - halfRim &&
              heights[2] - .0275f > rim.Position.Y - 1.89f + halfRim,
              "first phase passes high/middle; flow phase passes low before authored arrival");
    }
    private sealed class ApproximateFloatComparer : IEqualityComparer<float>
    {
        public bool Equals(float a, float b) => Mathf.IsEqualApprox(a, b);
        public int GetHashCode(float value) => 0;
    }
    private void CheckSuspension()
    {
        var cable = Stage.BellSuspension!;
        var height = cable.Mesh.GetAabb().Size.Y;
        Check(Mathf.IsEqualApprox(height, 2.9f) && cable.Scale.X == 1 && cable.Scale.Z == 1 &&
              Mathf.IsEqualApprox(cable.Position.Y + height * cable.Scale.Y * .5f, 6.6f), "frozen suspension anchor/radius and mesh resource remain unchanged");
        Check(Math.Abs(cable.Position.Y - height * cable.Scale.Y * .5f - (Stage.BellBody!.Position.Y + 3.74f)) < .05f,
              "suspension remains connected to the bell crown through payout");
    }
    private Dictionary<string, byte[]> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json").ToDictionary(p => Path.GetFileName(p), File.ReadAllBytes);
    private void SameFiles(Dictionary<string, byte[]> old)
    {
        var current = Files(); Check(old.Count == current.Count && old.All(p => current.TryGetValue(p.Key, out var bytes) && bytes.SequenceEqual(p.Value)), "presentation writes no slot bytes");
    }
    private void Load(string beat)
    {
        Press("LoadButton"); Action("Load current manual save · " + beat);
        Check(Session.Beat.Id == beat, "actual Load restores selected authored position");
    }
    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_BELL_FIXTURE") ?? "";
            var mode = System.Environment.GetEnvironmentVariable("LANTERNWAKE_BELL_MODE") ?? "";
            Check(Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")) &&
                  ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "owned debug fixture required");
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frames(1);
            Check(Observe<SessionStorage>("_storage").Mode == (mode == "preview" ? SessionMode.AuthorPreview : SessionMode.Normal), "expected actual launch mode");
            _game.Audio.SetMuted(true);
            var originalFiles = Files();
            if (mode != "preview") Load(mode == "resume" ? "ch5_s3_b004" : "ch5_s3_b003");
            var origin = Vector3.Zero; // authored BellBody local origin in the frozen scene
            var offset = new Vector3(0, -2.1f, 0);
            Check(Stage.BellBody != null && Stage.BellLoweredOffset == offset, "existing authored endpoint unchanged");
            CheckMarks();
            CheckSuspension();
            if (mode is "preview" or "resume")
            {
                Check(Stage.BellBody!.Position.IsEqualApprox(origin + offset * .5f), "preview/load settles descent presentation without replay");
                var state = Snapshot(); var files = Files(); await Frames(20);
                Check(Stage.BellBody.Position.IsEqualApprox(origin + offset * .5f) && Snapshot() == state, "loaded stage is stable and progress exact"); SameFiles(files);
            }
            else
            {
                Check(Stage.BellBody!.Position == origin, "before-release save keeps bell suspended");
                Press("SettingsButton"); Action("Toggle instant text");
                Press("AdvanceButton"); Check(Session.Beat.Id == "ch5_s3_b004", "same canonical start beat");
                var state = Snapshot(); var files = Files(); var rotation = Stage.BellBody.Rotation;
                await Frames(8);
                Check(Stage.BellBody.Position.Y < 0 && Stage.BellBody.Position.Y > -1.05f && Stage.BellBody.Position.X == 0 && Stage.BellBody.Position.Z == 0 && Stage.BellBody.Rotation == rotation, "observable guided vertical travel without lateral fall or rotation");
                CheckSuspension();
                Check(Snapshot() == state && Session.CanAdvance, "cosmetic travel never changes or gates progress"); SameFiles(files);
                Check(Stage.BellReleaseCamera!.Current, "fixed authored release viewpoint selected");
                Press("SaveButton");
                Press("AdvanceButton");
                Check(Snapshot() != state, "unchanged saved file cannot satisfy live-state equality after advancing away");
                Load("ch5_s3_b004");
                Check(Snapshot() == state, "Load restores full live transcript, facts, inventory, solved activities and derived snapshot fields");
                Check(Stage.BellBody.Position.IsEqualApprox(origin + offset * .5f), "save during motion restores a settled authored phase");
                CheckSuspension();
                await Frames(12); Check(Stage.BellBody.Position.IsEqualApprox(origin + offset * .5f), "retired pre-load tween cannot move loaded bell");
            }
            _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
            Press("AdvanceButton"); Check(Session.Beat.Id == "ch5_s3_b005", "next existing flow beat is immediately reachable");
            Press("SettingsButton"); Action("Toggle reduced motion");
            Check(Stage.BellBody!.Position.IsEqualApprox(origin + offset * .9f), "disabling motion settles current phase immediately");
            CheckSuspension();
            var unchanged = Snapshot(); var saved = Files(); await Frames(15);
            Check(Stage.BellBody.Position.IsEqualApprox(origin + offset * .9f) && Snapshot() == unchanged, "reduced-motion phase stays still without changing story"); SameFiles(saved);
            _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
            Press("AdvanceButton"); Check(Session.Beat.Id == "ch5_s3_b006" && Session.ActiveStageCues.Contains("bell_lowered"), "existing arrival cue remains at same authored beat");
            Check(Stage.BellBody.Position.IsEqualApprox(origin + offset), "arrival is seated immediately, never delayed after narration");
            CheckSuspension();
            _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
            Press("AdvanceButton"); Check(Session.Beat.Id == "ch5_s3_b007" && Stage.StoryCamera.Current && Stage.BellBody.Position.IsEqualApprox(origin + offset), "next beat restores wide camera while bell stays seated");
            if (mode != "preview")
            {
                var seatedState = Snapshot();
                Press("SaveButton");
                _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
                Press("AdvanceButton");
                Check(Snapshot() != seatedState, "seated live-state oracle rejects an unchanged save file after advance");
                Load("ch5_s3_b007");
                Check(Snapshot() == seatedState, "seated Load restores full live session state");
                Check(Stage.BellBody.Position.IsEqualApprox(origin + offset), "seated save reload retains endpoint");
                Press("LoadButton"); Action("Recover previous manual save · ch5_s3_b004");
                Check(Session.Beat.Id == "ch5_s3_b004" && Stage.BellBody.Position.IsEqualApprox(origin + offset * .5f), "earlier recovery rolls visual phase back without tween replay");
                CheckSuspension();
            }
            else SameFiles(originalFiles);
            GD.Print("LANTERNWAKE_BELL_DESCENT_OK " + JsonSerializer.Serialize(new { mode, checks = _checks }));
            Press("SettingsButton"); Action("Quit game");
        }
        catch (Exception error) { GD.PushError(error.ToString()); if (_game != null) await _game.Audio.StopAndRetireAsync(); GetTree().Quit(1); }
    }
}
#endif
