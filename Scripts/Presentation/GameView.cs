using Godot;
using Lanternwake.Core;
using Lanternwake.Conversation;
using System.Text.Json;

namespace Lanternwake.Presentation;

[Tool]
public partial class GameView : Node
{
    [ExportGroup("Scene dependencies")]
    [Export] public AudioDirector Audio { get; set; } = null!;
    [Export] public StageDirector Stage { get; set; } = null!;
    [Export] public Control InterfaceRoot { get; set; } = null!;
    [Export] public PackedScene ModalScene { get; set; } = null!;
    [Export] public PackedScene MicrophoneConsentScene { get; set; } = null!;
    [Export] public PackedScene ChoiceButtonScene { get; set; } = null!;
    [Export(PropertyHint.File, "*.json")] public string StoryPath { get; set; } = "res://Content/story.json";

    [ExportGroup("Reading defaults")]
    [Export(PropertyHint.Range, "1,200,1")] public float CharactersPerSecond { get; set; } = 55;
    [Export] public bool StartWithInstantText { get; set; }

    private Story _story = null!;
    private StorySession _session = null!;
    private SessionStorage? _storage;
    private StageDirector _stage = null!;
    private Label _chapter = null!, _speaker = null!, _status = null!, _place = null!;
    private RichTextLabel _dialogue = null!;
    private VBoxContainer _suggestions = null!;
    private PanelContainer _chatPanel = null!;
    private LineEdit _entry = null!;
    private Button _advance = null!, _talk = null!, _mic = null!, _send = null!;
    private Button _titleContentNote = null!;
    private Window? _modal;
    private PumasClient? _pumas;
    private readonly SpeechRecorder _speech = new();
    private CancellationTokenSource? _request, _speechRequest;
    private readonly OwnedOperations _operations = new();
    private bool _closing, _previewMode, _stagePreview;
    private int _previewIndex;
    private bool _busy, _started, _instant;
    private double _characters, _recordSeconds;
    private string _lastScene = "";
    private int _generation;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        try
        {
            GetTree().AutoAcceptQuit = false;
            _story = Story.Parse(Godot.FileAccess.GetFileAsString(StoryPath));
            _session = new(_story);
            _stage = Stage ?? throw new InvalidOperationException("Assign the Stage scene in the Inspector.");
            _instant = StartWithInstantText;
            var launch = SessionLaunch.Parse(OS.GetCmdlineUserArgs(), OS.IsDebugBuild());
            _previewMode = launch.Mode == SessionMode.AuthorPreview;
            _stagePreview = launch.StagePreview;
            _storage = new SessionStorage(launch.Mode, ProjectSettings.GlobalizePath("user://"), _story);
            if (launch.BeatId is { } beatId) _session = StoryContextPreview.CreateSessionAtBeat(_story, beatId);
            BindInterface(); ShowTitle();
            if (_stagePreview) ShowStagePreview();
            else if (launch.BeatId is not null) { _started = true; RenderBeat(false); }
            if (launch.Check == "--author-preview-smoke") RunAuthorPreviewSmoke(launch.BeatId!);
            else if (OS.GetCmdlineUserArgs().Contains("--save-isolation-smoke")) RunSaveIsolationSmoke();
            else if (OS.GetCmdlineUserArgs().Contains("--audio-smoke")) RunAudioSmoke();
            else if (OS.GetCmdlineUserArgs().Contains("--smoke")) RunSmoke();
            else if (OS.GetCmdlineUserArgs().Contains("--ui-smoke") || OS.GetCmdlineUserArgs().Contains("--live-ui-preview")) RunUiSmoke();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            var label = new Label { Text = "Lanternwake could not start.\n" + error.Message, Position = new(48, 48) }; AddChild(label);
            if ((OS.GetCmdlineUserArgs().Contains("--audio-smoke") || OS.GetCmdlineUserArgs().Contains("--smoke") || OS.GetCmdlineUserArgs().Contains("--ui-smoke") || OS.GetCmdlineUserArgs().Contains("--save-isolation-smoke") || OS.GetCmdlineUserArgs().Contains("--author-preview-smoke"))) QuitAfterAudio(1);
        }
    }
    // Layout and appearance are authored in Scenes/UI; unique scene names are the
    // binding contract, so containers can move without changing these callbacks.
    private void BindInterface()
    {
        if (InterfaceRoot is null || ModalScene is null || MicrophoneConsentScene is null || ChoiceButtonScene is null)
            throw new InvalidOperationException("Assign interface and dialog scenes in the Inspector.");
        _chapter = InterfaceRoot.GetNode<Label>("%ChapterLabel");
        _place = InterfaceRoot.GetNode<Label>("%PlaceLabel");
        _speaker = InterfaceRoot.GetNode<Label>("%SpeakerLabel");
        _dialogue = InterfaceRoot.GetNode<RichTextLabel>("%DialogueText");
        _status = InterfaceRoot.GetNode<Label>("%StatusLabel");
        _talk = InterfaceRoot.GetNode<Button>("%TalkButton");
        _advance = InterfaceRoot.GetNode<Button>("%AdvanceButton");
        _chatPanel = InterfaceRoot.GetNode<PanelContainer>("%ChatPanel");
        _suggestions = InterfaceRoot.GetNode<VBoxContainer>("%Suggestions");
        _entry = InterfaceRoot.GetNode<LineEdit>("%PlayerEntry");
        _mic = InterfaceRoot.GetNode<Button>("%MicrophoneButton");
        _send = InterfaceRoot.GetNode<Button>("%SendButton");
        _titleContentNote = InterfaceRoot.GetNode<Button>("%ContentNoteButton");
        _titleContentNote.Pressed += ShowTitleContentNote;
        _readingText.Register(_dialogue); _readingText.Register(_entry);
        EnableKeyboardReading(_dialogue);
        InterfaceRoot.GetNode<Button>("%EvidenceButton").Pressed += ShowEvidence;
        InterfaceRoot.GetNode<Button>("%HistoryButton").Pressed += ShowHistory;
        InterfaceRoot.GetNode<Button>("%SaveButton").Pressed += () => Save(false);
        InterfaceRoot.GetNode<Button>("%LoadButton").Pressed += ShowLoad;
        InterfaceRoot.GetNode<Button>("%SettingsButton").Pressed += ShowSettings;
        InterfaceRoot.GetNode<Button>("%SaveButton").Disabled = !_storage!.CanUseSaves;
        InterfaceRoot.GetNode<Button>("%LoadButton").Disabled = !_storage.CanUseSaves;
        _talk.Pressed += OpenConversation;
        _advance.Pressed += Advance;
        _entry.TextSubmitted += _ => SendReply();
        _mic.Pressed += ToggleMicrophone;
        _send.Pressed += SendReply;
        InterfaceRoot.GetNode<Button>("%ReturnButton").Pressed += ReturnToStory;
    }
    // Only the number, text and callbacks of these rows are determined at runtime.
    private Button ChoiceButton(string text, Action action)
    {
        var button = ChoiceButtonScene.Instantiate<Button>();
        button.Text = text;
        button.Pressed += action;
        return button;
    }
    private void ShowTitle()
    {
        // The title copy and initial HUD state belong to GameInterface.tscn.
        _stage.ShowLocation("harbor", "night", []);
        Audio.ShowLocation("harbor");
    }
    private void Advance()
    {
        if (_busy || _chatPanel.Visible || _modal is not null) return;
        if (!_started) { _started = true; RenderBeat(); return; }
        if (_dialogue.VisibleCharacters >= 0 && _dialogue.VisibleCharacters < _dialogue.GetTotalCharacterCount()) { _dialogue.VisibleCharacters = -1; return; }
        if (!_session.CanAdvance) { ShowActivity(); return; }
        if (_session.Advance()) { RenderBeat(); Save(true); }
        else ShowCompletion();
    }
    private void RenderBeat(bool playAudioCue = true)
    {
        _generation++;
        _titleContentNote.Visible = false;
        var scene = _session.Scene;
        Audio.ShowLocation(scene.Location);
        Audio.ApplyBeatCue(_session.Beat.Id, _session.Beat.StageCue, playAudioCue);
        if (_lastScene != scene.Id) { _stage.ShowLocation(scene.Location, scene.TimeOfDay, scene.CharacterIds); _lastScene = scene.Id; }
        _stage.ApplyAuthoredCues(_session.ActiveStageCues, _session.Beat.StageCue, _session.Beat.Id);
        _chapter.Text = (_previewMode ? "AUTHOR PREVIEW · " : "") + _session.Chapter.Title.ToUpperInvariant();
        _place.Text = scene.Title + "  ·  " + scene.TimeOfDay.Replace('_', ' ');
        _speaker.Text = DisplayName(_session.Beat.Speaker);
        _dialogue.Text = _session.Beat.Text; _characters = 0; _dialogue.VisibleCharacters = _instant ? -1 : 0;
        _talk.Visible = _session.Beat.Conversation is not null || _session.Beat.Exchange is not null;
        _talk.Text = _session.Beat.Exchange is { } exchange ? "Speak with " + DisplayName(exchange.CharacterId) : "Stay and talk";
        _advance.Text = !_session.CanAdvance ? "Examine evidence  ›" : _session.IsEnding ? "Finish  ›" : "Continue  ›";
        _status.Text = (_previewMode ? "AUTHOR PREVIEW · player saves disabled · " : "") + $"{_session.Progress:P0} · Space / Enter to continue · E evidence · H history";
    }
    private string DisplayName(string id) => id == "narrator" ? "" : id == "you" ? "You" : _story.Characters.FirstOrDefault(c => c.Id == id)?.Name ?? id;
    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) return;
        _operations.ObserveCompleted(error => GD.PushWarning(error.Message));
        if (_started && _exchangeReading is null && _dialogue.VisibleCharacters >= 0) { _characters += delta * CharactersPerSecond; _dialogue.VisibleCharacters = Math.Min((int)_characters, _dialogue.GetTotalCharacterCount()); }
        if (_speech.Recording) { _speech.Poll(); _recordSeconds += delta; _mic.Text = $"Stop ({_recordSeconds:0}s)"; if (_recordSeconds >= 30) ToggleMicrophone(); }
    }
    public override void _Input(InputEvent @event)
    {
        if (Engine.IsEditorHint() || _closing || _chatPanel is null || @event is not InputEventKey key) return;
        // LineEdit consumes Escape during GUI dispatch, so handle the intended
        // conversation cancellation before it merely releases text-field focus.
        if (_chatPanel.Visible && _modal is null && key.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            if (key.Pressed && !key.Echo) ReturnToStory();
            return;
        }
        if (_chatPanel.Visible || _modal is not null || _advance?.HasFocus() != true || key.Keycode is not (Key.Space or Key.Enter)) return;
        // The focused Button and global reveal shortcut can otherwise both act on
        // the same physical press. Own both edges here, before GUI dispatch.
        GetViewport().SetInputAsHandled();
        if (key.Pressed && !key.Echo) Advance();
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Engine.IsEditorHint()) return;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (_stagePreview && key.Keycode == Key.F10) { _previewIndex++; ShowStagePreview(); return; }
        if (OS.IsDebugBuild() && key.Keycode == Key.F12)
        {
            var folder = ProjectSettings.GlobalizePath("res://artifacts/captures"); Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, (_started ? _session.Scene.Id : "title") + ".png");
            GetViewport().GetTexture().GetImage().SavePng(path); _status.Text = "Development screenshot captured."; return;
        }
        if (key.Keycode == Key.Escape) { if (_modal is not null) _modal.EmitSignal(Window.SignalName.CloseRequested); else if (_chatPanel.Visible) ReturnToStory(); return; }
        if (_chatPanel.Visible || _modal is not null) return;
        if (key.Keycode is Key.Space or Key.Enter)
        {
            // Focused controls own keyboard activation through GUI dispatch.
            // Continue is handled once in _Input; other controls must not also
            // advance the story on key-down before their key-up activation.
            if (GetViewport().GuiGetFocusOwner() is null) Advance();
        }
        else if (key.Keycode == Key.H) ShowHistory();
        else if (key.Keycode == Key.E) ShowEvidence();
    }
    private void OpenConversation()
    {
        if (_session.Beat.Exchange is not null) { ShowAuthoredExchange(); return; }
        if (!_started || _busy || _closing || _chatPanel.Visible || _modal is not null || _session.Beat.Conversation is not { } chat) return;
        _conversationReading = new(_session, _session.Beat.Id, _generation, _speaker.Text, _dialogue.Text,
            _dialogue.VisibleCharacters, _characters, _status.Text);
        var generation = _generation;
        foreach (var child in _suggestions.GetChildren()) { _suggestions.RemoveChild(child); child.QueueFree(); }
        foreach (var suggestion in chat.Suggestions)
        {
            var captured = suggestion;
            var choice = ChoiceButton(captured, () =>
            {
                if (generation != _generation || !_chatPanel.Visible || _closing) return;
                _entry.Text = captured; _entry.GrabFocus(); _entry.CaretColumn = _entry.Text.Length;
            });
            _suggestions.AddChild(choice); _readingText.Register(choice);
        }
        _chatPanel.Visible = true; _entry.Text = ""; _entry.GrabFocus();
        _status.Text = "Talk is optional. Authored fallback is used if local Pumas is unavailable.";
    }
    private void CloseConversation()
    {
        var reading = _conversationReading;
        _conversationReading = null;
        // Load restores the same session object; the saved view also belongs to a beat and generation.
        var restore = _chatPanel.Visible && reading is not null && reading.Session == _session &&
            reading.BeatId == _session.Beat.Id && reading.Generation == _generation && !_closing;
        _generation++; _request?.Cancel(); _speechRequest?.Cancel(); _speech.Dispose(); _busy = false; _chatPanel.Visible = false; _mic.Text = "Use voice"; _mic.Disabled = false; _send.Disabled = false; _advance.Disabled = false; _entry.Editable = true; _advance.GrabFocus();
        if (restore)
        {
            _speaker.Text = reading!.Speaker; _dialogue.Text = reading.Text;
            _dialogue.VisibleCharacters = reading.VisibleCharacters; _characters = reading.Characters;
            _status.Text = reading.Status;
        }
    }
    private void SendReply() { if (_chatPanel.Visible && !_busy && !_closing && !_speech.Recording) _operations.Track(SendReplyAsync()); }
    private async Task SendReplyAsync()
    {
        if (!_chatPanel.Visible || _closing || _busy || _speech.Recording || string.IsNullOrWhiteSpace(_entry.Text) || _session.Beat.Conversation is not { } chat) return;
        var input = _entry.Text.Trim(); var generation = _generation;
        _busy = true; _entry.Editable = false; _send.Disabled = true; _advance.Disabled = true; _status.Text = "Listening for a reply from local Pumas…";
        _request?.Dispose(); _request = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            _pumas ??= new PumasClient();
            var reply = await _pumas.GenerateAsync(chat.CharacterId, _session.ConversationContext(), input, _request.Token);
            if (generation != _generation || !IsInsideTree()) return;
            var text = reply.Success ? reply.Text : chat.Fallback;
            _session.RecordConversation(input, text, reply.Success);
            _speaker.Text = DisplayName(chat.CharacterId); _dialogue.Text = text; _dialogue.VisibleCharacters = -1;
            _status.Text = reply.Success ? "Local Pumas conversation · optional interpretation, not canonical evidence" : "Authored reply · local Pumas unavailable (" + reply.ErrorCode + ")";
            _entry.Text = ""; Save(true);
        }
        catch (OperationCanceledException) { if (generation == _generation) _status.Text = "Conversation cancelled. You can retry or continue."; }
        catch (Exception error)
        {
            if (generation == _generation)
            {
                _session.RecordConversation(input, chat.Fallback, false); _speaker.Text = DisplayName(chat.CharacterId);
                _dialogue.Text = chat.Fallback; _dialogue.VisibleCharacters = -1;
                _status.Text = "Authored reply · local conversation configuration unavailable";
                GD.PushWarning("Conversation configuration: " + error.GetType().Name); Save(true);
            }
        }
        finally { if (generation == _generation) { _busy = false; _entry.Editable = true; _send.Disabled = false; _advance.Disabled = false; } }
    }
    private void ToggleMicrophone() { if (!_busy && !_closing) _operations.Track(ToggleMicrophoneAsync()); }
    private async Task ToggleMicrophoneAsync()
    {
        if (_busy) return;
        if (!_speech.Recording)
        {
            if (!_speech.Available) { ShowWindow("Cohere Transcribe via Pumas", _speech.Capability.Message); return; }
            var consent = MicrophoneConsentScene.Instantiate<ConfirmationDialog>();
            _mic.Disabled = true; AddChild(consent);
            consent.Confirmed += () => { try { _speech.Start(this); _recordSeconds = 0; _send.Disabled = true; _entry.Editable = false; } catch (Exception e) { _status.Text = e.Message; } _mic.Disabled = false; consent.QueueFree(); };
            consent.Canceled += () => { _mic.Disabled = false; consent.QueueFree(); }; consent.PopupCentered(); return;
        }
        _busy = true; _mic.Disabled = true; _status.Text = "Transcribing with Cohere through Pumas…";
        _speechRequest?.Dispose(); _speechRequest = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var generation = _generation;
        try { var text = await _speech.StopAndTranscribe(_speechRequest.Token); if (generation == _generation) { _entry.Text = text; _entry.GrabFocus(); _status.Text = "Review and edit your transcript, then choose Say this."; } }
        catch (Exception error) { if (generation == _generation) _status.Text = error.Message; }
        finally { if (generation == _generation) { _busy = false; _mic.Disabled = false; _mic.Text = "Use voice"; _send.Disabled = false; _entry.Editable = true; } }
    }
    private void Save(bool auto)
    {
        if (!_started) return;
        if (!_storage!.CanUseSaves) { if (!auto) _status.Text = "AUTHOR PREVIEW · saving and loading player slots is disabled."; return; }
        try { _storage.Write(auto, _session.Snapshot()); if (!auto) _status.Text = "Saved on this device."; }
        catch (Exception error) { _status.Text = "Could not save: " + error.Message; }
    }
    private void ShowLoad()
    {
        if (!_storage!.CanUseSaves) { _status.Text = "AUTHOR PREVIEW · saving and loading player slots is disabled."; return; }
        var candidates = new[] { _storage.Inspect(false, false), _storage.Inspect(false, true), _storage.Inspect(true, false), _storage.Inspect(true, true) };
        var actions = candidates.Where(c => c.Availability == SaveAvailability.Available).Select(candidate =>
            ((candidate.Previous ? "Recover previous " : "Load current ") + (candidate.Automatic ? "autosave" : "manual save") + " · " + candidate.Snapshot!.BeatId,
                (Action)(() => LoadCandidate(candidate)))).ToArray();
        ShowWindow("Load or recover a watch", "Choose the exact snapshot to replace your current in-memory progress. Closing leaves progress unchanged. Recovery is never automatic. Loading does not rewrite save files; subsequent saves may update them.\n\n" +
            string.Join("\n\n", candidates.Select(c => c.Description)), actions);
    }
    private void Load(bool auto)
    {
        try { var snapshot = _storage!.Read(auto); _session.Restore(snapshot); CloseConversation(); CloseModal(); _started = true; _lastScene = ""; RenderBeat(false); }
        catch (Exception error) { _status.Text = "Could not load: " + error.Message; CloseModal(); }
    }
    private void LoadCandidate(SaveCandidate candidate)
    {
        if (!_storage!.CanUseSaves) { _status.Text = "AUTHOR PREVIEW · saving and loading player slots is disabled."; return; }
        try
        {
            // Capture the inspected snapshot, not a possibly changed file at click time.
            var snapshot = candidate.Snapshot ?? throw new InvalidDataException("This snapshot is unavailable.");
            _session.Restore(snapshot); CloseConversation(); CloseModal(); _started = true; _lastScene = ""; RenderBeat(false);
            if (candidate.Previous) _status.Text = "Recovered the selected previous snapshot into this session. Save files were not rewritten.";
        }
        catch (Exception error) { _status.Text = "Could not load: " + error.Message; CloseModal(); }
    }

    private string HistoryText() => string.Join("\n\n", _session.History.Select(h => (DisplayName(h.Speaker) is { Length: > 0 } name ? name + (h.Generated ? " [optional local dialogue]" : "") + ":\n" : "") + h.Text));
    private string EvidenceText() => "OBJECTS\n\n" + string.Join("\n\n", _session.Inventory.Select(i => i.Name + "\n" + i.Description)) + "\n\nESTABLISHED FACTS\n\n" + string.Join("\n\n", _session.KnownFacts.Select(f => f.Text));
    private void ShowHistory() => ShowWindow("The record", HistoryText());
    private void ShowEvidence() => ShowWindow("Your catalogue", EvidenceText());
    private void ShowWindow(string title, string text, (string Text, Action Action)[]? actions = null, Action? dismiss = null)
    {
        if (_busy) return;
        CloseModal();
        _modal = ModalScene.Instantiate<Window>();
        _modal.Title = title;
        AddChild(_modal);
        var window = _modal;
        void Dismiss()
        {
            // A queued close/input callback belongs only to the window that raised it.
            if (_modal != window || _closing) return;
            if (dismiss is null) CloseModal(); else dismiss();
        }
        _modal.CloseRequested += Dismiss;
        _modal.WindowInput += input => { if (input is InputEventKey { Pressed: true, Keycode: Key.Escape }) Dismiss(); };
        var prose = _modal.GetNode<RichTextLabel>("%ModalText");
        prose.Text = text;
        _readingText.Register(prose);
        EnableKeyboardReading(prose);
        var actionRows = _modal.GetNode<VBoxContainer>("%ModalActions");
        _modal.GetNode<ScrollContainer>("%ModalActionsScroll").Visible = actions is { Length: > 0 };
        if (actions is not null)
            foreach (var action in actions)
            {
                var choice = ChoiceButton(action.Text, action.Action);
                actionRows.AddChild(choice); _readingText.Register(choice);
            }
        window.SizeChanged += () => FitModalActions(window);
        actionRows.MinimumSizeChanged += () => FitModalActions(window);
        FitModalActions(window);
        Callable.From(() => FitModalActions(window)).CallDeferred();
        _modal.GetNode<Button>("%ModalCloseButton").Pressed += Dismiss;
        _modal.PopupCentered();
    }
    private void CloseModal() { _exchangeReading = null; if (_modal is not null) { _modal.Hide(); _modal.Exclusive = false; _modal.QueueFree(); _modal = null; _advance.GrabFocus(); } }
    private void ShowStagePreview()
    {
        var locations = new[] { "harbor", "keeper_house", "archive", "lantern_room", "tide_cave" };
        var location = locations[_previewIndex % locations.Length];
        CloseConversation(); CloseModal(); _session = new StorySession(_story);
        while (_session.Scene.Location != location && !_session.IsEnding)
        {
            if (_session.Beat.Activity is { } activity) _session.AnswerActivity(activity.CorrectIndex);
            _session.Advance();
        }
        _started = true; _lastScene = ""; RenderBeat(false); _dialogue.VisibleCharacters = -1;
        _status.Text = "AUTHOR PREVIEW · F10 next stage · F12 screenshot · player save/load disabled";
    }

    private void RunSmoke()
    {
        var count = 1;
        while (!_session.IsEnding) { if (_session.Beat.Activity is { } activity) _session.AnswerActivity(activity.CorrectIndex); if (!_session.Advance()) throw new InvalidOperationException("Timeline blocked."); count++; }
        if (_session.Beat.Activity is { } finalActivity) _session.AnswerActivity(finalActivity.CorrectIndex);
        if (!_session.CanAdvance) throw new InvalidOperationException("Final activity is not solved.");
        _storage!.Write(false, _session.Snapshot()); var restored = new StorySession(_story); restored.Restore(_storage.Read(false));
        if (!restored.IsEnding) throw new InvalidOperationException("Smoke restore failed.");
        foreach (var location in new[] { "harbor", "keeper_house", "archive", "lantern_room", "tide_cave" }) _stage.ShowLocation(location, "night", _story.Characters.Select(c => c.Id).Take(2).ToArray());
        AuthoringSmoke.Run(this, _stage, InterfaceRoot);
        GD.Print($"LANTERNWAKE_SMOKE_OK beats={count} final={restored.Beat.Id}"); QuitAfterAudio();
    }
    private async void RunUiSmoke()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            void Check(bool condition, string claim) { if (!condition) throw new InvalidOperationException("UI smoke: " + claim); }
            Advance(); Check(_started && _dialogue.Text == _session.Beat.Text, "title starts authored story");
            var first = _session.Beat.Id; _dialogue.VisibleCharacters = 0; Advance();
            Check(_session.Beat.Id == first && _dialogue.VisibleCharacters == -1, "first advance reveals text only");
            ShowHistory(); Check(_modal is not null, "history opens"); CloseModal(); Check(_advance.HasFocus(), "modal close restores keyboard advance focus");
            ShowEvidence(); Check(_modal is not null, "catalogue opens"); CloseModal();
            ShowSettings(); Check(_modal is not null, "settings opens"); CloseModal();
            await RunReadingSizeSmoke();
            while (_session.Beat.Conversation is null && !_session.IsEnding)
            {
                if (_session.Beat.Activity is { } activity) _session.AnswerActivity(activity.CorrectIndex);
                _session.Advance();
            }
            RenderBeat(); OpenConversation(); Check(_chatPanel.Visible, "conversation opens");
            await CheckReadingChatSizing();
            ToggleMicrophone(); await _operations.DrainAsync();
            Check(_modal is not null && !_speech.Recording && !_busy && _entry.Editable,
                "unsupported Pumas/Cohere voice explains setup without capture or blocking typing");
            CloseModal();
            var suggestion = _suggestions.GetChildren().OfType<Button>().First(); suggestion.EmitSignal(BaseButton.SignalName.Pressed);
            Check(_entry.Text == suggestion.Text && _entry.Editable, "suggestions are editable");
            var livePreview = OS.GetCmdlineUserArgs().Contains("--live-ui-preview");
            var testInput = livePreview ? "What should I know about staying on the island?" : "A headless test reply.";
            var conversationBeat = _session.Beat.Id;
            _entry.Text = testInput; SendReply(); await _operations.DrainAsync();
            Check(_session.History.Any(h => h.Text == testInput), "reply reaches history");
            if (livePreview)
            {
                Check(_session.History.Last().Generated, "real Pumas reply required; authored fallback does not pass");
                Check(_session.Beat.Id == conversationBeat, "live dialogue cannot advance canonical story");
                CloseConversation();
                GD.Print("LANTERNWAKE_LIVE_UI_OK " + _session.History.Last().Text);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (DisplayServer.GetName() != "headless")
                {
                    var folder = ProjectSettings.GlobalizePath("res://artifacts/captures"); Directory.CreateDirectory(folder);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder, "live-pumas.png"));
                }
                return;
            }
            CloseConversation(); OpenConversation(); Check(_entry.Text == "" && _entry.Editable && !_busy, "reopening starts clean"); CloseConversation();
            Save(false); var savedBeat = _session.Beat.Id; _session.Advance(); Load(false);
            Check(_session.Beat.Id == savedBeat, "manual load restores position");
            _session.Advance(); Save(false); var newer = _session.Snapshot();
            ShowLoad();
            Check(_modal!.GetNode<RichTextLabel>("%ModalText").Text.Contains(savedBeat), "recovery menu describes previous snapshot");
            var recover = _modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text.StartsWith("Recover previous manual save"));
            Check(recover.Text.Contains(savedBeat), "recovery action identifies the exact beat");
            // Simulate an external replacement after inspection; choose exactly what was shown.
            var previousPath = Path.Combine(_storage!.OwnedTestDirectory!, "save.previous.json");
            SaveStore.Write(previousPath, newer);
            var primaryBytes = System.IO.File.ReadAllBytes(Path.Combine(_storage.OwnedTestDirectory!, "save.json"));
            var previousBytes = System.IO.File.ReadAllBytes(previousPath);
            recover.EmitSignal(Button.SignalName.Pressed);
            Check(_session.Beat.Id == savedBeat && _modal is null, "explicit recovery loads the displayed snapshot, not changed disk content");
            Check(System.IO.File.ReadAllBytes(previousPath).SequenceEqual(previousBytes) && System.IO.File.ReadAllBytes(Path.Combine(_storage.OwnedTestDirectory!, "save.json")).SequenceEqual(primaryBytes), "recovery does not rewrite either save file");
            ShowLoad(); CloseModal(); Check(_session.Beat.Id == savedBeat, "closing recovery chooser leaves progress unchanged");
            GD.Print("LANTERNWAKE_RECOVERY_UI_OK explicit displayed snapshot; no slot rewrite; cancel preserves progress");
            while (_session.Beat.Activity is null && !_session.IsEnding) _session.Advance();
            RenderBeat(); var gated = _session.Beat.Id; _dialogue.VisibleCharacters = -1; Advance();
            Check(_session.Beat.Id == gated && _modal is not null, "activity opens instead of advancing"); CloseModal();
            GD.Print("LANTERNWAKE_UI_SMOKE_OK title reveal history catalogue settings suggestion reply reopen save load activity");
            ShowSettings();
            _modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == "Quit game").EmitSignal(Button.SignalName.Pressed);
        }
        catch (Exception error) { GD.PushError(error.ToString()); QuitAfterAudio(1); }
    }

    public override async void _Notification(int what)
    {
        if (Engine.IsEditorHint()) return;
        if (what != NotificationWMCloseRequest || _closing) return;
        _closing = true; _generation++; _request?.Cancel(); _speechRequest?.Cancel(); _speech.Dispose();
        try { await _operations.DrainAsync(); }
        catch (Exception error) { GD.PushWarning("Shutdown operation: " + error.Message); }
        finally { _pumas?.Dispose(); _pumas = null; QuitAfterAudio(); }
    }
    public override void _ExitTree() { if (Engine.IsEditorHint()) return; _generation++; _request?.Cancel(); _request?.Dispose(); _speechRequest?.Cancel(); _speech.Dispose(); _storage?.Dispose(); }
}
