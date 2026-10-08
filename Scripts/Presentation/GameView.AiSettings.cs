using Godot;
using Lanternwake.Core;
using Lanternwake.Conversation;

namespace Lanternwake.Presentation;

public partial class GameView
{
    [Export] public PackedScene AiSettingsControlsScene { get; set; } = null!;
    private AiSettings _aiSettings = AiSettings.FromEnvironment();
    private string _aiSettingsPath = "", _aiSettingsLoadError = "";
    private string _openRouterKey = "", _credentialNotice = "";
    private ICredentialStore _credentialStore = new DisabledCredentialStore();
    private readonly CancellationTokenSource _credentialLifetime = new();
    private Task _credentialLoad = Task.CompletedTask;
    private CancellationTokenSource? _aiSetupRequest;
    private AiSettings? _lastTestedSettings;
    private bool _lastTestSucceeded;

    private void LoadAiSettings(SessionMode mode)
    {
        _aiSettingsPath = Path.Combine(mode == SessionMode.AutomatedTest ? _storage!.OwnedTestDirectory! :
            ProjectSettings.GlobalizePath("user://"), "ai-settings.json");
        try
        {
            _aiSettings = AiSettings.Load(_aiSettingsPath, AiSettings.FromEnvironment());
            if (System.Environment.GetEnvironmentVariable("LANTERNWAKE_PUMAS_SELECTION_OVERRIDE") == "1")
                _aiSettings = _aiSettings with { LocalLibrary = PumasLibrarySelection.FromEnvironment().Validate() };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _aiSettings = new(DialogueProvider.Pumas, false, AiSettings.DefaultEndpoint(DialogueProvider.Pumas), "");
            _aiSettingsLoadError = "AI settings could not be loaded. Open AI setup to configure them again.";
        }
        _speech.Configure(_aiSettings.Transcription);
        if (mode == SessionMode.Normal)
        {
            _openRouterKey = System.Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
            _credentialStore = new DesktopCredentialStore();
            _credentialLoad = LoadCredentialAsync(); _operations.Track(_credentialLoad);
        }
    }

    private async Task LoadCredentialAsync()
    {
        try
        {
            var saved = await _credentialStore.LoadAsync(_credentialLifetime.Token);
            if (!string.IsNullOrWhiteSpace(saved))
            {
                _openRouterKey = saved;
                _credentialNotice = "Key loaded from your desktop keyring.";
            }
            else _credentialNotice = _openRouterKey.Length > 0
                ? "Using OPENROUTER_API_KEY. Save AI settings to remember it in your desktop keyring."
                : "Save AI settings to remember your key in your desktop keyring.";
        }
        catch (OperationCanceledException) { }
        catch (Exception) { _credentialNotice = "Desktop keyring unavailable or locked. Unlock it to load or save a key. Keys are never saved to a file."; }
    }

    private string AiSummary => _aiSettingsLoadError.Length > 0 ? _aiSettingsLoadError :
        !_aiSettings.DialogueEnabled ? "AI conversations are off · authored replies available" :
        _aiSettings.Provider + " · " + _aiSettings.Model + " · " +
        (_lastTestedSettings is not null && _lastTestedSettings.Provider == _aiSettings.Provider &&
         _lastTestedSettings.Endpoint == _aiSettings.Endpoint && _lastTestedSettings.Model == _aiSettings.Model
            ? _lastTestSucceeded ? "last response test passed" : "last response test failed"
            : "connection not yet tested");

