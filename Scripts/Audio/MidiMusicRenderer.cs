using MeltySynth;

namespace Lanternwake.Audio;

public readonly record struct StereoFrame(float Left, float Right);
public sealed record MusicVoiceInput(string Name, int[] Channels, byte[] Midi);

/// <summary>Single-threaded live score. Every voice advances by the same sample count.</summary>
public sealed class MidiMusicRenderer
{
    public const int SampleRate = 32000, BlockFrames = 512, LoopFrames = 2560000;
    private const int SynthBlockFrames = 64;
    private readonly MidiFileSequencer[] _voices;
    private readonly float[] _gains, _targets;
    private readonly float[] _left = new float[BlockFrames], _right = new float[BlockFrames];
    private readonly float[] _mixLeft = new float[BlockFrames], _mixRight = new float[BlockFrames];
    private readonly float[] _historyLeft = new float[18432], _historyRight = new float[18432];
    private int _historyPosition;
    private long _framesRendered;
    public long FramesRendered => Interlocked.Read(ref _framesRendered);
    public int VoiceCount => _voices.Length;

    public MidiMusicRenderer(byte[] bank, IReadOnlyList<MusicVoiceInput> inputs)
    {
        using var stream = new MemoryStream(bank, writable: false);
        var font = new SoundFont(stream);
        var midis = new Dictionary<byte[], MidiFile>(ReferenceEqualityComparer.Instance);
        _voices = new MidiFileSequencer[inputs.Count];
        _gains = new float[inputs.Count]; _targets = new float[inputs.Count];
        for (int index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            if (input.Channels.Length == 0 || input.Channels.Any(channel => channel is < 0 or > 15))
                throw new InvalidDataException("Invalid MIDI channels: " + input.Name);
            if (!midis.TryGetValue(input.Midi, out var midi))
            {
                using var midiStream = new MemoryStream(input.Midi, writable: false);
                midi = new MidiFile(midiStream);
                long loopFrames = (long)Math.Ceiling(midi.Length.TotalSeconds * SampleRate / SynthBlockFrames) * SynthBlockFrames;
                if (loopFrames != LoopFrames)
                    throw new InvalidDataException("Music must share the 80-second score clock: " + input.Name);
                midis.Add(input.Midi, midi);
            }
            var synth = new Synthesizer(font, new SynthesizerSettings(SampleRate)
            { EnableReverbAndChorus = false, BlockSize = SynthBlockFrames, MaximumPolyphony = 64 }) { MasterVolume = 3.2f };
            var sequencer = new MidiFileSequencer(synth);
            sequencer.OnSendMessage = (target, channel, command, data1, data2) =>
            {
                if (Array.IndexOf(input.Channels, channel) >= 0) target.ProcessMidiMessage(channel, command, data1, data2);
            };
            sequencer.Play(midi, true);
            _voices[index] = sequencer;
        }
    }

    public void SetGains(ReadOnlySpan<float> gains, bool immediate = false)
    {
        if (gains.Length != _voices.Length) throw new ArgumentException("Supply a gain for every score voice.");
        foreach (var gain in gains)
            if (!float.IsFinite(gain) || gain is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(gains));
        gains.CopyTo(_targets);
        if (immediate) gains.CopyTo(_gains);
    }

    public void Render(Span<StereoFrame> output)
    {
        if (output.Length != BlockFrames) throw new ArgumentException("Render complete score blocks.");
        Array.Clear(_mixLeft); Array.Clear(_mixRight);
        for (int voice = 0; voice < _voices.Length; voice++)
        {
            // Inaudible voices still advance their MIDI events and release tails:
            // activating a layer never starts its phrase on a different clock.
            _voices[voice].Render(_left, _right);
            float gain = _gains[voice], difference = _targets[voice] - gain;
            for (int frame = 0; frame < BlockFrames; frame++)
            {
                float level = gain + difference * ((frame + 1f) / BlockFrames);
                _mixLeft[frame] += _left[frame] * level;
                _mixRight[frame] += _right[frame] * level;
            }
            _gains[voice] = _targets[voice];
        }
        for (int frame = 0; frame < BlockFrames; frame++)
        {
            int cursor = _historyPosition, length = _historyLeft.Length;
            float left = _mixLeft[frame] + .12f * _historyRight[(cursor + length - 9472) % length] + .07f * _historyLeft[(cursor + length - 15936) % length];
            float right = _mixRight[frame] + .12f * _historyLeft[(cursor + length - 11264) % length] + .07f * _historyRight[(cursor + length - 18304) % length];
            if (!float.IsFinite(left) || !float.IsFinite(right)) throw new InvalidDataException("Synth produced nonfinite audio.");
            output[frame] = new(left, right);
            _historyLeft[cursor] = _mixLeft[frame]; _historyRight[cursor] = _mixRight[frame];
            _historyPosition = (cursor + 1) % length;
        }
        Interlocked.Add(ref _framesRendered, BlockFrames);
    }
}
