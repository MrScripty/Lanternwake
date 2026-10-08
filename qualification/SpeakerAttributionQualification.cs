#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

public partial class SpeakerAttributionQualification : Node
{
    private GameView? _game;
    private int _checks;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private T Read<T>(string field) => (T)typeof(GameView).GetField(field, Private)!.GetValue(_game)!;
    private void Check(bool value, string claim) { if (!value) throw new InvalidOperationException("Speaker attribution: " + claim); _checks++; }
    private async Task Layout() { for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Press(string name) => NativeGameControls.Press(_game!, name);
    private void Action(string caption) => Read<Window>("_modal").GetNode<VBoxContainer>("%ModalActions")
        .GetChildren().OfType<Button>().Single(b => b.Text == caption).EmitSignal(BaseButton.SignalName.Pressed);
    private void Render(SaveData checkpoint)
    {
        Read<StorySession>("_session").Restore(checkpoint);
        typeof(GameView).GetMethod("RenderBeat", Private)!.Invoke(_game, [false]);
    }
    private static bool Inside(Rect2 outer, Rect2 inner) => inner.Position.X >= outer.Position.X - 1 &&
        inner.Position.Y >= outer.Position.Y - 1 && inner.End.X <= outer.End.X + 1 && inner.End.Y <= outer.End.Y + 1;

    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_SPEAKER_FIXTURE") ?? "";
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")) &&
                ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "owned profile required");
            var story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            var traversal = new StorySession(story);
            var checkpoints = new Dictionary<string, SaveData>();
            SaveData? conversation = null, longRecording = null, exchangeCheckpoint = null;
            int longest = 0;
            do
            {
                checkpoints.TryAdd(traversal.Beat.Speaker, traversal.Snapshot());
                if (traversal.Beat.Conversation is not null) conversation ??= traversal.Snapshot();
                if (traversal.Beat.Exchange is not null) exchangeCheckpoint ??= traversal.Snapshot();
                if (traversal.Beat.Speaker == "ivo" && traversal.Beat.Text.Length > longest)
                { longest = traversal.Beat.Text.Length; longRecording = traversal.Snapshot(); }
                if (traversal.Beat.Activity is { } activity) traversal.AnswerActivity(activity.CorrectIndex);
            } while (traversal.Advance());
            var export = System.Environment.GetEnvironmentVariable("LANTERNWAKE_SPEAKER_EXPORT") ?? "";
            if (export.Length > 0)
            {
                Check(Path.IsPathFullyQualified(export) && File.Exists(Path.Combine(export, "owned-fixture")), "marked export directory required");
                foreach (var (name, save) in new Dictionary<string, SaveData>
                {
                    ["recording"] = longRecording!, ["conversation"] = conversation!, ["exchange"] = exchangeCheckpoint!,
                }) File.WriteAllText(Path.Combine(export, name + ".json"), JsonSerializer.Serialize(save, Story.Json));
            }
            var main = GD.Load<PackedScene>("res://Scenes/Main.tscn");
            for (int authored = 0; authored < 2; authored++)
            {
                _game = main.Instantiate<GameView>();
                var passage = _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText");
                Label? custom = null;
                if (authored == 1)
                {
                    custom = new Label { Name = "SpeakerLabel", UniqueNameInOwner = true, MouseFilter = Control.MouseFilterEnum.Ignore };
                    custom.AddThemeFontSizeOverride("font_size", 31);
                    passage.GetParent().AddChild(custom); custom.Owner = passage.Owner;
                    passage.GetParent().MoveChild(custom, passage.GetIndex());
                }
                AddChild(_game); await Layout();
                Press("AdvanceButton"); await Layout();
                var speaker = Read<Label?>("_speaker");
                Check(speaker is not null, "normal Main renders a speaker label when the authored scene omits one");
                if (custom is not null) Check(speaker == custom && passage.GetParent().GetChildren().OfType<Label>().Count() == 1,
                    "authored speaker control is reused without a duplicate fallback");
                else Check(speaker!.GetParent() == passage.GetParent() && speaker.GetIndex() == passage.GetIndex() - 1 && speaker.Owner is null &&
                    speaker.FocusMode == Control.FocusModeEnum.None && speaker.MouseFilter == Control.MouseFilterEnum.Ignore,
                    "fallback is unsaved, immediately above prose and does not intercept input");
                int baseFont = speaker!.GetThemeFontSize("font_size");
                foreach (int percent in new[] { 100, 150, 100 })
                {
                    Press("SettingsButton");
                    if (percent == 150) { Action("Larger reading text"); Action("Larger reading text"); }
                    else Action("Reset reading text size · " + Read<ReadingTextStyle>("_readingText").Percent + "%");
                    Read<Window>("_modal").EmitSignal(Window.SignalName.CloseRequested);
                    await Layout();
                    Check(speaker.GetThemeFontSize("font_size") == (int)Math.Round(baseFont * percent / 100d, MidpointRounding.AwayFromZero), "speaker uses inherited or authored font without compounded scaling");
                    foreach (var character in story.Characters)
                    {
                        Render(checkpoints[character.Id]); await Layout();
                        Check(speaker.IsVisibleInTree() && speaker.Text == character.Name && _game.CurrentSpeakerText == character.Name,
                            "visible canonical speaker name: " + character.Id);
                        Check(passage.Text == Read<StorySession>("_session").Beat.Text, "primary authored prose stays exact");
                        Check(Inside(_game.InterfaceRoot.GetGlobalRect(), speaker.GetGlobalRect()) &&
                            speaker.GetGlobalRect().End.Y <= passage.GetGlobalRect().Position.Y + 1 &&
                            Inside(_game.InterfaceRoot.GetGlobalRect(), _game.InterfaceRoot.GetNode<Button>("%AdvanceButton").GetGlobalRect()),
                            "speaker and continue fit without overlapping prose");
                    }
                    Render(checkpoints["narrator"]); await Layout();
                    Check(speaker.Text == "" && (authored == 1 || !speaker.Visible), "fallback collapses on narration while authored visibility is retained");
                }
                Render(longRecording!); passage.VisibleCharacters = -1; await Layout();
                Check(speaker.Text == "Ivo Vale (recording)", "recorded material remains explicitly identified");
                Press("SaveButton"); var storage = Read<SessionStorage>("_storage"); var expected = JsonSerializer.Serialize(storage.Read(false), Story.Json);
                Press("LoadButton"); Action("Load current manual save · " + longRecording!.BeatId); await Layout();
                Check(speaker.IsVisibleInTree() && speaker.Text == "Ivo Vale (recording)" && JsonSerializer.Serialize(storage.Read(false), Story.Json) == expected,
                    "actual Load restores recorded attribution without rewriting the saved snapshot");
                Render(conversation!); passage.VisibleCharacters = 5;
                typeof(GameView).GetField("_characters", Private)!.SetValue(_game, 5d);
                var pausedName = speaker.Text; var pausedText = passage.Text;
                Press("TalkButton"); Press("ReturnButton"); await Layout();
                Check(speaker.Text == pausedName && passage.Text == pausedText && (authored == 1 || speaker.Visible == (pausedName.Length > 0)), "optional conversation Return preserves speaker and canonical passage");
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    Press("TalkButton");
                    _game.InterfaceRoot.GetNode<LineEdit>("%PlayerEntry").Text = "What does the record establish?";
                    Press("SendButton"); await Layout();
                    var chat = Read<StorySession>("_session").Beat.Conversation!;
                    var character = story.Characters.Single(c => c.Id == chat.CharacterId);
                    Check(speaker.IsVisibleInTree() && speaker.Text == character.Name && passage.Text == chat.Fallback &&
                        _game.CurrentStatusText.StartsWith("Authored reply · AI conversations are turned off.", StringComparison.Ordinal),
                        "free response with AI off visibly identifies the authored speaker and capability state");
                    var replies = Read<StorySession>("_session").History.TakeLast(2).ToArray();
                    Check(replies.Length == 2 && replies[1].Speaker == character.Id && !replies[1].Generated && replies[1].Text == chat.Fallback,
                        "fallback is saved as authored interpretation without fabricated generation");
                    Press("ReturnButton"); await Layout();
                    Check(speaker.Text == pausedName && passage.Text == pausedText && (authored == 1 || !speaker.Visible),
                        "repeated fallback Return restores intentional unlabelled narration");
                }
                for (int option = 0; option < 3; option++)
                {
                    Render(exchangeCheckpoint!); passage.VisibleCharacters = -1;
                    var before = speaker.Text;
                    var exchange = Read<StorySession>("_session").Beat.Exchange!;
                    Press("TalkButton"); Action(exchange.Options[option].Label); await Layout();
                    var response = Read<Window>("_modal").GetNode<RichTextLabel>("%ModalText").Text;
                    Check(response.Contains("You:\n", StringComparison.Ordinal) && response.Contains(story.Characters.Single(c => c.Id == exchange.CharacterId).Name + ":\n", StringComparison.Ordinal) &&
                        response.EndsWith(exchange.Options[option].Reply, StringComparison.Ordinal), "each authored intention has its distinct speaker-labelled reply");
                    Action("Back to story"); await Layout();
                    Check(speaker.Text == before && (authored == 1 || !speaker.Visible), "authored exchange Return restores narration attribution");
                    Press("LoadButton"); Action("Load current autosave · " + exchangeCheckpoint!.BeatId); await Layout();
                    Check(Read<StorySession>("_session").ChosenExchangeIndex == option && speaker.Text == before,
                        "reloaded intention preserves speaker and selected authored response");
                    Press("TalkButton"); Check(Read<Window>("_modal").GetNode<RichTextLabel>("%ModalText").Text == response,
                        "repeated authored dialogue stays read only with the same labels");
                    Action("Back to story");
                }
                Check(await _game.Audio.StopAndRetireAsync(), "native playback ownership drains");
                _game.Free(); _game = null; await Layout();
            }
            GD.Print($"LANTERNWAKE_SPEAKER_ATTRIBUTION_OK checks={_checks} authored_and_fallback=true reading=100/150/100"); GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            if (GodotObject.IsInstanceValid(_game)) { await _game!.Audio.StopAndRetireAsync(); _game.Free(); _game = null; }
            GetTree().Quit(1);
        }
    }
}
#endif
