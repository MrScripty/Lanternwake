namespace Lanternwake.Conversation;

/// <summary>Bounded owned stereo samples, including zeroing old arrays on growth/disposal.</summary>
public sealed class SpeechSampleBuffer : IReadOnlyList<StereoSample>, IDisposable
{
    private StereoSample[] _frames = [];
    public int SampleRate { get; }
    public int CapacityLimit { get; }
    public int Count { get; private set; }
    public bool Full => Count == CapacityLimit;
    public bool Disposed { get; private set; }
    public StereoSample this[int index] => !Disposed && index >= 0 && index < Count ? _frames[index] : throw new ArgumentOutOfRangeException(nameof(index));
    public SpeechSampleBuffer(int sampleRate)
    {
        if (sampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        SampleRate = sampleRate; CapacityLimit = Math.Min(sampleRate * 30, (24 * 1024 * 1024 - 4096) / 8);
    }
    public void Append(ReadOnlySpan<StereoSample> frames)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        var count = Math.Min(frames.Length, CapacityLimit - Count);
        for (var i = 0; i < count; i++)
            if (!float.IsFinite(frames[i].Left) || !float.IsFinite(frames[i].Right) ||
                Math.Abs(frames[i].Left) > 1 || Math.Abs(frames[i].Right) > 1)
                throw new InvalidDataException("The audio source returned invalid samples.");
        if (Count + count > _frames.Length)
        {
            var next = new StereoSample[Math.Min(CapacityLimit, Math.Max(Count + count, Math.Max(4096, _frames.Length * 2)))];
            _frames.AsSpan(0, Count).CopyTo(next); Array.Clear(_frames); _frames = next;
        }
        frames[..count].CopyTo(_frames.AsSpan(Count)); Count += count;
    }
    public IEnumerator<StereoSample> GetEnumerator() { for (var i = 0; i < Count; i++) yield return this[i]; }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    public void Dispose() { if (Disposed) return; Array.Clear(_frames); _frames = []; Count = 0; Disposed = true; }
}
