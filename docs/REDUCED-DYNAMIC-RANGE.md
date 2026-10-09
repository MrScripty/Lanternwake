# Optional sound range reduction

Sound settings offers Reduce loud/quiet differences from the startup menu and
in-story Reading settings. Tab reaches the toggle after Mute and Space toggles
it. The control honors the session's reading text size.

The choice applies immediately and lasts for the running session, including a new
story. Reopening Sound settings reflects the live state; a fresh process defaults
to the original mix. Independent channel volume and mute remain available.

The authored Master bus has a disabled-by-default downward compressor named
Master / reduced range: -24 dB threshold, 4:1 ratio, 2 ms attack, 250 ms release,
fully wet and zero makeup gain. It softens loud material without boosting quiet
material. The Music-to-Dialogue sidechain remains active. AudioDirector locates
the compressor by resource name and requires it at startup; the bus resource,
Sound settings scene and script therefore belong together when editing this
feature. Godot documents the controls in its
[AudioEffectCompressor reference](https://docs.godotengine.org/en/4.6/classes/class_audioeffectcompressor.html).

## Verification

After normal setup/build with GODOT_MONO set, run:

```sh
python3 integration/qa/reduced_range.py
```

The fixture checks 35 native conditions and five fresh-process conditions:
startup/in-story controls, Tab/Space, enlarged text, independent mute/levels,
reopen/new-story retention, unchanged state/save bytes and the fresh default.
An owned deterministic WAV tone and native Master-bus capture compare quiet/loud
RMS with the effect disabled, enabled and disabled again. Missing optional music
still permits the toggle. Capture is after the compressor and before the Master
fader/mute; it is not a physical output-device recording.

Measured loud/quiet RMS ratios were approximately 26.7 disabled and 1.65 enabled;
disabling restored the original levels. A rendered Linux keyboard pass exercised
the complete toggle at 150% text in a 640x480 panel. Compatibility-renderer limits
were observed. These checks establish controls and processing, not human listening
preference, physical hearing or assistive-tool acceptance. Generated audio and QA
captures remain outside Git. The runner is part of scripts/verify.sh.
