using Godot;
namespace Lanternwake.Conversation;

/// <summary>Consent-triggered Godot capture; transcription receives an operation-local buffer.</summary>
public sealed class SpeechRecorder : IDisposable
{
    private AudioStreamPlayer? _player;
    private AudioEffectCapture? _capture;
    private int _bus = -1, _sampleRate;
    private List<StereoSample> _samples = [];
    public bool Recording => _player is not null;
    private readonly PumasSpeechTranscriber _transcriber = new();
    public SpeechCapability Capability => _transcriber.Capability;
    public bool Available => Capability.Status != SpeechAvailability.Unsupported;
    public void Start(Node owner)
    {
        if (!Available) throw new NotSupportedException(Capability.Message);
        if (Recording) return;
        _samples.Clear(); _sampleRate = (int)AudioServer.GetMixRate();
        _bus = AudioServer.BusCount;
        AudioServer.AddBus(); AudioServer.SetBusName(_bus, "LanternwakeCapture");
        _capture = new AudioEffectCapture { BufferLength = 2f };
        AudioServer.AddBusEffect(_bus, _capture); AudioServer.SetBusMute(_bus, true);
        _player = new AudioStreamPlayer { Stream = new AudioStreamMicrophone(), Bus = "LanternwakeCapture" };
        owner.AddChild(_player); _player.Play();
    }
    public void Poll()
    {
        if (_capture is null) return;
        var remaining = Math.Max(0, _sampleRate * 30 - _samples.Count);
        foreach (var frame in _capture.GetBuffer(Math.Min(_capture.GetFramesAvailable(), remaining))) _samples.Add(new(frame.X, frame.Y));
    }
    public async Task<string> StopAndTranscribe(CancellationToken cancellation)
    {
        Poll(); Stop();
        var samples = _samples; _samples = [];
        var sampleRate = _sampleRate;
        try
        {
            return await _transcriber.TranscribeAsync(samples, sampleRate, cancellation);
        }
        finally { samples.Clear(); }
    }
    private void Stop()
    {
        if (_player is not null) { _player.Stop(); _player.QueueFree(); _player = null; }
        if (_bus >= 0) { AudioServer.RemoveBus(_bus); _bus = -1; }
        _capture = null;
    }
    public void Dispose() { Stop(); _samples.Clear(); }
}
