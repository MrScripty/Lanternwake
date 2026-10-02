using System.Diagnostics;

namespace Lanternwake.Conversation;

public readonly record struct StereoSample(float Left, float Right);

/// <summary>Owns one local speech process, its immutable sample input, and temporary files.</summary>
public sealed class LocalSpeechTranscriber(string executable, string model, string? temporaryDirectory = null)
{
    public async Task<string> TranscribeAsync(IReadOnlyList<StereoSample> samples, int sampleRate, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!File.Exists(executable) || !File.Exists(model)) throw new InvalidOperationException("Local speech executable/model is unavailable.");
        if (sampleRate is < 8000 or > 192000 || samples.Count == 0 || samples.Count > sampleRate * 30)
            throw new InvalidOperationException("Speech requires up to 30 seconds of valid microphone samples.");
        var folder = temporaryDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        var prefix = Path.Combine(folder, "lanternwake-voice-" + Guid.NewGuid().ToString("N"));
        var wav = prefix + ".wav";
        try
        {
            var count = (int)(samples.Count * 16000d / sampleRate);
            using (var writer = new BinaryWriter(File.Create(wav)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(16000); writer.Write(32000); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (var i = 0; i < count; i++)
                {
                    if (i % 16000 == 0) cancellation.ThrowIfCancellationRequested();
                    var frame = samples[Math.Min(samples.Count - 1, (int)(i * (double)sampleRate / 16000d))];
                    var mono = (frame.Left + frame.Right) * .5f;
                    writer.Write((short)((float.IsFinite(mono) ? Math.Clamp(mono, -1f, 1f) : 0f) * short.MaxValue));
                }
            }
            cancellation.ThrowIfCancellationRequested();
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in new[] { "-m", model, "-f", wav, "-otxt", "-of", prefix, "-nt", "-t", "2" }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start local speech engine.");
            var stderr = process.StandardError.ReadToEndAsync(cancellation);
            var stdout = process.StandardOutput.ReadToEndAsync(cancellation);
            try { await process.WaitForExitAsync(cancellation).ConfigureAwait(false); }
            catch
            {
                try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                try { await Task.WhenAll(stderr, stdout).ConfigureAwait(false); } catch (OperationCanceledException) { }
                throw;
            }
            await Task.WhenAll(stderr, stdout).ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(prefix + ".txt")) throw new InvalidOperationException("Local speech engine failed. Your audio was not uploaded.");
            return (await File.ReadAllTextAsync(prefix + ".txt", cancellation).ConfigureAwait(false)).Trim();
        }
        finally { foreach (var file in new[] { wav, prefix + ".txt" }) if (File.Exists(file)) File.Delete(file); }
    }
}
