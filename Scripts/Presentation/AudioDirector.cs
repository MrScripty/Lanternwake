using Godot;
using Lanternwake.Audio;

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
    [Export] public AudioStreamPlayer Dialogue { get; set; } = null!;
    [ExportGroup("Adaptive score")]
    [Export] public MusicScore Score { get; set; } = null!;
    [Export(PropertyHint.File, "*.json")] public string MusicCatalogPath { get; set; } = "res://Assets/Music/catalog.json";
    [Export(PropertyHint.File, "*.sf2")] public string MusicBankPath { get; set; } = "res://Assets/Music/Source/Saltmere-Acoustic.sf2";
    [Export(PropertyHint.Range, "0.05,10,0.05")] public double MusicFadeSeconds { get; set; } = 3;
    [ExportGroup("Speech clarity")]
    [Export] public bool SpeechDucking { get; set; } = true;
    [Export(PropertyHint.Range, "0,12,0.1")] public float SpeechMusicDipDb { get; set; } = 3;
    [Export(PropertyHint.Range, "0,12,0.1")] public float SpeechPresenceDipDb { get; set; } = 3;
    [Export(PropertyHint.Range, "0.01,1,0.01")] public double SpeechAttackSeconds { get; set; } = .07;
    [Export(PropertyHint.Range, "0.05,3,0.05")] public double SpeechReleaseSeconds { get; set; } = .65;
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
    private BufferedMusicSynth? _musicSynth;
    private AudioStreamGeneratorPlayback? _musicPlayback;
    private readonly StereoFrame[] _synthFrames = new StereoFrame[MidiMusicRenderer.BlockFrames];
    private readonly Vector2[] _nativeFrames = new Vector2[MidiMusicRenderer.BlockFrames];
    public long MusicFramesRendered => _musicSynth?.FramesRendered ?? 0;
    public int MusicUnderruns => _musicPlayback?.GetSkips() ?? 0;
    public bool MusicWorkerRunning => _musicSynth?.WorkerRunning ?? false;
    private float[] _stemLevels = Array.Empty<float>(), _stemStarts = Array.Empty<float>(), _stemTargets = Array.Empty<float>();
    private MusicCatalog _catalog = null!;
    private string _musicLocation = "", _musicChapter = "";
    public string CurrentCharacterTheme { get; private set; } = "";
    public string CurrentMusicEnvironment => _musicLocation;
    public string CurrentMusicChapter => _musicChapter;
    public int StemCount => _stemLevels.Length;
    public int GetLayerSlot(string role, string id) => _catalog.Slots(role, id).Single();
    private double _musicElapsed, _musicDuration;
    private AudioEffectEQ? _musicEq;
    private float _eqPresence2k, _eqPresence4k, _musicBaseDb, _speechBlend;
    private bool _externalDialogueActive;
    public MusicMix? CurrentMusicMix { get; private set; }
    public float SpeechBlend => _speechBlend;
    public float GetStemLevel(int index) => _stemLevels[index];
    public float GetTargetStemLevel(int index) => _stemTargets[index];
    public bool Muted => AudioServer.IsBusMute(Bus("Master"));
    private int _reducedRangeEffect = -1;
    public bool ReducedDynamicRange => AudioServer.IsBusEffectEnabled(Bus("Master"), _reducedRangeEffect);
    public string CurrentLocation => _location;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        if (AmbienceA is null || AmbienceB is null || Music is null || Effects is null || Dialogue is null)
            throw new InvalidOperationException("Assign AudioDirector's five authored players in the Inspector.");
        foreach (var name in new[] { "Master", "Music", "Ambience", "Effects", "Dialogue" }) _ = Bus(name);
        var master = Bus("Master");
        for (int i = 0; i < AudioServer.GetBusEffectCount(master); i++)
            if (AudioServer.GetBusEffect(master, i) is AudioEffectCompressor { ResourceName: "Master / reduced range" })
                _reducedRangeEffect = i;
        if (_reducedRangeEffect < 0) throw new InvalidOperationException("Assign the optional reduced-range compressor to the Master bus.");
        Score.Validate();
        _catalog = MusicCatalog.Load(MusicCatalogPath);
        _stemLevels = new float[_catalog.Stems.Length]; _stemStarts = new float[_stemLevels.Length]; _stemTargets = new float[_stemLevels.Length];
        _musicBaseDb = Music.VolumeDb;
        // An absent optional music stream must not create a null playback owner.
        if (Music.Stream is null) return;
        if (Music.Stream is not AudioStreamGenerator authored || authored.MixRate != MidiMusicRenderer.SampleRate)
            throw new InvalidOperationException("Assign the 32 kHz generator to Music in the Inspector.");
        var inputs = new List<MusicVoiceInput>();
        foreach (var layer in _catalog.Layers)
        {
            var midi = ReadMusicFile("res://Assets/Music/Source/" + layer.Midi);
            foreach (var stem in layer.Stems) inputs.Add(new(stem.Name, stem.Channels, midi));
        }
        var renderer = new MidiMusicRenderer(ReadMusicFile(MusicBankPath), inputs);
        // Own a copy of the authored generator; editor resources remain untouched.
        Music.Stream = (AudioStreamGenerator)authored.Duplicate();
        var musicBus = Bus("Music");
        for (int i = 0; i < AudioServer.GetBusEffectCount(musicBus); i++)
            if (AudioServer.GetBusEffect(musicBus, i) is AudioEffectEQ eq && eq.GetBandCount() == 10) { _musicEq = eq; break; }
        if (_musicEq is not null) { _eqPresence2k = _musicEq.GetBandGainDb(6); _eqPresence4k = _musicEq.GetBandGainDb(7); }
        ShowTitleMusic(immediate: true);
        _musicSynth = new BufferedMusicSynth(renderer, _stemLevels);
        PlayAndTrack(Music);
        _musicPlayback = (AudioStreamGeneratorPlayback)Music.GetStreamPlayback();
        // Godot's generator playback keeps a raw stream pointer through its final
        // native mix. A native metadata reference survives player/tree removal
        // and C# wrapper disposal, and retires with the playback itself.
        using var streamOwner = Variant.From(Music.Stream);
        _musicPlayback.SetMeta("lanternwake_generator_owner", streamOwner);
        PumpMusic();
    }
    private static byte[] ReadMusicFile(string path)
    {
        if (!Godot.FileAccess.FileExists(path)) throw new InvalidDataException("Missing packaged music source: " + path);
        return Godot.FileAccess.GetFileAsBytes(path);
    }
    private void PumpMusic()
    {
        if (_stopped || _musicPlayback is null || _musicSynth is null) return;
        while (_musicPlayback.CanPushBuffer(_nativeFrames.Length) && _musicSynth.TryRead(_synthFrames))
        {
            for (int index = 0; index < _nativeFrames.Length; index++)
                _nativeFrames[index] = new(_synthFrames[index].Left, _synthFrames[index].Right);
            if (!_musicPlayback.PushBuffer(_nativeFrames)) throw new InvalidOperationException("Music output buffer rejected a complete block.");
        }
    }
    public void ShowTitleMusic(bool immediate = false) => SetMusicMix(Score.TitleMix, "harbor", "ch1", "", immediate);
    public void ApplyStoryMusic(string sceneId, string location, string chapterId, IReadOnlyList<string> reachedBeatIds)
    {
        var focus = Score.ResolveFocus(sceneId, reachedBeatIds);
        var character = focus == MusicSpot.FocusKind.None ? "" : focus.ToString().ToLowerInvariant();
        SetMusicMix(Score.Resolve(sceneId, reachedBeatIds), location, chapterId, character);
    }
    private void SetMusicMix(MusicMix mix, string location, string chapterId, string character, bool immediate = false)
    {
        if (_stopped) return;
        if (CurrentMusicMix == mix && _musicLocation == location && _musicChapter == chapterId && CurrentCharacterTheme == character && !immediate) return;
        var levels = mix.Levels();
        CurrentMusicMix = mix;
        _musicLocation = location; _musicChapter = chapterId; CurrentCharacterTheme = character;
        Array.Copy(_stemLevels, _stemStarts, _stemLevels.Length);
        Array.Clear(_stemTargets);
        var suiteId = mix.Suite switch { MusicMix.SuiteKind.Saltmere => "saltmere", MusicMix.SuiteKind.Undertow => "undertow", MusicMix.SuiteKind.NightLedger => "night_ledger", _ => "open_horizon" };
        var moodSlots = _catalog.Slots("mood", suiteId);
        if (moodSlots.Length != 4) throw new InvalidDataException("Mood arrangements require four stems.");
        for (int i = 0; i < 4; i++) _stemTargets[moodSlots[i]] = levels[i];
        if (!mix.SilenceStoryLayers)
        {
            if (mix.Environment > 0)
                foreach (var slot in moodSlots) _stemTargets[slot] *= mix.MoodUnderEnvironment;
            _stemTargets[GetLayerSlot("environment", location)] = mix.Environment;
            _stemTargets[GetLayerSlot("journey", chapterId)] = mix.Journey;
            if (character.Length > 0)
            {
                _stemTargets[GetLayerSlot("character", character)] = mix.Character;
                _stemTargets[moodSlots[1]] *= mix.ThemeUnderCharacter;
            }
        }
        _musicElapsed = 0;
        _musicDuration = immediate ? 0 : double.IsFinite(MusicFadeSeconds) ? Math.Clamp(MusicFadeSeconds, .05, 10) : 3;
        if (immediate) UpdateMusic(0);
    }
    private void UpdateMusic(double delta)
    {
        if (_stopped) return;
        _musicElapsed += delta;
        float t = _musicDuration == 0 ? 1 : (float)Math.Clamp(_musicElapsed / _musicDuration, 0, 1);
        t = t * t * (3 - 2 * t);
        for (int i = 0; i < _stemLevels.Length; i++)
        {
            _stemLevels[i] = Mathf.Lerp(_stemStarts[i], _stemTargets[i], t);
        }
        _musicSynth?.SetGains(_stemLevels);
        bool speaking = SpeechDucking && (_externalDialogueActive || Dialogue.Playing) && !AudioServer.IsBusMute(Bus("Dialogue")) && GetLevel("Dialogue") > 0;
        double tau = speaking ? SpeechAttackSeconds : SpeechReleaseSeconds;
        tau = double.IsFinite(tau) ? Math.Clamp(tau, .01, 3) : .65;
        _speechBlend = Mathf.Lerp(_speechBlend, speaking ? 1 : 0, (float)(1 - Math.Exp(-delta / tau)));
        float gainDip = float.IsFinite(SpeechMusicDipDb) ? Math.Clamp(SpeechMusicDipDb, 0, 12) : 3;
        float presenceDip = float.IsFinite(SpeechPresenceDipDb) ? Math.Clamp(SpeechPresenceDipDb, 0, 12) : 3;
        Music.VolumeDb = _musicBaseDb - gainDip * _speechBlend;
        _musicEq?.SetBandGainDb(6, _eqPresence2k - presenceDip * _speechBlend);
        _musicEq?.SetBandGainDb(7, _eqPresence4k - presenceDip * _speechBlend);
    }
    // Future voice providers can use this player, or signal an external Dialogue-bus clip.
    public void SetDialogueActive(bool active) { if (!_stopped) _externalDialogueActive = active; }
    public void PlayDialogue(AudioStream stream)
    {
        if (_stopped) return;
        StopDialogue();
        Dialogue.Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        PlayAndTrack(Dialogue);
    }
    public void StopDialogue()
    {
        _externalDialogueActive = false;
        Dialogue?.Stop();
        if (Dialogue is not null) Dialogue.Stream = null;
    }

    private static int Bus(string name)
    {
        int index = AudioServer.GetBusIndex(name);
        return index >= 0 ? index : throw new InvalidOperationException($"Missing audio bus '{name}'. Restore default_bus_layout.tres.");
    }
    private void PlayAndTrack(AudioStreamPlayer player)
    {
        if (player.Stream is null) return;
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
        if (!play || _lastCueBeat != beatId) StopDialogue();
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
    public void SetReducedDynamicRange(bool value) => AudioServer.SetBusEffectEnabled(Bus("Master"), _reducedRangeEffect, value);
    public float GetLevel(string name) => AudioServer.GetBusVolumeLinear(Bus(name));
    public void SetLevel(string name, float level)
    {
        if (name is not ("Music" or "Ambience" or "Effects" or "Dialogue")) throw new ArgumentException("Unknown adjustable audio channel.", nameof(name));
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
        _musicSynth?.Dispose(); _musicSynth = null;
        foreach (var player in new[] { AmbienceA, AmbienceB, Music }) if (player is not null) StopLoop(player);
        _musicPlayback = null;
        StopDialogue();
        if (Music is not null) Music.VolumeDb = _musicBaseDb;
        _musicEq?.SetBandGainDb(6, _eqPresence2k);
        _musicEq?.SetBandGainDb(7, _eqPresence4k);
        _speechBlend = 0;
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
        if (Engine.IsEditorHint()) return;
        try { UpdateMusic(delta); PumpMusic(); RetireReleasedPlaybacks(); }
        catch { StopPlayback(); throw; }
    }
    public async Task<bool> StopAndRetireAsync()
    {
        StopPlayback();
        var tree = GetTree();
        var deadline = Time.GetTicksMsec() + 2000;
        while (true)
        {
            RetireReleasedPlaybacks();
            if (_playbacks.Count == 0) return true;
            if (Time.GetTicksMsec() >= deadline) return false;
            // The director may leave the tree while retirement is pending.
            // Bind the awaiter to the surviving tree so teardown can settle it.
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
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