    private void ShowAiSetup()
    {
        if (_busy || _closing) return;
        ShowWindow("AI setup", "Configure dialogue, transcription and character voices separately. The story works without AI.");
        if (_modal is null) return;
        var window = _modal;
        var introduction = window.GetNode<RichTextLabel>("%ModalText");
        introduction.CustomMinimumSize = new(0, 80); introduction.SizeFlagsVertical = Control.SizeFlags.Fill;
        window.GetNode<ScrollContainer>("%ModalActionsScroll").SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var form = AiSettingsControlsScene.Instantiate<VBoxContainer>();
        var rows = window.GetNode<VBoxContainer>("%ModalActions");
        rows.Visible = true; window.GetNode<ScrollContainer>("%ModalActionsScroll").Visible = true; rows.AddChild(form);
        var tabs = form.GetNode<TabContainer>("Capabilities");
        tabs.SetTabTitle(0, "Dialogue (LLM)"); tabs.SetTabTitle(2, "Character voices");
        tabs.TabChanged += _ => window.GetNode<ScrollContainer>("%ModalActionsScroll").ScrollVertical = 0;
        var dialogue = tabs.GetNode<VBoxContainer>("Dialogue");
        var transcription = tabs.GetNode<VBoxContainer>("Transcription");
        var voices = tabs.GetNode<VBoxContainer>("Voices");
        BindSpeechPreferences(transcription, _aiSettings.Transcription);
        BindSpeechPreferences(voices, _aiSettings.CharacterSpeech);
        var assignments = voices.GetNode<VBoxContainer>("Assignments");
        var voiceChoices = new List<(string Character, OptionButton Choice)>();
        foreach (var character in _story.Characters)
        {
            var label = new Label { Text = character.Name }; assignments.AddChild(label); _readingText.Register(label);
            var choice = new OptionButton { Disabled = true, FitToLongestItem = false };
            choice.AddItem(_aiSettings.CharacterVoices.TryGetValue(character.Id, out var voice) && voice.Length > 0
                ? voice + " (saved; currently unavailable)" : "No character voices available yet");
            assignments.AddChild(choice); voiceChoices.Add((character.Id, choice));
        }
        var provider = dialogue.GetNode<OptionButton>("Provider");
        var endpoint = dialogue.GetNode<LineEdit>("Endpoint");
        var libraryRoot = dialogue.GetNode<LineEdit>("LibraryRoot");
        var observer = dialogue.GetNode<LineEdit>("PumasObserver");
        libraryRoot.Text = _aiSettings.LocalLibrary.Root; observer.Text = _aiSettings.LocalLibrary.ObserverExecutable;
        var enabled = dialogue.GetNode<CheckButton>("DialogueEnabled");
        var key = dialogue.GetNode<LineEdit>("ApiKey");
        var model = dialogue.GetNode<OptionButton>("Model");
        var filter = dialogue.GetNode<LineEdit>("ModelFilter");
        var refresh = dialogue.GetNode<Button>("RefreshModels");
        var test = dialogue.GetNode<Button>("TestConnection");
        var save = form.GetNode<Button>("SaveConfiguration");
        var stack = window.GetNode<VBoxContainer>("Margin/Stack");
        save.Reparent(stack); stack.MoveChild(save, stack.GetChildCount() - 2);
        _readingText.Register(save);
        var status = form.GetNode<Label>("ConnectionStatus");
        var credentialStatus = dialogue.GetNode<Label>("CredentialStatus");
        var forget = dialogue.GetNode<Button>("ForgetKey");
        provider.AddItem("Pumas (local)", (int)DialogueProvider.Pumas);
        provider.AddItem("OpenRouter (hosted)", (int)DialogueProvider.OpenRouter);
        provider.Select((int)_aiSettings.Provider); endpoint.Text = _aiSettings.Endpoint;
        enabled.SetPressedNoSignal(_aiSettings.DialogueEnabled); key.Text = _openRouterKey;
        var available = new List<AiModel>();
        var selection = _aiSettings.Model;
        var requesting = false;
        if (selection.Length > 0) available.Add(new(selection, selection + " (saved selection; refresh to check availability)"));
        bool Active() => !_closing && _modal == window && GodotObject.IsInstanceValid(form) && !form.IsQueuedForDeletion();
        void UpdateModelChoices()
        {
            model.Clear();
            foreach (var item in available.Where(item => (item.Name + " " + item.Id).Contains(filter.Text, StringComparison.OrdinalIgnoreCase)))
            {
                var label = item.Name.Length > 90 ? item.Name[..90] + "…" : item.Name;
                model.AddItem(label); model.SetItemMetadata(model.ItemCount - 1, item.Id);
                if (item.Id == selection) model.Select(model.ItemCount - 1);
            }
            if (model.ItemCount == 0) { model.AddItem("No models available — refresh the provider list"); model.SetItemDisabled(0, true); }
            test.Disabled = requesting || model.IsItemDisabled(model.Selected);
            FitModalActions(window);
        }
        string SelectedModel() => model.Selected >= 0 && !model.IsItemDisabled(model.Selected) ? model.GetItemMetadata(model.Selected).AsString() : "";
        AiSettings Draft(bool requireModel) => new AiSettings((DialogueProvider)provider.GetSelectedId(),
            requireModel && enabled.ButtonPressed, endpoint.Text.Trim(), requireModel ? SelectedModel() : "")
        {
            LocalLibrary = new(libraryRoot.Text.Trim(), observer.Text.Trim()),
            Transcription = ReadSpeechPreferences(transcription, _aiSettings.Transcription),
            CharacterSpeech = ReadSpeechPreferences(voices, _aiSettings.CharacterSpeech),
            CharacterVoices = ReadSpeechPreferences(voices, _aiSettings.CharacterSpeech).Provider == _aiSettings.CharacterSpeech.Provider
                ? new(_aiSettings.CharacterVoices) : new()
        }.Validate();
        string Key() => key.Text.Trim();
        void ProviderAppearance()
        {
            var hosted = provider.GetSelectedId() == (int)DialogueProvider.OpenRouter;
            var needsKey = hosted || new[] { transcription, voices }.Any(speech =>
                speech.GetNode<OptionButton>("Provider").GetSelectedId() == (int)DialogueProvider.OpenRouter);
            key.Visible = needsKey; dialogue.GetNode<Label>("ApiKeyLabel").Visible = needsKey;
            credentialStatus.Visible = needsKey; forget.Visible = needsKey;
            forget.Disabled = requesting || _previewMode;
            filter.Visible = hosted;
            endpoint.Editable = !requesting && !hosted;
            libraryRoot.Editable = !requesting; observer.Editable = !requesting;
            dialogue.GetNode<Label>("ProviderNote").Text = hosted
                ? "OpenRouter sends optional dialogue to a hosted service. Test responses and conversations use your account's credits. Save AI settings to remember your API key in your desktop keyring."
                : "Pumas supplies the model list. Start Pumas and load a llama.cpp model, then refresh. Model loading is managed in Pumas.";
            FitModalActions(window);
        }
        void SetRequesting(bool value)
        {
            requesting = value;
            provider.Disabled = value; model.Disabled = value; filter.Editable = !value;
            key.Editable = !value; enabled.Disabled = value; refresh.Disabled = value;
            save.Disabled = value || _previewMode;
            foreach (var speech in new[] { transcription, voices })
            {
                speech.GetNode<OptionButton>("Provider").Disabled = value;
                speech.GetNode<Button>("ManageKey").Disabled = value;
                speech.GetNode<LineEdit>("Endpoint").Editable = !value &&
                    speech.GetNode<OptionButton>("Provider").GetSelectedId() == (int)DialogueProvider.Pumas;
                speech.GetNode<CheckButton>("Enabled").Disabled = value;
            }
            ProviderAppearance(); UpdateModelChoices();
        }
        async Task RefreshAsync()
        {
            if (!Active()) return;
            AiSettings draft;
            try { draft = Draft(false); }
            catch (InvalidDataException error) { status.Text = error.Message; return; }
            _aiSetupRequest?.Cancel(); _aiSetupRequest?.Dispose(); _aiSetupRequest = new();
            var token = _aiSetupRequest.Token;
            SetRequesting(true);
            status.Text = "Reading models from " + draft.Provider + "…";
            try
            {
                if (draft.Provider == DialogueProvider.OpenRouter) await _credentialLoad.WaitAsync(token);
                if (!Active() || token.IsCancellationRequested) return;
                using var connection = new AiConnection(draft, Key());
                var result = await connection.ListModelsAsync(token);
                if (!Active() || token.IsCancellationRequested) return;
                selection = SelectedModel(); available = result.Models.ToList(); UpdateModelChoices();
                status.Text = !result.Success ? AiError(result.ErrorCode) : available.Count == 0
                    ? draft.Provider == DialogueProvider.Pumas ? "No compatible models are ready. Load a llama.cpp model in Pumas, then refresh." : "No text conversation models were returned by OpenRouter."
                    : available.Count + " models available. Select one, then test a response.";
            }
            catch (Exception) { if (Active() && !token.IsCancellationRequested) status.Text = "Could not read the provider's model list. Check its URL and credentials."; }
            finally
            {
                if (Active()) SetRequesting(false);
            }
        }
        async Task TestAsync()
        {
            AiSettings draft;
            try { draft = (Draft(true) with { DialogueEnabled = true }).Validate(); }
            catch (InvalidDataException error) { status.Text = error.Message; return; }
            _aiSetupRequest?.Cancel(); _aiSetupRequest?.Dispose(); _aiSetupRequest = new();
            var token = _aiSetupRequest.Token;
            SetRequesting(true);
            status.Text = "Requesting a test reply from " + draft.Provider + "…";
            try
            {
                if (draft.Provider == DialogueProvider.OpenRouter) await _credentialLoad.WaitAsync(token);
                if (!Active() || token.IsCancellationRequested) return;
                var reply = await GenerateWithSettings(draft, Key(), "nessa",
                    "Speaking character: Nessa, an island keeper. Player: Ada. A storm has grounded the ferry. No other facts are known.",
                    "Can the ferry leave during the storm?", token);
                if (!Active() || token.IsCancellationRequested) return;
                _lastTestedSettings = draft; _lastTestSucceeded = reply.Success;
                status.Text = reply.Success ? "Response received from " + draft.Provider + ":\n" + reply.Text : AiError(reply.ErrorCode);
            }
            catch (Exception) { if (Active() && !token.IsCancellationRequested) status.Text = "Could not test the selected model. Check provider setup and try again."; }
            finally { if (Active()) SetRequesting(false); }
        }
        refresh.Pressed += () => _operations.Track(RefreshAsync());
        test.Pressed += () => _operations.Track(TestAsync());
        filter.TextChanged += _ => UpdateModelChoices();
        model.ItemSelected += _ => selection = SelectedModel();
        provider.ItemSelected += _ =>
        {
            var selected = (DialogueProvider)provider.GetSelectedId();
            endpoint.Text = selected == _aiSettings.Provider ? _aiSettings.Endpoint : AiSettings.DefaultEndpoint(selected);
            selection = selected == _aiSettings.Provider ? _aiSettings.Model : "";
            available.Clear(); if (selection.Length > 0) available.Add(new(selection, selection + " (saved selection)"));
            filter.Text = ""; status.Text = "Refresh the model list for " + selected + ".";
            ProviderAppearance(); UpdateModelChoices();
        };
        endpoint.TextChanged += _ => { available.Clear(); selection = ""; UpdateModelChoices(); status.Text = "The URL changed. Refresh models from this address."; };
        save.Disabled = _previewMode;
        async Task SaveAsync()
        {
            AiSettings draft;
            try { draft = Draft(true); }
            catch (InvalidDataException error) { status.Text = error.Message; return; }
            var enteredKey = Key();
            var keyWasEdited = key.Text != _openRouterKey;
            _aiSetupRequest?.Cancel(); _aiSetupRequest?.Dispose(); _aiSetupRequest = new();
            var token = _aiSetupRequest.Token;
            SetRequesting(true); status.Text = "Saving settings… Unlock your desktop keyring if prompted.";
            var keySaved = false;
            try
            {
                await _credentialLoad.WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (!keyWasEdited && enteredKey.Length == 0) enteredKey = _openRouterKey;
                if (draft.Provider == DialogueProvider.OpenRouter || draft.Transcription.Provider == DialogueProvider.OpenRouter ||
                    draft.CharacterSpeech.Provider == DialogueProvider.OpenRouter)
                {
                    if (enteredKey.Length > 0) { await _credentialStore.SaveAsync(enteredKey, token); keySaved = true; }
                    else if (_openRouterKey.Length > 0) await _credentialStore.ForgetAsync(token);
                    _openRouterKey = enteredKey;
                    _credentialNotice = enteredKey.Length > 0 ? "Key saved in your desktop keyring." : "No key saved.";
                }
                token.ThrowIfCancellationRequested();
                draft.Save(_aiSettingsPath); _aiSettings = draft;
                // Retarget only after an existing setup request settles; its original client remains owned.
                _setupSelectionChanged = true; _speech.Configure(draft.Transcription); _aiSettingsLoadError = "";
                if (Active())
                {
                    credentialStatus.Text = _credentialNotice;
                    status.Text = "AI settings saved. Dialogue changes apply to the next conversation. Speech preferences are saved for when support becomes available.";
                }
                if (_mainMenu is not null) _mainMenu.GetNode<Label>("%MenuAiSummary").Text = AiSummary + "\nVoice input and character speech are currently unavailable.";
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (Active()) status.Text = keySaved ? "The key was saved in your keyring, but preferences could not be saved. Try again."
                    : "Settings were not saved. Check the settings location and unlock your desktop keyring, then try again. No API key was written to a file.";
            }
            finally { if (Active()) SetRequesting(false); }
        }
        async Task ForgetAsync()
        {
            _aiSetupRequest?.Cancel(); _aiSetupRequest?.Dispose(); _aiSetupRequest = new();
            var token = _aiSetupRequest.Token;
            SetRequesting(true); status.Text = "Removing the saved key… Unlock your desktop keyring if prompted.";
            try
            {
                await _credentialLoad.WaitAsync(token);
                await _credentialStore.ForgetAsync(token);
                _openRouterKey = ""; _credentialNotice = "Saved key removed. OPENROUTER_API_KEY can still supply a key on a future launch.";
                if (Active()) { key.Text = ""; credentialStatus.Text = _credentialNotice; status.Text = "Saved OpenRouter key removed."; }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (Active()) status.Text = "Could not remove the saved key. Unlock your desktop keyring and try again."; }
            finally { if (Active()) SetRequesting(false); }
        }
        async Task PopulateKeyAsync()
        {
            var initial = key.Text;
            await _credentialLoad;
            if (!Active()) return;
            if (key.Text == initial) key.Text = _openRouterKey;
            credentialStatus.Text = _credentialNotice;
        }
        foreach (var speech in new[] { transcription, voices })
        {
            speech.GetNode<OptionButton>("Provider").ItemSelected += _ =>
            {
                ProviderAppearance();
                foreach (var (character, choice) in voiceChoices)
                {
                    choice.Clear();
                    choice.AddItem(voices.GetNode<OptionButton>("Provider").GetSelectedId() == (int)_aiSettings.CharacterSpeech.Provider &&
                        _aiSettings.CharacterVoices.TryGetValue(character, out var savedVoice) && savedVoice.Length > 0
                        ? savedVoice + " (saved; currently unavailable)" : "No character voices available yet");
                }
            };
            speech.GetNode<Button>("ManageKey").Pressed += () => { tabs.CurrentTab = 0; key.GrabFocus(); };
        }
        save.Pressed += () => _operations.Track(SaveAsync());
        forget.Pressed += () => _operations.Track(ForgetAsync());
        _operations.Track(PopulateKeyAsync());
        foreach (var label in new[] { form, dialogue, transcription, voices }.SelectMany(row => row.GetChildren()).OfType<Label>()) _readingText.Register(label);
        foreach (var button in new[] { form, dialogue, transcription, voices }.SelectMany(row => row.GetChildren()).OfType<Button>()) _readingText.Register(button);
        ProviderAppearance(); UpdateModelChoices();
        status.Text = _aiSettingsLoadError.Length > 0 ? _aiSettingsLoadError : "Refresh models to check what the provider currently offers.";
        enabled.GrabFocus();
    }

