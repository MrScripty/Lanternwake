using Godot;
using Lanternwake.Core;
using Lanternwake.Conversation;
using System.Text.Json;

namespace Lanternwake.Presentation;

public partial class GameView : Node
{
    private Story _story = null!;
    private StorySession _session = null!;
    private StageDirector _stage = null!;
    private Label _chapter = null!, _speaker = null!, _status = null!, _place = null!;
    private RichTextLabel _dialogue = null!;
    private VBoxContainer _suggestions = null!;
    private PanelContainer _chatPanel = null!;
    private LineEdit _entry = null!;
    private Button _advance = null!, _talk = null!, _mic = null!, _send = null!;
    private Window? _modal;
    private PumasClient? _pumas;
    private readonly SpeechRecorder _speech = new();
    private CancellationTokenSource? _request, _speechRequest;
    private readonly OwnedOperations _operations = new();
    private bool _closing, _previewMode;
    private int _previewIndex;
    private bool _busy, _started, _instant;
    private double _characters, _recordSeconds;
    private string _lastScene = "";
    private int _generation;
    private readonly Color _ink = new("e8e4d8"), _gold = new("deb77d"), _muted = new("9ab0b3");

    public override void _Ready()
    {
        try
        {
            GetTree().AutoAcceptQuit = false;
            _story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            _session = new(_story);
            _stage = new StageDirector(); AddChild(_stage);
            BuildInterface(); ShowTitle();
            _previewMode = OS.IsDebugBuild() && OS.GetCmdlineUserArgs().Contains("--stage-preview");
            if (_previewMode) ShowStagePreview();
            if (OS.GetCmdlineUserArgs().Contains("--smoke")) RunSmoke();
            else if (OS.GetCmdlineUserArgs().Contains("--ui-smoke")) RunUiSmoke();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            var label = new Label { Text = "Lanternwake could not start.\n" + error.Message, Position = new(48, 48) }; AddChild(label);
            if ((OS.GetCmdlineUserArgs().Contains("--smoke") || OS.GetCmdlineUserArgs().Contains("--ui-smoke"))) GetTree().Quit(1);
        }
    }
    private void BuildInterface()
    {
        var layer = new CanvasLayer(); AddChild(layer);
        var root = new Control(); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(root);
        root.Theme = CreateTheme();
        var top = new MarginContainer { OffsetLeft = 38, OffsetTop = 26, OffsetRight = -38, OffsetBottom = 86 };
        top.SetAnchorsPreset(Control.LayoutPreset.TopWide); root.AddChild(top);
        var topRow = new HBoxContainer(); top.AddChild(topRow);
        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; topRow.AddChild(titles);
        _chapter = new Label { Text = "L A N T E R N W A K E" }; _chapter.AddThemeColorOverride("font_color", _gold); titles.AddChild(_chapter);
        _place = new Label(); _place.AddThemeFontSizeOverride("font_size", 15); _place.AddThemeColorOverride("font_color", _muted); titles.AddChild(_place);
        topRow.AddChild(Button("Evidence", ShowEvidence)); topRow.AddChild(Button("History", ShowHistory));
        topRow.AddChild(Button("Save", () => Save(false))); topRow.AddChild(Button("Load", ShowLoad));
        topRow.AddChild(Button("Settings", ShowSettings));
        var bottom = new PanelContainer { OffsetLeft = 38, OffsetRight = -38, OffsetTop = -290, OffsetBottom = -28 };
        bottom.SetAnchorsPreset(Control.LayoutPreset.BottomWide); root.AddChild(bottom);
        var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 28); margin.AddThemeConstantOverride("margin_right", 28); margin.AddThemeConstantOverride("margin_top", 18); margin.AddThemeConstantOverride("margin_bottom", 16); bottom.AddChild(margin);
        var stack = new VBoxContainer(); margin.AddChild(stack);
        _speaker = new Label(); _speaker.AddThemeColorOverride("font_color", _gold); stack.AddChild(_speaker);
        _dialogue = new RichTextLabel { BbcodeEnabled = false, FitContent = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(0, 125), ScrollActive = true }; stack.AddChild(_dialogue);
        var controls = new HBoxContainer(); stack.AddChild(controls);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart }; _status.AddThemeFontSizeOverride("font_size", 14); _status.AddThemeColorOverride("font_color", _muted); controls.AddChild(_status);
        _talk = Button("Stay and talk", OpenConversation); controls.AddChild(_talk);
        _advance = Button("Continue  ›", Advance); controls.AddChild(_advance);
        _chatPanel = new PanelContainer { OffsetLeft = 210, OffsetRight = -210, OffsetTop = 150, OffsetBottom = -320, Visible = false };
        _chatPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _chatPanel.OffsetLeft = 210; _chatPanel.OffsetRight = -210; _chatPanel.OffsetTop = 150; _chatPanel.OffsetBottom = -320; root.AddChild(_chatPanel);
        var chatMargin = new MarginContainer(); foreach (var side in new[] { "left", "right", "top", "bottom" }) chatMargin.AddThemeConstantOverride("margin_" + side, 24); _chatPanel.AddChild(chatMargin);
        var chatStack = new VBoxContainer(); chatMargin.AddChild(chatStack);
        var title = new Label { Text = "A MOMENT BETWEEN THE LINES" }; title.AddThemeColorOverride("font_color", _gold); chatStack.AddChild(title);
        var note = new Label { Text = "Choose a starting thought, then edit it or write your own. Optional talk never changes the main mystery.", AutowrapMode = TextServer.AutowrapMode.WordSmart }; note.AddThemeFontSizeOverride("font_size", 15); chatStack.AddChild(note);
        _suggestions = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; chatStack.AddChild(_suggestions);
        _entry = new LineEdit { PlaceholderText = "What do you want to say?", MaxLength = 1000, CustomMinimumSize = new(0, 48) }; _entry.TextSubmitted += _ => SendReply(); chatStack.AddChild(_entry);
        var chatButtons = new HBoxContainer(); chatStack.AddChild(chatButtons);
        _mic = Button("Use voice", ToggleMicrophone); chatButtons.AddChild(_mic);
        _send = Button("Say this", SendReply); chatButtons.AddChild(_send);
        chatButtons.AddChild(Button("Return to story", CloseConversation));
    }
    private Theme CreateTheme()
    {
        var theme = new Theme { DefaultFontSize = 21 };
        var panel = new StyleBoxFlat { BgColor = new Color(.025f,.055f,.067f,.96f), BorderColor = new Color(.42f,.49f,.47f,.6f), BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1, CornerRadiusTopLeft = 5, CornerRadiusBottomRight = 5 };
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetColor("font_color", "Label", _ink); theme.SetColor("default_color", "RichTextLabel", _ink);
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
        {
            var box = new StyleBoxFlat { BgColor = state == "normal" ? new Color(.08f,.14f,.16f,1) : new Color(.2f,.28f,.28f,1), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, ContentMarginLeft = 15, ContentMarginRight = 15, ContentMarginTop = 10, ContentMarginBottom = 10 };
            if (state == "focus") { box.BorderColor = _gold; box.BorderWidthBottom = 2; }
            theme.SetStylebox(state, "Button", box);
        }
        theme.SetFontSize("font_size", "Button", 16);
        return theme;
    }
    private static Button Button(string text, Action action) { var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.All }; button.Pressed += action; return button; }
    private static Button WrappedButton(string text, Action action)
    {
        var button = Button(text, action); button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        button.CustomMinimumSize = new(0, 48); return button;
    }
    private void ShowTitle()
    {
        _stage.ShowLocation("harbor", "night", []);
        _speaker.Text = "A MYSTERY IN FIVE CHAPTERS";
        _dialogue.Text = "You came to catalogue a dead man's possessions.\nThe first thing you find is tomorrow's weather.\nThe second is your name.";
        _dialogue.VisibleCharacters = -1; _talk.Visible = false; _advance.Text = "Arrive on the island  ›";
        _status.Text = "Original procedural 3D art · authored story · optional local conversation";
    }
    private void Advance()
    {
        if (_busy || _chatPanel.Visible || _modal is not null) return;
        if (!_started) { _started = true; RenderBeat(); return; }
        if (_dialogue.VisibleCharacters >= 0 && _dialogue.VisibleCharacters < _dialogue.GetTotalCharacterCount()) { _dialogue.VisibleCharacters = -1; return; }
        if (!_session.CanAdvance) { ShowActivity(); return; }
        if (_session.Advance()) { RenderBeat(); Save(true); }
        else ShowWindow("The light remains", "You have reached the end of Lanternwake. Your evidence and conversations remain in History.\n\nThank you for keeping watch.");
    }
    private void RenderBeat()
    {
        _generation++;
        var scene = _session.Scene;
        if (_lastScene != scene.Id) { _stage.ShowLocation(scene.Location, scene.TimeOfDay, scene.CharacterIds); _lastScene = scene.Id; }
        _stage.ApplyAuthoredCues(_session.ActiveStageCues);
        _chapter.Text = _session.Chapter.Title.ToUpperInvariant();
        _place.Text = scene.Title + "  ·  " + scene.TimeOfDay.Replace('_', ' ');
        _speaker.Text = DisplayName(_session.Beat.Speaker);
        _dialogue.Text = _session.Beat.Text; _characters = 0; _dialogue.VisibleCharacters = _instant ? -1 : 0;
        _talk.Visible = _session.Beat.Conversation is not null;
        _advance.Text = !_session.CanAdvance ? "Examine evidence  ›" : _session.IsEnding ? "Finish  ›" : "Continue  ›";
        _status.Text = $"{_session.Progress:P0} · Space / Enter to continue · E evidence · H history";
    }
    private string DisplayName(string id) => id == "narrator" ? "" : id == "you" ? "You" : _story.Characters.FirstOrDefault(c => c.Id == id)?.Name ?? id;
    public override void _Process(double delta)
    {
        _operations.ObserveCompleted(error => GD.PushWarning(error.Message));
        if (_started && _dialogue.VisibleCharacters >= 0) { _characters += delta * 55; _dialogue.VisibleCharacters = Math.Min((int)_characters, _dialogue.GetTotalCharacterCount()); }
        if (_speech.Recording) { _speech.Poll(); _recordSeconds += delta; _mic.Text = $"Stop ({_recordSeconds:0}s)"; if (_recordSeconds >= 30) ToggleMicrophone(); }
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (_previewMode && key.Keycode == Key.F10) { _previewIndex++; ShowStagePreview(); return; }
        if (OS.IsDebugBuild() && key.Keycode == Key.F12)
        {
            var folder = ProjectSettings.GlobalizePath("res://artifacts/captures"); Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, (_started ? _session.Scene.Id : "title") + ".png");
            GetViewport().GetTexture().GetImage().SavePng(path); _status.Text = "Development screenshot captured."; return;
        }
        if (key.Keycode == Key.Escape) { if (_modal is not null) CloseModal(); else if (_chatPanel.Visible) CloseConversation(); return; }
        if (_chatPanel.Visible || _modal is not null) return;
        if (key.Keycode is Key.Space or Key.Enter) Advance();
        else if (key.Keycode == Key.H) ShowHistory();
        else if (key.Keycode == Key.E) ShowEvidence();
    }
    private void OpenConversation()
    {
        if (!_started || _busy || _session.Beat.Conversation is not { } chat) return;
        foreach (var child in _suggestions.GetChildren()) { _suggestions.RemoveChild(child); child.QueueFree(); }
        foreach (var suggestion in chat.Suggestions)
        {
            var captured = suggestion;
            _suggestions.AddChild(WrappedButton(captured, () => { _entry.Text = captured; _entry.GrabFocus(); _entry.CaretColumn = _entry.Text.Length; }));
        }
        _chatPanel.Visible = true; _entry.Text = ""; _entry.GrabFocus();
        _status.Text = "Talk is optional. Authored fallback is used if local Pumas is unavailable.";
    }
    private void CloseConversation()
    {
        _generation++; _request?.Cancel(); _speechRequest?.Cancel(); _speech.Dispose(); _busy = false; _chatPanel.Visible = false; _mic.Text = "Use voice"; _mic.Disabled = false; _send.Disabled = false; _advance.Disabled = false; _entry.Editable = true; _advance.GrabFocus();
    }
    private void SendReply() { if (!_busy && !_closing && !_speech.Recording) _operations.Track(SendReplyAsync()); }
    private async Task SendReplyAsync()
    {
        if (_busy || _speech.Recording || string.IsNullOrWhiteSpace(_entry.Text) || _session.Beat.Conversation is not { } chat) return;
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
            if (!_speech.Available) { ShowWindow("Local speech setup", "Voice-to-text is not available yet. Install and configure local whisper.cpp and a speech model as described in docs/SPEECH.md. Typed and editable suggested replies remain available.\n\nNo audio has been recorded."); return; }
            var consent = new ConfirmationDialog { Title = "Use your microphone?", DialogText = "Record up to 30 seconds on this device and transcribe locally with whisper.cpp. Audio is deleted after transcription. You can edit the transcript before choosing Say this. Nothing is sent automatically.", OkButtonText = "Start recording" };
            _mic.Disabled = true; AddChild(consent);
            consent.Confirmed += () => { try { _speech.Start(this); _recordSeconds = 0; _send.Disabled = true; _entry.Editable = false; } catch (Exception e) { _status.Text = e.Message; } _mic.Disabled = false; consent.QueueFree(); };
            consent.Canceled += () => { _mic.Disabled = false; consent.QueueFree(); }; consent.PopupCentered(new(620, 230)); return;
        }
        _busy = true; _mic.Disabled = true; _status.Text = "Transcribing locally…";
        _speechRequest?.Dispose(); _speechRequest = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var generation = _generation;
        try { var text = await _speech.StopAndTranscribe(_speechRequest.Token); if (generation == _generation) { _entry.Text = text; _entry.GrabFocus(); _status.Text = "Review and edit your transcript, then choose Say this."; } }
        catch (Exception error) { if (generation == _generation) _status.Text = error.Message; }
        finally { if (generation == _generation) { _busy = false; _mic.Disabled = false; _mic.Text = "Use voice"; _send.Disabled = false; _entry.Editable = true; } }
    }
    private string SavePath(bool auto) => ProjectSettings.GlobalizePath(auto ? "user://autosave.json" : "user://save.json");
    private void Save(bool auto)
    {
        if (!_started) return;
        try { SaveStore.Write(SavePath(auto), _session.Snapshot()); if (!auto) _status.Text = "Saved on this device."; }
        catch (Exception error) { _status.Text = "Could not save: " + error.Message; }
    }
    private void ShowLoad()
    {
        ShowWindow("Load a saved watch", "Loading replaces the current unsaved position.", [ ("Load manual save", () => Load(false)), ("Load autosave", () => Load(true)) ]);
    }
    private void Load(bool auto)
    {
        try { var snapshot = SaveStore.Read(SavePath(auto)); _session.Restore(snapshot); CloseConversation(); CloseModal(); _started = true; _lastScene = ""; RenderBeat(); }
        catch (Exception error) { _status.Text = "Could not load: " + error.Message; CloseModal(); }
    }
    private void ShowActivity()
    {
        if (_session.Beat.Activity is not { } activity) return;
        ShowWindow("Compare the evidence", activity.Prompt, activity.Options.Select((option, index) => (option, (Action)(() =>
        {
            if (_session.AnswerActivity(index)) { CloseModal(); _status.Text = activity.Explanation; _advance.Text = "Continue  ›"; Save(true); }
            else { _status.Text = "That does not fit the evidence yet. Check your catalogue and try again."; CloseModal(); }
        }))).ToArray());
    }
    private void ShowHistory() => ShowWindow("The record", string.Join("\n\n", _session.History.Select(h => (DisplayName(h.Speaker) is { Length: > 0 } name ? name + (h.Generated ? " [optional local dialogue]" : "") + ":\n" : "") + h.Text)));
    private void ShowEvidence() => ShowWindow("Your catalogue", "OBJECTS\n\n" + string.Join("\n\n", _session.Inventory.Select(i => i.Name + "\n" + i.Description)) + "\n\nESTABLISHED FACTS\n\n" + string.Join("\n\n", _session.KnownFacts.Select(f => f.Text)));
    private void ShowSettings() => ShowWindow("Reading settings", "All dialogue is text. Use mouse or keyboard. Voice input is optional and local.\n\nSpace / Enter: reveal or advance\nEscape: close panel\nH: history · E: evidence\n\nOptional local conversation may improvise; only the authored catalogue establishes facts.", [("Toggle instant text", () => { _instant = !_instant; if (_instant) _dialogue.VisibleCharacters = -1; _status.Text = _instant ? "Instant text enabled" : "Typewriter text enabled"; CloseModal(); }), ("Toggle reduced motion", () => { _stage.MotionEnabled = !_stage.MotionEnabled; _status.Text = _stage.MotionEnabled ? "Environmental motion enabled" : "Reduced motion enabled"; CloseModal(); }), ("Quit game", () => { CloseModal(); _Notification((int)NotificationWMCloseRequest); })]);
    private void ShowWindow(string title, string text, (string Text, Action Action)[]? actions = null)
    {
        if (_busy) return;
        CloseModal();
        _modal = new Window { Title = title, Size = new(940, 600), Transient = true, Exclusive = true, Theme = CreateTheme() }; AddChild(_modal); _modal.CloseRequested += CloseModal;
        _modal.WindowInput += input => { if (input is InputEventKey { Pressed: true, Keycode: Key.Escape }) CloseModal(); };
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 24); _modal.AddChild(margin);
        var stack = new VBoxContainer(); margin.AddChild(stack);
        stack.AddChild(new RichTextLabel { Text = text, BbcodeEnabled = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        if (actions is not null) foreach (var action in actions) stack.AddChild(WrappedButton(action.Text, action.Action));
        stack.AddChild(Button("Close", CloseModal)); _modal.PopupCentered();
    }
    private void CloseModal() { if (_modal is not null) { _modal.Hide(); _modal.Exclusive = false; _modal.QueueFree(); _modal = null; _advance.GrabFocus(); } }
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
        _started = true; _lastScene = ""; RenderBeat(); _dialogue.VisibleCharacters = -1;
        _status.Text = "DEVELOPMENT PREVIEW · F10 next stage · F12 screenshot · no automatic save";
    }

    private void RunSmoke()
    {
        var count = 1;
        while (!_session.IsEnding) { if (_session.Beat.Activity is { } activity) _session.AnswerActivity(activity.CorrectIndex); if (!_session.Advance()) throw new InvalidOperationException("Timeline blocked."); count++; }
        if (_session.Beat.Activity is { } finalActivity) _session.AnswerActivity(finalActivity.CorrectIndex);
        if (!_session.CanAdvance) throw new InvalidOperationException("Final activity is not solved.");
        var path = ProjectSettings.GlobalizePath("user://smoke-save.json");
        SaveStore.Write(path, _session.Snapshot()); var restored = new StorySession(_story); restored.Restore(SaveStore.Read(path));
        if (!restored.IsEnding) throw new InvalidOperationException("Smoke restore failed.");
        foreach (var location in new[] { "harbor", "keeper_house", "archive", "lantern_room", "tide_cave" }) _stage.ShowLocation(location, "night", _story.Characters.Select(c => c.Id).Take(2).ToArray());
        GD.Print($"LANTERNWAKE_SMOKE_OK beats={count} final={restored.Beat.Id}"); GetTree().Quit();
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
            while (_session.Beat.Conversation is null && !_session.IsEnding)
            {
                if (_session.Beat.Activity is { } activity) _session.AnswerActivity(activity.CorrectIndex);
                _session.Advance();
            }
            RenderBeat(); OpenConversation(); Check(_chatPanel.Visible, "conversation opens");
            var suggestion = _suggestions.GetChildren().OfType<Button>().First(); suggestion.EmitSignal(BaseButton.SignalName.Pressed);
            Check(_entry.Text == suggestion.Text && _entry.Editable, "suggestions are editable");
            _entry.Text = "A headless test reply."; SendReply(); await _operations.DrainAsync();
            Check(_session.History.Any(h => h.Text == "A headless test reply."), "reply reaches history");
            CloseConversation(); OpenConversation(); Check(_entry.Text == "" && _entry.Editable && !_busy, "reopening starts clean"); CloseConversation();
            Save(false); var savedBeat = _session.Beat.Id; _session.Advance(); Load(false);
            Check(_session.Beat.Id == savedBeat, "manual load restores position");
            while (_session.Beat.Activity is null && !_session.IsEnding) _session.Advance();
            RenderBeat(); var gated = _session.Beat.Id; _dialogue.VisibleCharacters = -1; Advance();
            Check(_session.Beat.Id == gated && _modal is not null, "activity opens instead of advancing"); CloseModal();
            GD.Print("LANTERNWAKE_UI_SMOKE_OK title reveal history catalogue settings suggestion reply reopen save load activity");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    public override async void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest || _closing) return;
        _closing = true; _generation++; _request?.Cancel(); _speechRequest?.Cancel(); _speech.Dispose();
        try { await _operations.DrainAsync(); }
        catch (Exception error) { GD.PushWarning("Shutdown operation: " + error.Message); }
        finally { _pumas?.Dispose(); _pumas = null; GetTree().Quit(); }
    }
    public override void _ExitTree() { _generation++; _request?.Cancel(); _request?.Dispose(); _speechRequest?.Cancel(); _speech.Dispose(); }
}
