using Godot;
using Lanternwake.Core;
namespace Lanternwake.Conversation;

/// <summary>Explicitly consented capture and a bounded, zeroed, operation-owned stereo clip.</summary>
public sealed class SpeechRecorder : IDisposable
{
    private ISpeechCaptureSource? _source;
    private SpeechSampleBuffer? _samples;
    private CancellationTokenSource? _operation;
    private readonly PumasSpeechTranscriber _transcriber;
    private readonly SpeechRuntimeAdmission _admission;
    private readonly SpeechCaptureKind _kind;
    private readonly Func<Node, ISpeechCaptureSource> _factory;
    private SpeechServiceSettings _nextSettings = new();
    private bool _disposed;
    private double _silentSeconds;
    public bool Recording => _source is not null;
    public bool HasRecording => _samples is { Count: > 0 };
    public bool Pending => _operation is not null || _transcriber.Pending;
    public string FinishReason => _transcriber.FinishReason;
    public SpeechCapability Capability => _transcriber.Capability;
    public bool Available => Capability.Status != SpeechAvailability.Unsupported;
    public SpeechRecorder() : this(SpeechRuntimeAdmission.Installed, SpeechCaptureKind.Microphone) { }
    internal SpeechRecorder(SpeechRuntimeAdmission admission, SpeechCaptureKind kind, Func<Node, ISpeechCaptureSource>? factory = null,
        PumasSpeechTranscriber? transcriber = null)
    {
        _admission = admission; _kind = kind; _transcriber = transcriber ?? new(admission, kind);
        _factory = factory ?? (owner => new GodotSpeechCaptureSource(owner, admission, kind));
    }
    public void Configure(SpeechServiceSettings settings)
    {
        _nextSettings = settings.Validate();
        if (!Recording && !HasRecording && !Pending) _transcriber.Configure(_nextSettings);
    }
    public async Task<SpeechCapability> PrepareAsync(CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Recording || HasRecording || Pending) return new(SpeechAvailability.Unsupported, "Finish or discard the previous recording first.");
        _transcriber.Configure(_nextSettings);
        return await _transcriber.PrepareAsync(cancellation);
    }
    public Task<SpeechCapability> RecheckAsync(CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transcriber.PrepareAsync(cancellation, preserveSelection: true);
    }
    public void Start(Node owner)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Recording || HasRecording || Pending || !_admission.Allows(_kind) || !_transcriber.Prepared)
            throw new InvalidOperationException("Capture requires fresh consent and an available selected runtime.");
        try { _source = _factory(owner); _samples = new(_source.SampleRate); _silentSeconds = 0; }
        catch { Discard(); throw; }
    }
    public void Poll(double delta = 0)
    {
        if (_source is null || _samples is null) return;
        StereoSample[] frames = [];
        try
        {
            frames = _source.Read(_samples.CapacityLimit - _samples.Count);
            _samples.Append(frames);
            _silentSeconds = frames.Length == 0 ? _silentSeconds + delta : 0;
            if (_silentSeconds >= 3) throw new InvalidOperationException("The audio device returned no frames. Recording was discarded.");
            if (_samples.Full) StopCapture(); // Buffer bound never submits a request.
        }
        catch { Discard(); throw; }
        finally { Array.Clear(frames); }
    }
    public void StopCapture() { _source?.Dispose(); _source = null; }
    public async Task<string> StopAndTranscribe(CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Poll(); StopCapture();
        var samples = _samples ?? throw new InvalidOperationException("No recording is available.");
        _samples = null;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _operation = operation;
        try { return await _transcriber.TranscribeAsync(samples, samples.SampleRate, operation.Token); }
        finally
        {
            // Close requests cancellation, but never zeroes a buffer still owned by its original transport.
            samples.Dispose(); if (ReferenceEquals(_operation, operation)) _operation = null;
        }
    }
    public void Discard() { _operation?.Cancel(); StopCapture(); _samples?.Dispose(); _samples = null; }
    public void Dispose() { if (_disposed) return; _disposed = true; Discard(); _transcriber.Dispose(); }
}
