#if DEBUG
using System.Text.Json;
using Lanternwake.Core;

namespace Lanternwake.Qualification;

// Observe the existing live session, including derived state omitted from SaveData.
// No presentation timer belongs to the authored restore contract.
internal static class BellRuntimeState
{
    internal static string Read(StorySession session) => JsonSerializer.Serialize(new
    {
        snapshot = session.Snapshot(),
        facts = session.KnownFacts.ToArray(),
        inventory = session.Inventory.ToArray(),
        chapterId = session.Chapter.Id,
        sceneId = session.Scene.Id,
        activeStageCues = session.ActiveStageCues,
        canAdvance = session.CanAdvance,
        isEnding = session.IsEnding,
        progress = session.Progress,
        chosenExchangeIndex = session.ChosenExchangeIndex
    }, Story.Json);
}
#endif
