namespace Lanternwake.Core;

/// <summary>Read-only author navigation; never changes story order, gates or player state.</summary>
public sealed record StoryAuthoringEntry(int SceneIndex, int BeatIndex, string ChapterTitle, Scene Scene, Beat Beat)
{
    public string Label => $"{Scene.Id} / {Beat.Id} · {Beat.Speaker} · {Beat.Text}";
    public string Context => string.Join("\n", new[]
    {
        $"{ChapterTitle} · {Scene.Title}",
        $"Scene: {Scene.Id} | {Scene.Location} | {Scene.TimeOfDay}",
        $"Cast: {string.Join(", ", Scene.CharacterIds)}",
        $"Beat: {Beat.Id} ({BeatIndex + 1}/{Scene.Beats.Length})",
        $"Unlock facts: {string.Join(", ", Beat.UnlockFacts ?? [])}",
        $"Unlock items: {string.Join(", ", Beat.UnlockItems ?? [])}",
        $"Stage cue: {Beat.StageCue ?? "none"}",
        Beat.Activity is { } activity ? $"Required evidence: {activity.Prompt}\nAnswer: {activity.Options[activity.CorrectIndex]}\nExplanation: {activity.Explanation}" : "Required evidence: none",
        Beat.Conversation is { } chat ? $"Optional conversation: {chat.CharacterId}\nPrompt: {chat.Prompt}\nAllowed facts: {string.Join(", ", chat.AllowedFacts)}\nSuggestions: {string.Join(" / ", chat.Suggestions)}\nAuthored fallback: {chat.Fallback}" : "Optional conversation: none"
    });
}

public sealed class StoryAuthoringIndex
{
    public StoryAuthoringEntry[] Entries { get; }
    public StoryAuthoringIndex(Story story)
    {
        var sceneIndex = 0;
        Entries = story.Chapters.SelectMany(chapter => chapter.Scenes.SelectMany(scene =>
        {
            var index = sceneIndex++;
            return scene.Beats.Select((beat, beatIndex) => new StoryAuthoringEntry(index, beatIndex, chapter.Title, scene, beat));
        })).ToArray();
    }

    public StoryAuthoringEntry[] Search(string query)
    {
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0) return [];
        return Entries.Where(entry => terms.All(term =>
            (entry.Label + "\n" + entry.Context).Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
}
