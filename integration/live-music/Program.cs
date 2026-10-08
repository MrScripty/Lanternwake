using System.Diagnostics;
using System.Text.Json;
using Lanternwake.Audio;

var root = Path.GetFullPath(args[0]);
var bank = File.ReadAllBytes(Path.Combine(root, "Source/Saltmere-Acoustic.sf2"));
using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "catalog.json")));
var inputs = new List<MusicVoiceInput>();
foreach (var layer in catalog.RootElement.GetProperty("layers").EnumerateArray())
{
    var midi = File.ReadAllBytes(Path.Combine(root, "Source", layer.GetProperty("midi").GetString()!));
    foreach (var stem in layer.GetProperty("stems").EnumerateArray())
        inputs.Add(new(stem.GetProperty("name").GetString()!, stem.GetProperty("channels").EnumerateArray().Select(channel => channel.GetInt32()).ToArray(), midi));
}
int count = 0;
void Check(bool condition, string claim) { if (!condition) throw new Exception(claim); count++; }
var block = new StereoFrame[MidiMusicRenderer.BlockFrames];
double Energy(StereoFrame frame) => (frame.Left * frame.Left + frame.Right * frame.Right) / 2.0;

if (args.Length > 1)
{
    foreach (var line in File.ReadLines(args[1]).Where(line => line.StartsWith("LANTERNWAKE_MUSIC_PREVIEW ")))
    {
        using var mix = JsonDocument.Parse(line["LANTERNWAKE_MUSIC_PREVIEW ".Length..]);
        var gains = mix.RootElement.GetProperty("stems").EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetSingle());
        string place = mix.RootElement.GetProperty("location").GetString()!;
        var localInputs = inputs.Where(input => input.Name == place + "_place").ToArray();
        var moodInputs = inputs.Take(16).Where(input => gains[input.Name] > 0).ToArray();
        var local = new MidiMusicRenderer(bank, localInputs); var mood = new MidiMusicRenderer(bank, moodInputs);
        local.SetGains(localInputs.Select(input => gains[input.Name]).ToArray(), true);
        mood.SetGains(moodInputs.Select(input => gains[input.Name]).ToArray(), true);
        var localEnergy = new double[80]; var moodEnergy = new double[80];
        var moodBlock = new StereoFrame[block.Length];
        for (int frame = 0; frame < MidiMusicRenderer.LoopFrames; frame += block.Length)
        {
            local.Render(block); mood.Render(moodBlock);
            for (int sample = 0; sample < block.Length; sample += 32)
            {
                int second = (frame + sample) / MidiMusicRenderer.SampleRate;
                localEnergy[second] += Energy(block[sample]); moodEnergy[second] += Energy(moodBlock[sample]);
            }
        }
        double minimum = Enumerable.Range(0, 78).Min(start => Math.Sqrt(localEnergy.Skip(start).Take(3).Sum() / Math.Max(1e-12, moodEnergy.Skip(start).Take(3).Sum())));
        Check(minimum >= .5, "Location stays audible at every loop entry phase: " + place);
        Console.WriteLine($"LANTERNWAKE_LIVE_LOCATION_OK location={place} minimum_place_to_mood_rms={minimum:F3}");
    }
    Check(count == 5, "Native scene mix log covers all five locations.");
}
else
{
    var renderer = new MidiMusicRenderer(bank, inputs);
    var gains = new float[inputs.Count];
    renderer.Render(block);
    Check(block.All(frame => frame == default), "Unmixed live music is genuinely silent.");
    foreach (var name in new[] { "harbor_place", "keeper_house_place", "archive_place", "lantern_room_place", "tide_cave_place" })
    {
        Array.Clear(gains); gains[inputs.FindIndex(input => input.Name == name)] = .85f;
        renderer.SetGains(gains, true);
        double peak = 0, energy = 0;
        for (int index = 0; index < 125; index++)
        {
            renderer.Render(block);
            foreach (var frame in block) { energy += Energy(frame); peak = Math.Max(peak, Math.Max(Math.Abs(frame.Left), Math.Abs(frame.Right))); }
        }
        Check(energy > .01 && peak < 1, "Live MIDI voice produces bounded audio: " + name);
    }
    long frameCount = renderer.FramesRendered;
    Array.Clear(gains); renderer.SetGains(gains);
    for (int index = 0; index < 64; index++) renderer.Render(block);
    Check(block.All(frame => frame == default) && renderer.FramesRendered > frameCount, "Silent layers preserve their clock after reflection tails finish.");
    gains[0] = .195f; gains[1] = .0864f; gains[2] = .045f;
    gains[16] = .85f; gains[22] = .6f; gains[26] = .36f;
    renderer.SetGains(gains, true);
    var timer = Stopwatch.StartNew();
    const int measuredBlocks = 625;
    for (int index = 0; index < measuredBlocks; index++) renderer.Render(block);
    double audioSeconds = measuredBlocks * block.Length / (double)MidiMusicRenderer.SampleRate;
    Check(timer.Elapsed.TotalSeconds < audioSeconds, "Full score synthesizes faster than real time.");
    Console.WriteLine($"LANTERNWAKE_SYNTH_BENCHMARK voices={renderer.VoiceCount} audio_seconds={audioSeconds:F1} compute_seconds={timer.Elapsed.TotalSeconds:F3}");
    using (var worker = new BufferedMusicSynth(new MidiMusicRenderer(bank, inputs), gains))
    {
        var deadline = Stopwatch.StartNew(); int blocks = 0;
        while (blocks < 32 && deadline.Elapsed.TotalSeconds < 3)
            if (worker.TryRead(block)) blocks++; else Thread.Yield();
        Check(blocks == 32 && worker.WorkerRunning && worker.FramesRendered > 4096, "Worker queue feeds bounded blocks while preserving the common clock.");
        worker.Dispose();
        Check(!worker.WorkerRunning && !worker.TryRead(block), "Stop joins the worker and prevents late PCM reads.");
    }
    try { new MidiMusicRenderer(bank, [new("invalid", [16], inputs[0].Midi)]); Check(false, "Invalid channels rejected."); }
    catch (InvalidDataException) { count++; }
    try { new MidiMusicRenderer(bank, [new("invalid", [0], [1, 2, 3])]); Check(false, "Malformed MIDI rejected."); }
    catch (Exception error) when (error is InvalidDataException or EndOfStreamException or NotSupportedException) { count++; }
    // A valid MIDI lasting one tick beyond 80 seconds would make its sequencer
    // loop one synth block later than the other layers, drifting on every repeat.
    byte[] driftingMidi = [.. "MThd"u8.ToArray(), 0, 0, 0, 6, 0, 0, 0, 1, 1, 224,
        .. "MTrk"u8.ToArray(), 0, 0, 0, 9, 0, 0xc0, 0, 0x84, 0xd8, 1, 0xff, 0x2f, 0];
    try { new MidiMusicRenderer(bank, [new("drifting", [0], driftingMidi)]); Check(false, "Drifting loop rejected."); }
    catch (InvalidDataException error) when (error.Message.Contains("80-second score clock")) { count++; }
}
Console.WriteLine($"LANTERNWAKE_SYNTH_OK assertions={count}");
