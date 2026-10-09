using Godot;

namespace Lanternwake.Conversation;

internal interface ISpeechCaptureSource : IDisposable
{
    int SampleRate { get; }
    StereoSample[] Read(int maximum);
}

/// <summary>The existing muted Godot capture path, with uniquely owned bus/playback lifetime.</summary>
internal sealed class GodotSpeechCaptureSource : ISpeechCaptureSource
{
    private AudioStreamPlayer? _player;
    private AudioStreamPlayback? _playback;
    private AudioStream? _stream;
    private AudioEffectCapture? _capture;
    private string? _busName;
    #if DEBUG
    private int _phase;
    #endif
    public int SampleRate { get; }
    public GodotSpeechCaptureSource(Node owner, SpeechRuntimeAdmission admission, SpeechCaptureKind kind)
    {
        if (!admission.Allows(kind)) throw new NotSupportedException("Capture source is not qualified.");
        SampleRate = (int)AudioServer.GetMixRate();
        try
        {
            if (kind == SpeechCaptureKind.Microphone)
            {
                if (!ProjectSettings.GetSetting("audio/driver/enable_input", false).AsBool() ||
                    AudioServer.GetDriverName() == "Dummy" || AudioServer.GetInputDeviceList().Length == 0)
                    throw new InvalidOperationException("The microphone device is unavailable. Typed replies remain available.");
                _stream = new AudioStreamMicrophone(); // Input starts only at the explicitly consented Play below.
            }
            else
            {
#if DEBUG
                _stream = new AudioStreamGenerator { MixRate = SampleRate, BufferLength = .25f };
#else
                throw new NotSupportedException("Owned synthetic capture is debug-only.");
#endif
            }
            var index = AudioServer.BusCount;
            _busName = "LanternwakeCapture-" + Guid.NewGuid().ToString("N");
            AudioServer.AddBus(); AudioServer.SetBusName(index, _busName);
            _capture = new AudioEffectCapture { BufferLength = 2f };
            AudioServer.AddBusEffect(index, _capture); AudioServer.SetBusMute(index, true);
            _player = new AudioStreamPlayer { Stream = _stream, Bus = _busName };
            owner.AddChild(_player); _player.Play();
            if (!_player.HasStreamPlayback() || (_playback = _player.GetStreamPlayback()) is null || !_playback.IsPlaying())
                throw new InvalidOperationException("The audio device could not start. Typed replies remain available.");
        }
        catch { Dispose(); throw; }
    }
    public StereoSample[] Read(int maximum)
    {
        if (_capture is null || _playback is null || !_playback.IsPlaying())
            throw new InvalidOperationException("The audio device stopped. Recording was discarded.");
        if (_capture.GetDiscardedFrames() > 0)
            throw new InvalidOperationException("Audio capture fell behind. Recording was discarded; try a shorter clip.");
#if DEBUG
        if (_playback is AudioStreamGeneratorPlayback generator)
        {
            var frames = new Vector2[Math.Min(4096, generator.GetFramesAvailable())];
            try
            {
                for (var i = 0; i < frames.Length; i++, _phase++)
                    frames[i] = new(.12f * MathF.Sin(_phase * .04f), .08f * MathF.Cos(_phase * .03f));
                if (frames.Length > 0 && !generator.PushBuffer(frames)) throw new InvalidOperationException("Owned generator rejected frames.");
            }
            finally { Array.Clear(frames); }
        }
#endif
        var native = _capture.GetBuffer(Math.Min(maximum, _capture.GetFramesAvailable()));
        try
        {
            var result = new StereoSample[native.Length];
            for (var i = 0; i < native.Length; i++) result[i] = new(native[i].X, native[i].Y);
            return result;
        }
        finally { Array.Clear(native); }
    }
    public void Dispose()
    {
        // Microphone playback's Stop deactivates input immediately; no OS permission is accepted here.
        _playback?.Stop();
        if (GodotObject.IsInstanceValid(_player)) { _player!.Stop(); _player.Stream = null; _player.QueueFree(); }
        _player = null;
        _playback?.Dispose(); _playback = null;
        _capture?.ClearBuffer();
        if (_busName is { } name)
        {
            var index = AudioServer.GetBusIndex(name); // Other buses may have shifted since acquisition.
            if (index >= 0) AudioServer.RemoveBus(index);
            _busName = null;
        }
        _capture?.Dispose(); _capture = null;
        _stream?.Dispose(); _stream = null;
    }
}
