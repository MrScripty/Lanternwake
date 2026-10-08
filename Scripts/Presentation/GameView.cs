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
    private Label _chapter = null!;
    private Label? _place, _speaker, _status;
    private string _placeText = "", _speakerText = "", _statusText = "";
    private RichTextLabel _dialogue = null!;
    private VBoxContainer _suggestions = null!;
    private PanelContainer _chatPanel = null!;
    private LineEdit _entry = null!;
    private Button _advance = null!, _talk = null!, _mic = null!, _send = null!;
    private Button _titleContentNote = null!;
    private Window? _modal;
    private readonly SpeechRecorder _speech = new();
    private CancellationTokenSource? _request, _speechRequest;
    private readonly OwnedOperations _operations = new();
    private bool _closing, _previewMode, _stagePreview;
    private int _previewIndex;
    private bool _busy, _started, _instant;
    private double _characters, _recordSeconds;
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
            LoadAiSettings(launch.Mode);
            if (launch.BeatId is { } beatId) _session = StoryContextPreview.CreateSessionAtBeat(_story, beatId);
            BindInterface(); ShowTitle();
            if (_stagePreview) ShowStagePreview();
            else if (launch.BeatId is not null) { _started = true; RenderBeat(false); }
            if (!_previewMode && launch.Check is null or "--ui-smoke" or "--live-ui-preview") ShowMainMenu();
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
        _place = InterfaceRoot.GetNodeOrNull<Label>("%PlaceLabel");
        _speaker = InterfaceRoot.GetNodeOrNull<Label>("%SpeakerLabel");
        _dialogue = InterfaceRoot.GetNode<RichTextLabel>("%DialogueText");
        _status = InterfaceRoot.GetNodeOrNull<Label>("%StatusLabel");
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
    // These informational labels may be removed from an authored interface.
    private void SetPlaceText(string text)
    {
        _placeText = text;
        if (_place is not null) _place.Text = text;
    }
    private void SetSpeakerText(string text)
    {
        _speakerText = text;
        if (_speaker is not null) _speaker.Text = text;
    }
    private void SetStatusText(string text)
    {
        _statusText = text;
        if (_status is not null) _status.Text = text;
        else if (_chatPanel is not null && _chatPanel.Visible)
        {
            var note = _chatPanel.GetNodeOrNull<Label>("Margin/Stack/Note");
            if (note is not null) note.Text = text;
        }
    }
    public string CurrentPlaceText => _place?.Text ?? _placeText;
    public string CurrentSpeakerText => GetSpeakerText();
    public string CurrentStatusText => GetStatusText();
    private string GetSpeakerText() => _speaker?.Text ?? _speakerText;
    private string GetStatusText() => _status?.Text ?? _statusText;
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
        Audio.ShowTitleMusic();
    }
    private void Advance()
    {
        if (_busy || MainMenuVisible || _chatPanel.Visible || _modal is not null) return;
        if (!_started) { _started = true; RenderBeat(); return; }
        if (_dialogue.VisibleCharacters >= 0 && _dialogue.VisibleCharacters < _dialogue.GetTotalCharacterCount()) { _dialogue.VisibleCharacters = -1; return; }
        if (!_session.CanAdvance) { ShowActivity(); return; }
        if (_session.Advance()) { RenderBeat(); Save(true); }
        else ShowCompletion();
    }
    private void RenderBeat(bool playAudioCue = true)
    {
        if (MainMenuVisible) HideMainMenu();
        _generation++;
        _titleContentNote.Visible = false;
        var scene = _session.Scene;
        Audio.ShowLocation(scene.Location);
        Audio.ApplyBeatCue(_session.Beat.Id, _session.Beat.StageCue, playAudioCue);
        var beatIndex = Array.FindIndex(scene.Beats, beat => beat.Id == _session.Beat.Id);
        var reachedBeatIds = scene.Beats.Take(beatIndex + 1).Select(beat => beat.Id).ToArray();
        Audio.ApplyStoryMusic(scene.Id, scene.Location, _session.Chapter.Id, reachedBeatIds);
        _stage.ShowLocation(scene.Location, scene.TimeOfDay, scene.CharacterIds, scene.Id, reachedBeatIds);
        _stage.ApplyAuthoredCues(_session.ActiveStageCues, _session.Beat.StageCue, _session.Beat.Id, playAudioCue);
        _stage.ApplyPerformance(scene.Id, _session.Beat.Id, _session.Beat.Speaker);
        _chapter.Text = (_previewMode ? "AUTHOR PREVIEW · " : "") + _session.Chapter.Title.ToUpperInvariant();
        SetPlaceText(scene.Title + "  ·  " + scene.TimeOfDay.Replace('_', ' '));
        SetSpeakerText(DisplayName(_session.Beat.Speaker));
        _dialogue.Text = _session.Beat.Text; _characters = 0; _dialogue.VisibleCharacters = _instant ? -1 : 0;
        _talk.Visible = _session.Beat.Conversation is not null || _session.Beat.Exchange is not null;
        _talk.Text = _session.Beat.Exchange is { } exchange ? "Speak with " + DisplayName(exchange.CharacterId) : "Stay and talk";
        _advance.Text = !_session.CanAdvance ? "Examine evidence  ›" : _session.IsEnding ? "Finish  ›" : "Continue  ›";
        SetStatusText((_previewMode ? "AUTHOR PREVIEW · player saves disabled · " : "") + $"{_session.Progress:P0} · Space / Enter to continue · E evidence · H history");
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
            GetViewport().GetTexture().GetImage().SavePng(path); SetStatusText("Development screenshot captured."); return;
        }
        if (key.Keycode == Key.Escape) { if (_modal is not null) _modal.EmitSignal(Window.SignalName.CloseRequested); else if (_chatPanel.Visible) ReturnToStory(); else if (!MainMenuVisible) ShowMainMenu(); return; }
        if (MainMenuVisible) return;
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
        _conversationReading = new(_session, _session.Beat.Id, _generation, GetSpeakerText(), _dialogue.Text,
            _dialogue.VisibleCharacters, _characters, GetStatusText());
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
        SetStatusText("Talk is optional. Authored fallback is used if local Pumas is unavailable.");
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
            SetSpeakerText(reading!.Speaker); _dialogue.Text = reading.Text;
            _dialogue.VisibleCharacters = reading.VisibleCharacters; _characters = reading.Characters;
            SetStatusText(reading.Status);
        }
    }
    private void SendReply() { if (_chatPanel.Visible && !_busy && !_closing && !_speech.Recording) _operations.Track(SendReplyAsync()); }
    private async Task SendReplyAsync()
    {
        if (!_chatPanel.Visible || _closing || _busy || _speech.Recording || string.IsNullOrWhiteSpace(_entry.Text) || _session.Beat.Conversation is not { } chat) return;
        var input = _entry.Text.Trim(); var generation = _generation;
        _busy = true; _entry.Editable = false; _send.Disabled = true; _advance.Disabled = true; SetStatusText("Waiting for a reply from " + _aiSettings.Provider + "…");
        _request?.Dispose(); _request = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            if (_aiSettings.Provider == DialogueProvider.OpenRouter)
                await _credentialLoad.WaitAsync(_request.Token);
            _request.Token.ThrowIfCancellationRequested();
            if (generation != _generation || !IsInsideTree()) return;
            var reply = await GenerateWithSettings(_aiSettings, _openRouterKey, chat.CharacterId, _session.ConversationContext(), input, _request.Token);
            if (generation != _generation || !IsInsideTree()) return;
            var text = reply.Success ? reply.Text : chat.Fallback;
            _session.RecordConversation(input, text, reply.Success);
            SetSpeakerText(DisplayName(chat.CharacterId)); _dialogue.Text = text; _dialogue.VisibleCharacters = -1;
            SetStatusText(reply.Success ? _aiSettings.Provider + " conversation · optional interpretation, not canonical evidence" : "Authored reply · " + AiError(reply.ErrorCode));
            _entry.Text = ""; Save(true);
        }
        catch (OperationCanceledException) { if (generation == _generation) SetStatusText("Conversation cancelled. You can retry or continue."); }
        catch (Exception error)
        {
            if (generation == _generation)
            {
                _session.RecordConversation(input, chat.Fallback, false); SetSpeakerText(DisplayName(chat.CharacterId));
                _dialogue.Text = chat.Fallback; _dialogue.VisibleCharacters = -1;
                SetStatusText("Authored reply · local conversation configuration unavailable");
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
            consent.Confirmed += () => { try { _speech.Start(this); _recordSeconds = 0; _send.Disabled = true; _entry.Editable = false; } catch (Exception e) { SetStatusText(e.Message); } _mic.Disabled = false; consent.QueueFree(); };
            consent.Canceled += () => { _mic.Disabled = false; consent.QueueFree(); }; consent.PopupCentered(); return;
        }
        _busy = true; _mic.Disabled = true; SetStatusText("Transcribing with Cohere through Pumas…");
        _speechRequest?.Dispose(); _speechRequest = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var generation = _generation;
        try { var text = await _speech.StopAndTranscribe(_speechRequest.Token); if (generation == _generation) { _entry.Text = text; _entry.GrabFocus(); SetStatusText("Review and edit your transcript, then choose Say this."); } }
        catch (Exception error) { if (generation == _generation) SetStatusText(error.Message); }
        finally { if (generation == _generation) { _busy = false; _mic.Disabled = false; _mic.Text = "Use voice"; _send.Disabled = false; _entry.Editable = true; } }
    }
    private bool Save(bool auto)
    {
        if (!_started) return false;
        if (!_storage!.CanUseSaves) { if (!auto) SetStatusText("AUTHOR PREVIEW · saving and loading player slots is disabled."); return auto; }
        try { _storage.Write(auto, _session.Snapshot()); if (!auto) SetStatusText("Saved on this device."); return true; }
        catch (Exception error) { SetStatusText("Could not save: " + error.Message); return false; }
    }
    private void ShowLoad()
    {
        if (!_storage!.CanUseSaves) { SetStatusText("AUTHOR PREVIEW · saving and loading player slots is disabled."); return; }
        var candidates = new[] { _storage.Inspect(false, false), _storage.Inspect(false, true), _storage.Inspect(true, false), _storage.Inspect(true, true) };
        var actions = candidates.Where(c => c.Availability == SaveAvailability.Available).Select(candidate =>
            ((candidate.Previous ? "Recover previous " : "Load current ") + (candidate.Automatic ? "autosave" : "manual save") + " · " + candidate.Snapshot!.BeatId,
                (Action)(() => LoadCandidate(candidate)))).ToArray();
        ShowWindow("Load or recover a watch", "Choose the exact snapshot to replace your current in-memory progress. Closing leaves progress unchanged. Recovery is never automatic. Loading does not rewrite save files; subsequent saves may update them.\n\n" +
            string.Join("\n\n", candidates.Select(c => c.Description)), actions);
    }
    private void Load(bool auto)
    {
        try { var snapshot = _storage!.Read(auto); _session.Restore(snapshot); CloseConversation(); CloseModal(); _started = true; RenderBeat(false); }
        catch (Exception error) { SetStatusText("Could not load: " + error.Message); CloseModal(); }
    }
    private void LoadCandidate(SaveCandidate candidate)
    {
        if (!_storage!.CanUseSaves) { SetStatusText("AUTHOR PREVIEW · saving and loading player slots is disabled."); return; }
        try
        {
            // Capture the inspected snapshot, not a possibly changed file at click time.
            var snapshot = candidate.Snapshot ?? throw new InvalidDataException("This snapshot is unavailable.");
            _session.Restore(snapshot); CloseConversation(); CloseModal(); _started = true; RenderBeat(false);
            if (candidate.Previous) SetStatusText("Recovered the selected previous snapshot into this session. Save files were not rewritten.");
        }
        catch (Exception error) { SetStatusText("Could not load: " + error.Message); CloseModal(); }
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
    private void CloseModal()
    {
        CancelAiSetup(); _setupRequest?.Cancel(); _exchangeReading = null;
        if (_modal is not { } modal) return;
        _modal = null;
        modal.Hide(); modal.Exclusive = false; modal.QueueFree();
        if (MainMenuVisible) FocusMainMenu(); else if (_chatPanel.Visible) _entry.GrabFocus(); else _advance.GrabFocus();
    }
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
        _started = true; RenderBeat(false); _dialogue.VisibleCharacters = -1;
        SetStatusText("AUTHOR PREVIEW · F10 next stage · F12 screenshot · player save/load disabled");
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
            Check(MainMenuVisible && !InterfaceRoot.Visible && !_started, "startup opens menu before the story");
            _Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            Check(!_started, "unhandled Enter cannot advance story behind the menu");
            _mainMenu!.GetNode<Button>("%MenuAiSetup").EmitSignal(BaseButton.SignalName.Pressed);
            Check(_modal?.Title == "AI setup", "menu opens AI configuration and speech availability");
            var aiForm = _modal!.GetNode<VBoxContainer>("%ModalActions").GetNode<VBoxContainer>("AiSettingsControls");
            var aiDialogue = aiForm.GetNode<VBoxContainer>("Capabilities/Dialogue");
            var aiProvider = aiDialogue.GetNode<OptionButton>("Provider");
            var originalAiSettings = _aiSettings;
            var aiTranscription = aiForm.GetNode<VBoxContainer>("Capabilities/Transcription");
            var aiVoices = aiForm.GetNode<VBoxContainer>("Capabilities/Voices");
            Check(aiDialogue.GetNode<LineEdit>("ApiKey").Secret && aiDialogue.GetNode<LineEdit>("ApiKey").Text.Length == 0,
                "credential field is masked and automated checks do not load user secrets");
            aiTranscription.GetNode<LineEdit>("Endpoint").Text = "http://127.0.0.1:8081/";
            aiTranscription.GetNode<CheckButton>("Enabled").SetPressedNoSignal(true);
            aiVoices.GetNode<LineEdit>("Endpoint").Text = "http://127.0.0.1:8082/";
            aiVoices.GetNode<CheckButton>("Enabled").ButtonPressed = true;
            Check(aiTranscription.GetNode<OptionButton>("Model").Disabled && aiVoices.GetNode<OptionButton>("Model").Disabled &&
                aiVoices.GetNode<VBoxContainer>("Assignments").Visible &&
                aiVoices.GetNode<VBoxContainer>("Assignments").GetChildCount() == _story.Characters.Length * 2,
                "speech models stay unavailable and enabling voice preferences exposes character assignments");
            var transcriptionProvider = aiTranscription.GetNode<OptionButton>("Provider");
            transcriptionProvider.Select((int)DialogueProvider.OpenRouter);
            transcriptionProvider.EmitSignal(OptionButton.SignalName.ItemSelected, (long)DialogueProvider.OpenRouter);
            Check(aiTranscription.GetNode<LineEdit>("Endpoint").Text == AiSettings.DefaultEndpoint(DialogueProvider.OpenRouter) &&
                !aiTranscription.GetNode<LineEdit>("Endpoint").Editable && aiTranscription.GetNode<Button>("ManageKey").Visible &&
                aiDialogue.GetNode<LineEdit>("ApiKey").Visible && aiProvider.GetSelectedId() == (int)DialogueProvider.Pumas &&
                aiVoices.GetNode<LineEdit>("Endpoint").Text == "http://127.0.0.1:8082/",
                "transcription has an independent hosted provider and can manage the shared key with local dialogue");
            aiTranscription.GetNode<Button>("ManageKey").EmitSignal(BaseButton.SignalName.Pressed);
            Check(aiForm.GetNode<TabContainer>("Capabilities").CurrentTab == 0 && aiDialogue.GetNode<LineEdit>("ApiKey").HasFocus(),
                "speech credential action focuses the shared masked key");
            transcriptionProvider.Select((int)DialogueProvider.Pumas);
            transcriptionProvider.EmitSignal(OptionButton.SignalName.ItemSelected, (long)DialogueProvider.Pumas);
            Check(aiTranscription.GetNode<LineEdit>("Endpoint").Text == "http://127.0.0.1:8081/" &&
                aiTranscription.GetNode<LineEdit>("Endpoint").Editable,
                "switching transcription providers restores the edited Pumas URL");
            aiProvider.Select((int)DialogueProvider.OpenRouter); aiProvider.EmitSignal(OptionButton.SignalName.ItemSelected, (long)DialogueProvider.OpenRouter);
            Check(aiDialogue.GetNode<LineEdit>("Endpoint").Text == AiSettings.DefaultEndpoint(DialogueProvider.OpenRouter) && aiDialogue.GetNode<LineEdit>("ApiKey").Visible, "OpenRouter offers its endpoint and masked key");
            aiProvider.Select((int)DialogueProvider.Pumas); aiProvider.EmitSignal(OptionButton.SignalName.ItemSelected, (long)DialogueProvider.Pumas);
            var aiModels = aiDialogue.GetNode<OptionButton>("Model");
            aiModels.AddItem("Fixture model supplied by catalog");
            aiModels.SetItemMetadata(aiModels.ItemCount - 1, "fixture-dialogue"); aiModels.Select(aiModels.ItemCount - 1);
            aiDialogue.GetNode<CheckButton>("DialogueEnabled").SetPressedNoSignal(true);
            ((Button)_modal!.FindChild("SaveConfiguration", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await _operations.DrainAsync();
            Check(AiSettings.Load(_aiSettingsPath, originalAiSettings).Model == "fixture-dialogue", "selected model persists through real settings action");
            var savedAi = AiSettings.Load(_aiSettingsPath, originalAiSettings);
            Check(savedAi.Transcription.Enabled && savedAi.Transcription.Endpoint == "http://127.0.0.1:8081/" &&
                savedAi.CharacterSpeech.Enabled && savedAi.CharacterSpeech.Endpoint == "http://127.0.0.1:8082/",
                "speech preferences save independently through the real settings form");
            CloseModal();
            _mainMenu.GetNode<Button>("%MenuAiSetup").EmitSignal(BaseButton.SignalName.Pressed);
            aiForm = _modal!.GetNode<VBoxContainer>("%ModalActions").GetNode<VBoxContainer>("AiSettingsControls");
            aiDialogue = aiForm.GetNode<VBoxContainer>("Capabilities/Dialogue");
            aiProvider = aiDialogue.GetNode<OptionButton>("Provider");
            aiTranscription = aiForm.GetNode<VBoxContainer>("Capabilities/Transcription");
            aiVoices = aiForm.GetNode<VBoxContainer>("Capabilities/Voices");
            transcriptionProvider = aiTranscription.GetNode<OptionButton>("Provider");
            transcriptionProvider.Select((int)DialogueProvider.OpenRouter);
            transcriptionProvider.EmitSignal(OptionButton.SignalName.ItemSelected, (long)DialogueProvider.OpenRouter);
            var voicesProvider = aiVoices.GetNode<OptionButton>("Provider");
            voicesProvider.Select((int)DialogueProvider.OpenRouter);
            voicesProvider.EmitSignal(OptionButton.SignalName.ItemSelected, (long)DialogueProvider.OpenRouter);
            ((Button)_modal!.FindChild("SaveConfiguration", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await _operations.DrainAsync();
            savedAi = AiSettings.Load(_aiSettingsPath, originalAiSettings);
            Check(savedAi.Provider == DialogueProvider.Pumas && savedAi.Transcription.Provider == DialogueProvider.OpenRouter &&
                savedAi.CharacterSpeech.Provider == DialogueProvider.OpenRouter &&
                savedAi.Transcription.Endpoint == AiSettings.DefaultEndpoint(DialogueProvider.OpenRouter) &&
                savedAi.CharacterSpeech.Endpoint == AiSettings.DefaultEndpoint(DialogueProvider.OpenRouter),
                "both speech providers persist independently through the real save action");
            CloseModal();
            _mainMenu.GetNode<Button>("%MenuAiSetup").EmitSignal(BaseButton.SignalName.Pressed);
            aiForm = _modal!.GetNode<VBoxContainer>("%ModalActions").GetNode<VBoxContainer>("AiSettingsControls");
            aiDialogue = aiForm.GetNode<VBoxContainer>("Capabilities/Dialogue");
            aiProvider = aiDialogue.GetNode<OptionButton>("Provider");
            aiTranscription = aiForm.GetNode<VBoxContainer>("Capabilities/Transcription");
            aiVoices = aiForm.GetNode<VBoxContainer>("Capabilities/Voices");
            Check(aiTranscription.GetNode<OptionButton>("Provider").GetSelectedId() == (int)DialogueProvider.OpenRouter &&
                aiVoices.GetNode<OptionButton>("Provider").GetSelectedId() == (int)DialogueProvider.OpenRouter &&
                aiProvider.GetSelectedId() == (int)DialogueProvider.Pumas && aiDialogue.GetNode<LineEdit>("ApiKey").Visible,
                "reopened speech tabs retain their providers and shared credential access");
            var configurationBytes = File.ReadAllBytes(_aiSettingsPath);
            aiDialogue.GetNode<CheckButton>("DialogueEnabled").SetPressedNoSignal(false);
            aiDialogue.GetNode<LineEdit>("ApiKey").Text = "fixture-key-never-persist-to-file";
            ((Button)_modal!.FindChild("SaveConfiguration", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await _operations.DrainAsync();
            Check(File.ReadAllBytes(_aiSettingsPath).SequenceEqual(configurationBytes) &&
                aiForm.GetNode<Label>("ConnectionStatus").Text.StartsWith("Settings were not saved.", StringComparison.Ordinal) &&
                !aiForm.GetNode<Label>("ConnectionStatus").Text.Contains("fixture-key-never-persist-to-file"),
                "speech OpenRouter selections require secure key storage even when dialogue uses Pumas");
            aiDialogue.GetNode<LineEdit>("ApiKey").Text = "";
            aiProvider.Select((int)DialogueProvider.Pumas); aiProvider.EmitSignal(OptionButton.SignalName.ItemSelected, (long)DialogueProvider.Pumas);
            aiDialogue.GetNode<LineEdit>("Endpoint").Text = "http://example.invalid/";
            ((Button)_modal!.FindChild("SaveConfiguration", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await _operations.DrainAsync();
            Check(File.ReadAllBytes(_aiSettingsPath).SequenceEqual(configurationBytes), "invalid endpoint cannot overwrite configuration");
            _aiSettings = originalAiSettings; originalAiSettings.Save(_aiSettingsPath);
            GD.Print("LANTERNWAKE_AI_SETTINGS_UI_OK independent LLM transcription voices preferences; masked key; keyring failure has no plaintext fallback; test storage only");
            CloseModal();
            Check(_mainMenu.GetNode<Button>("%MenuNewGame").HasFocus(), "closing menu settings restores menu focus");
            _mainMenu.GetNode<Button>("%MenuReading").EmitSignal(BaseButton.SignalName.Pressed);
            Check(_modal?.Title == "Reading settings", "reading settings available before starting"); CloseModal();
            _mainMenu.GetNode<Button>("%MenuSound").EmitSignal(BaseButton.SignalName.Pressed);
            Check(_modal?.Title == "Sound settings", "sound settings available before starting"); CloseModal();
            _mainMenu.GetNode<Button>("%MenuNewGame").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!MainMenuVisible && InterfaceRoot.Visible && _started, "new story enters the game");
            var menuBeat = _session.Beat.Id;
            ShowMainMenu();
            _mainMenu.GetNode<Button>("%MenuContinue").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!MainMenuVisible && _session.Beat.Id == menuBeat, "resume preserves story position");
            GD.Print("LANTERNWAKE_MAIN_MENU_OK startup settings AI availability new story resume keyboard focus");
            _started = false;
            _Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            Check(_started && _dialogue.Text == _session.Beat.Text, "Enter starts authored story with the authored interface");
            _started = false;
            _advance.EmitSignal(BaseButton.SignalName.Pressed);
            Check(_started && _dialogue.Text == _session.Beat.Text, "arrival button starts authored story with the authored interface");
            var first = _session.Beat.Id; _dialogue.VisibleCharacters = 0;
            _Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            Check(_session.Beat.Id == first && _dialogue.VisibleCharacters == -1, "first advance reveals text only");
            _advance.EmitSignal(BaseButton.SignalName.Pressed);
            Check(_session.Beat.Id != first && _dialogue.Text == _session.Beat.Text, "continue button advances and renders the next authored beat");
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
            Check(_entry.HasFocus() && _entry.Editable && !_speech.Recording,
                "unavailable voice modal restores editable conversation focus without recording");
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
        _closing = true; _generation++; _request?.Cancel(); _speechRequest?.Cancel(); _speechRequest?.Dispose(); _setupRequest?.Cancel(); CancelAiSetup(); _speech.Dispose();
        try { await _operations.DrainAsync(); }
        catch (Exception error) { GD.PushWarning("Shutdown operation: " + error.Message); }
        finally { _setupPumas?.Dispose(); _setupPumas = null; QuitAfterAudio(); }
    }
    public override void _ExitTree() { if (Engine.IsEditorHint()) return; _generation++; _request?.Cancel(); _request?.Dispose(); _speechRequest?.Cancel(); _speechRequest?.Dispose(); _setupRequest?.Cancel(); CancelAiSetup(); _credentialLifetime.Cancel(); _aiSetupRequest?.Dispose(); _setupPumas?.Dispose(); _speech.Dispose(); _storage?.Dispose(); }
}
