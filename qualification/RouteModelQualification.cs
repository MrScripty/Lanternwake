#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

public partial class RouteModelQualification : Node
{
    private GameView? _game;
    private int _checks;
    private T Read<T>(string field) => (T)typeof(GameView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_game)!;
    private Window Modal => Read<Window>("_modal");
    private StorySession Session => Read<StorySession>("_session");
    private Button Button(string text) => Modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == text);
    private void Action(string text) => Button(text).EmitSignal(BaseButton.SignalName.Pressed);
    private void Press(string name) => NativeGameControls.Press(_game!, name);
    private void Check(bool value, string claim) { if (!value) throw new Exception("Route model: " + claim); _checks++; }
    private async Task Frames(int count = 2) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private string Snapshot() => JsonSerializer.Serialize(Session.Snapshot(), Story.Json);
    private Label Block(string name) => Modal.GetNode<VBoxContainer>("%ModalActions").GetNode<VBoxContainer>("RouteModelBoard").FindChildren(name, "Label", true, false).OfType<Label>().Single();
    private string RescueArea() => Block("RescueOneBlock").GetParent().Name == "unplotted" ? "unplotted" : Block("RescueOneBlock").GetParent().GetParent().Name.ToString();
    private void Present(SaveData save)
    {
        Session.Restore(save);
        typeof(GameView).GetMethod("RenderBeat", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_game, [false]);
        _game!.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
    }
    public override async void _Ready()
    {
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_ROUTE_FIXTURE") ?? "";
            Check(Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")) &&
                ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "owned profile required");
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frames();
            Press("AdvanceButton");
            Press("SettingsButton"); Action("Larger reading text"); Action("Larger reading text"); Action("Toggle instant text");
            var story = Read<Story>("_story"); var traversal = new StorySession(story);
            while (traversal.Beat.Id != RouteReconstruction.ActivityBeatId)
            {
                if (traversal.Beat.Activity is { } activity) traversal.AnswerActivity(activity.CorrectIndex);
                if (!traversal.Advance()) throw new Exception("Missing route checkpoint");
            }
            var target = traversal.Snapshot(); Present(target); Press("SaveButton");
            var state = Snapshot(); var directory = ProjectSettings.GlobalizePath("user://");
            var files = Directory.GetFiles(directory, "*.json").ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);
            void Unchanged()
            {
                Check(Snapshot() == state && !Session.CanAdvance, "exploration preserves pending beat, transcript, facts/items and solved gates");
                var now = Directory.GetFiles(directory, "*.json");
                Check(now.Length == files.Count && now.All(path => files[Path.GetFileName(path)]!.SequenceEqual(File.ReadAllBytes(path))), "every save/settings byte remains unchanged");
            }
            Press("AdvanceButton"); Action("Explore route model"); await Frames();
            Check(Modal.Title == "Route reconstruction" && RescueArea() == "unplotted", "real model begins without a fabricated departure position");
            Check(Button("Previous supported position").Disabled && !Button("Next supported position").Disabled, "initial controls bound supported movement");
            var retiredNext = Button("Next supported position"); var retiredBack = Button("Back to question");
            Action("Next supported position"); Check(RescueArea() == "east", "actual Next moves rescue block onto eastern approach");
            var active = Modal; retiredNext.EmitSignal(BaseButton.SignalName.Pressed); retiredBack.EmitSignal(BaseButton.SignalName.Pressed);
            Check(Modal == active && Modal.GetNode<RichTextLabel>("%ModalText").Text.Contains("Rescue one: Eastern approach"), "retired model buttons cannot move or close the replacement");
            Action("Next supported position");
            Check(Button("Next supported position").Disabled && RescueArea() == "east" && Button("Compare corrected approach").HasFocus(), "issued route stops at exposed turn with enabled focus");
            Action("Compare corrected approach"); Check(RescueArea() == "unplotted", "switching route resets the comparison");
            Action("Next supported position");
            Check(RescueArea() == "west" && Button("Next supported position").Disabled && Modal.GetNode<RichTextLabel>("%ModalText").Text.Contains("no safe arrival"), "corrected route stops at its conditional decision point");
            Check(Block("WorkboatBlock").GetParent().GetParent().Name == "quay", "workboat never leaves its documented mooring");
            Modal.Size = new(640, 480); await Frames(4);
            var scroll = Modal.GetNode<ScrollContainer>("%ModalActionsScroll");
            var board = Modal.GetNode<VBoxContainer>("%ModalActions").GetNode<VBoxContainer>("RouteModelBoard");
            Check(board.Size.X <= scroll.Size.X + 1, $"board fits 640px window: board={board.Size.X} scroll={scroll.Size.X}");
            var authoredFont = Modal.Theme.HasFontSize("font_size", "Label") ? Modal.Theme.GetFontSize("font_size", "Label") : Modal.Theme.DefaultFontSize;
            Check(Block("RescueOneBlock").GetThemeFontSize("font_size") == (int)Math.Round(authoredFont * 1.5, MidpointRounding.AwayFromZero),
                "board uses 150 percent of the owner's authored label font");
            var bar = scroll.GetVScrollBar(); bar.GrabFocus();
            bar.EmitSignal(Control.SignalName.GuiInput, new InputEventKey { Pressed = true, Keycode = Key.Home }); await Frames();
            Check(bar.HasFocus() && scroll.ScrollVertical == 0, "keyboard Home exposes the whole board above the action rows");
            Action("Read supporting passages");
            var sourceText = string.Join("\n\n", Session.Scene.Beats.Where(b => RouteReconstruction.SourceBeatIds.Contains(b.Id)).Select(b => b.Text));
            Check(Modal.Title == "Route comparison sources" && Modal.GetNode<RichTextLabel>("%ModalText").Text == sourceText, "source reader shows only the exact earlier canonical passages");
            Modal.EmitSignal(Window.SignalName.WindowInput, new InputEventKey { Pressed = true, Keycode = Key.Escape });
            Check(RescueArea() == "west" && Button("Read supporting passages").HasFocus(), "source Escape retains model position and restores focus"); Unchanged();
            Action("Previous supported position"); Check(RescueArea() == "unplotted", "comparison can return to unplotted");
            Action("Next supported position"); Action("Reset blocks"); Check(RescueArea() == "unplotted", "Reset clears only model placement");
            var cancelled = Button("Next supported position");
            Modal.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
            Check(Modal.Title == "Compare the evidence" && Button("Explore route model").HasFocus(), "Close returns to same unanswered question and origin focus");
            cancelled.EmitSignal(BaseButton.SignalName.Pressed); Unchanged();
            Action("Explore route model"); var beforeLoad = Button("Next supported position");
            Press("LoadButton"); Action("Load current manual save · " + RouteReconstruction.ActivityBeatId);
            beforeLoad.EmitSignal(BaseButton.SignalName.Pressed); Check(Read<Window?>("_modal") is null, "load retires model callbacks even on the same beat/session"); Unchanged();
            _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").VisibleCharacters = -1;
            Press("AdvanceButton");
            Check(Session.Beat.Activity!.Options.All(option => Button(option) is not null), "unchanged canonical answer options remain available without model use");
            Action(Session.Beat.Activity.Options[Session.Beat.Activity.CorrectIndex]);
            Check(Session.CanAdvance && !RouteReconstruction.AvailableAt(Session), "only the explicit canonical answer solves the gate");
            Press("AdvanceButton"); Check(Session.Beat.Id != RouteReconstruction.ActivityBeatId, "ordinary Continue rejoins the fixed authored story");
            GD.Print($"LANTERNWAKE_ROUTE_MODEL_OK checks={_checks} routes=2 snapshot_and_files_preserved=true small_window=640x480 reading=150");
            Press("SettingsButton"); Action("Quit game");
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
