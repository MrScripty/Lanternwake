namespace Lanternwake.Audio;

/// <summary>Bounded PCM queue. The worker owns the synth; the main thread owns Godot APIs.</summary>
public sealed class BufferedMusicSynth : IDisposable
{
    private readonly MidiMusicRenderer _renderer;
    private readonly object _gate = new();
    private readonly AutoResetEvent _requested = new(false);
    private readonly StereoFrame[] _queue = new StereoFrame[4096];
    private readonly float[] _gains;
    private readonly Thread _worker;
    private int _read, _write, _count;
    private volatile bool _stopping;
    private bool _disposed;
    private Exception? _failure;
    public long FramesRendered => _renderer.FramesRendered;
    public bool WorkerRunning => _worker.IsAlive;

    public BufferedMusicSynth(MidiMusicRenderer renderer, ReadOnlySpan<float> initialGains)
    {
        _renderer = renderer; _gains = initialGains.ToArray();
        renderer.SetGains(initialGains, immediate: true);
        var block = new StereoFrame[MidiMusicRenderer.BlockFrames];
        // Prime before starting the player, avoiding a silent startup buffer.
        for (int index = 0; index < _queue.Length / block.Length; index++) { renderer.Render(block); Enqueue(block); }
        _worker = new Thread(Produce) { IsBackground = true, Name = "Lanternwake MIDI synthesis" };
        _worker.Start();
    }

    public void SetGains(ReadOnlySpan<float> gains)
    {
        lock (_gate)
        {
            if (_stopping) return;
            if (gains.Length != _gains.Length) throw new ArgumentException("Supply every score voice gain.");
            gains.CopyTo(_gains);
        }
    }

    public bool TryRead(Span<StereoFrame> output)
    {
        lock (_gate)
        {
            if (_failure is not null) throw new InvalidOperationException("Live music synthesis failed.", _failure);
            if (_count < output.Length || _stopping) return false;
            foreach (ref var frame in output) { frame = _queue[_read]; _read = (_read + 1) % _queue.Length; }
            _count -= output.Length;
        }
        _requested.Set();
        return true;
    }

    private void Enqueue(ReadOnlySpan<StereoFrame> block)
    {
        lock (_gate)
        {
            foreach (var frame in block) { _queue[_write] = frame; _write = (_write + 1) % _queue.Length; }
            _count += block.Length;
        }
    }

    private void Produce()
    {
        var block = new StereoFrame[MidiMusicRenderer.BlockFrames];
        var gains = new float[_gains.Length];
        try
        {
            while (!_stopping)
            {
                bool available;
                lock (_gate) { available = _count <= _queue.Length - block.Length; _gains.CopyTo(gains, 0); }
                if (!available) { _requested.WaitOne(); continue; }
                _renderer.SetGains(gains);
                _renderer.Render(block);
                Enqueue(block);
            }
        }
        catch (Exception error) { lock (_gate) _failure = error; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _stopping = true; _requested.Set();
        if (!_worker.Join(TimeSpan.FromSeconds(2))) throw new TimeoutException("Music synth worker did not stop.");
        _requested.Dispose(); _disposed = true;
    }
}
