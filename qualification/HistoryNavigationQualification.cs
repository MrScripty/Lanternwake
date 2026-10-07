#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

public partial class HistoryNavigationQualification : Node
{
    private GameView _game = null!;
    private int _checks;
    private T Observe<T>(string name) => (T)typeof(GameView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_game)!;
    private StorySession Session => Observe<StorySession>("_session");
    private Window? Modal => Observe<Window?>("_modal");
    private void Check(bool value, string claim) { if (!value) throw new Exception("History qualification: " + claim); _checks++; }
    private void Press(string name) => _game.InterfaceRoot.GetNode<Button>("%" + name).EmitSignal(BaseButton.SignalName.Pressed);
    private void Action(string prefix) => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text.StartsWith(prefix, StringComparison.Ordinal)).EmitSignal(BaseButton.SignalName.Pressed);
    private T Tool<T>(string name) where T : Node => Modal!.GetNode<T>("Margin/Stack/HistoryTools/History" + name);
    private void Search(string text) { var input = Tool<LineEdit>("Search"); input.Text = text; input.EmitSignal(LineEdit.SignalName.TextChanged, text); }
    private RichTextLabel Prose => Modal!.GetNode<RichTextLabel>("%ModalText");
    private void Close() => Modal!.EmitSignal(Window.SignalName.CloseRequested);
    private async Task Frames() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private string State() => JsonSerializer.Serialize(Session.Snapshot(), Story.Json);
    private Dictionary<string, byte[]> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json").ToDictionary(p => p, File.ReadAllBytes);
    private void Load(bool automatic) { Press("LoadButton"); Action("Load current " + (automatic ? "autosave" : "manual save")); }
    private async Task Key(Key key, bool shift = false)
    {
        using var press = new InputEventKey { Keycode = key, Pressed = true, ShiftPressed = shift };
        using var release = new InputEventKey { Keycode = key, Pressed = false, ShiftPressed = shift };
        Modal!.PushInput(press); Modal.PushInput(release); await Frames();
    }
    private async Task Pad(JoyButton button)
    {
        using var press = new InputEventJoypadButton { ButtonIndex = button, Pressed = true };
        using var release = new InputEventJoypadButton { ButtonIndex = button, Pressed = false };
        Modal!.PushInput(press); Modal.PushInput(release); await Frames();
    }
    private void Advance()
    {
        _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
        if (!Session.CanAdvance)
        {
            Press("AdvanceButton"); Action(Session.Beat.Activity!.Options[Session.Beat.Activity.CorrectIndex]);
        }
        Press("AdvanceButton");
    }
    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_HISTORY_FIXTURE") ?? "";
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")), "owned debug fixture");
            Check(ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "private normal player saves");
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frames();
            _game.Audio.SetMuted(true);
            var story = Observe<Story>("_story");
            if (System.Environment.GetEnvironmentVariable("LANTERNWAKE_HISTORY_PREPARE") == "1")
            {
                Press("AdvanceButton");
                while (Session.Chapter.Id != story.Chapters[1].Id) Advance();
                Press("SaveButton");
                while (Session.Chapter.Id != story.Chapters[2].Id) Advance();
                Check(Files().Count >= 3, "earlier manual and later automatic slots produced by real progression");
            }
            Load(true);
            Check(Session.Chapter.Id == story.Chapters[2].Id, "later automatic slot loads");
            Press("HistoryButton"); await Frames();
            Check(Tool<OptionButton>("Chapter").ItemCount == 4, "later history has three reached chapters");
            Tool<Button>("Latest").EmitSignal(BaseButton.SignalName.Pressed);
            Close(); Load(false); await Frames();
            Check(Session.Chapter.Id == story.Chapters[1].Id, "earlier manual slot restores");
            var state = State(); var files = Files();
            var inventory = Session.Inventory.Select(i => i.Id).ToArray(); var facts = Session.KnownFacts.Select(f => f.Id).ToArray();
            foreach (var percent in new[] { 100, 150 })
            {
                Observe<ReadingTextStyle>("_readingText").SetPercent(percent);
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    Press("HistoryButton"); Modal!.Size = new(640, 480); await Frames();
                    Check(Tool<Button>("Latest").HasFocus(), "latest receives opening focus");
                    Check(Tool<OptionButton>("Chapter").ItemCount == 3, "restore hides later chapter from navigation");
                    Check(Prose.Size.Y > 100 && Prose.GetGlobalRect().End.Y <= Modal.Size.Y && Modal.GetNode<Button>("%ModalCloseButton").GetGlobalRect().End.Y <= Modal.Size.Y, "100/150 small native layout retains prose and Close");
                    var theme = GD.Load<Theme>("res://Scenes/UI/LanternwakeTheme.tres");
                    Check(Tool<LineEdit>("Search").GetThemeFontSize("font_size") == (int)Math.Round(theme.GetFontSize("font_size", "LineEdit") * percent / 100d, MidpointRounding.AwayFromZero), "search scales with session reading size");
                    Check(Tool<OptionButton>("Chapter").GetPopup().GetThemeFontSize("font_size") == (int)Math.Round(theme.GetFontSize("font_size", "PopupMenu") * percent / 100d, MidpointRounding.AwayFromZero), "native chapter popup rows scale with reading size");
                    Search("query-with-no-reached-match"); await Frames();
                    Check(Prose.Text == "No reached entries match your search." && Tool<Label>("Count").Text.StartsWith("0 of "), "no results communicates reached boundary");
                    Search(story.Chapters[2].Scenes[0].Beats[0].Text);
                    Check(Tool<Label>("Count").Text.StartsWith("0 of "), "later slot prose excluded after earlier restore");
                    Search(Session.History[0].Text.ToUpperInvariant());
                    Check(Prose.Text.Contains(Session.History[0].Text), "actual reached text case insensitive search");
                    var latest = Tool<Button>("Latest"); latest.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
                    Check(Tool<LineEdit>("Search").Text == "" && Prose.Text.EndsWith(Session.History[^1].Text, StringComparison.Ordinal), "latest clears filter and ends at restored transcript");
                    var bar = Prose.GetVScrollBar();
                    Check(bar.HasFocus() && bar.Value > 0 && bar.Value <= bar.MaxValue - bar.Page + 1, "latest scrolls actual wrapped text and transfers reading focus");
                    Check(latest.FindNextValidFocus() == Tool<OptionButton>("Chapter") && latest.FindPrevValidFocus() == Tool<LineEdit>("Search"), "keyboard loop reaches search and chapter");
                    Check(latest.GetNode<Control>(latest.FocusNeighborBottom) == Tool<OptionButton>("Chapter"), "controller directional neighbor reaches chapter");
                    latest.GrabFocus(); await Key(Godot.Key.Tab, true);
                    Check(Tool<LineEdit>("Search").HasFocus(), "actual Shift-Tab reaches editable search");
                    await Key(Godot.Key.Tab); Check(latest.HasFocus(), "actual Tab returns latest focus");
                    await Pad(JoyButton.DpadDown); Check(Tool<OptionButton>("Chapter").HasFocus(), "actual controller down reaches chapters");
                    await Pad(JoyButton.DpadDown); Check(bar.HasFocus(), "actual controller down reaches reached prose");
                    await Pad(JoyButton.DpadRight); Check(Modal!.GetNode<Button>("%ModalCloseButton").HasFocus(), "controller right leaves prose for Close");
                    var chapters = Tool<OptionButton>("Chapter");
                    chapters.Select(1); chapters.EmitSignal(OptionButton.SignalName.ItemSelected, 1L); await Frames();
                    Check(bar.Value == 0 && bar.HasFocus(), "first chapter jump reaches first recorded paragraph");
                    Search("no-match");
                    chapters.Select(2); chapters.EmitSignal(OptionButton.SignalName.ItemSelected, 2L); await Frames();
                    Check(Tool<LineEdit>("Search").Text == "" && bar.Value > 0, "chapter jump clears search and reaches restored second chapter");
                    latest.EmitSignal(BaseButton.SignalName.Pressed); Search("query-with-no-reached-match"); await Frames();
                    Check(bar.Value == 0 && Prose.Text.Contains("No reached entries"), "new search retires pending jump");
                    latest.GrabFocus(); await Key(Godot.Key.Tab); await Key(Godot.Key.Tab);
                    Check(Modal!.GetNode<Button>("%ModalCloseButton").HasFocus(), "no-results Tab skips hidden scrollbar to Close");
                    latest.EmitSignal(BaseButton.SignalName.Pressed);
                    typeof(GameView).GetField("_closing", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_game, true);
                    await Frames(); Check(!Prose.GetVScrollBar().HasFocus(), "shutdown retires pending focus transfer");
                    typeof(GameView).GetField("_closing", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_game, false);
                    latest.EmitSignal(BaseButton.SignalName.Pressed); var retired = Modal; Close(); Press("HistoryButton"); await Frames();
                    Check(Modal != retired && Prose.GetVScrollBar().Value == 0, "pending retired jump cannot scroll reopened reader");
                    Close(); await Frames(); Check(_game.InterfaceRoot.GetNode<Button>("%AdvanceButton").HasFocus(), "close returns story focus");
                    Check(State() == state && inventory.SequenceEqual(Session.Inventory.Select(i => i.Id)) && facts.SequenceEqual(Session.KnownFacts.Select(f => f.Id)), "all reading actions preserve story state and inventory");
                    var after = Files(); Check(files.Count == after.Count && files.All(p => after[p.Key].SequenceEqual(p.Value)), "every save byte preserved");
                }
            }
            Session.Restore(new(StorySession.CurrentSaveVersion, story.Title, story.Chapters[0].Scenes[0].Beats[0].Id, [], []));
            Press("HistoryButton"); await Frames();
            Check(Tool<Button>("Latest").Disabled && Tool<OptionButton>("Chapter").Disabled && Tool<LineEdit>("Search").HasFocus(), "empty restored transcript has safe search focus and no chapter links");
            Check(Prose.Text == "No reached history yet.", "empty transcript does not fabricate reached prose");
            Close(); await Frames();
            Session.Restore(JsonSerializer.Deserialize<SaveData>(state, Story.Json)!);
            GD.Print($"LANTERNWAKE_HISTORY_NAVIGATION_OK checks={_checks} prepare={System.Environment.GetEnvironmentVariable("LANTERNWAKE_HISTORY_PREPARE")} earlier-restore fresh-process search latest chapter lifecycle focus 100/150 layout state/save bytes");
            // Use the player's shutdown path so native audio releases before engine exit.
            Press("SettingsButton"); Action("Quit game");
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            if (_game?.Audio is { } audio) await audio.StopAndRetireAsync();
            GetTree().Quit(1);
        }
    }
}
#endif
