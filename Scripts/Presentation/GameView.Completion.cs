using Godot;
using Lanternwake.Core;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private void ShowCompletion(string? notice = null)
    {
        if (!_started || !_session.IsEnding || _busy || _closing) return;
        var completed = _session;
        Window? completion = null;
        var actions = new List<(string Text, Action Action)>
        {
            ("Read the record", () => ShowWindow("The record", HistoryText(), [("Back to ending", () => ShowCompletion())])),
            ("Review evidence", () => ShowWindow("Your catalogue", EvidenceText(), [("Back to ending", () => ShowCompletion())]))
        };
        if (_storage!.CanUseSaves)
            actions.Add(("Save finished watch", () => { Save(false); ShowCompletion(_status.Text); }));
        if (!_previewMode) actions.Add(("Start a new watch", ConfirmNewWatch));
        actions.Add(("Quit game", () => { CloseModal(); _Notification((int)NotificationWMCloseRequest); }));
        ShowWindow("The light remains", "You have reached the end of Lanternwake. Your evidence and conversations remain in the record.\n\nThank you for keeping watch." +
            (notice is null ? "" : "\n\n" + notice), actions.Select(action => (action.Text, (Action)(() =>
            {
                if (_modal == completion && _session == completed && !_busy && !_closing) action.Action();
            }))).ToArray());
        completion = _modal;
        _modal?.GetNode<VBoxContainer>("%ModalActions").GetChild<Button>(0).GrabFocus();
    }

    private void ConfirmNewWatch()
    {
        if (_previewMode || !_session.IsEnding || _busy || _closing) return;
        var completed = _session;
        Window? confirmation = null;
        ShowWindow("Start a new watch?", "Starting again replaces the record in this session. Save the finished watch first if you want to reopen it later.\n\nYour manual save stays unchanged until you choose Save. Automatic checkpoints will update as you progress through the new watch. Starting again does not change the story's fixed ending.",
            [("Keep this watch", () => { if (_modal == confirmation && _session == completed) ShowCompletion(); }),
             ("Start new watch", () =>
             {
                 // A retired confirmation cannot reset a loaded or replacement session.
                 if (_modal != confirmation || _session != completed || _closing) return;
                 StartNewWatch();
             })]);
        confirmation = _modal;
        confirmation?.GetNode<VBoxContainer>("%ModalActions").GetChild<Button>(0).GrabFocus();
    }

    private void StartNewWatch()
    {
        if (_previewMode || !_session.IsEnding || _busy || _closing) return;
        CloseConversation(); CloseModal();
        _entry.Text = "";
        _session = new StorySession(_story);
        _lastScene = "";
        // Restart is a timeline discontinuity, just like loading: retire historical effects.
        RenderBeat(false);
        _advance.GrabFocus();
    }
}
