#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

public partial class MissingMarginQualification : Node
{
    private const string Target = "ch2_s4_b019a";
    private GameView _game = null!;
    private int _checks;
    private T Read<T>(string field) => (T)typeof(GameView).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_game)!;
    private StorySession Session => Read<StorySession>("_session");
    private Window? Modal => Read<Window?>("_modal");
    private RichTextLabel Passage => _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText");
    private Button[] Actions => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().ToArray();
    private void Action(string caption) => Actions.Single(b => b.Text == caption).EmitSignal(BaseButton.SignalName.Pressed);
    private void Press(string name) => NativeGameControls.Press(_game, name);
    private void Check(bool value, string claim) { if (!value) throw new Exception("Missing Margin: " + claim); _checks++; }
    private string State() => JsonSerializer.Serialize(Session.Snapshot(), Story.Json);
    private async Task Frames(int count = 2) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Load(string beat = Target) { Press("LoadButton"); Action("Load current manual save · " + beat); }
    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_MARGIN_FIXTURE") ?? "";
            var directory = ProjectSettings.GlobalizePath("user://");
            Check(Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")) &&
                directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "owned disposable profile required");
            Directory.CreateDirectory(directory);
            var story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            var traversal = new StorySession(story);
            while (traversal.Beat.Id != Target)
            {
                if (traversal.Beat.Activity is { } activity) traversal.AnswerActivity(activity.CorrectIndex);
                if (!traversal.Advance()) throw new Exception("Missing target");
            }
            var initial = traversal.Snapshot(); var manual = Path.Combine(directory, "save.json");
            SaveStore.Write(manual, initial); var initialBytes = File.ReadAllBytes(manual);
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frames();
            Read<Control>("_mainMenu").GetNode<Button>("%MenuNewGame").EmitSignal(BaseButton.SignalName.Pressed);
            Press("SettingsButton"); Action("Toggle instant text");
            for (int index = 0; index < 3; index++)
            {
                File.WriteAllBytes(manual, initialBytes); Load();
                var state = State(); var exchange = Session.Beat.Exchange!;
                var beforeFiles = Directory.GetFiles(directory, "*.json").ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);
                Press("TalkButton"); var retired = Actions[index]; var retiredWindow = Modal;
                Check(Actions.Select(b => b.Text).SequenceEqual(exchange.Options.Select(o => o.Label)) && Actions[0].HasFocus(), "exact authored intentions with keyboard focus");
                Modal!.EmitSignal(Window.SignalName.WindowInput, new InputEventKey { Pressed = true, Keycode = Key.Escape });
                Check(Modal is null && State() == state && Directory.GetFiles(directory, "*.json").Length == beforeFiles.Count &&
                    beforeFiles.All(f => File.ReadAllBytes(Path.Combine(directory, f.Key!)).SequenceEqual(f.Value)), "interrupted choice changes no progress or files");
                Press("TalkButton"); var selector = Modal; var duplicate = Actions[index];
                retired.EmitSignal(BaseButton.SignalName.Pressed); retiredWindow!.EmitSignal(Window.SignalName.CloseRequested);
                Check(Modal == selector && State() == state, "retired interrupted controls cannot select in a new entry");
                Action(exchange.Options[index].Label); var response = Modal; var selected = State();
                Check(Session.ChosenExchangeIndex == index && Session.History.Count == initial.History.Count + 2 &&
                    Session.History.TakeLast(2).All(l => !l.Generated), "one distinct authored pair retained");
                Check(Modal!.GetNode<RichTextLabel>("%ModalText").Text.EndsWith(exchange.Options[index].Reply, StringComparison.Ordinal), "matching canonical local response displayed");
                duplicate.EmitSignal(BaseButton.SignalName.Pressed); selector!.EmitSignal(Window.SignalName.CloseRequested);
                Check(Modal == response && State() == selected, "double activation cannot replace or duplicate chosen response");
                Check(JsonSerializer.Serialize(Read<SessionStorage>("_storage").Read(true), Story.Json) == selected,
                    "explicit choice autosaves exact authored progress");
                Action("Back to story"); Press("TalkButton");
                Check(Actions.Length == 1 && Actions[0].Text == "Back to story" && State() == selected, "repeated entry reads selected response without reselecting");
                Action("Back to story"); Press("SaveButton"); Load(); Press("TalkButton");
                Check(Session.ChosenExchangeIndex == index && State() == selected && Actions.Length == 1, "manual save/reload retains chosen response");
                Action("Back to story"); Press("AdvanceButton");
                Check(Session.Beat.Id == "ch2_s4_b020" && Session.Beat.Conversation is not null, "all intentions return to original optional conversation");
                var beforeChat = State(); Press("TalkButton");
                Check(Read<Control>("_chatPanel").Visible && _game.CurrentStatusText.Contains("Authored fallback"), "unavailable free response retains honest authored capability notice");
                Press("ReturnButton"); Check(State() == beforeChat, "return from unsent free response does not add dialogue or advance");
                if (index == 0)
                {
                    Press("TalkButton"); Read<LineEdit>("_entry").Text = "Let's keep the source and its limits together.";
                    Press("SendButton"); await Frames(4);
                    Check(Session.History[^1].Text == Session.Beat.Conversation!.Fallback && !Session.History[^1].Generated &&
                        _game.CurrentStatusText.StartsWith("Authored reply", StringComparison.Ordinal), "disabled inference uses the real authored fallback with an honest status");
                    Press("ReturnButton"); Check(Session.Beat.Id == "ch2_s4_b020" && Passage.Text == Session.Beat.Text,
                        "return after authored fallback restores original passage without advancing");
                }
                Press("AdvanceButton"); Check(Session.Beat.Id == "ch2_s4_b021", "normal continuation reaches original next document");
            }
            File.WriteAllBytes(manual, initialBytes); Load(); Press("TalkButton"); var stale = Actions[0];
            Press("LoadButton"); Action("Load current manual save · " + Target); var restored = State();
            stale.EmitSignal(BaseButton.SignalName.Pressed);
            Check(Modal is null && State() == restored && Session.ChosenExchangeIndex is null, "same-beat load retires uncommitted controls");
            Press("AdvanceButton"); Check(Session.Beat.Id == "ch2_s4_b020" && Session.History.Count == initial.History.Count + 1, "declining the exchange keeps fixed progression");
            GD.Print($"LANTERNWAKE_MISSING_MARGIN_OK checks={_checks} choices=3 cancel_load_return=true authored_only=true");
            Press("SettingsButton"); Action("Quit game");
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString()); if (_game is not null) await _game.Audio.StopAndRetireAsync(); GetTree().Quit(1);
        }
    }
}
#endif
