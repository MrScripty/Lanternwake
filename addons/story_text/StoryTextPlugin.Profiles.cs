#if TOOLS
using Godot;
using Lanternwake.Core;
using File = System.IO.File;

public partial class StoryTextPlugin
{
    private OptionButton _profileCharacters = null!, _profileFields = null!;
    private TextEdit _profileText = null!, _previewText = null!;
    private Label _profileNotice = null!, _previewBudget = null!;
    private int _profileCharacterIndex, _profileFieldIndex;
    private string? _profileCharacterId;
    private bool _profileLoading, _profileDirty;
    private bool HasDraft => _dirty || _profileDirty;

    private void InitializeProfiles()
    {
        _profileCharacters = _dock.GetNode<OptionButton>("Profiles/Characters");
        _profileFields = _dock.GetNode<OptionButton>("Profiles/Fields");
        _profileText = _dock.GetNode<TextEdit>("Profiles/Text");
        _profileNotice = _dock.GetNode<Label>("Profiles/Notice");
        _previewText = _dock.GetNode<TextEdit>("Preview/Text");
        _previewBudget = _dock.GetNode<Label>("Preview/Budget");
        foreach (var field in CharacterProfileEdit.Fields)
            _profileFields.AddItem(field == "dialogueStyle" ? "RUNTIME · dialogueStyle" : "AUTHOR ONLY · " + field);
        _profileCharacters.ItemSelected += index => SelectProfile((int)index, _profileFieldIndex);
        _profileFields.ItemSelected += index => SelectProfile(_profileCharacterIndex, (int)index);
        _profileText.TextChanged += () =>
        {
            if (!_profileLoading) { _profileDirty = true; UpdateEditLocks(); _status.Text = "Unsaved dossier draft. Save the selected field or Reload. Runtime preview shows saved content only."; }
        };
        _dock.GetNode<Button>("Profiles/Save").Pressed += SaveProfile;
        _dock.GetNode<Button>("Profiles/Reload").Pressed += Reload;
        _dock.GetNode<CheckButton>("ProfilesToggle").Toggled += visible => _dock.GetNode<Control>("Profiles").Visible = visible;
        _dock.GetNode<CheckButton>("PreviewToggle").Toggled += visible => _dock.GetNode<Control>("Preview").Visible = visible;
    }

    private void ReloadProfiles()
    {
        _profileCharacters.Clear();
        foreach (var character in _story.Characters) _profileCharacters.AddItem(character.Name);
        _profileCharacterIndex = Math.Max(0, Array.FindIndex(_story.Characters, c => c.Id == _profileCharacterId));
        DisplayProfile();
    }

    private void SelectProfile(int characterIndex, int fieldIndex)
    {
        if (HasDraft)
        {
            _profileCharacters.Select(_profileCharacterIndex); _profileFields.Select(_profileFieldIndex);
            _status.Text = "Save or Reload your current draft before changing dossier selection.";
            return;
        }
        _profileCharacterIndex = characterIndex; _profileFieldIndex = fieldIndex;
        DisplayProfile();
    }

    private void DisplayProfile()
    {
        _profileLoading = true;
        var character = _story.Characters[_profileCharacterIndex];
        _profileCharacterId = character.Id;
        var field = CharacterProfileEdit.Fields[_profileFieldIndex];
        _profileCharacters.Select(_profileCharacterIndex); _profileFields.Select(_profileFieldIndex);
        _profileText.Text = CharacterProfileEdit.Read(character, field);
        _profileNotice.Text = field == "dialogueStyle"
            ? $"SENT TO MODEL · behavior only · {CharacterProfileEdit.DialogueStyleLimit} character limit. No history, secrets, new facts or arc outcomes. Blank keeps the existing voice card. Saving does not qualify model behavior."
            : $"AUTHOR ONLY · never sent to model · {CharacterProfileEdit.AuthorFieldLimit} character limit. May contain spoilers. Cite established canon in sources; this field cannot grant knowledge or change the plot.";
        _profileLoading = false;
    }

    private void UpdateEditLocks()
    {
        // Saving either surface reloads the document; never silently discard the other draft.
        _profileText.Editable = !_dirty;
        _text.Editable = !_profileDirty;
        _speakers.Disabled = _profileDirty;
    }

    private void SaveProfile()
    {
        if (_dirty) { _status.Text = "Save or Reload your beat draft before saving a dossier."; return; }
        try
        {
            var path = ProjectSettings.GlobalizePath(_storyPath);
            var result = CharacterProfileEdit.Apply(_loaded, File.ReadAllText(path), _profileCharacterId!,
                CharacterProfileEdit.Fields[_profileFieldIndex], _profileText.Text);
            PublishEdit(result);
        }
        catch (Exception error) { _status.Text = error.Message; }
    }

    private void DisplayPreview(Beat beat)
    {
        try
        {
            var context = StoryContextPreview.AtBeat(_story, beat.Id);
            _previewText.Text = context ?? "No optional conversation at this beat. No model context is available.";
            _previewBudget.Text = context is null ? "No model request" : $"Saved character context: {context.Length}/12000 characters. Isolated progress; no player save or optional history.";
        }
        catch (Exception error)
        {
            _previewText.Text = "Preview unavailable: " + error.Message;
            _previewBudget.Text = "Invalid context; no model request made";
        }
    }
}
#endif
