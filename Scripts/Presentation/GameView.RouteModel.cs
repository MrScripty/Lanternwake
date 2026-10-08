using Godot;
using Lanternwake.Core;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private void ShowRouteModel()
    {
        if (_busy || _closing || !RouteReconstruction.AvailableAt(_session)) return;
        var session = _session; var generation = _generation;
        var model = new RouteReconstruction();
        bool Current(Window? owner) => owner is not null && _modal == owner && _session == session &&
            _generation == generation && !_busy && !_closing && RouteReconstruction.AvailableAt(session);
        void Back(Window? owner) { if (Current(owner)) ShowActivity("Explore route model"); }
        void Sources(Window? owner)
        {
            if (!Current(owner)) return;
            var source = _story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats)
                .Where(beat => RouteReconstruction.SourceBeatIds.Contains(beat.Id));
            Window? reader = null;
            void Return() { if (Current(reader)) Render("Read supporting passages"); }
            ShowWindow("Route comparison sources", string.Join("\n\n", source.Select(beat => beat.Text)),
                [("Back to route model", Return)], Return);
            reader = _modal;
            if (reader is not null)
            {
                reader.GetNode<Button>("%ModalCloseButton").Text = "Back to route model";
                reader.GetNode<VBoxContainer>("%ModalActions").GetChild<Button>(0).GrabFocus();
            }
        }
        void Render(string? focus = null)
        {
            Window? owner = null;
            var actions = new List<(string, Action)>();
            void Add(string title, Action change) => actions.Add((title, () =>
            {
                if (!Current(owner)) return;
                change(); Render(title);
            }));
            Add("Next supported position", () => model.MoveForward());
            Add("Previous supported position", () => model.MoveBack());
            var compare = model.Route == RouteReconstruction.RouteKind.IssuedEastern ? "Compare corrected approach" : "Compare issued route";
            Add(compare, () => model.SelectRoute(model.Route == RouteReconstruction.RouteKind.IssuedEastern
                ? RouteReconstruction.RouteKind.CorrectedWestern : RouteReconstruction.RouteKind.IssuedEastern));
            Add("Reset blocks", model.Reset);
            actions.Add(("Read supporting passages", () => Sources(owner))); actions.Add(("Back to question", () => Back(owner)));
            ShowWindow("Route reconstruction", model.RouteTitle + "\n\nRescue one: " + model.Current.Title + "\n" + model.Current.Note +
                "\n\nAbstract labelled blocks, not a scale map. The west workboat stays at its documented mooring. Rescue two and the maintenance station have no established positions here.\n\nCompare only the supported positions. No fatal moment, imagined safe harbor or new historical outcome is simulated. Exploring does not answer the question or change your watch.", actions.ToArray(), () => Back(owner));
            owner = _modal;
            if (owner is null) return;
            var rows = owner.GetNode<VBoxContainer>("%ModalActions");
            var board = RouteBoard(model);
            rows.AddChild(board); rows.MoveChild(board, 0);
            foreach (var label in board.FindChildren("*", "Label", true, false).OfType<Label>()) _readingText.Register(label);
            var scrollbar = owner.GetNode<ScrollContainer>("%ModalActionsScroll").GetVScrollBar();
            scrollbar.FocusMode = Control.FocusModeEnum.All;
            scrollbar.GuiInput += input =>
            {
                if (!scrollbar.HasFocus() || input is not InputEventKey { Pressed: true } key) return;
                double? target = key.Keycode switch
                {
                    Key.Home => scrollbar.MinValue, Key.End => scrollbar.MaxValue - scrollbar.Page,
                    Key.Pageup => scrollbar.Value - scrollbar.Page, Key.Pagedown => scrollbar.Value + scrollbar.Page,
                    _ => null
                };
                if (target is null) return;
                scrollbar.Value = Math.Clamp(target.Value, scrollbar.MinValue, Math.Max(scrollbar.MinValue, scrollbar.MaxValue - scrollbar.Page));
                scrollbar.AcceptEvent();
            };
            var buttons = rows.GetChildren().OfType<Button>().ToArray();
            buttons.Single(button => button.Text == "Next supported position").Disabled = !model.CanMoveForward;
            buttons.Single(button => button.Text == "Previous supported position").Disabled = !model.CanMoveBack;
            owner.GetNode<Button>("%ModalCloseButton").Text = "Back to question";
            (buttons.FirstOrDefault(button => button.Text == focus && !button.Disabled) ??
                buttons.FirstOrDefault(button => button.Text == "Next supported position" && !button.Disabled) ??
                buttons.Single(button => button.Text == compare)).GrabFocus();
            FitModalActions(owner);
        }
        Render();
    }

    private static VBoxContainer RouteBoard(RouteReconstruction model)
    {
        var board = new VBoxContainer { Name = "RouteModelBoard", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var areas = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        board.AddChild(areas);
        Label Label(string text, string name = "")
        {
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            if (name.Length > 0) label.Name = name;
            return label;
        }
        foreach (var (id, title) in new[] { ("quay", "WEST QUAY"), ("east", "EASTERN APPROACH"), ("west", "SHELTERED APPROACH") })
        {
            var card = new PanelContainer { Name = id, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var stack = new VBoxContainer(); card.AddChild(stack); areas.AddChild(card);
            stack.AddChild(Label(title));
            if (id == "quay") stack.AddChild(Label("WEST WORKBOAT\nDocumented mooring", "WorkboatBlock"));
            if (model.Current.Area == id) stack.AddChild(Label("RESCUE ONE\n" + model.Current.Title, "RescueOneBlock"));
        }
        var unplotted = new VBoxContainer { Name = "unplotted" }; board.AddChild(unplotted);
        unplotted.AddChild(Label("NOT PLOTTED · positions not established by the comparison"));
        if (model.Current.Area == "unplotted") unplotted.AddChild(Label("RESCUE ONE", "RescueOneBlock"));
        unplotted.AddChild(Label("RESCUE TWO · MAINTENANCE STATION"));
        return board;
    }
}
