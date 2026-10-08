using Godot;
using Lanternwake.Core;

namespace Lanternwake.Presentation;

public partial class GameView
{
    [Export] public PackedScene MainMenuScene { get; set; } = null!;
    private Control? _mainMenu;
    private bool MainMenuVisible => _mainMenu?.Visible == true;

    private void ShowMainMenu()
    {
        if (_busy || _closing || _previewMode) return;
        CloseConversation(); CloseModal();
        if (_mainMenu is null)
        {
            _mainMenu = MainMenuScene.Instantiate<Control>();
            InterfaceRoot.GetParent().AddChild(_mainMenu);
            _mainMenu.GetNode<Button>("%MenuNewGame").Pressed += RequestNewStory;
            _mainMenu.GetNode<Button>("%MenuContinue").Pressed += () =>
            {
                if (_started) HideMainMenu(); else ShowLoad();
            };
            _mainMenu.GetNode<Button>("%MenuAiSetup").Pressed += ShowAiSetup;
            _mainMenu.GetNode<Button>("%MenuReading").Pressed += ShowSettings;
            _mainMenu.GetNode<Button>("%MenuSound").Pressed += ShowAudioSettings;
            _mainMenu.GetNode<Button>("%MenuContentNote").Pressed += () =>
            {
                if (MainMenuVisible && _modal is null) ShowContentNote(false);
            };
            _mainMenu.GetNode<Button>("%MenuQuit").Pressed += () => _Notification((int)NotificationWMCloseRequest);
        }
        InterfaceRoot.Hide(); _mainMenu.Show();
        var resume = _mainMenu.GetNode<Button>("%MenuContinue");
        resume.Text = _started ? "Resume story" : "Load game";
        resume.Disabled = !_started && (!_storage!.CanUseSaves ||
            !new[] { _storage.Inspect(false, false), _storage.Inspect(false, true),
                _storage.Inspect(true, false), _storage.Inspect(true, true) }
                .Any(candidate => candidate.Availability == SaveAvailability.Available));
        _mainMenu.GetNode<Label>("%MenuAiSummary").Text =
            AiSummary +
            "\nVoice input and character speech are currently unavailable.";
        FocusMainMenu();
    }

    private void FocusMainMenu()
    {
        if (_mainMenu is null) return;
        var resume = _mainMenu.GetNode<Button>("%MenuContinue");
        (_started && !resume.Disabled ? resume : _mainMenu.GetNode<Button>("%MenuNewGame")).GrabFocus();
    }

    private void HideMainMenu()
    {
        _mainMenu?.Hide(); InterfaceRoot.Show(); _advance.GrabFocus();
    }

    private void RequestNewStory()
    {
        if (_started)
        {
            ShowWindow("Start a new story?", "Leave your current session and begin again? Your existing save slots remain available until you save or advance in the new story.",
                [("Start new story", StartNewStory), ("Keep current story", CloseModal)]);
        }
        else StartNewStory();
    }

    private void StartNewStory()
    {
        CloseConversation(); CloseModal(); HideMainMenu();
        _session = new StorySession(_story); _started = false;
        Advance();
    }

}
