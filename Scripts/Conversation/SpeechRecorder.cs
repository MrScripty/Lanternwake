using Godot;
using System.Diagnostics;
namespace Lanternwake.Conversation;

/// <summary>Local microphone capture and explicitly configured whisper.cpp transcription. No cloud audio.</summary>
public sealed class SpeechRecorder : IDisposable
{
    private AudioStreamPlayer? _player;
    private AudioEffectCapture? _capture;
    private int _bus = -1;
    private List<Vector2> _samples = [];
    public bool Recording => _player is not null;
    public bool Available => File.Exists(System.Environment.GetEnvironmentVariable("LANTERNWAKE_WHISPER_CLI")) && File.Exists(System.Environment.GetEnvironmentVariable("LANTERNWAKE_WHISPER_MODEL"));
    public void Start(Node owner)
    {
        if (!Available) throw new InvalidOperationException("Local speech requires LANTERNWAKE_WHISPER_CLI and LANTERNWAKE_WHISPER_MODEL. Typed replies work without them.");
        if (Recording) return;
        _samples.Clear();
        _bus = AudioServer.BusCount;
        AudioServer.AddBus(); AudioServer.SetBusName(_bus, "LanternwakeCapture");
        _capture = new AudioEffectCapture { BufferLength = 2f };
        AudioServer.AddBusEffect(_bus, _capture);
        AudioServer.SetBusMute(_bus, true);
        _player = new AudioStreamPlayer { Stream = new AudioStreamMicrophone(), Bus = "LanternwakeCapture" };
        owner.AddChild(_player); _player.Play();
    }
    public void Poll()
    {
        if (_capture is null) return;
        var remaining = Math.Max(0, (int)(AudioServer.GetMixRate() * 30) - _samples.Count);
        _samples.AddRange(_capture.GetBuffer(Math.Min(_capture.GetFramesAvailable(), remaining)));
    }
    public async Task<string> StopAndTranscribe(CancellationToken cancellation)
    {
        Poll(); Stop();
        // Transfer ownership before awaiting: a later capture never shares this buffer.
        var samples = _samples; _samples = [];
        if (samples.Count == 0) throw new InvalidOperationException("No microphone samples received. Check the operating system microphone permission.");
        var prefix = Path.Combine(Path.GetTempPath(), "lanternwake-voice-" + Guid.NewGuid().ToString("N"));
        var wav = prefix + ".wav";
        try
        {
            // whisper.cpp accepts 16 kHz mono PCM. Capture rate comes from Godot, not an assumed device rate.
            var inputRate = AudioServer.GetMixRate();
            var count = (int)(samples.Count * 16000d / inputRate);
            using (var writer = new BinaryWriter(File.Create(wav)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(16000); writer.Write(32000); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (var i = 0; i < count; i++)
                {
                    var frame = samples[Math.Min(samples.Count - 1, (int)(i * inputRate / 16000d))];
                    writer.Write((short)(Math.Clamp((frame.X + frame.Y) * .5f, -1f, 1f) * short.MaxValue));
                }
            }
            var info = new ProcessStartInfo(System.Environment.GetEnvironmentVariable("LANTERNWAKE_WHISPER_CLI")!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in new[] { "-m", System.Environment.GetEnvironmentVariable("LANTERNWAKE_WHISPER_MODEL")!, "-f", wav, "-otxt", "-of", prefix, "-nt" }) info.ArgumentList.Add(arg);
            using var process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException("Could not start local speech engine.");
            var stderr = process.StandardError.ReadToEndAsync(cancellation);
            var stdout = process.StandardOutput.ReadToEndAsync(cancellation);
            try { await process.WaitForExitAsync(cancellation); }
            catch { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); try { await Task.WhenAll(stderr, stdout); } catch (OperationCanceledException) { } throw; }
            await Task.WhenAll(stderr, stdout);
            if (process.ExitCode != 0 || !File.Exists(prefix + ".txt")) throw new InvalidOperationException("Local speech engine failed. Your audio was not uploaded.");
            return (await File.ReadAllTextAsync(prefix + ".txt", cancellation)).Trim();
        }
        finally { foreach (var file in new[] { wav, prefix + ".txt" }) if (File.Exists(file)) File.Delete(file); samples.Clear(); }
    }
    private void Stop()
    {
        if (_player is not null) { _player.Stop(); _player.QueueFree(); _player = null; }
        if (_bus >= 0) { AudioServer.RemoveBus(_bus); _bus = -1; }
        _capture = null;
    }
    public void Dispose() { Stop(); _samples.Clear(); }
}
