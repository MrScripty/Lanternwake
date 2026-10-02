using Lanternwake.Conversation;

if (args.Length != 4) { Console.Error.WriteLine("Usage: speech-smoke whisper-cli model.bin sample.wav temporary-directory"); return 2; }
var (executable, model, input, temporary) = (args[0], args[1], args[2], args[3]);
var samples = new List<StereoSample>();
int sampleRate = 0, channels = 0, bits = 0;
using (var reader = new BinaryReader(File.OpenRead(input)))
{
    if (new string(reader.ReadChars(4)) != "RIFF") throw new Exception("Fixture must be RIFF WAV.");
    reader.ReadInt32(); if (new string(reader.ReadChars(4)) != "WAVE") throw new Exception("Fixture must be WAV.");
    while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
    {
        var kind = new string(reader.ReadChars(4)); var size = reader.ReadInt32(); var next = reader.BaseStream.Position + size + (size % 2);
        if (kind == "fmt ") { if (reader.ReadInt16() != 1) throw new Exception("PCM fixture required."); channels = reader.ReadInt16(); sampleRate = reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt16(); bits = reader.ReadInt16(); }
        if (kind == "data")
        {
            if (channels != 1 || bits != 16) throw new Exception("Mono16 fixture required.");
            for (var n = 0; n < size / 2; n++) { var sample = reader.ReadInt16() / 32768f; samples.Add(new(sample, sample)); }
        }
        reader.BaseStream.Position = next;
    }
}
Directory.CreateDirectory(temporary);
if (Directory.EnumerateFileSystemEntries(temporary).Any()) throw new Exception("Test temporary directory must start empty.");
var transcriber = new LocalSpeechTranscriber(executable, model, temporary);
var transcript = await transcriber.TranscribeAsync(samples, sampleRate, CancellationToken.None);
if (!transcript.Contains("country", StringComparison.OrdinalIgnoreCase) || !transcript.Contains("fellow", StringComparison.OrdinalIgnoreCase)) throw new Exception("Known speech fixture was not recognized.");
if (Directory.EnumerateFileSystemEntries(temporary).Any()) throw new Exception("Success leaked temporary files.");
Console.WriteLine("PASS real local sample transcription and temp cleanup: " + transcript);
var highRateSamples = samples.SelectMany(sample => new[] { sample, sample, sample }).ToArray();
var resampled = await transcriber.TranscribeAsync(highRateSamples, sampleRate * 3, CancellationToken.None);
if (!resampled.Contains("country", StringComparison.OrdinalIgnoreCase)) throw new Exception("48 kHz conversion failed.");
Console.WriteLine("PASS 48 kHz input resampling without integer overflow");
var longSamples = Enumerable.Range(0, sampleRate * 30).Select(i => samples[i % samples.Count]).ToArray();
using (var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(150)))
{
    try { await transcriber.TranscribeAsync(longSamples, sampleRate, cancel.Token); throw new Exception("Expected cancellation."); }
    catch (OperationCanceledException) { Console.WriteLine("PASS active transcription cancellation and process reaping"); }
}
if (Directory.EnumerateFileSystemEntries(temporary).Any()) throw new Exception("Cancellation leaked temporary files.");
var retry = await transcriber.TranscribeAsync(samples, sampleRate, CancellationToken.None);
if (!retry.Contains("country", StringComparison.OrdinalIgnoreCase)) throw new Exception("Retry after cancellation failed.");
if (Directory.EnumerateFileSystemEntries(temporary).Any()) throw new Exception("Retry leaked temporary files.");
Console.WriteLine("PASS independent retry after cancellation; no microphone qualification claimed.");
return 0;
