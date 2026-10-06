using Lanternwake.Core;

namespace Lanternwake.Presentation;

public partial class GameView
{
    // Presentation only: optional replies remain in History, while Return resumes the paused passage.
    private sealed record ConversationReading(StorySession Session, string BeatId, int Generation,
        string Speaker, string Text, int VisibleCharacters, double Characters, string Status);
    private ConversationReading? _conversationReading;

    private void ApplyInstantTextToPausedPassage()
    {
        if (!_chatPanel.Visible || _closing || _conversationReading is not { } reading ||
            reading.Session != _session || reading.BeatId != _session.Beat.Id || reading.Generation != _generation) return;
        // Enabling instant text explicitly reveals this passage; disabling it does not hide text again.
        _conversationReading = reading with { VisibleCharacters = _instant ? -1 : reading.VisibleCharacters, Status = _status.Text };
    }

    private void ReturnToStory()
    {
        if (!_chatPanel.Visible || _closing) return;
        CloseConversation();
    }
}
