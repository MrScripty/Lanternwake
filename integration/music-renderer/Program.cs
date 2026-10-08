using MeltySynth;
using System.Text.Json;

// An offline authoring tool. No synthesizer, SoundFont, or MIDI parser runs in Godot.
if (args.Length != 3) throw new ArgumentException("Usage: MusicRenderer bank.sf2 catalog.json output-directory");
const int rate = 32000;
const int frames = 80 * rate;
var font = new SoundFont(args[0]);
Directory.CreateDirectory(args[2]);
var catalog = JsonSerializer.Deserialize<RenderCatalog>(File.ReadAllText(args[1]), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException("Missing score catalog.");
foreach (var layer in catalog.Layers)
{
    var path = Path.Combine(Path.GetDirectoryName(args[1])!, "Source", layer.Midi);
    foreach (var stem in layer.Stems)
    {
        var channels = stem.Channels;
        var name = stem.Name;
        var synth = new Synthesizer(font, new SynthesizerSettings(rate)
        {
            EnableReverbAndChorus = false, BlockSize = 64, MaximumPolyphony = 64
        }) { MasterVolume = 3.2f };
        var midi = new MidiFile(path);
        if (Math.Abs(midi.Length.TotalSeconds - 80) > .002) throw new InvalidDataException("Stem is not exactly 32 bars: " + path);
        var sequencer = new MidiFileSequencer(synth);
        sequencer.OnSendMessage = (target, channel, command, data1, data2) =>
        {
            if (channels.Contains(channel)) target.ProcessMidiMessage(channel, command, data1, data2);
        };
        sequencer.Play(midi, true);
        var left = new float[frames]; var right = new float[frames];
        // Warm up a complete loop so sustained instruments have their previous release tails.
        sequencer.Render(left, right);
        sequencer.Render(left, right);
        var samples = new short[frames * 2];
        double peak = 0, sumSquares = 0;
        for (int i = 0; i < frames; i++)
        {
            // Circular, quiet room reflections preserve the common grid and loop tails.
            float l = left[i] + .12f * right[(i + frames - 9472) % frames] + .07f * left[(i + frames - 15936) % frames];
            float r = right[i] + .12f * left[(i + frames - 11264) % frames] + .07f * right[(i + frames - 18304) % frames];
            // The sequencer's loop boundary is block-quantized; suppress only the final 4 ms
            // and first 4 ms rather than shortening or overlapping the musical phrase.
            float seam = Math.Min(1f, Math.Min(i, frames - 1 - i) / 128f);
            l *= seam; r *= seam;
            if (!float.IsFinite(l) || !float.IsFinite(r) || Math.Max(Math.Abs(l), Math.Abs(r)) >= .65)
                throw new InvalidDataException("Nonfinite or excessive stem peak: " + path);
            samples[2 * i] = (short)Math.Round(l * 32767, MidpointRounding.ToEven);
            samples[2 * i + 1] = (short)Math.Round(r * 32767, MidpointRounding.ToEven);
            peak = Math.Max(peak, Math.Max(Math.Abs(l), Math.Abs(r)));
            sumSquares += (l * l + r * r) / 2;
        }
        using var output = new BinaryWriter(File.Create(Path.Combine(args[2], name + ".wav")));
        output.Write("RIFF"u8); output.Write(36 + samples.Length * 2); output.Write("WAVEfmt "u8);
        output.Write(16); output.Write((short)1); output.Write((short)2); output.Write(rate);
        output.Write(rate * 4); output.Write((short)4); output.Write((short)16); output.Write("data"u8);
        output.Write(samples.Length * 2);
        foreach (short sample in samples) output.Write(sample);
        Console.WriteLine($"{name} peak={peak:F4} rms={Math.Sqrt(sumSquares / frames):F4}");
    }
}

record RenderCatalog(RenderLayer[] Layers);
record RenderLayer(string Midi, RenderStem[] Stems);
record RenderStem(string Name, int[] Channels);
