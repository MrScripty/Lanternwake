#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Owned test scene. Production buttons drive completion, replay and fresh-process resume.</summary>
public partial class WatchCompletionQualification : Node
{
    private GameView _game = null!;
    private Story _story = null!;
    private string _root = "";
    private int _checks, _beats;
    private StorySession Session => Observe<StorySession>("_session");
    private Window? Modal => Observe<Window?>("_modal");
    private SessionStorage Storage => Observe<SessionStorage>("_storage");
    private T Observe<T>(string name) => (T)typeof(GameView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_game)!;
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Watch completion: " + claim);
        _checks++;
    }
    private void Press(string name) => _game.InterfaceRoot.GetNode<Button>("%" + name).EmitSignal(BaseButton.SignalName.Pressed);
    private Button ActionButton(string text) => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == text);
    private void Action(string text) => ActionButton(text).EmitSignal(BaseButton.SignalName.Pressed);
    private void Close() => Modal!.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private static string Snapshot(SaveData save) => JsonSerializer.Serialize(save, Story.Json);
    private Dictionary<string, byte[]> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json").ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes);
    private void Unchanged(Dictionary<string, byte[]> files)
    {
        var current = Files();
        Check(files.Count == current.Count && files.All(p => current[p.Key].SequenceEqual(p.Value)), "every save byte preserved");
    }

    private void LoadFinished()
    {
        var before = Files();
        Press("LoadButton");
        Action("Load current manual save · " + _story.Chapters.Last().Scenes.Last().Beats.Last().Id);
        Check(Session.IsEnding, "actual Load resumes completed watch");
        Unchanged(before);
        _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
        Press("AdvanceButton");
        Check(Modal!.Title == "The light remains", "actual Finish opens completion menu after resume");
    }

    private async Task CompleteWatch()
    {
        Press("AdvanceButton");
        Press("SettingsButton"); Action("Larger reading text"); Action("Larger reading text");
        Action("Sound settings");
        Modal!.GetNode<CheckButton>("Margin/Stack/ModalActionsScroll/ModalActions/AudioSettingsControls/Mute").ButtonPressed = true;
        Close(); Press("SettingsButton"); Action("Toggle instant text");
        string chapter = "";
        while (true)
        {
            _beats++;
            if (Session.Chapter.Id != chapter) { chapter = Session.Chapter.Id; GD.Print("Completion traversal: " + chapter); }
            if (Session.Beat.Activity is { } activity)
            {
                Press("AdvanceButton"); Action(activity.Options[activity.CorrectIndex]);
                if (Session.IsEnding)
                    Check(_game.InterfaceRoot.GetNode<Button>("%AdvanceButton").Text == "Finish  ›", "solving final evidence exposes Finish before opening completion");
            }
            if (Session.IsEnding) break;
            var before = Session.Beat.Id;
            Press("AdvanceButton"); await Frame();
            Check(Session.Beat.Id != before, "actual Continue advances canonical beat");
        }
        Check(_beats == _story.Chapters.Sum(c => c.Scenes.Sum(s => s.Beats.Length)), "all authored beats reached through native controls");
        Press("AdvanceButton");
        Check(Modal!.Title == "The light remains", "actual Finish opens completion menu");
        Action("Save finished watch");
        Check(Modal!.GetNode<RichTextLabel>("%ModalText").Text.Contains("Saved on this device."), "ending reports successful explicit manual save");
        Check(Snapshot(Storage.Read(false)) == Snapshot(Session.Snapshot()), "finished watch saved without schema change");
    }

    private async Task ReviewAndRestart()
    {
        var finished = Snapshot(Session.Snapshot()); var before = Files();
        Action("Read the record");
        var text = Modal!.GetNode<RichTextLabel>("%ModalText").Text;
        Check(text.Contains(_story.Chapters[0].Scenes[0].Beats[0].Text) && text.Contains(Session.Beat.Text), "ending record contains first and final authored lines");
        Close(); Action("Review evidence");
        text = Modal!.GetNode<RichTextLabel>("%ModalText").Text;
        Check(Session.KnownFacts.All(f => text.Contains(f.Text)) && Session.Inventory.All(i => text.Contains(i.Description)), "ending catalogue preserves all established evidence");
        Action("Back to ending");
        var staleSave = ActionButton("Save finished watch");
        Action("Start a new watch");
        Check(ActionButton("Keep this watch").HasFocus(), "restart confirmation initially focuses safe cancellation");
        var staleRestart = ActionButton("Start new watch");
        Action("Keep this watch");
        staleRestart.EmitSignal(BaseButton.SignalName.Pressed);
        Check(Session.IsEnding && Snapshot(Session.Snapshot()) == finished, "cancel and retired confirmation preserve finished session"); Unchanged(before);
        Action("Start a new watch"); Close();
        Check(Session.IsEnding && Snapshot(Session.Snapshot()) == finished, "closing restart confirmation preserves completed watch"); Unchanged(before);
        Press("AdvanceButton");

        // Exercise visible save-failure feedback without touching any real user slots.
        var manual = Path.Combine(ProjectSettings.GlobalizePath("user://"), "save.json");
        var preserved = Path.Combine(_root, "finished-manual-preserved");
        File.Move(manual, preserved); Directory.CreateDirectory(manual);
        try
        {
            Action("Save finished watch");
            Check(Modal!.GetNode<RichTextLabel>("%ModalText").Text.Contains("Could not save:"), "ending exposes manual save failure without claiming success");
        }
        finally { Directory.Delete(manual); File.Move(preserved, manual); }
        Unchanged(before);

        Action("Start a new watch");
        _game.Audio.ApplyBeatCue("owned-completion-probe", "bell_lowered", true);
        Check(_game.Audio.Effects.Playing, "synthetic native effect active before restart discontinuity");
        Action("Start new watch");
        Check(Modal is null && !Session.IsEnding && Session.Beat.Id == _story.Chapters[0].Scenes[0].Beats[0].Id, "confirmed restart renders first authored beat immediately");
        Check(Session.History.Count == 1 && Session.SolvedActivities.Count == 0 && Session.ActiveStageCues.Length == 0, "restart removes old transcript, solved gates and historical cues");
        Check(Observe<ReadingTextStyle>("_readingText").Percent == 150 && _game.Audio.Muted, "restart retains session reading and sound preferences");
        Check(!_game.Audio.Effects.Playing && _game.Audio.CurrentLocation == Session.Scene.Location, "restart suppresses historical effect and selects first location audio");
        Unchanged(before);
        staleSave.EmitSignal(BaseButton.SignalName.Pressed);
        Check(Modal is null && !Session.IsEnding, "retired ending save callback cannot affect replacement watch"); Unchanged(before);
        Press("AdvanceButton");
        Check(Session.History.Count == 2 && Snapshot(Storage.Read(true)) == Snapshot(Session.Snapshot()), "new watch Continue creates ordinary fresh autosave");
        Check(File.ReadAllBytes(manual).SequenceEqual(before["save.json"]), "new autosave does not overwrite finished manual watch");
        var second = Session.Beat.Id;
        staleRestart.EmitSignal(BaseButton.SignalName.Pressed);
        Check(Session.Beat.Id == second, "late restart confirmation cannot reset advancing new watch");

        while (Session.Beat.Conversation is null)
        {
            if (Session.Beat.Activity is { } activity) { Press("AdvanceButton"); Action(activity.Options[activity.CorrectIndex]); }
            Press("AdvanceButton"); await Frame();
        }
        var chat = Session.Beat.Conversation!;
        Press("TalkButton");
        var entry = _game.InterfaceRoot.GetNode<LineEdit>("%PlayerEntry");
        entry.Text = "A new watch, a freely edited question — λ."; Press("SendButton");
        var deadline = Time.GetTicksMsec() + 5000;
        while (Observe<bool>("_busy") && Time.GetTicksMsec() < deadline) await Frame();
        Check(!Observe<bool>("_busy") && Session.History.Last().Text == chat.Fallback && !Session.History.Last().Generated, "free input works with authored fallback in replay without provider call");
        Check(!Session.SolvedActivities.Any(), "new watch has not inherited solved evidence gates");
        Press("ReturnButton");
        LoadFinished();
        Check(Snapshot(Session.Snapshot()) == finished, "finished manual record reopens after playing and chatting in new watch");
        Check(!_game.Audio.Effects.Playing, "completed-save load does not replay historical bell effect");
    }

    public override async void _Ready()
    {
        try
        {
            _root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_COMPLETION_FIXTURE") ?? "";
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(_root) && File.Exists(Path.Combine(_root, "owned-fixture")), "explicit owned debug fixture required");
            Check(ProjectSettings.GlobalizePath("user://").StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "normal user slots are inside owned fixture");
            _story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frame();
            var mode = System.Environment.GetEnvironmentVariable("LANTERNWAKE_COMPLETION_MODE") ?? "complete";
            if (mode == "preview")
            {
                Check(Storage.Mode == SessionMode.AuthorPreview && Session.IsEnding, "selected final-beat author preview remains isolated");
                _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
                if (Session.Beat.Activity is { } activity)
                {
                    Check(!Session.CanAdvance, "preview leaves final evidence unsolved");
                    Press("AdvanceButton"); Action(activity.Options[activity.CorrectIndex]);
                    Check(_game.InterfaceRoot.GetNode<Button>("%AdvanceButton").Text == "Finish  ›", "preview final evidence exposes Finish before opening completion");
                }
                Press("AdvanceButton");
                Check(Modal!.Title == "The light remains", "preview reaches ending only after solving final evidence");
                var actions = Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Select(b => b.Text).ToArray();
                Check(!actions.Contains("Save finished watch") && !actions.Contains("Start a new watch"), "author preview cannot enter player replay or write finished saves");
            }
            else
            {
                Check(Storage.Mode == SessionMode.Normal, "actual normal-mode slots");
                if (mode == "complete") await CompleteWatch();
                else
                {
                    Press("SettingsButton"); Action("Larger reading text"); Action("Larger reading text"); Action("Toggle instant text");
                    _game.Audio.SetMuted(true); LoadFinished();
                }
                await ReviewAndRestart();
            }
            GD.Print("LANTERNWAKE_WATCH_COMPLETION_OK " + JsonSerializer.Serialize(new { mode, checks = _checks, nativeBeats = _beats, finalBeat = Session.Beat.Id }));
            Action("Quit game");
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
