#if DEBUG
using Godot;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Find the actual visible control after startup moved into a menu.</summary>
internal static class NativeGameControls
{
    public static Button Button(GameView game, string name)
    {
        var menu = game.InterfaceRoot.GetParent().GetNodeOrNull<Control>("MainMenu");
        if (menu?.Visible == true)
        {
            if (name == "AdvanceButton" && menu.GetNode<Button>("%MenuContinue").Text == "Load game")
                return menu.GetNode<Button>("%MenuNewGame");
            if (name == "ContentNoteButton") return menu.GetNode<Button>("%MenuContentNote");
            if (name == "SettingsButton") return menu.GetNode<Button>("%MenuReading");
        }
        return game.InterfaceRoot.GetNode<Button>("%" + name);
    }

    public static void Press(GameView game, string name) => Button(game, name).EmitSignal(BaseButton.SignalName.Pressed);
}
#endif
