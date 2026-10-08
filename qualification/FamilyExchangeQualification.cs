#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

// Real player controls with an owned legacy save; no provider transport.
public partial class FamilyExchangeQualification : Node
{
    private GameView _game = null!;
    private int _checks;
    private T Observe<T>(string name) => (T)typeof(GameView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_game)!;
    private StorySession Session => Observe<StorySession>("_session");
    private Window? Modal => Observe<Window?>("_modal");
    private RichTextLabel Passage => _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText");
    private void Check(bool value, string claim) { if (!value) throw new Exception(claim); _checks++; }
    private void Press(string name) => NativeGameControls.Press(_game, name);
    private Button[] Choices => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().ToArray();
    private void Action(string caption) => Choices.Single(b => b.Text == caption).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private string Snapshot() => JsonSerializer.Serialize(Session.Snapshot(), Story.Json);
    private Dictionary<string, byte[]> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json").ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes);
    private void SameFiles(Dictionary<string, byte[]> before)
    {
        var after = Files(); Check(before.Count == after.Count && before.All(p => after.TryGetValue(p.Key, out var bytes) && bytes.SequenceEqual(p.Value)), "save bytes unchanged");
    }
    private void LoadTarget()
    {
        Press("LoadButton"); Action("Load current manual save · ch4_s1a_b035");
        Check(Session.Beat.Id == "ch4_s1a_b035" && Session.ChosenExchangeIndex is null && Session.CanAdvance, "legacy manual save restores optional exchange");
    }
    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_FAMILY_EXCHANGE_FIXTURE") ?? "";
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")), "owned debug fixture required");
            Check(ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "owned save boundary");
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frame();
            var preview = Observe<SessionStorage>("_storage").Mode == SessionMode.AuthorPreview;
            var expected = System.Environment.GetEnvironmentVariable("LANTERNWAKE_FAMILY_EXCHANGE_EXPECTED_MODE");
            Check(expected is "player" or "preview" && preview == (expected == "preview"), "Family exchange launch mode does not match the expected test mode.");
            if (!preview) LoadTarget();
            Check(Session.Beat.Id == "ch4_s1a_b035", "target reached through Load or isolated author preview");
            Press("SettingsButton"); Action("Larger reading text"); Action("Larger reading text");
            Modal!.EmitSignal(Window.SignalName.CloseRequested);
            var exchange = Session.Beat.Exchange!;
            _game.Audio.SetMuted(true);
            for (var index = 0; index < (preview ? 1 : 3); index++)
            {
                if (!preview) LoadTarget();
                Passage.VisibleCharacters = 5;
                typeof(GameView).GetField("_characters", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_game, 5d);
                var text = Passage.Text; var state = Snapshot(); var files = Files();
                var status = _game.CurrentStatusText;
                Press("TalkButton");
                Check(Modal is not null && Choices.Select(b => b.Text).SequenceEqual(exchange.Options.Select(o => o.Label)), "exact authored options in order");
                Check(Choices[0].HasFocus(), "first intention has keyboard focus without selection");
                Modal!.Size = new Vector2I(640, 480); await Frame(); await Frame();
                Check(Passage.VisibleCharacters == 5 && Observe<double>("_characters") == 5d, "typewriter paused behind exchange");
                Choices.Last().GrabFocus(); await Frame(); await Frame();
                var scroll = Modal.GetNode<ScrollContainer>("%ModalActionsScroll");
                Check(Choices.All(b => b.GetThemeFontSize("font_size") == 24) && scroll.FollowFocus && Choices.Last().HasFocus(), "150 percent choices retain focus and scroll following");
                Check(Choices.Last().GlobalPosition.Y >= scroll.GlobalPosition.Y - 1 && Choices.Last().GlobalPosition.Y + Choices.Last().Size.Y <= scroll.GlobalPosition.Y + scroll.Size.Y + 1, "last enlarged intention visible in small viewport");
                var cancelled = Choices[index]; var retiredWindow = Modal;
                Modal.EmitSignal(Window.SignalName.WindowInput, new InputEventKey { Pressed = true, Keycode = Key.Escape });
                Check(Modal is null && Snapshot() == state && Passage.Text == text && Passage.VisibleCharacters == 5, "Escape resumes exact unspoken passage"); SameFiles(files);
                Press("TalkButton"); var selector = Modal; var staleChoice = Choices[index];
                cancelled.EmitSignal(BaseButton.SignalName.Pressed); retiredWindow!.EmitSignal(Window.SignalName.CloseRequested);
                Check(Modal == selector && Snapshot() == state, "retired controls cannot select or close new exchange");
                Action(exchange.Options[index].Label);
                var response = Modal;
                Check(Session.ChosenExchangeIndex == index && Session.History.Count == JsonSerializer.Deserialize<SaveData>(state, Story.Json)!.History.Count + 2, "explicit selection appends one authored pair");
                Check(Modal!.GetNode<RichTextLabel>("%ModalText").Text.EndsWith(exchange.Options[index].Reply, StringComparison.Ordinal), "exact selected consequence displayed");
                staleChoice.EmitSignal(BaseButton.SignalName.Pressed); selector!.EmitSignal(Window.SignalName.CloseRequested);
                Check(Modal == response && Session.ChosenExchangeIndex == index, "retired selector is inert after choice");
                await Frame(); await Frame(); Check(Passage.VisibleCharacters == 5, "passage stays paused across selector and response");
                if (preview) SameFiles(files);
                else Check(JsonSerializer.Serialize(Observe<SessionStorage>("_storage").Read(true), Story.Json) == Snapshot(), "authored pair autosaved");
                var chosen = Snapshot(); Action("Back to story");
                Check(Modal is null && Passage.Text == text && Passage.VisibleCharacters == 5 && Snapshot() == chosen, "Return restores exact passage without advance");
                Check(_game.CurrentStatusText == status, "successful exchange restores original reading status");
                Press("TalkButton"); Check(Choices.Length == 1 && Choices[0].Text == "Back to story", "chosen exchange reopens read only");
                var saved = Files(); Action("Back to story"); Check(Snapshot() == chosen, "review does not repeat pair"); SameFiles(saved);
                if (!preview)
                {
                    Press("LoadButton"); Action("Load current autosave · ch4_s1a_b035");
                    Check(Session.ChosenExchangeIndex == index && Snapshot() == chosen, "actual Load restores selected intention");
                    Passage.VisibleCharacters = -1; Press("AdvanceButton");
                    Check(Session.Beat.Id == "ch4_s1a_b036", "choice rejoins unchanged next beat");
                }
            }
            if (!preview)
            {
                LoadTarget(); Passage.VisibleCharacters = 5;
                typeof(GameView).GetField("_characters", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_game, 5d);
                var text = Passage.Text; var faultState = Snapshot();
                var autosave = Path.Combine(ProjectSettings.GlobalizePath("user://"), "autosave.json");
                var autoBytes = File.Exists(autosave) ? File.ReadAllBytes(autosave) : null;
                var manualPath = Path.Combine(ProjectSettings.GlobalizePath("user://"), "save.json");
                var manualBytes = File.ReadAllBytes(manualPath);
                File.Delete(autosave); Directory.CreateDirectory(autosave);
                try
                {
                    var faultFiles = Files(); Press("TalkButton"); Action(exchange.Options[0].Label);
                    var warning = _game.CurrentStatusText;
                    Check(warning.StartsWith("Could not save: ", StringComparison.Ordinal), "actual filesystem failure reports save warning");
                    Check(Session.ChosenExchangeIndex == 0 && Session.History.Count == JsonSerializer.Deserialize<SaveData>(faultState, Story.Json)!.History.Count + 2, "failed autosave retains selected pair in memory");
                    var chosen = Snapshot(); Action("Back to story");
                    Check(Modal is null && Passage.Text == text && Passage.VisibleCharacters == 5 && Snapshot() == chosen, "failed save Return restores paused passage and exact memory state");
                    await Frame(); await Frame();
                    Check(_game.CurrentStatusText == warning, "failed autosave warning survives Return and process frames");
                    Press("TalkButton"); Modal!.EmitSignal(Window.SignalName.CloseRequested);
                    Check(_game.CurrentStatusText == warning && Snapshot() == chosen, "warning survives read-only reopening and Escape"); SameFiles(faultFiles);
                }
                finally { Directory.Delete(autosave); if (autoBytes is not null) File.WriteAllBytes(autosave, autoBytes); }
                Press("SaveButton");
                Check(_game.CurrentStatusText == "Saved on this device." && JsonSerializer.Serialize(Observe<SessionStorage>("_storage").Read(false), Story.Json) == Snapshot(), "successful manual retry clears warning and persists selected pair");
                // Restore the legacy fixture before existing stale-control checks.
                File.WriteAllBytes(manualPath, manualBytes);
                LoadTarget(); Press("TalkButton"); var stale = Choices[0];
                Press("LoadButton"); Action("Load current manual save · ch4_s1a_b035"); var state = Snapshot(); var files = Files();
                stale.EmitSignal(BaseButton.SignalName.Pressed);
                Check(Modal is null && Snapshot() == state, "pre-load exchange control is inert"); SameFiles(files);
                Press("TalkButton"); var window = Modal;
                typeof(GameView).GetField("_closing", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_game, true);
                Choices[0].EmitSignal(BaseButton.SignalName.Pressed); window!.EmitSignal(Window.SignalName.CloseRequested);
                Check(Modal == window && Snapshot() == state, "shutdown exchange callbacks are inert"); SameFiles(files);
                typeof(GameView).GetField("_closing", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_game, false);
                window.EmitSignal(Window.SignalName.CloseRequested); Passage.VisibleCharacters = -1; Press("AdvanceButton");
                Check(Session.Beat.Id == "ch4_s1a_b036", "unspoken exchange is skippable");
            }
            GD.Print("LANTERNWAKE_FAMILY_EXCHANGE_OK " + JsonSerializer.Serialize(new { preview, checks = _checks }));
            Press("SettingsButton"); Action("Quit game");
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString()); if (_game is not null) await _game.Audio.StopAndRetireAsync(); GetTree().Quit(1);
        }
    }
}
#endif
