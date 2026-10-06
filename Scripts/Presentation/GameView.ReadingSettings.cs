using Godot;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private readonly ReadingTextStyle _readingText = new();
    private Button? _smallerText, _largerText, _resetText;

    private void ShowSettings()
    {
        if (_busy || _closing) return;
        Window? settings = null;
        ShowWindow("Reading settings", "All dialogue is text. Use mouse or keyboard. Voice input is optional and local.\n\nSpace / Enter: reveal or advance\nEscape: close panel\nH: history · E: evidence\n\nOptional local conversation may improvise; only the authored catalogue establishes facts.\n\nText size applies to reading and choices for this game session.",
            [("Smaller reading text", () => SetReadingSize(_readingText.Percent - ReadingTextStyle.StepPercent)),
             ("Larger reading text", () => SetReadingSize(_readingText.Percent + ReadingTextStyle.StepPercent)),
             ("Reset reading text size", () => SetReadingSize(ReadingTextStyle.MinimumPercent)),
             ("Toggle instant text", () => { _instant = !_instant; if (_instant) _dialogue.VisibleCharacters = -1; _status.Text = _instant ? "Instant text enabled" : "Typewriter text enabled"; ApplyInstantTextToPausedPassage(); CloseModal(); }),
             ("Toggle reduced motion", () => { _stage.MotionEnabled = !_stage.MotionEnabled; _status.Text = _stage.MotionEnabled ? "Environmental motion enabled" : "Reduced motion enabled"; CloseModal(); }),
             ("Sound settings", ShowAudioSettings),
             ("Local conversation setup", ShowPumasSetup),
             ("Content note", () => { if (settings is not null && _modal == settings) ShowContentNote(true); }),
             ("Quit game", () => { CloseModal(); _Notification((int)NotificationWMCloseRequest); })]);
        settings = _modal;
        if (_modal is null) return;
        var actions = _modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().ToArray();
        _smallerText = actions.Single(b => b.Text == "Smaller reading text");
        _largerText = actions.Single(b => b.Text == "Larger reading text");
        _resetText = actions.Single(b => b.Text == "Reset reading text size");
        UpdateReadingSizeControls();
    }

    private void SetReadingSize(int percent)
    {
        if (_closing) return;
        _readingText.SetPercent(Math.Clamp(percent, ReadingTextStyle.MinimumPercent, ReadingTextStyle.MaximumPercent));
        UpdateReadingSizeControls();
        FitModalActions(_modal);
    }

    private void UpdateReadingSizeControls()
    {
        if (_modal is null || _modal.Title != "Reading settings") return;
        _smallerText!.Disabled = _readingText.Percent == ReadingTextStyle.MinimumPercent;
        _largerText!.Disabled = _readingText.Percent == ReadingTextStyle.MaximumPercent;
        _resetText!.Text = $"Reset reading text size · {_readingText.Percent}%";
        // A control disabled at a size limit must not strand keyboard focus.
        if (_smallerText.HasFocus() && _smallerText.Disabled || _largerText.HasFocus() && _largerText.Disabled)
            _resetText.GrabFocus();
    }

    private static void EnableKeyboardReading(RichTextLabel prose)
    {
        // Native arrows scroll; Tab still reaches actions and return controls.
        var scrollbar = prose.GetVScrollBar();
        scrollbar.FocusMode = Control.FocusModeEnum.All;
        scrollbar.FocusNeighborTop = scrollbar.GetPath();
        scrollbar.FocusNeighborBottom = scrollbar.GetPath();
        scrollbar.GuiInput += input =>
        {
            if (!scrollbar.HasFocus() || input is not InputEventKey { Pressed: true } key) return;
            double? target = key.Keycode switch
            {
                Key.Pageup => scrollbar.Value - scrollbar.Page,
                Key.Pagedown => scrollbar.Value + scrollbar.Page,
                Key.Home => scrollbar.MinValue,
                Key.End => scrollbar.MaxValue - scrollbar.Page,
                _ => null,
            };
            if (target is null) return;
            scrollbar.Value = Math.Clamp(target.Value, scrollbar.MinValue,
                Math.Max(scrollbar.MinValue, scrollbar.MaxValue - scrollbar.Page));
            scrollbar.AcceptEvent();
        };
    }

    private static void FitModalActions(Window? window)
    {
        if (window is null || !GodotObject.IsInstanceValid(window) || !window.IsInsideTree() || window.IsQueuedForDeletion()) return;
        var scroll = window.GetNodeOrNull<ScrollContainer>("%ModalActionsScroll");
        var rows = window.GetNodeOrNull<VBoxContainer>("%ModalActions");
        if (scroll is null || rows is null) return;
        scroll.CustomMinimumSize = new(0, Math.Min(rows.GetCombinedMinimumSize().Y, window.Size.Y * .5f));
    }
}
