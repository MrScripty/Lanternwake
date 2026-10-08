using Godot;

namespace Lanternwake.Presentation;

public partial class GameView
{
    [Export] public PackedScene AudioSettingsControlsScene { get; set; } = null!;

    private bool _quitScheduled;
    private async void QuitAfterAudio(int code = 0)
    {
        if (_quitScheduled) return;
        _quitScheduled = true;
        _closing = true;
        SetProcessUnhandledKeyInput(false);
        if (InterfaceRoot is not null) InterfaceRoot.ProcessMode = ProcessModeEnum.Disabled;
        if (Audio is not null && !await Audio.StopAndRetireAsync())
        {
            GD.PushError("Audio shutdown timed out while waiting for native playback release.");
            code = 1;
        }
        GetTree().Quit(code);
    }

    private void ShowAudioSettings()
    {
        if (_busy || _closing) return;
        ShowWindow("Sound settings", "Every clue-bearing sound is also described in the authored text. These levels apply immediately and last for this game session.\n\nUse Tab to choose a control and arrow keys to adjust it.");
        var controls = AudioSettingsControlsScene.Instantiate<VBoxContainer>();
        var rows = _modal!.GetNode<VBoxContainer>("%ModalActions");
        rows.Visible = true;
        _modal.GetNode<ScrollContainer>("%ModalActionsScroll").Visible = true;
        rows.AddChild(controls);
        var mute = controls.GetNode<CheckButton>("Mute");
        mute.SetPressedNoSignal(Audio.Muted);
        mute.Toggled += Audio.SetMuted;
        _readingText.Register(mute);
        foreach (var channel in new[] { "Music", "Ambience", "Effects", "Dialogue" })
        {
            var slider = controls.GetNode<HSlider>(channel);
            var label = controls.GetNode<Label>(channel + "Label");
            _readingText.Register(label);
            string title = channel switch { "Ambience" => "Location ambience", "Dialogue" => "Character voices", _ => channel };
            slider.SetValueNoSignal(Math.Round(Audio.GetLevel(channel) * 100));
            void UpdateLabel() => label.Text = $"{title}: {slider.Value:0}%";
            UpdateLabel();
            slider.ValueChanged += value => { Audio.SetLevel(channel, (float)value / 100); UpdateLabel(); };
            slider.TooltipText = title + " volume; zero is silent";
        }
        FitModalActions(_modal);
        mute.GrabFocus();
    }
}
