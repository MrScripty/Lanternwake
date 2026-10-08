using Godot;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private ConversationReading? _exchangeReading;
    private void ShowAuthoredExchange(ConversationReading? reading = null)
    {
        if (!_started || _busy || _closing || _chatPanel.Visible || _modal is not null || _session.Beat.Exchange is not { } exchange) return;
        var session = _session;
        var generation = _generation;
        var beatId = session.Beat.Id;
        reading ??= new(session, beatId, generation, GetSpeakerText(), _dialogue.Text, _dialogue.VisibleCharacters, _characters, GetStatusText());
        Window? window = null;
        bool Current() => window is not null && _modal == window && _session == session && _generation == generation &&
            _session.Beat.Id == beatId && !_busy && !_closing;
        void Finish()
        {
            if (!Current()) return;
            CloseModal();
            SetSpeakerText(reading.Speaker); _dialogue.Text = reading.Text;
            _dialogue.VisibleCharacters = reading.VisibleCharacters; _characters = reading.Characters; SetStatusText(reading.Status);
        }
        var name = DisplayName(exchange.CharacterId);
        if (session.ChosenExchangeIndex is { } chosen)
        {
            var option = exchange.Options[chosen];
            ShowWindow("With " + name, "You:\n" + option.Label + "\n\n" + name + ":\n" + option.Reply,
                [("Back to story", Finish)], Finish);
        }
        else
        {
            var actions = exchange.Options.Select((option, index) => (option.Label, (Action)(() =>
            {
                if (!Current() || !session.RecordExchange(index)) return;
                CloseModal();
                if (!Save(true)) reading = reading with { Status = GetStatusText() };
                ShowAuthoredExchange(reading);
            }))).ToArray();
            ShowWindow("With " + name, exchange.Prompt, actions, Finish);
        }
        window = _modal;
        _exchangeReading = reading;
        window?.GetNode<VBoxContainer>("%ModalActions").GetChild<Button>(0).GrabFocus();
    }
}
