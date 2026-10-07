using Godot;
using Lanternwake.Core;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private void ShowHistory() => ShowHistoryReader();

    private void ShowHistoryReader(Action? returnAction = null, string returnLabel = "Close")
    {
        if (_busy || _closing) return;
        var navigation = new HistoryNavigation(_story, _session.History);
        ShowWindow("The record", "", dismiss: returnAction);
        var window = _modal!;
        var prose = window.GetNode<RichTextLabel>("%ModalText");
        var close = window.GetNode<Button>("%ModalCloseButton");
        close.Text = returnLabel;
        window.GetNode<VBoxContainer>("%HistoryTools").Show();
        var search = window.GetNode<LineEdit>("%HistorySearch");
        var latest = window.GetNode<Button>("%HistoryLatest");
        latest.Disabled = navigation.Entries.Length == 0;
        var chapter = window.GetNode<OptionButton>("%HistoryChapter");
        chapter.AddItem("Jump to reached chapter");
        foreach (var reached in navigation.Chapters) chapter.AddItem(reached.Title);
        chapter.Disabled = navigation.Chapters.Length == 0;
        var count = window.GetNode<Label>("%HistoryCount");
        foreach (var control in new Control[] { search, latest, chapter, count })
        {
            _readingText.Register(control);
        }
        // OptionButton's popup is a separate Window and does not inherit its font override.
        if (_readingText.Percent != ReadingTextStyle.MinimumPercent)
        {
            var popup = chapter.GetPopup();
            popup.AddThemeFontSizeOverride("font_size", (int)Math.Round(popup.GetThemeFontSize("font_size") * _readingText.Percent / 100d, MidpointRounding.AwayFromZero));
        }
        var revision = 0;
        var paragraphs = new Dictionary<int, int>();
        bool Current() => !_closing && _modal == window && GodotObject.IsInstanceValid(window) && !window.IsQueuedForDeletion();
        void Render()
        {
            if (!Current()) return;
            revision++;
            var matches = navigation.Search(search.Text);
            paragraphs.Clear();
            var paragraph = 0;
            var blocks = new List<string>();
            foreach (var entry in matches)
            {
                paragraphs[entry.Index] = paragraph;
                var block = (entry.SpeakerName.Length > 0 ? entry.SpeakerName + (entry.Line.Generated ? " [optional local dialogue]" : "") + ":\n" : "") + entry.Line.Text;
                blocks.Add(block); paragraph += block.Count(c => c == '\n') + 2;
            }
            prose.Text = matches.Length == 0 ? (navigation.Entries.Length == 0 ? "No reached history yet." : "No reached entries match your search.") : string.Join("\n\n", blocks);
            prose.GetVScrollBar().Value = 0;
            count.Text = search.Text.Trim().Length == 0 ? $"{matches.Length} reached entries" : $"{matches.Length} of {navigation.Entries.Length} reached entries";
        }
        async void Jump(int index)
        {
            if (!Current()) return;
            search.Text = "";
            Render();
            var requested = revision;
            // RichTextLabel must finish wrapping at the current native reading size.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!Current() || revision != requested || !paragraphs.TryGetValue(index, out var paragraph)) return;
            prose.ScrollToParagraph(paragraph);
            prose.GetVScrollBar().GrabFocus();
        }
        search.TextChanged += _ => { chapter.Select(0); Render(); };
        latest.Pressed += () => Jump(navigation.Entries.Length - 1);
        chapter.ItemSelected += selected => { if (selected > 0) Jump(navigation.Chapters[(int)selected - 1].FirstEntryIndex); };
        // Explicit loop supports Tab/Shift-Tab and native UI directional focus.
        var focus = new Control[] { search, latest, chapter, prose.GetVScrollBar(), close }.Where(c => c is not BaseButton b || !b.Disabled).ToArray();
        for (var i = 0; i < focus.Length; i++)
        {
            var next = focus[(i + 1) % focus.Length].GetPath();
            var previous = focus[(i + focus.Length - 1) % focus.Length].GetPath();
            focus[i].FocusNext = next; focus[i].FocusPrevious = previous;
            if (focus[i] is Button or OptionButton) { focus[i].FocusNeighborBottom = next; focus[i].FocusNeighborTop = previous; }
        }
        // Up/Down on the prose scrollbar read the transcript; Right leaves it.
        prose.GetVScrollBar().FocusNeighborRight = close.GetPath();
        close.FocusNeighborLeft = prose.GetVScrollBar().GetPath();
        search.FocusNeighborBottom = latest.GetPath();
        Render();
        if (latest.Disabled) search.GrabFocus(); else latest.GrabFocus();
    }
}
