using Lanternwake.Core;

namespace Lanternwake.Presentation;

public partial class GameView
{
    // Presentation only: optional replies remain in History, while Return resumes the paused passage.
    private sealed record ConversationReading(StorySession Session, string BeatId, int Generation,
        string Speaker, string Text, int VisibleCharacters, double Characters, string Status);
    private ConversationReading? _conversationReading;

    private void ReturnToStory()
    {
        if (!_chatPanel.Visible || _closing) return;
        CloseConversation();
    }
}
