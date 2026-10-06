using Godot;
using Lanternwake.Core;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private void RunAuthorPreviewSmoke(string expectedBeat)
    {
        try
        {
            if (!_previewMode || _storage!.Mode != SessionMode.AuthorPreview || !_started || _session.Beat.Id != expectedBeat || _dialogue.Text != _session.Beat.Text)
                throw new InvalidOperationException("Selected-beat preview did not render the requested isolated state.");
            if (!_chapter.Text.Contains("AUTHOR PREVIEW") || !InterfaceRoot.GetNode<Button>("%SaveButton").Disabled || !InterfaceRoot.GetNode<Button>("%LoadButton").Disabled)
                throw new InvalidOperationException("Selected-beat preview is not visibly non-persisting.");
            if (_session.Beat.Activity is not null && _session.CanAdvance)
                throw new InvalidOperationException("Preview solved the current activity before the author could test it.");
            var expected = StoryContextPreview.CreateSessionAtBeat(_story, expectedBeat);
            if (!_session.ActiveStageCues.SequenceEqual(expected.ActiveStageCues) || !_session.KnownFacts.Select(f => f.Id).SequenceEqual(expected.KnownFacts.Select(f => f.Id)))
                throw new InvalidOperationException("Preview lost authored cues or gated knowledge.");
            GD.Print("LANTERNWAKE_SELECTED_BEAT_OK " + expectedBeat + " isolated progress; current activity unsolved; player saves disabled");
            _Notification((int)NotificationWMCloseRequest);
        }
        catch (Exception error) { GD.PushError(error.ToString()); QuitAfterAudio(1); }
    }
}
