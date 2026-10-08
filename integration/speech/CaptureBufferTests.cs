using System.Reflection;
using Lanternwake.Conversation;

internal static class CaptureBufferTests
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool value, string claim) { checks++; if (!value) throw new Exception(claim); }
        StereoSample[] Storage(SpeechSampleBuffer buffer) => (StereoSample[])typeof(SpeechSampleBuffer).GetField("_frames", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(buffer)!;
        var buffer = new SpeechSampleBuffer(8000);
        var first = Enumerable.Repeat(new StereoSample(.25f, -.5f), 4096).ToArray();
        buffer.Append(first); var old = Storage(buffer);
        buffer.Append(first); Check(old.All(f => f == default), "Growth must zero old audio storage.");
        Check(buffer.Count == 8192 && buffer[8191] == first[0], "Growth preserves frozen stereo samples.");
        var kept = Storage(buffer); buffer.Dispose(); buffer.Dispose();
        Check(kept.All(f => f == default) && buffer.Count == 0 && buffer.Disposed, "Discard zeroes all storage and is repeatable.");
        try { buffer.Append(first); throw new Exception("Disposed append accepted."); } catch (ObjectDisposedException) { checks++; }
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, -1.1f, 1.1f })
        {
            using var valid = new SpeechSampleBuffer(48000); valid.Append(first);
            try { valid.Append([new(invalid, 0)]); throw new Exception("Invalid PCM accepted."); } catch (InvalidDataException) { checks++; }
            Check(valid.Count == first.Length && valid[0] == first[0], "Invalid append leaves retained audio unchanged.");
        }
        foreach (var rate in new[] { 7999, 192001, int.MaxValue })
            try { _ = new SpeechSampleBuffer(rate); throw new Exception("Invalid rate accepted."); } catch (ArgumentOutOfRangeException) { checks++; }
        using var bounded = new SpeechSampleBuffer(8000);
        var excess = Enumerable.Repeat(first[0], bounded.CapacityLimit + 100).ToArray(); bounded.Append(excess);
        Check(bounded.Full && bounded.Count == 240000, "Capture stops at exactly 30 seconds, even with excess frames.");
        var held = Storage(bounded); bounded.Dispose(); Check(held.All(f => f == default), "Full clip is zeroed on cancellation.");
        using var highRate = new SpeechSampleBuffer(192000);
        Check(highRate.CapacityLimit * 8L * 4 / 3 + 4096 < 32 * 1024 * 1024L, "Maximum-rate PCM leaves bounded JSON envelope headroom.");
        Check(!SpeechRuntimeAdmission.Installed.Allows(SpeechCaptureKind.Microphone), "Installed-runtime admission stays independently closed.");
        Console.WriteLine($"PASS capture buffer assertions={checks}; synthetic PCM only, no microphone or inference claim.");
    }
}
