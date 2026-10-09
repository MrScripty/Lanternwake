#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

public partial class HistoryResumeQualification : Node
{
    private GameView _game = null!;
    private int _checks;
    private T Read<T>(string field) => (T)typeof(GameView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_game)!;
    private StorySession Session => Read<StorySession>("_session");
    private Window? Modal => Read<Window?>("_modal");
    private RichTextLabel Prose => Modal!.GetNode<RichTextLabel>("%ModalText");
    private void Press(string name) => NativeGameControls.Press(_game, name);
    private void Close() => Modal!.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
    private void Action(string text) => Modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == text).EmitSignal(BaseButton.SignalName.Pressed);
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("History resume: " + claim);
        _checks++;
    }
    private async Task Frames(int count = 4)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Key(Key key)
    {
        Modal!.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Modal.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Frames();
    }
    private string State() => JsonSerializer.Serialize(Session.Snapshot(), Story.Json);
    private Dictionary<string, byte[]> Slots() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json").ToDictionary(p => Path.GetFileName(p), File.ReadAllBytes);
    private void Preserved(string state, Dictionary<string, byte[]> slots)
    {
        Check(State() == state, "reading keeps the exact beat, history and solved gates");
        var now = Slots();
        Check(now.Count == slots.Count && slots.All(p => now.TryGetValue(p.Key, out var bytes) && bytes.SequenceEqual(p.Value)), "reading preserves every manual/autosave/recovery byte");
    }
    private void Recent()
    {
        var bar = Prose.GetVScrollBar();
        var latest = Prose.GetParagraphOffset(Prose.GetParagraphCount() - 1);
        Check(bar.Page > 0 && bar.MaxValue > bar.Page && bar.Value >= Math.Min(latest, bar.MaxValue - bar.Page) - 1 && bar.Value + bar.Page > latest,
            $"late record opens at recent end after layout (value={bar.Value}, max={bar.MaxValue}, page={bar.Page}, paragraphs={Prose.GetParagraphCount()}, size={Prose.Size}, window={Modal!.Size})");
        Check(!Prose.ScrollFollowing && !Prose.ScrollFollowingVisibleCharacters, "no ongoing scroll-following policy enabled");
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            var fixture = OS.GetEnvironment("LANTERNWAKE_HISTORY_FIXTURE");
            if (!Path.IsPathFullyQualified(fixture) || !File.Exists(Path.Combine(fixture, "owned-fixture")) || !ProjectSettings.GlobalizePath("user://").StartsWith(fixture + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Owned isolated history fixture required.");
            // Headless native windows have no usable client area. Embed this
            // fixture's dialogs so the normal containers can lay out their prose.
            GetTree().Root.GuiEmbedSubwindows = true;
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frames();
            Press("AdvanceButton"); var fresh = State(); var freshSlots = Slots();
            Press("HistoryButton"); await Frames();
            Check(Prose.Text == Session.Beat.Text && Prose.GetVScrollBar().Value == 0, "new story opens its complete first passage at the beginning");
            Close(); Preserved(fresh, freshSlots);

            var story = Read<Story>("_story"); var setup = new StorySession(story);
            while (setup.Beat.Id != "ch4_s1a_b035")
            {
                if (setup.Beat.Activity is { } activity) setup.AnswerActivity(activity.CorrectIndex);
                if (!setup.Advance()) throw new InvalidOperationException("Late fixture beat missing.");
            }
            SaveStore.Write(Path.Combine(ProjectSettings.GlobalizePath("user://"), "save.json"), setup.Snapshot());
            Press("LoadButton"); Action("Load current manual save · ch4_s1a_b035"); await Frames();
            Check(Session.Beat.Id == setup.Beat.Id && State() == JsonSerializer.Serialize(setup.Snapshot(), Story.Json), "actual Load restores the exact late story fixture");
            var state = State(); var slots = Slots();
            foreach (var percent in new[] { 100, 150 })
            {
                if (percent == 150) { Press("SettingsButton"); Action("Larger reading text"); Action("Larger reading text"); Close(); }
                Press("HistoryButton"); Modal!.Size = new(640, 480); await Frames(); Recent();
                var text = Prose.Text;
                Check(text.StartsWith(story.Chapters[0].Scenes[0].Beats[0].Text, StringComparison.Ordinal) && text.EndsWith(Session.Beat.Text, StringComparison.Ordinal), "complete chronological record retained from arrival through current passage");
                var bar = Prose.GetVScrollBar(); bar.GrabFocus(); await Key(Godot.Key.Pageup);
                Check(bar.HasFocus() && bar.Value < bar.MaxValue - bar.Page - 1, "Page Up reaches earlier context with scrollbar focus");
                await Key(Godot.Key.Home); Check(bar.Value == 0, "Home reaches original arrival");
                await Frames(8); Check(bar.Value == 0, "one-time opening scroll does not pull manual scrollback forward");
                await Key(Godot.Key.Down); Check(bar.Value > 0, "Down reads earlier record");
                await Key(Godot.Key.Up); Check(bar.Value == 0, "Up returns to arrival");
                await Key(Godot.Key.End); Recent();
                Preserved(state, slots);
                await Key(Godot.Key.Tab);
                Check(Modal!.GetNode<Button>("%ModalCloseButton").HasFocus(), "Tab reaches Close from scrollbar");
                await Key(Godot.Key.Enter);
                Check(Modal is null && _game.InterfaceRoot.GetNode<Button>("%AdvanceButton").HasFocus(), "keyboard Close restores story focus");
                Press("HistoryButton"); Modal!.Size = new(640, 480); await Frames(); Recent();
                Check(Prose.Text == text, "reopening resets only position, never record content"); Close(); Preserved(state, slots);
            }
            // A reader retired before its deferred scroll must not move a replacement panel.
            Press("HistoryButton"); Close(); Press("EvidenceButton"); await Frames();
            Check(Modal?.Title == "Your catalogue" && Prose.GetVScrollBar().Value == 0, "retired deferred reader cannot scroll the replacement catalogue"); Close();
            // Busy History is inert even when another panel is already open.
            Press("EvidenceButton"); var catalogue = Modal;
            typeof(GameView).GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_game, true);
            Press("HistoryButton"); await Frames();
            Check(Modal == catalogue && Prose.GetVScrollBar().Value == 0, "busy History cannot schedule a scroll on another panel");
            typeof(GameView).GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_game, false); Close(); Preserved(state, slots);
            await _game.Audio.StopAndRetireAsync();
            Press("HistoryButton"); _game.Free(); await Frames();
            Check(!GodotObject.IsInstanceValid(_game), "teardown before deferred dispatch safely retires the reader");
            GD.Print($"LANTERNWAKE_HISTORY_RESUME_OK checks={_checks} fresh_late_reopen_keyboard=true state_unchanged=true embedded_headless=true");
            GetTree().Quit();
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
