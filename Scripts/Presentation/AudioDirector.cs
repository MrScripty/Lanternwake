using Godot;

namespace Lanternwake.Presentation;

/// <summary>Owns the game's authored audio players, routing and transition lifetime.</summary>
[GlobalClass]
[Tool]
public partial class AudioDirector : Node
{
    [ExportGroup("Players")]
    [Export] public AudioStreamPlayer AmbienceA { get; set; } = null!;
    [Export] public AudioStreamPlayer AmbienceB { get; set; } = null!;
    [Export] public AudioStreamPlayer Music { get; set; } = null!;
    [Export] public AudioStreamPlayer Effects { get; set; } = null!;
    [ExportGroup("Location loops")]
    [Export] public AudioStreamWav Harbor { get; set; } = null!;
    [Export] public AudioStreamWav KeeperHouse { get; set; } = null!;
    [Export] public AudioStreamWav Archive { get; set; } = null!;
    [Export] public AudioStreamWav LanternRoom { get; set; } = null!;
    [Export] public AudioStreamWav TideCave { get; set; } = null!;
    [ExportGroup("Transitions")]
    [Export(PropertyHint.Range, "0.05,3,0.05")] public double FadeSeconds { get; set; } = 0.6;
    private AudioStreamPlayer? _current, _outgoing;
    private Tween? _fade;
    private readonly List<AudioStreamPlayback> _playbacks = new();
    private string _location = "";
    private bool _stopped;
    private string _lastCueBeat = "";
    public bool Muted => AudioServer.IsBusMute(Bus("Master"));
    public string CurrentLocation => _location;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        if (AmbienceA is null || AmbienceB is null || Music is null || Effects is null)
            throw new InvalidOperationException("Assign AudioDirector's four authored players in the Inspector.");
        foreach (var name in new[] { "Master", "Music", "Ambience", "Effects" }) _ = Bus(name);
        if (Music.Stream is AudioStreamWav theme) Music.Stream = LoopCopy(theme);
        PlayAndTrack(Music);
    }

    private static int Bus(string name)
    {
        int index = AudioServer.GetBusIndex(name);
        return index >= 0 ? index : throw new InvalidOperationException($"Missing audio bus '{name}'. Restore default_bus_layout.tres.");
    }
    private void PlayAndTrack(AudioStreamPlayer player)
    {
        player.Play();
        // Missing streams or failed playback creation leave no handle to own.
        // GetStreamPlayback also reports an engine error on an inactive player.
        if (player.HasStreamPlayback() && player.GetStreamPlayback() is { } playback)
            _playbacks.Add(playback);
    }
    private static AudioStreamWav LoopCopy(AudioStreamWav source)
    {
        // Never mutate shared imported resources or the user's editor asset.
        var loop = (AudioStreamWav)source.Duplicate();
        loop.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        loop.LoopBegin = 0;
        loop.LoopEnd = (int)Math.Round(loop.GetLength() * loop.MixRate);
        return loop;
    }
    public void ShowLocation(string location)
    {
        if (_stopped) return;
        if (location == _location) return; // same-location scenes retain their playhead
        var stream = location switch
        {
            "harbor" => Harbor, "keeper_house" => KeeperHouse, "archive" => Archive,
            "lantern_room" => LanternRoom, "tide_cave" => TideCave,
            _ => throw new ArgumentException("Unknown audio location: " + location, nameof(location))
        };
        if (stream is null) throw new InvalidOperationException("Assign ambience for " + location + " in the Inspector. For a fresh checkout, run bash scripts/setup.sh (Windows: py -3 scripts/setup_audio.py), then import the project in Godot.");
        _fade?.Kill();
        _fade?.Dispose();
        _fade = null;
        // A new request owns both slots. Retire the older outgoing slot before reuse.
        if (_outgoing is not null) StopLoop(_outgoing);
        var incoming = _current == AmbienceA ? AmbienceB : AmbienceA;
        _outgoing = _current;
        _current = incoming;
        _location = location;
        incoming.Stream = LoopCopy(stream);
        incoming.VolumeLinear = 0;
        PlayAndTrack(incoming);
        _fade = CreateTween().SetParallel();
        var duration = double.IsFinite(FadeSeconds) ? Math.Clamp(FadeSeconds, .05, 3) : .6;
        _fade.TweenProperty(incoming, "volume_linear", 1.0, duration);
        var retiring = _outgoing;
        if (retiring is not null) _fade.TweenProperty(retiring, "volume_linear", 0.0, duration);
        _fade.Chain().TweenCallback(Callable.From(() => { if (retiring is not null) StopLoop(retiring); _outgoing = null; }));
    }
    public void ApplyBeatCue(string beatId, string? cue, bool play)
    {
        if (_stopped) return;
        if (!play)
        {
            // A load/preview is a timeline discontinuity, including the same beat.
            Effects.Stop();
            _lastCueBeat = beatId;
            return;
        }
        if (_lastCueBeat == beatId) return;
        _lastCueBeat = beatId;
        if (play && cue == "bell_lowered")
        {
            PlayAndTrack(Effects);
        }
    }
    public void SetMuted(bool value) => AudioServer.SetBusMute(Bus("Master"), value);
    public float GetLevel(string name) => AudioServer.GetBusVolumeLinear(Bus(name));
    public void SetLevel(string name, float level)
    {
        if (name is not ("Music" or "Ambience" or "Effects")) throw new ArgumentException("Unknown adjustable audio channel.", nameof(name));
        if (!float.IsFinite(level)) throw new ArgumentOutOfRangeException(nameof(level));
        int index = Bus(name);
        float bounded = Math.Clamp(level, 0, 1);
        AudioServer.SetBusMute(index, bounded == 0);
        AudioServer.SetBusVolumeLinear(index, bounded);
    }
    private static void StopLoop(AudioStreamPlayer player)
    {
        player.Stop();
        var owned = player.Stream;
        player.Stream = null;
        owned?.Dispose();
    }
    public void StopPlayback()
    {
        _stopped = true;
        _fade?.Kill();
        _fade?.Dispose();
        _fade = null;
        foreach (var player in new[] { AmbienceA, AmbienceB, Music }) if (player is not null) StopLoop(player);
        Effects?.Stop();
    }
    private void RetireReleasedPlaybacks()
    {
        for (int i = _playbacks.Count - 1; i >= 0; i--)
        {
            // We own one reference. Native player/server ownership has ended at one.
            if (_playbacks[i].GetReferenceCount() != 1) continue;
            _playbacks[i].Dispose();
            _playbacks.RemoveAt(i);
        }
    }
    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) RetireReleasedPlaybacks();
    }
    public async Task<bool> StopAndRetireAsync()
    {
        StopPlayback();
        var deadline = Time.GetTicksMsec() + 2000;
        while (true)
        {
            RetireReleasedPlaybacks();
            if (_playbacks.Count == 0) return true;
            if (Time.GetTicksMsec() >= deadline) return false;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
    public override void _ExitTree()
    {
        if (Engine.IsEditorHint()) return;
        StopPlayback();
        foreach (var playback in _playbacks) playback.Dispose();
        _playbacks.Clear();
    }
}
