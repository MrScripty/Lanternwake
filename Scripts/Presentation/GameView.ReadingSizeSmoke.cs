using Godot;
using System.Text.Json;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private async Task RunReadingSizeSmoke()
    {
        void Check(bool condition, string claim)
        {
            if (!condition) throw new InvalidOperationException("Reading size smoke: " + claim);
        }
        async Task Layout()
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var paths = new[] { "res://Content/story.json", "res://Scenes/UI/LanternwakeTheme.tres", "res://Scenes/UI/GameInterface.tscn", "res://Scenes/UI/ModalWindow.tscn" };
        var source = paths.ToDictionary(p => p, p => System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(p)));
        var state = JsonSerializer.Serialize(_session.Snapshot());
        var saveDirectory = _storage!.OwnedTestDirectory ?? throw new InvalidOperationException("Reading smoke requires isolated test storage.");
        var saveFiles = (Directory.Exists(saveDirectory) ? Directory.GetFiles(saveDirectory, "*.json") : [])
            .ToDictionary(p => p, System.IO.File.ReadAllBytes);
        var text = _dialogue.Text;
        var reveal = _dialogue.VisibleCharacters;
        var characters = _characters;
        var processing = IsProcessing();
        var baseSize = _dialogue.GetThemeFontSize("normal_font_size");
        var entrySize = _entry.GetThemeFontSize("font_size");
        var modalSize = GD.Load<Theme>("res://Scenes/UI/LanternwakeTheme.tres").DefaultFontSize;
        SetProcess(false);
        try
        {
            _dialogue.VisibleCharacters = 17; _characters = 17;
            ShowSettings();
            var settings = _modal;
            Check(_readingText.Percent == 100 && _smallerText!.Disabled && !_largerText!.Disabled, "default and lower bound controls");
            for (var attempt = 0; attempt < 2; attempt++)
            {
                _largerText!.GrabFocus(); _largerText.EmitSignal(Button.SignalName.Pressed);
                Check(_readingText.Percent == 125 && _modal == settings, "size change keeps settings open");
                Check(_dialogue.GetThemeFontSize("normal_font_size") == (int)Math.Round(baseSize * 1.25, MidpointRounding.AwayFromZero), "125 percent uses authored base");
                _largerText.EmitSignal(Button.SignalName.Pressed);
                await Layout();
                Check(_readingText.Percent == 150 && _largerText.Disabled && _resetText!.HasFocus(), "upper bound and reachable focus");
                Check(_dialogue.GetThemeFontSize("normal_font_size") == (int)Math.Round(baseSize * 1.5, MidpointRounding.AwayFromZero), "repeated changes do not compound");
                Check(_entry.GetThemeFontSize("font_size") == (int)Math.Round(entrySize * 1.5, MidpointRounding.AwayFromZero), "editable reply is scaled");
                Check(_dialogue.VisibleCharacters == 17 && _characters == 17 && _dialogue.Text == text, "current reveal and reading text preserved");
                _resetText!.EmitSignal(Button.SignalName.Pressed);
                Check(_dialogue.GetThemeFontSize("normal_font_size") == baseSize && !_dialogue.HasThemeFontSizeOverride("normal_font_size"), "reset restores inherited default exactly");
            }
            CloseModal();

            var fixture = string.Join("\n", Enumerable.Repeat("Long reading fixture: the record stays readable while its text reflows. 語言の記録 remains editable and separate from canonical narrative.", 24));
            _dialogue.Text = fixture; _dialogue.VisibleCharacters = -1;
            await Layout();
            var normalHeight = _dialogue.GetContentHeight();
            _dialogue.GetVScrollBar().Value = 40;
            var position = _dialogue.GetVScrollBar().Value;
            SetReadingSize(150); await Layout();
            Check(_dialogue.GetContentHeight() > normalHeight && _dialogue.GetVScrollBar().MaxValue > _dialogue.GetVScrollBar().Page, "larger dialogue reflows and scrolls");
            Check(_dialogue.GetVScrollBar().Value == position, "size change preserves scroll offset");

            var choiceText = string.Join(' ', Enumerable.Repeat("A long editable evidence choice 語", 20));
            ShowWindow("Reading layout fixture", fixture, [(choiceText, () => { }), (choiceText, () => { }), (choiceText, () => { })]);
            _modal!.Size = new(640, 480); await Layout();
            var body = _modal.GetNode<RichTextLabel>("%ModalText");
            var actions = _modal.GetNode<VBoxContainer>("%ModalActions");
            var actionScroll = _modal.GetNode<ScrollContainer>("%ModalActionsScroll");
            Check(body.GetThemeFontSize("normal_font_size") == _dialogue.GetThemeFontSize("normal_font_size"), "new modal adopts selected reading size");
            Check(actionScroll.GetVScrollBar().MaxValue > actionScroll.GetVScrollBar().Page, "long enlarged choices can scroll in 640x480 modal");
            var last = actions.GetChildren().OfType<Button>().Last();
            last.GrabFocus(); await Layout();
            var target = last.GetGlobalRect(); var viewport = actionScroll.GetGlobalRect();
            // A single oversized choice can exceed the viewport, but its focused start must be reachable.
            Check(last.HasFocus() && target.End.Y > viewport.Position.Y && target.Position.Y < viewport.End.Y, "keyboard focus reaches long final choice");
            var close = _modal.GetNode<Button>("%ModalCloseButton");
            close.GrabFocus(); await Layout();
            Check(close.HasFocus() && close.GetGlobalRect().End.Y <= _modal.Size.Y, "close remains reachable at maximum text size");
            CloseModal();

            ShowHistory(); Check(_modal!.GetNode<RichTextLabel>("%ModalText").GetThemeFontSize("normal_font_size") == (int)Math.Round(modalSize * 1.5, MidpointRounding.AwayFromZero), "history adopts 150 percent"); CloseModal();
            ShowEvidence(); Check(_modal!.GetNode<RichTextLabel>("%ModalText").GetThemeFontSize("normal_font_size") == (int)Math.Round(modalSize * 1.5, MidpointRounding.AwayFromZero), "catalogue adopts 150 percent"); CloseModal();
            ShowSettings(); Check(_readingText.Percent == 150 && _largerText!.Disabled, "reopening preserves session size");
            _resetText!.EmitSignal(Button.SignalName.Pressed); CloseModal();
            var instant = _instant; var motion = _stage.MotionEnabled;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                ShowSettings();
                _modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == "Toggle instant text").EmitSignal(Button.SignalName.Pressed);
                Check(_modal is null && _instant == (attempt == 0 ? !instant : instant), "instant-text route preserved");
                ShowSettings();
                _modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == "Toggle reduced motion").EmitSignal(Button.SignalName.Pressed);
                Check(_modal is null && _stage.MotionEnabled == (attempt == 0 ? !motion : motion), "reduced-motion route preserved");
            }

            var authored = new RichTextLabel { Theme = GD.Load<Theme>("res://Scenes/UI/LanternwakeTheme.tres") };
            authored.AddThemeFontSizeOverride("normal_font_size", 18); AddChild(authored);
            var isolated = new ReadingTextStyle(); isolated.Register(authored); isolated.SetPercent(150);
            Check(authored.GetThemeFontSize("normal_font_size") == 27, "authored control override supplies base");
            isolated.Reset(); Check(authored.GetThemeFontSize("normal_font_size") == 18 && authored.HasThemeFontSizeOverride("normal_font_size"), "reset preserves authored override");
            authored.Free(); isolated.SetPercent(125);
            try { isolated.SetPercent(175); throw new InvalidOperationException("Out-of-range scale accepted."); }
            catch (ArgumentOutOfRangeException) { }
            Check(new ReadingTextStyle().Percent == 100, "new game starts at authored default");
            Check(JsonSerializer.Serialize(_session.Snapshot()) == state, "sizing preserves narrative position, history and gates");
            Check(source.All(p => System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(p.Key)).SequenceEqual(p.Value)), "shared themes, scenes and narrative remain unchanged");
            Check(saveFiles.All(p => System.IO.File.ReadAllBytes(p.Key).SequenceEqual(p.Value)), "sizing never rewrites player progress");
            Check(!Directory.Exists(saveDirectory) || Directory.GetFiles(saveDirectory, "*.json").Length == saveFiles.Count, "sizing does not create progress slots");
            GD.Print("LANTERNWAKE_READING_SIZE_OK 100/125/150 reset repeat reopen reveal scroll reflow 640x480 choices focus; session-only and source/save unchanged");
        }
        finally
        {
            CloseModal(); _readingText.Reset(); _dialogue.Text = text;
            _dialogue.VisibleCharacters = reveal; _characters = characters; SetProcess(processing);
        }
    }

    private async Task CheckReadingChatSizing()
    {
        var suggestion = _suggestions.GetChildren().OfType<Button>().Last();
        var label = suggestion.Text;
        var entry = _entry.Text;
        var baseSize = suggestion.GetThemeFontSize("font_size");
        var scroll = (ScrollContainer)_suggestions.GetParent();
        try
        {
            _entry.Text = "An editable draft 語"; _entry.CaretColumn = _entry.Text.Length;
            suggestion.Text = string.Join(' ', Enumerable.Repeat("A long suggestion 語", 30));
            SetReadingSize(150);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (suggestion.GetThemeFontSize("font_size") != (int)Math.Round(baseSize * 1.5, MidpointRounding.AwayFromZero)
                || _entry.Text != "An editable draft 語" || _entry.CaretColumn != _entry.Text.Length)
                throw new InvalidOperationException("Reading size must scale suggestions without changing an editable draft or caret.");
            if (scroll.GetVScrollBar().MaxValue <= scroll.GetVScrollBar().Page)
                throw new InvalidOperationException("Enlarged long suggestions must remain scrollable.");
            suggestion.GrabFocus();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var target = suggestion.GetGlobalRect(); var visible = scroll.GetGlobalRect();
            var returnButton = InterfaceRoot.GetNode<Button>("%ReturnButton");
            if (!suggestion.HasFocus() || target.End.Y <= visible.Position.Y || target.Position.Y >= visible.End.Y
                || _entry.GetGlobalRect().End.Y > _chatPanel.GetGlobalRect().End.Y
                || returnButton.GetGlobalRect().End.Y > _chatPanel.GetGlobalRect().End.Y)
                throw new InvalidOperationException("Chat suggestion, draft and Return must remain reachable at maximum size.");
            GD.Print("LANTERNWAKE_READING_CHAT_OK enlarged suggestions scroll/focus; draft caret and Return preserved");
        }
        finally { suggestion.Text = label; _entry.Text = entry; SetReadingSize(100); _entry.GrabFocus(); }
    }
}
