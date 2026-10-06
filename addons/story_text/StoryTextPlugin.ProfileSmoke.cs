#if TOOLS
using Godot;
using Lanternwake.Core;
using File = System.IO.File;

public partial class StoryTextPlugin
{
    private void RunProfileSmoke(string path)
    {
        void Check(bool condition, string claim)
        {
            if (!condition) throw new InvalidOperationException("Character authoring smoke: " + claim);
        }
        var characterIndex = Array.FindIndex(_story.Characters, c => c.Id == "nessa");
        _profileCharacters.EmitSignal(OptionButton.SignalName.ItemSelected, (long)characterIndex);
        _profileFields.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
        Check(_profileNotice.Text.Contains("AUTHOR ONLY"), "history marked author-only");
        _profileText.Text = "PRIVATE_DOSSIER_SENTINEL 語";
        _profileText.EmitSignal(TextEdit.SignalName.TextChanged);
        Check(_profileDirty && !_text.Editable && _speakers.Disabled, "dossier draft locks beat editing");
        var diskBefore = File.ReadAllText(path);
        _dock.GetNode<Button>("Actions/Save").EmitSignal(Button.SignalName.Pressed);
        Check(File.ReadAllText(path) == diskBefore && _profileDirty, "beat save cannot discard dossier draft");
        _profileFields.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
        Check(_profileFieldIndex == 0 && _profileText.Text.Contains("PRIVATE_DOSSIER"), "dirty field selection retained");
        _dock.GetNode<Button>("Profiles/Save").EmitSignal(Button.SignalName.Pressed);
        Check(!_profileDirty && _text.Editable && _profileCharacterId == "nessa" && _profileText.Text.Contains("PRIVATE_DOSSIER"), "dossier save reload retains character and field");
        Check(Story.Parse(File.ReadAllText(path)).Characters.Single(c => c.Id == "nessa").AuthoringProfile!.History == _profileText.Text, "writer edit persists");

        var chat = _index!.Entries.First(e => e.Beat.Conversation?.CharacterId == "nessa");
        _search.Text = chat.Beat.Id; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text);
        _matches.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
        _dock.GetNode<CheckButton>("PreviewToggle").EmitSignal(BaseButton.SignalName.Toggled, true);
        Check(_dock.GetNode<Control>("Preview").Visible && _previewText.Text == StoryContextPreview.AtBeat(_story, chat.Beat.Id), "visible preview equals production builder");
        Check(!_previewText.Text.Contains("PRIVATE_DOSSIER") && _previewBudget.Text.Contains("no player save"), "preview excludes biography and labels isolated state");
        var styleIndex = Array.IndexOf(CharacterProfileEdit.Fields, "dialogueStyle");
        _profileFields.EmitSignal(OptionButton.SignalName.ItemSelected, (long)styleIndex);
        Check(_profileNotice.Text.Contains("SENT TO MODEL"), "runtime field transmission is explicit");
        _profileText.Text = "Use concise practical sentences."; _profileText.EmitSignal(TextEdit.SignalName.TextChanged);
        _dock.GetNode<Button>("Profiles/Save").EmitSignal(Button.SignalName.Pressed);
        Check(_previewText.Text.Contains("Use concise practical sentences.") && !_profileDirty, "saved runtime style refreshes exact context");

        diskBefore = File.ReadAllText(path);
        _profileText.Text = new string('x', 601); _profileText.EmitSignal(TextEdit.SignalName.TextChanged);
        _dock.GetNode<Button>("Profiles/Save").EmitSignal(Button.SignalName.Pressed);
        Check(_profileDirty && File.ReadAllText(path) == diskBefore, "oversize style is rejected without losing draft");
        _profileText.Text = "A pending edit"; _profileText.EmitSignal(TextEdit.SignalName.TextChanged);
        File.WriteAllText(path, diskBefore + "\n");
        _dock.GetNode<Button>("Profiles/Save").EmitSignal(Button.SignalName.Pressed);
        Check(_profileDirty && File.ReadAllText(path) == diskBefore + "\n", "external change blocks dossier publication");
        _dock.GetNode<Button>("Profiles/Reload").EmitSignal(Button.SignalName.Pressed);
        Check(!_profileDirty && _profileText.Text == "Use concise practical sentences.", "reload discards dossier draft and recovers");
        _text.Text = "Pending beat edit"; _text.EmitSignal(TextEdit.SignalName.TextChanged);
        Check(!_profileText.Editable, "beat draft locks dossier editing");
        diskBefore = File.ReadAllText(path);
        _dock.GetNode<Button>("Profiles/Save").EmitSignal(Button.SignalName.Pressed);
        Check(_dirty && File.ReadAllText(path) == diskBefore, "dossier save cannot discard beat draft");
        _dock.GetNode<Button>("Profiles/Reload").EmitSignal(Button.SignalName.Pressed);
        Check(!_dirty && _profileText.Editable && _text.Text == chat.Beat.Text, "shared reload recovers both edit surfaces");
        _dock.GetNode<CheckButton>("PreviewToggle").EmitSignal(BaseButton.SignalName.Toggled, false);
        GD.Print("LANTERNWAKE_CHARACTER_AUTHORING_OK profiles save reload conflict bounds draft isolation and exact production context preview");
    }
}
#endif
