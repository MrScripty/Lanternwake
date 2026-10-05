#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Owned native title/settings content-note controls; copy oracle is the existing bible.</summary>
public partial class ContentNoteQualification : Node
{
    private GameView _game = null!;
    private string _expected = "";
    private int _checks, _beats;
    private T Observe<T>(string name) => (T)typeof(GameView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_game)!;
    private StorySession Session => Observe<StorySession>("_session");
    private Window? Modal => Observe<Window?>("_modal");
    private Button TitleNote => _game.InterfaceRoot.GetNode<Button>("%ContentNoteButton");
    private RichTextLabel Dialogue => _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText");
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Content note: " + claim);
        _checks++;
    }
    private void Press(string name) => _game.InterfaceRoot.GetNode<Button>("%" + name).EmitSignal(BaseButton.SignalName.Pressed);
    private Button ActionButton(string text) => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == text);
    private void Action(string text) => ActionButton(text).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private string Snapshot() => JsonSerializer.Serialize(Session.Snapshot(), Story.Json);
    private Dictionary<string, byte[]> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json").ToDictionary(p => Path.GetFileName(p), File.ReadAllBytes);
    private void Preserved(string state, Dictionary<string, byte[]> files)
    {
        Check(Snapshot() == state, "note does not change beat, transcript, knowledge-derived state or solved gates");
        var current = Files();
        Check(files.Count == current.Count && files.All(p => current.TryGetValue(p.Key, out var bytes) && bytes.SequenceEqual(p.Value)), "every primary/previous save byte preserved");
    }
    private async Task ReadNote(bool settings)
    {
        Check(Modal?.Title == "Content note", "actual control opens content note");
        var text = Modal!.GetNode<RichTextLabel>("%ModalText");
        Check(text.Text == _expected && !text.BbcodeEnabled, "complete note matches existing bible exactly, with paragraph formatting only");
        Check(text.GetThemeFontSize("normal_font_size") == 32, "note uses current 150% reading size");
        var close = Modal.GetNode<Button>("%ModalCloseButton");
        Check(close.Text == (settings ? "Back to settings" : "Return to title") && close.HasFocus(), "focused close names its destination");
        Modal.Size = new Vector2I(480, 320); await Frame(); await Frame();
        var bar = text.GetVScrollBar();
        Check(bar.Visible && bar.MaxValue > bar.Page && bar.FocusMode == Control.FocusModeEnum.All, "enlarged exact note has keyboard-focusable scrolling in small modal");
        bar.Value = 0; bar.GrabFocus();
        await Frame();
        Modal.PushInput(new InputEventKey { Keycode = Key.Down, PhysicalKeycode = Key.Down, Pressed = true });
        Modal.PushInput(new InputEventKey { Keycode = Key.Down, PhysicalKeycode = Key.Down, Pressed = false }); await Frame();
        Check(bar.HasFocus() && bar.Value > 0, $"real viewport keyboard action scrolls note prose (focus={bar.HasFocus()}, value={bar.Value}, step={bar.Step})");
        Modal.PushInput(new InputEventKey { Keycode = Key.Tab, PhysicalKeycode = Key.Tab, Pressed = true });
        Modal.PushInput(new InputEventKey { Keycode = Key.Tab, PhysicalKeycode = Key.Tab, Pressed = false }); await Frame();
        Check(close.HasFocus() && close.GlobalPosition.Y + close.Size.Y <= Modal.Size.Y, "close remains reachable in enlarged small modal");
    }
    private async Task TitleFlow()
    {
        Check(!Observe<bool>("_started") && TitleNote.Visible, "title exposes note before Arrival");
        var title = Dialogue.Text; var state = Snapshot(); var files = Files();
        TitleNote.GrabFocus();
        Check(TitleNote.HasFocus() && TitleNote.Size.Y >= 48, "title note is a focused normal-size control");
        Press("ContentNoteButton"); await ReadNote(false);
        var oldClose = Modal!.GetNode<Button>("%ModalCloseButton"); var oldWindow = Modal;
        _game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.Escape });
        Check(Modal is null && !Observe<bool>("_started") && Dialogue.Text == title, "Escape returns to unchanged title without starting story");
        Check(_game.InterfaceRoot.GetNode<Button>("%AdvanceButton").HasFocus(), "title return restores Arrival focus"); Preserved(state, files);
        Press("SettingsButton"); var settings = Modal;
        oldClose.EmitSignal(BaseButton.SignalName.Pressed); oldWindow.EmitSignal(Window.SignalName.CloseRequested); Press("ContentNoteButton");
        Check(Modal == settings, "retired title-note close and covered title control cannot replace Settings");
        Modal!.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
        Press("ContentNoteButton"); Modal!.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
        Check(!Observe<bool>("_started") && Modal is null, "visible Return to title also preserves start choice"); Preserved(state, files);
    }
    private async Task SettingsFlow()
    {
        var state = Snapshot(); var files = Files(); var generation = Observe<int>("_generation"); var dialogue = Dialogue.Text;
        Press("SettingsButton"); var retiredOpen = ActionButton("Content note"); Action("Content note");
        var note = Modal; retiredOpen.EmitSignal(BaseButton.SignalName.Pressed);
        Check(Modal == note, "retired Settings action cannot replace current note");
        await ReadNote(true); var oldClose = Modal!.GetNode<Button>("%ModalCloseButton");
        oldClose.EmitSignal(BaseButton.SignalName.Pressed); var settings = Modal;
        oldClose.EmitSignal(BaseButton.SignalName.Pressed);
        Check(Modal == settings && Modal?.Title == "Reading settings", "note returns to Settings; retired return is inert");
        Check(Observe<int>("_generation") == generation && Dialogue.Text == dialogue, "reading note does not render another beat or replace passage"); Preserved(state, files);
        Action("Content note"); Modal!.EmitSignal(Window.SignalName.WindowInput, new InputEventKey { Pressed = true, Keycode = Key.Escape });
        Check(Modal?.Title == "Reading settings", "note Window Escape returns to Settings");
        Modal!.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed); Preserved(state, files);
    }
    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_CONTENT_NOTE_FIXTURE") ?? "";
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")), "owned debug fixture required");
            Check(ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "userdata is owned fixture");
            var line = Godot.FileAccess.GetFileAsString("res://docs/bible/05_PACING_ACCESSIBILITY_QA.md").Split('\n').Single(l => l.StartsWith("- Content note: ", StringComparison.Ordinal));
            _expected = line["- Content note: ".Length..].TrimEnd('\r');
            _expected = char.ToUpperInvariant(_expected[0]) + _expected[1..];
            _expected = _expected.Replace(". No child", ".\n\nNo child", StringComparison.Ordinal);
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frame();
            var preview = Observe<SessionStorage>("_storage").Mode == SessionMode.AuthorPreview;
            var resume = System.Environment.GetEnvironmentVariable("LANTERNWAKE_CONTENT_NOTE_MODE") == "resume";
            Press("SettingsButton"); Action("Larger reading text"); Action("Larger reading text"); Action("Toggle instant text"); _game.Audio.SetMuted(true);
            if (!preview) await TitleFlow();
            if (resume)
            {
                var state = Files(); Press("LoadButton");
                var candidate = Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text.StartsWith("Load current manual save · ", StringComparison.Ordinal));
                candidate.EmitSignal(BaseButton.SignalName.Pressed);
                Check(!TitleNote.Visible && Session.Beat.Conversation is not null, "fresh process Load hides title note and restores ordinary story");
                var current = Files(); Check(state.Count == current.Count && state.All(p => current[p.Key].SequenceEqual(p.Value)), "Load and note do not rewrite persisted slots");
            }
            else if (!preview)
            {
                Press("AdvanceButton"); Check(Observe<bool>("_started") && !TitleNote.Visible, "ordinary Arrival remains one explicit action");
                while (Session.Beat.Conversation is null)
                {
                    if (Session.Beat.Activity is { } activity) { Press("AdvanceButton"); Action(activity.Options[activity.CorrectIndex]); }
                    Press("AdvanceButton"); _beats++; await Frame();
                }
                Press("SaveButton");
            }
            else Check(!TitleNote.Visible, "author preview offers note through Settings without a title reset");
            Press("ContentNoteButton"); Check(Modal is null, "late hidden title-note callback is inert after start/load/preview");
            await SettingsFlow();
            Press("TalkButton"); var entry = _game.InterfaceRoot.GetNode<LineEdit>("%PlayerEntry"); entry.Text = "Unsent note-reading draft 語";
            var before = Snapshot(); var files = Files(); await SettingsFlow();
            Check(_game.InterfaceRoot.GetNode<PanelContainer>("%ChatPanel").Visible && entry.Text == "Unsent note-reading draft 語" && entry.Editable, "note preserves open editable chat draft"); Preserved(before, files);
            Press("ReturnButton"); Preserved(before, files);
            GD.Print("LANTERNWAKE_CONTENT_NOTE_OK " + JsonSerializer.Serialize(new { preview, resume, checks = _checks, nativeAdvances = _beats }));
            Press("SettingsButton"); Action("Quit game");
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString()); if (_game is not null) await _game.Audio.StopAndRetireAsync(); GetTree().Quit(1);
        }
    }
}
#endif
