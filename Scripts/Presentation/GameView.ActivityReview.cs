using Godot;
using Lanternwake.Core;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private void ShowActivity(string? focusAction = null)
    {
        if (_busy || _closing || _session.CanAdvance || _session.Beat.Activity is not { } activity) return;
        var session = _session;
        var generation = _generation;
        var beatId = session.Beat.Id;
        Window? question = null;
        bool Current(Window? window) => window is not null && _modal == window && _session == session &&
            _generation == generation && _session.Beat.Id == beatId && !_session.CanAdvance && !_busy && !_closing;

        void Review(string title, string text, string origin)
        {
            if (!Current(question)) return;
            Window? review = null;
            void Return()
            {
                if (Current(review)) ShowActivity(origin);
            }
            ShowWindow(title, text, [("Back to question", Return)], Return);
            review = _modal;
            if (review is not null) review.GetNode<Button>("%ModalCloseButton").Text = "Back to question";
            review?.GetNode<VBoxContainer>("%ModalActions").GetChild<Button>(0).GrabFocus();
        }

        var actions = activity.Options.Select((option, index) => (option, (Action)(() =>
        {
            // Loads restore the same session object: both generation and window ownership matter.
            if (!Current(question)) return;
            if (_session.AnswerActivity(index))
            {
                CloseModal(); SetStatusText(activity.Explanation);
                _advance.Text = _session.IsEnding ? "Finish  ›" : "Continue  ›";
                Save(true);
            }
            else
            {
                if (activity.OptionFeedback is { } feedback)
                {
                    Review("Check the source", feedback[index], option);
                    return;
                }
                SetStatusText("That does not fit the evidence yet. Check your catalogue and try again.");
                CloseModal();
            }
        }))).ToList();
        actions.Add(("Review known evidence", () => Review("Your catalogue", EvidenceText(), "Review known evidence")));
        actions.Add(("Read the record", () => Review("The record", HistoryText(), "Read the record")));
        if (RouteReconstruction.AvailableAt(session)) actions.Add(("Explore route model", () =>
        {
            if (Current(question)) ShowRouteModel();
        }));
        ShowWindow("Compare the evidence", activity.Prompt, actions.ToArray());
        question = _modal;
        question?.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>()
            .First(button => button.Text == (focusAction ?? "Review known evidence")).GrabFocus();
    }
}
