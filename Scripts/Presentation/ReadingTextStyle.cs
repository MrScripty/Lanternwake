using Godot;

namespace Lanternwake.Presentation;

/// <summary>Session-only reading sizes relative to each control's authored font.</summary>
public sealed class ReadingTextStyle
{
    public const int MinimumPercent = 100, MaximumPercent = 150, StepPercent = 25;
    public int Percent { get; private set; } = MinimumPercent;
    private sealed record Target(Control Control, string Key, int Size, bool HadOverride);
    private readonly Dictionary<Control, Target> _targets = [];

    public void Register(Control control)
    {
        if (_targets.ContainsKey(control)) return;
        var key = control is RichTextLabel ? "normal_font_size" : "font_size";
        var target = new Target(control, key, control.GetThemeFontSize(key), control.HasThemeFontSizeOverride(key));
        _targets.Add(control, target);
        control.TreeExiting += () => _targets.Remove(control);
        Apply(target);
    }

    public void SetPercent(int percent)
    {
        if (percent < MinimumPercent || percent > MaximumPercent || (percent - MinimumPercent) % StepPercent != 0)
            throw new ArgumentOutOfRangeException(nameof(percent));
        Percent = percent;
        foreach (var target in _targets.Values) Apply(target);
    }

    public void Reset() => SetPercent(MinimumPercent);

    private void Apply(Target target)
    {
        if (Percent == MinimumPercent && !target.HadOverride) target.Control.RemoveThemeFontSizeOverride(target.Key);
        else target.Control.AddThemeFontSizeOverride(target.Key,
            (int)Math.Round(target.Size * Percent / 100d, MidpointRounding.AwayFromZero));
    }
}
