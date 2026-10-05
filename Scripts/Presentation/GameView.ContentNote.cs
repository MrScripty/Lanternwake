using Godot;

namespace Lanternwake.Presentation;

public partial class GameView
{
    // Existing authored note from docs/bible/05_PACING_ACCESSIBILITY_QA.md; no plot solution.
    private const string ContentNoteText = "Bereavement, historical adult deaths by drowning (described non-graphically), institutional concealment, storm danger, difficult family memories.\n\nNo child injury, gore, self-harm storyline, or jump scare is authored.";

    private void ShowTitleContentNote()
    {
        if (!_started && _modal is null) ShowContentNote(false);
    }

    private void ShowContentNote(bool returnToSettings)
    {
        if (_busy || _closing) return;
        Window? note = null;
        void Return()
        {
            if (note is null || _modal != note || _busy || _closing) return;
            if (returnToSettings) ShowSettings(); else CloseModal();
        }
        ShowWindow("Content note", ContentNoteText, dismiss: Return);
        note = _modal;
        if (note is null) return;
        // Keyboard users can read an enlarged note that exceeds the prose viewport.
        var scrollbar = note.GetNode<RichTextLabel>("%ModalText").GetVScrollBar();
        scrollbar.FocusMode = Control.FocusModeEnum.All;
        scrollbar.FocusNeighborTop = scrollbar.GetPath();
        scrollbar.FocusNeighborBottom = scrollbar.GetPath();
        var close = note.GetNode<Button>("%ModalCloseButton");
        close.Text = returnToSettings ? "Back to settings" : "Return to title";
        close.GrabFocus();
    }
}