    private static async Task<PumasReply> GenerateWithSettings(AiSettings settings, string key, string character, string context, string text, CancellationToken token)
    {
        if (!settings.DialogueEnabled) return PumasReply.Unavailable("ai_disabled");
        if (settings.Provider == DialogueProvider.Pumas)
        {
            using var client = new PumasClient(new Uri(settings.Endpoint), settings.Model);
            return await client.GenerateAsync(character, context, text, token);
        }
        using var connection = new AiConnection(settings, key);
        return await connection.GenerateOpenRouterAsync(character, context, text, token);
    }

    private void CancelAiSetup() { _aiSetupRequest?.Cancel(); if (_closing) _credentialLifetime.Cancel(); }

    private static void BindSpeechPreferences(VBoxContainer form, SpeechServiceSettings preferences)
    {
        var provider = form.GetNode<OptionButton>("Provider");
        var endpoint = form.GetNode<LineEdit>("Endpoint");
        var model = form.GetNode<OptionButton>("Model");
        provider.AddItem("Pumas (local)", (int)DialogueProvider.Pumas);
        provider.AddItem("OpenRouter (hosted)", (int)DialogueProvider.OpenRouter);
        provider.Select((int)preferences.Provider);
        endpoint.Text = preferences.Endpoint;
        form.GetNode<CheckButton>("Enabled").SetPressedNoSignal(preferences.Enabled);
        var selected = preferences.Provider;
        var drafts = new Dictionary<DialogueProvider, (string Endpoint, string Model)>
            { [selected] = (preferences.Endpoint, preferences.Model) };
        void ShowProvider(string modelId)
        {
            model.Clear();
            model.AddItem(modelId.Length > 0 ? modelId + " (saved; currently unavailable)" : "No compatible models available yet");
            model.SetItemMetadata(0, modelId);
            var hosted = selected == DialogueProvider.OpenRouter;
            endpoint.Editable = !hosted;
            form.GetNode<Button>("ManageKey").Visible = hosted;
            var capability = form.Name == "Transcription" ? "Transcription" : "Character speech";
            form.GetNode<Label>("Availability").Text = capability + " through " + selected +
                " is not available in this build yet. Provider, URL and enable preferences can be saved for future support." +
                (form.Name == "Transcription" ? " Microphone recording remains off." : " Voice choices will appear when supported.");
        }
        ShowProvider(preferences.Model);
        provider.ItemSelected += _ =>
        {
            drafts[selected] = (endpoint.Text, model.GetItemMetadata(0).AsString());
            selected = (DialogueProvider)provider.GetSelectedId();
            var draft = drafts.GetValueOrDefault(selected, (AiSettings.DefaultEndpoint(selected), ""));
            endpoint.Text = draft.Item1;
            ShowProvider(draft.Item2);
        };
        var assignments = form.GetNodeOrNull<VBoxContainer>("Assignments");
        if (assignments is not null)
        {
            assignments.Visible = preferences.Enabled;
            form.GetNode<CheckButton>("Enabled").Toggled += value => assignments.Visible = value;
        }
    }

