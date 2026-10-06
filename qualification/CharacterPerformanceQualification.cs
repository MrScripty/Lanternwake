#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

public partial class CharacterPerformanceQualification : Node
{
    private GameView _game = null!;
    private int _checks;
    private T Observe<T>(string field) => (T)typeof(GameView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_game)!;
    private StorySession Session => Observe<StorySession>("_session");
    private StageDirector Director => Observe<StageDirector>("_stage");
    private StageScene Stage => Director.GetChildren().OfType<StageScene>().Single();
    private StageCharacter[] Actors => Stage.CastOrigin.GetChildren().OfType<StageCharacter>().ToArray();
    private void Check(bool value, string claim) { if (!value) throw new Exception(claim); _checks++; }
    private void Press(string name) => _game.InterfaceRoot.GetNode<Button>("%" + name).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private Dictionary<string, byte[]> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json").ToDictionary(p => Path.GetFileName(p), File.ReadAllBytes);
    private void SameFiles(Dictionary<string, byte[]> files) { var now = Files(); Check(now.Count == files.Count && files.All(p => now.TryGetValue(p.Key, out var b) && b.SequenceEqual(p.Value)), "poses write no player slots"); }
    private string PoseState() => JsonSerializer.Serialize(Actors.Select(a => new { a.CharacterId, a.ActivePose,
        torso = a.Torso!.Transform, head = a.Head!.Transform, left = a.LeftShoulder!.Transform,
        leftElbow = a.LeftElbow!.Transform, right = a.RightShoulder!.Transform, rightElbow = a.RightElbow!.Transform }), new JsonSerializerOptions { IncludeFields = true });
    private void Present(SaveData snapshot)
    {
        Session.Restore(snapshot);
        typeof(GameView).GetField("_started", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_game, true);
        typeof(GameView).GetMethod("RenderBeat", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_game, [false]);
        _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
    }
    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_PERFORMANCE_FIXTURE") ?? "";
            Check(Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")) && ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "owned debug fixture required");
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frames(1);
            Check(Observe<SessionStorage>("_storage").Mode == SessionMode.Normal, "real Main uses normal owned storage");
            _game.Audio.SetMuted(true); Director.MotionEnabled = false;
            var story = Observe<Story>("_story"); var traversal = new StorySession(story); var snapshots = new Dictionary<string, SaveData>();
            do { snapshots[traversal.Beat.Id] = traversal.Snapshot(); if (traversal.Beat.Activity is { } activity) traversal.AnswerActivity(activity.CorrectIndex); } while (traversal.Advance());
            Check(snapshots.Count == 1439, "fixture traversal uses every unchanged canonical beat; no human-duration claim");
            var fixtures = Path.Combine(root, "fixtures"); Directory.CreateDirectory(fixtures);
            foreach (var beat in new[] { "ch1_s2_b002", "ch1_s2_b008", "ch3_s1_b014", "ch3_s2_b001", "ch4_s1_b006", "ch5_s3_b003", "ch5_s3_b004", "ch5_s5_b002" })
                File.WriteAllText(Path.Combine(fixtures, beat + ".json"), JsonSerializer.Serialize(snapshots[beat], Story.Json));
            Present(snapshots["ch5_s5_b003"]);
            foreach (var actor in Actors)
            {
                var rootPosition = actor.Position; var feet = actor.GetChildren().OfType<MeshInstance3D>().Select(m => m.GlobalTransform).ToArray();
                var meshes = actor.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
                var resources = meshes.Select(m => m.Mesh).ToArray(); var hands = new List<Vector3>();
                foreach (var family in Enum.GetValues<StageCharacter.PoseFamily>())
                {
                    actor.ApplyPose(family); hands.Add(actor.RightElbow!.GlobalPosition + actor.RightElbow.GlobalBasis * new Vector3(0, -.45f, .04f));
                    var first = PoseState(); for (var i = 0; i < 10; i++) actor.ApplyPose(family);
                    Check(PoseState() == first, actor.CharacterId + " pose application is idempotent: " + family);
                    Check(actor.Position == rootPosition && actor.GetChildren().OfType<MeshInstance3D>().Select(m => m.GlobalTransform).SequenceEqual(feet), actor.CharacterId + " pose keeps feet and safe stage placement fixed");
                    Check(meshes.Select(m => m.Mesh).SequenceEqual(resources), actor.CharacterId + " authored mesh resources stay unchanged");
                }
                Check(hands[0].DistanceTo(hands[1]) > .1f && hands[1].DistanceTo(hands[2]) > .1f && hands[0].DistanceTo(hands[2]) > .05f, actor.CharacterId + " three families have distinct readable hand placements");
            }
            foreach (var (beat, worker) in Director.Performance!.WorkingBeatActors)
            {
                Present(snapshots[beat]);
                Check(Actors.Single(a => a.CharacterId == worker).ActivePose == StageCharacter.PoseFamily.Working, "explicit authored worker: " + beat);
                Check(Actors.Length == Session.Scene.CharacterIds.Count(id => id is not ("ivo" or "operator" or "clerk")), "only authored living cast appears");
            }
            Present(snapshots["ch1_s2_b002"]);
            Check(Actors.Single(a => a.CharacterId == "nessa").ActivePose == StageCharacter.PoseFamily.Working && Actors.Single(a => a.CharacterId == "ada").ActivePose == StageCharacter.PoseFamily.Listening, "live speaker works while audience listens");
            var turn = Actors.Single(a => a.CharacterId == "ada").Head!.RotationDegrees.Y;
            Check(Math.Abs(turn) > 5, "listener turns toward living speaker rather than fixed camera");
            Present(snapshots["ch1_s2_b003"]);
            Check(Actors.Single(a => a.CharacterId == "ada").ActivePose == StageCharacter.PoseFamily.Working && Actors.Single(a => a.CharacterId == "nessa").ActivePose == StageCharacter.PoseFamily.Listening, "speaker change transfers performance roles");
            foreach (var beat in new[] { "ch4_s1_b006", "ch5_s3_b004", "ch5_s5_b003" })
            {
                Present(snapshots[beat]);
                Check(Actors.All(a => a.ActivePose == StageCharacter.PoseFamily.Resting && a.Breathing!.Enabled == false), "testimony/pressure/quiet aftermath is still and restrained: " + beat);
                var state = BellRuntimeState.Read(Session); var pose = PoseState(); var files = Files(); await Frames(20);
                Check(BellRuntimeState.Read(Session) == state && PoseState() == pose && Session.CanAdvance, "reading/resting cannot auto-advance or gate progress"); SameFiles(files);
            }
            Present(snapshots["ch3_s1_b014"]);
            var savedState = BellRuntimeState.Read(Session); var savedPose = PoseState(); Press("SaveButton");
            Press("AdvanceButton"); Check(BellRuntimeState.Read(Session) != savedState, "actual Continue leaves saved performance beat");
            var slots = Files(); Press("LoadButton");
            Observe<Window>("_modal").GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == "Load current manual save · ch3_s1_b014").EmitSignal(BaseButton.SignalName.Pressed);
            Check(BellRuntimeState.Read(Session) == savedState && PoseState() == savedPose, "actual Load restores full live state and authored pose immediately"); SameFiles(slots);
            await Frames(20); Check(PoseState() == savedPose, "reduced motion pose remains stable without replay");
            Present(snapshots["ch2_s1_b002"]); Check(Actors.All(a => a.ActivePose is null), "undirected scenes restore authored neutral sculptures");
            GD.Print("LANTERNWAKE_CHARACTER_PERFORMANCE_OK " + JsonSerializer.Serialize(new { checks = _checks }));
            Press("SettingsButton"); Observe<Window>("_modal").GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == "Quit game").EmitSignal(BaseButton.SignalName.Pressed);
        }
        catch (Exception error) { GD.PushError(error.ToString()); if (_game != null) await _game.Audio.StopAndRetireAsync(); GetTree().Quit(1); }
    }
}
#endif
