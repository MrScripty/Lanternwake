namespace Lanternwake.Core;

public sealed record ReachedHistoryEntry(int Index, TranscriptLine Line, string SpeakerName, string? ChapterId);
public sealed record ReachedHistoryChapter(string Id, string Title, int FirstEntryIndex);

/// <summary>A read-only projection of the actual transcript, never an authored-text index.</summary>
public sealed class HistoryNavigation
{
    public ReachedHistoryEntry[] Entries { get; }
    public ReachedHistoryChapter[] Chapters { get; }

    public HistoryNavigation(Story story, IEnumerable<TranscriptLine> history)
    {
        var chaptersByBeat = story.Chapters.SelectMany(c => c.Scenes.SelectMany(s => s.Beats.Select(b => (b.Id, Chapter: c))))
            .ToDictionary(p => p.Id, p => p.Chapter, StringComparer.Ordinal);
        var names = story.Characters.ToDictionary(c => c.Id, c => c.Name, StringComparer.Ordinal);
        Entries = history.Select((line, i) => new ReachedHistoryEntry(i, line,
            line.Speaker == "narrator" ? "" : line.Speaker == "you" ? "You" : names.GetValueOrDefault(line.Speaker, line.Speaker),
            line.BeatId is { } beatId && chaptersByBeat.TryGetValue(beatId, out var chapter) ? chapter.Id : null)).ToArray();
        Chapters = story.Chapters.Where(c => Entries.Any(e => e.ChapterId == c.Id))
            .Select(c => new ReachedHistoryChapter(c.Id, c.Title, Entries.First(e => e.ChapterId == c.Id).Index)).ToArray();
    }

    public ReachedHistoryEntry[] Search(string query)
    {
        var text = query.Trim();
        return text.Length == 0 ? Entries : Entries.Where(e => e.Line.Text.Contains(text, StringComparison.OrdinalIgnoreCase)
            || e.SpeakerName.Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
