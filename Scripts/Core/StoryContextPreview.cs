namespace Lanternwake.Core;

/// <summary>Isolated authored progress, not a player save. Uses the production context builder.</summary>
public static class StoryContextPreview
{
    public static string? AtBeat(Story story, string beatId)
    {
        if (!story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).Any(b => b.Id == beatId))
            throw new InvalidDataException("Unknown preview beat.");
        var preview = new StorySession(story);
        while (preview.Beat.Id != beatId)
        {
            if (preview.Beat.Activity is { } activity) preview.AnswerActivity(activity.CorrectIndex);
            if (!preview.Advance()) throw new InvalidOperationException("Could not reach authored preview beat.");
        }
        return preview.Beat.Conversation is null ? null : preview.ConversationContext();
    }
}
