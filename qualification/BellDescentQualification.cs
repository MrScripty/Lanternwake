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
    private void Press(string name) => _game.InterfaceRoot.GetNode<Button>("%" + name).EmitSignal(BaseButton.SignalName.Pressed);
    private void Action(string caption) => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == caption).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private string Snapshot() => JsonSerializer.Serialize(Session.Snapshot(), Story.Json);
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
                Press("SaveButton"); Load("ch5_s3_b004");
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
                Press("SaveButton"); Load("ch5_s3_b007"); Check(Stage.BellBody.Position.IsEqualApprox(origin + offset), "seated save reload retains endpoint");
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
