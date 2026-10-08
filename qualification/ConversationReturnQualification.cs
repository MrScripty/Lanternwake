#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Actual offline conversation/Return controls in disposable normal and preview slots.</summary>
public partial class ConversationReturnQualification : Node
{
    private GameView _game = null!;
    private int _checks, _conversations, _beats;
    private T Observe<T>(string name) => (T)typeof(GameView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_game)!;
    private StorySession Session => Observe<StorySession>("_session");
    private RichTextLabel Dialogue => _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText");
    private LineEdit Entry => _game.InterfaceRoot.GetNode<LineEdit>("%PlayerEntry");
    private PanelContainer Chat => _game.InterfaceRoot.GetNode<PanelContainer>("%ChatPanel");
    private Window? Modal => Observe<Window?>("_modal");
    private Button Suggestion => Observe<VBoxContainer>("_suggestions").GetChild<Button>(0);
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Conversation return: " + claim);
        _checks++;
    }
    private void Press(string name) => NativeGameControls.Press(_game, name);
    private void Action(string text) => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == text).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private string Snapshot() => JsonSerializer.Serialize(Session.Snapshot(), Story.Json);
    private Dictionary<string, byte[]> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json").ToDictionary(p => Path.GetFileName(p), File.ReadAllBytes);
    private void Preserved(string state, Dictionary<string, byte[]> files)
    {
        Check(Snapshot() == state, "Return/cancel/retired controls preserve exact beat, transcript and activity state");
        var current = Files();
        Check(files.Count == current.Count && files.All(p => current.TryGetValue(p.Key, out var bytes) && bytes.SequenceEqual(p.Value)), "every primary/previous save byte preserved");
    }
    private async Task Drain()
    {
        await Observe<OwnedOperations>("_operations").DrainAsync(); await Frame();
        Check(!Observe<bool>("_busy"), "owned optional work has settled");
    }
    private void Authored()
    {
        var story = Observe<Story>("_story"); var id = Session.Beat.Speaker;
        var name = id == "narrator" ? "" : id == "you" ? "You" : story.Characters.FirstOrDefault(c => c.Id == id)?.Name ?? id;
        Check(Dialogue.Text == Session.Beat.Text && _game.CurrentSpeakerText == name,
            "Return restores current authored passage and its speaker after optional reply");
        Check(!Chat.Visible && _game.InterfaceRoot.GetNode<Button>("%AdvanceButton").HasFocus(), "Return restores story keyboard focus");
    }
    private async Task ClosedControls()
    {
        Press("TalkButton"); var retired = Suggestion;
        Entry.Text = "Cancelled draft 語"; Press("ReturnButton");
        var state = Snapshot(); var files = Files(); var text = Dialogue.Text;
        Press("SendButton"); await Drain();
        Check(Snapshot() == state && Dialogue.Text == text, "hidden Send cannot submit a cancelled draft after Return"); Preserved(state, files);
        Entry.EmitSignal(LineEdit.SignalName.TextSubmitted, "Cancelled draft 語"); await Drain(); Preserved(state, files);
        var generation = Observe<int>("_generation"); Press("ReturnButton");
        Check(Observe<int>("_generation") == generation, "retired Return cannot invalidate a later interaction generation");
        Entry.Text = "Owned closed entry"; retired.EmitSignal(BaseButton.SignalName.Pressed);
        Check(Entry.Text == "Owned closed entry", "retired suggestion cannot edit closed conversation");
        Press("TalkButton"); Entry.Text = "New open draft 語"; retired.EmitSignal(BaseButton.SignalName.Pressed);
        Check(Entry.Text == "New open draft 語", "previous opening's suggestion cannot replace new draft");
        Press("TalkButton"); Check(Entry.Text == "New open draft 語", "repeated Talk does not abandon current draft");
        Press("ReturnButton"); Authored(); Preserved(state, files);
    }
    private async Task Conversation(bool preview)
    {
        _conversations++;
        var beat = Session.Beat; var chat = beat.Conversation!;
        if (!preview) Press("SaveButton");
        var facts = Session.KnownFacts.Select(f => f.Id).ToArray(); var items = Session.Inventory.Select(i => i.Id).ToArray();
        var generation = Observe<int>("_generation"); var reveal = Dialogue.VisibleCharacters;
        var speaker = _game.CurrentSpeakerText;
        Press("TalkButton"); Suggestion.EmitSignal(BaseButton.SignalName.Pressed);
        var input = Entry.Text; var before = Session.History.Count;
        Press("SendButton"); await Drain();
        Check(Dialogue.Text == chat.Fallback && Session.History.Count == before + 2 && Session.History[^2].Text == input,
            "explicit Send displays and records exact authored fallback through native controls");
        Check(Session.KnownFacts.Select(f => f.Id).SequenceEqual(facts) && Session.Inventory.Select(i => i.Id).SequenceEqual(items), "optional reply unlocks no knowledge or objects");
        var state = Snapshot(); var files = Files();
        Press("ReturnButton"); Authored();
        Check(Dialogue.VisibleCharacters == reveal && Observe<int>("_generation") == generation + 1, "Return restores reveal state and retires request generation without rendering another beat");
        Preserved(state, files);
        Press("HistoryButton");
        Check(Modal!.GetNode<RichTextLabel>("%ModalText").Text.Contains(chat.Fallback), "optional reply remains readable in record after returning to plot");
        Modal.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed); Preserved(state, files);
        await ClosedControls();
        if (!preview) await InChatReadingSettings(false);
        Press("TalkButton");
        if (!preview)
        {
            Press("LoadButton"); Action("Load current manual save · " + beat.Id);
            Authored(); Check(Session.History.Count == before, "loading pre-chat manual snapshot discards only later optional replies");
        }
        else Press("ReturnButton");
        Dialogue.VisibleCharacters = -1;
        Check(_game.CurrentSpeakerText == speaker, "same authored speaker survives reopening/load");
        Press("TalkButton"); Entry.Text = "Cancel immediately after Send"; Press("SendButton"); Press("ReturnButton");
        var cancelled = Snapshot(); var cancelledFiles = Files(); await Drain(); Authored(); Preserved(cancelled, cancelledFiles);
    }
    private async Task InChatReadingSettings(bool preview)
    {
        if (Observe<bool>("_instant")) { Press("SettingsButton"); Action("Toggle instant text"); }
        Check(!Observe<bool>("_instant"), "visible setting selects typewriter before partial passage probe");
        async Task PausePartialPassage()
        {
            _game.SetProcess(true);
            if (!preview) { Press("LoadButton"); Action("Load current autosave · " + Session.Beat.Id); }
            var deadline = Time.GetTicksMsec() + 5000;
            while (Dialogue.VisibleCharacters <= 0 && Time.GetTicksMsec() < deadline) await Frame();
            Check(!Observe<bool>("_instant") && Dialogue.VisibleCharacters > 0 && Dialogue.VisibleCharacters < Dialogue.GetTotalCharacterCount(),
                "actual typewriter frames partially reveal the authored passage");
            _game.SetProcess(false);
        }
        await PausePartialPassage();
        var pausedReveal = Dialogue.VisibleCharacters; var pausedTimer = Observe<double>("_characters");
        var state = Snapshot(); var files = Files();
        Press("TalkButton"); _game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.Escape }); Authored();
        Check(Dialogue.VisibleCharacters == pausedReveal && Observe<double>("_characters") == pausedTimer,
            "unchanged typewriter setting preserves exact paused reveal and timer"); Preserved(state, files);
        Press("TalkButton"); Press("SettingsButton"); Action("Reset reading text size · 150%");
        Action("Larger reading text"); Action("Larger reading text");
        Modal!.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
        Press("ReturnButton"); Authored();
        Check(Dialogue.VisibleCharacters == pausedReveal && Observe<double>("_characters") == pausedTimer && Observe<ReadingTextStyle>("_readingText").Percent == 150,
            "in-chat sizing alone preserves paused reveal and timer"); Preserved(state, files);
        if (!preview)
        {
            var beatId = Session.Beat.Id; Press("AdvanceButton");
            Check(Session.Beat.Id == beatId && Dialogue.VisibleCharacters == -1, "first Continue reveals the restored typewriter passage without skipping it");
            await PausePartialPassage(); pausedReveal = Dialogue.VisibleCharacters; pausedTimer = Observe<double>("_characters");
        }
        Press("TalkButton"); Entry.Text = "Unsent settings draft 語";
        Press("SettingsButton"); Action("Toggle instant text");
        Check(Chat.Visible && Observe<bool>("_instant") && Dialogue.VisibleCharacters == -1 && Entry.Text == "Unsent settings draft 語",
            "in-chat instant setting reveals current text and preserves open draft");
        Press("ReturnButton"); Authored();
        Check(Dialogue.VisibleCharacters == -1 && Observe<bool>("_instant") && Observe<double>("_characters") == pausedTimer,
            "Return honors newly enabled instant text without restarting paused typewriter"); Preserved(state, files);
        Check(_game.CurrentStatusText == "Instant text enabled", "returned status reflects the newly selected instant setting");
        _game.SetProcess(true); await Frame();
        Check(Dialogue.VisibleCharacters == -1, "returned instant passage remains fully revealed on next native frame");
        _game.SetProcess(false);
        Press("TalkButton"); Press("SettingsButton"); Action("Toggle instant text");
        Press("SettingsButton"); Action("Toggle instant text");
        Press("SettingsButton"); Action("Toggle instant text");
        Press("ReturnButton"); Authored();
        Check(!Observe<bool>("_instant") && Dialogue.VisibleCharacters == -1,
            "enabling then disabling in chat retains explicit reveal rather than rewinding passage"); Preserved(state, files);
        Press("SettingsButton"); Action("Toggle instant text"); _game.SetProcess(true);
    }
    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_CONVERSATION_RETURN_FIXTURE") ?? "";
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")), "owned debug fixture required");
            Check(ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "userdata stays inside fixture");
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frame();
            var preview = Observe<SessionStorage>("_storage").Mode == SessionMode.AuthorPreview;
            var closedOnly = System.Environment.GetEnvironmentVariable("LANTERNWAKE_CONVERSATION_RETURN_MODE") == "closed";
            if (!preview) Press("AdvanceButton");
            Press("SettingsButton"); Action("Larger reading text"); Action("Larger reading text");
            if (preview)
            {
                Modal!.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
                await InChatReadingSettings(true);
            }
            else Action("Toggle instant text");
            _game.Audio.SetMuted(true);
            while (true)
            {
                _beats++;
                if (Session.Beat.Conversation is not null)
                {
                    GD.Print("Conversation-return probe: " + Session.Beat.Id);
                    if (closedOnly) { await ClosedControls(); break; }
                    await Conversation(preview);
                    if (preview) break;
                }
                if (Session.Beat.Activity is { } activity)
                {
                    Press("AdvanceButton"); Press("ReturnButton"); Action(activity.Options[activity.CorrectIndex]);
                    Check(Session.CanAdvance, "late hidden Return does not invalidate an active evidence question");
                }
                if (Session.IsEnding) break;
                var before = Session.Beat.Id; Press("AdvanceButton"); await Frame();
                Check(Session.Beat.Id != before, "actual Continue advances required plot");
            }
            Check(closedOnly || _conversations == (preview ? 1 : 28), "all expected authored conversation points qualified");
            Check(preview || closedOnly || _beats == Observe<Story>("_story").Chapters.Sum(c => c.Scenes.Sum(s => s.Beats.Length)), "entire required story reached via controls");
            GD.Print("LANTERNWAKE_CONVERSATION_RETURN_OK " + JsonSerializer.Serialize(new { preview, closedOnly, checks = _checks, conversations = _conversations, nativeBeats = _beats }));
            Press("SettingsButton"); Action("Quit game");
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            if (_game is not null) await _game.Audio.StopAndRetireAsync();
            GetTree().Quit(1);
        }
    }
}
#endif