    private static SpeechServiceSettings ReadSpeechPreferences(VBoxContainer form, SpeechServiceSettings previous) =>
        previous with { Enabled = form.GetNode<CheckButton>("Enabled").ButtonPressed,
            Provider = (DialogueProvider)form.GetNode<OptionButton>("Provider").GetSelectedId(),
            Endpoint = form.GetNode<LineEdit>("Endpoint").Text.Trim(),
            Model = form.GetNode<OptionButton>("Model").GetItemMetadata(0).AsString() };

    private static string AiError(string code) => code switch
    {
        "ai_disabled" => "AI conversations are turned off. Enable them in AI setup to use a model.",
        "api_key_missing" => "Enter an OpenRouter API key to test or use this model.",
        "authentication_failed" => "The provider rejected your API key. Check the key and try again.",
        "credits_required" => "Your OpenRouter account needs credits for this model.",
        "model_unavailable" => "The selected model is not ready. Refresh models and choose an available model.",
        "wrong_provider" => "This Pumas integration needs a llama.cpp conversation model.",
        "pumas_contract" => "Pumas serving is unavailable or incompatible with this game's integration.",
        "timeout" => "The provider took too long. Try again when the model is ready.",
        "rate_limited" => "The provider is busy or rate limited. Try again later.",
        "cancelled" => "The request was cancelled.",
        "invalid_response" => "The provider returned an unsupported response.",
        _ => "Could not reach the provider. Check its URL and make sure it is running."
    };
}
