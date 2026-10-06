#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Owned fixture: actual question/review controls, all authored gates, no provider transport.</summary>
public partial class ActivityReviewQualification : Node
{
    private GameView _game = null!;
    private Story _story = null!;
    private int _checks, _activities, _beats;
    private bool _optionalRecord;
    private Action? _afterAdvance;
    private StorySession Session => Observe<StorySession>("_session");
    private Window? Modal => Observe<Window?>("_modal");
    private T Observe<T>(string name) => (T)typeof(GameView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_game)!;
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Question review: " + claim);
        _checks++;
    }
    private void Press(string name) => _game.InterfaceRoot.GetNode<Button>("%" + name).EmitSignal(BaseButton.SignalName.Pressed);
    private Button ActionButton(string text) => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == text);
    private void Action(string text) => ActionButton(text).EmitSignal(BaseButton.SignalName.Pressed);
    private Button CloseButton => Modal!.GetNode<Button>("%ModalCloseButton");
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private string Snapshot() => JsonSerializer.Serialize(Session.Snapshot(), Story.Json);
    private Dictionary<string, byte[]> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json").ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes);
    private void Unchanged(string state, Dictionary<string, byte[]> files)
    {
        Check(Snapshot() == state && !Session.CanAdvance, "unanswered beat, transcript and solved gates remain exact");
        var current = Files();
        Check(files.Count == current.Count && files.All(p => current.TryGetValue(p.Key, out var bytes) && bytes.SequenceEqual(p.Value)), "every current/previous manual/automatic save byte preserved");
    }
    private void Question(EvidenceActivity activity)
    {
        Check(Modal?.Title == "Compare the evidence" && Modal.GetNode<RichTextLabel>("%ModalText").Text == activity.Prompt, "same authored question prompt restored");
        var buttons = Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().ToArray();
        Check(buttons.Select(b => b.Text).SequenceEqual(activity.Options.Concat(["Review known evidence", "Read the record"])), "exact authored options, order and two review actions");
    }
    private async Task Layout()
    {
        Modal!.Size = new Vector2I(640, 480); await Frame(); await Frame();
        var scroll = Modal.GetNode<ScrollContainer>("%ModalActionsScroll");
        Check(scroll.FollowFocus && scroll.CustomMinimumSize.Y <= Modal.Size.Y * .5f, "enlarged question actions scroll within half the small window");
        var buttons = Modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().ToArray();
        Check(buttons.All(b => b.GetThemeFontSize("font_size") == 24), "all answer/review choices use 150% authored text");
        buttons.Last().GrabFocus(); await Frame(); await Frame();
        Check(buttons.Last().HasFocus() && buttons.Last().GlobalPosition.Y >= scroll.GlobalPosition.Y - 1 &&
            buttons.Last().GlobalPosition.Y + buttons.Last().Size.Y <= scroll.GlobalPosition.Y + scroll.Size.Y + 1,
            "focused last review action is actually visible in scrolling viewport");
        CloseButton.GrabFocus();
        Check(CloseButton.HasFocus() && CloseButton.GlobalPosition.Y + CloseButton.Size.Y <= Modal.Size.Y, "question Close stays reachable at enlarged size");
    }

    private async Task ReviewActivity(bool preview)
    {
        _activities++;
        var activity = Session.Beat.Activity!;
        if (!preview) Press("SaveButton");
        var state = Snapshot(); var files = Files(); var beatId = Session.Beat.Id;
        Press("AdvanceButton"); Question(activity);
        Check(ActionButton("Review known evidence").HasFocus(), "opening question focuses evidence without selecting an answer");
        await Layout();
        var staleAnswer = ActionButton(activity.Options[activity.CorrectIndex]);
        var staleReview = ActionButton("Review known evidence"); var staleQuestionClose = CloseButton;
        var oldQuestion = Modal!;
        Action("Review known evidence");
        Check(Modal!.Title == "Your catalogue", "visible question action opens catalogue");
        var expected = "OBJECTS\n\n" + string.Join("\n\n", Session.Inventory.Select(i => i.Name + "\n" + i.Description)) +
            "\n\nESTABLISHED FACTS\n\n" + string.Join("\n\n", Session.KnownFacts.Select(f => f.Text));
        var text = Modal.GetNode<RichTextLabel>("%ModalText").Text;
        Check(text == expected, "catalogue displays exactly current objects and established facts");
        Check(_story.Facts.Except(Session.KnownFacts).All(f => !text.Contains(f.Text)) &&
            _story.Items.Except(Session.Inventory).All(i => !text.Contains(i.Description)), "unlocked boundary excludes every future fact/object description");
        var review = Modal;
        staleAnswer.EmitSignal(BaseButton.SignalName.Pressed); staleReview.EmitSignal(BaseButton.SignalName.Pressed);
        staleQuestionClose.EmitSignal(BaseButton.SignalName.Pressed); oldQuestion.EmitSignal(Window.SignalName.CloseRequested);
        oldQuestion.EmitSignal(Window.SignalName.WindowInput, new InputEventKey { Pressed = true, Keycode = Key.Escape });
        Check(Modal == review, "retired answer, review and all close routes cannot affect catalogue"); Unchanged(state, files);
        var staleBack = ActionButton("Back to question"); var staleReviewClose = CloseButton;
        Action("Back to question"); Question(activity);
        Check(ActionButton("Review known evidence").HasFocus(), "return restores evidence-action keyboard focus");
        var returned = Modal;
        staleBack.EmitSignal(BaseButton.SignalName.Pressed); staleReviewClose.EmitSignal(BaseButton.SignalName.Pressed);
        Check(Modal == returned, "retired return/close callbacks cannot replace question"); Unchanged(state, files);

        Action("Read the record");
        text = Modal!.GetNode<RichTextLabel>("%ModalText").Text;
        string Name(string id) => id == "narrator" ? "" : id == "you" ? "You" : _story.Characters.FirstOrDefault(c => c.Id == id)?.Name ?? id;
        expected = string.Join("\n\n", Session.History.Select(h => (Name(h.Speaker) is { Length: > 0 } name ?
            name + (h.Generated ? " [optional local dialogue]" : "") + ":\n" : "") + h.Text));
        Check(Modal.Title == "The record" && text == expected, "record contains exactly the reached transcript in order");
        if (_optionalRecord) Check(text.Contains(" [optional local dialogue]"), "fixture-generated conversation remains explicitly optional");
        Check(CloseButton.Text == "Back to question", "review Close explains its return destination");
        // Exercise actual Window Escape, Close button and main-view Escape routes independently.
        Modal.EmitSignal(Window.SignalName.WindowInput, new InputEventKey { Pressed = true, Keycode = Key.Escape }); Question(activity);
        Check(ActionButton("Read the record").HasFocus(), "record Escape restores originating focus");
        Action("Read the record"); CloseButton.EmitSignal(BaseButton.SignalName.Pressed); Question(activity);
        Action("Review known evidence"); _game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.Escape }); Question(activity);
        Unchanged(state, files);
        var beforeCancel = ActionButton(activity.Options[activity.CorrectIndex]);
        CloseButton.EmitSignal(BaseButton.SignalName.Pressed);
        Check(Modal is null && _game.InterfaceRoot.GetNode<Button>("%AdvanceButton").HasFocus(), "cancelling question returns to same beat with Continue focus");
        beforeCancel.EmitSignal(BaseButton.SignalName.Pressed); Unchanged(state, files);
        Press("AdvanceButton");
        if (activity.OptionFeedback is { } feedback)
        {
            for (var wrong = 0; wrong < activity.Options.Length; wrong++)
            {
                if (wrong == activity.CorrectIndex) continue;
                var retiredQuestion = Modal;
                var retiredAnswer = ActionButton(activity.Options[activity.CorrectIndex]);
                Action(activity.Options[wrong]);
                Check(Modal?.Title == "Check the source" && Modal.GetNode<RichTextLabel>("%ModalText").Text == feedback[wrong], "wrong option shows its exact authored explanation in the keyboard reader");
                var feedbackWindow = Modal;
                retiredAnswer.EmitSignal(BaseButton.SignalName.Pressed);
                retiredQuestion!.EmitSignal(Window.SignalName.CloseRequested);
                Check(Modal == feedbackWindow, "retired question cannot solve or close feedback"); Unchanged(state, files);
                Action("Back to question"); Question(activity);
                Check(ActionButton(activity.Options[wrong]).HasFocus(), "retry returns focus to the originating option without selecting it");
            }
        }
        else
        {
            Action(activity.Options.First(option => option != activity.Options[activity.CorrectIndex]));
            Check(Modal is null && _game.InterfaceRoot.GetNode<Label>("%StatusLabel").Text.Contains("does not fit"), "wrong answer retains ordinary retry feedback");
            Unchanged(state, files); Press("AdvanceButton");
        }

        if (!preview)
        {
            var loadedSession = Session;
            var retired = ActionButton(activity.Options[activity.CorrectIndex]);
            var retiredRecord = ActionButton("Read the record"); var retiredClose = CloseButton;
            Press("LoadButton"); Action("Load current manual save · " + beatId);
            Check(Session == loadedSession, "real Load restores the same session instance");
            _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
            Press("AdvanceButton"); var restored = Modal;
            retired.EmitSignal(BaseButton.SignalName.Pressed); retiredRecord.EmitSignal(BaseButton.SignalName.Pressed); retiredClose.EmitSignal(BaseButton.SignalName.Pressed);
            Check(Modal == restored, "pre-load callbacks cannot answer/review/close restored question"); Question(activity); Unchanged(state, files);
        }
        if (Session.IsEnding)
        {
            Action("Review known evidence"); var retiring = Modal;
            typeof(GameView).GetField("_closing", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_game, true);
            try
            {
                Action("Back to question"); CloseButton.EmitSignal(BaseButton.SignalName.Pressed);
                staleAnswer.EmitSignal(BaseButton.SignalName.Pressed);
                Check(Modal == retiring, "late review/close/answer callbacks are inert during shutdown"); Unchanged(state, files);
            }
            finally { typeof(GameView).GetField("_closing", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_game, false); }
            Action("Back to question");
        }
        var answeredButton = ActionButton(activity.Options[activity.CorrectIndex]);
        Action(activity.Options[activity.CorrectIndex]);
        Check(Modal is null && Session.CanAdvance && Session.SolvedActivities.Contains(beatId), "only explicit correct answer solves current evidence gate");
        if (Session.IsEnding) Check(_game.InterfaceRoot.GetNode<Button>("%AdvanceButton").Text == "Finish  ›", "final answer preserves corrected Finish label");
        if (preview)
        {
            var current = Files(); Check(files.Count == current.Count && files.All(p => current[p.Key].SequenceEqual(p.Value)), "author-preview answer writes no player slot");
        }
        else Check(JsonSerializer.Serialize(Observe<SessionStorage>("_storage").Read(true), Story.Json) == Snapshot(), "correct answer retains ordinary autosave semantics");
        staleAnswer.EmitSignal(BaseButton.SignalName.Pressed);
        _afterAdvance = () =>
        {
            var advanced = Snapshot(); var saved = Files();
            answeredButton.EmitSignal(BaseButton.SignalName.Pressed); staleReview.EmitSignal(BaseButton.SignalName.Pressed);
            Check(Modal is null && Snapshot() == advanced, "late solved-question callbacks cannot affect the next authored beat");
            var current = Files();
            Check(saved.Count == current.Count && saved.All(p => current[p.Key].SequenceEqual(p.Value)), "late callbacks after Continue write no extra save");
        };
    }

    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_ACTIVITY_REVIEW_FIXTURE") ?? "";
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")), "explicit owned debug fixture required");
            Check(ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "all user slots are inside owned fixture");
            _story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frame();
            var preview = Observe<SessionStorage>("_storage").Mode == SessionMode.AuthorPreview;
            if (!preview) Press("AdvanceButton");
            Press("SettingsButton"); Action("Larger reading text"); Action("Larger reading text"); Action("Toggle instant text");
            _game.Audio.SetMuted(true);
            string chapter = "";
            while (true)
            {
                _beats++;
                if (Session.Chapter.Id != chapter) { chapter = Session.Chapter.Id; GD.Print("Question-review traversal: " + chapter); }
                if (!preview && !_optionalRecord && Session.Beat.Conversation is not null)
                {
                    var known = Session.KnownFacts.ToArray();
                    Session.RecordConversation("Owned test question", "Owned optional local reply", true);
                    _optionalRecord = true;
                    Check(Session.KnownFacts.SequenceEqual(known), "optional fixture conversation unlocks no facts");
                }
                if (Session.Beat.Activity is not null) await ReviewActivity(preview);
                if (Session.IsEnding) break;
                var before = Session.Beat.Id; Press("AdvanceButton");
                _afterAdvance?.Invoke(); _afterAdvance = null; await Frame();
                Check(Session.Beat.Id != before, "actual Continue advances canonical beat after explicit answer");
            }
            Check(_activities == (preview ? 1 : _story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).Count(b => b.Activity is not null)), "all expected authored evidence activities independently reviewed");
            Check(_beats == (preview ? 1 : _story.Chapters.Sum(c => c.Scenes.Sum(s => s.Beats.Length))), "all expected authored beats reached through native controls");
            GD.Print("LANTERNWAKE_ACTIVITY_REVIEW_OK " + JsonSerializer.Serialize(new { preview, checks = _checks, activities = _activities, nativeBeats = _beats }));
            Press("AdvanceButton"); Action("Quit game");
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
