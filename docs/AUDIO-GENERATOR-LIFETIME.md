# Music generator retirement

Quickly quitting a muted author preview could print `Audio shutdown timed out
while waiting for native playback release` and leave the process hung. This
generator lifetime defect is separate from missing-stream null-playback handling.

Godot 4.6.3's `AudioStreamGeneratorPlayback` stores a raw generator pointer
([header](https://github.com/godotengine/godot/blob/4.6.3-stable/servers/audio/effects/audio_stream_generator.h#L82)).
Stopping its player schedules a final native mix before playback deletion
([server](https://github.com/godotengine/godot/blob/4.6.3-stable/servers/audio/audio_server.cpp#L1278)).
`AudioDirector.StopLoop` previously cleared and disposed the duplicated generator
immediately. The mixer could then read freed memory for its sampling rate.
A diagnostic observed generator mixing in the resampler loop with the authored
32,000 Hz rate replaced by NaN in freed memory. The mixer held the driver mutex,
preventing shutdown.

The music playback now holds a native metadata reference to its generator.
Native `Variant` resource ownership survives C# wrapper disposal, including
director removal and whole-tree shutdown. The reference has no cycle and ends
when playback is destroyed. The temporary managed Variant is explicitly
disposed. WAV playback already owns its stream through a native `Ref`.

The new regression also exposed a pending-release task that never settled after
the director was freed: its frame awaiter targeted that director. Retirement now
awaits through the surviving SceneTree. The two-second production deadline is
unchanged.

Run `python3 integration/qa/generator_lifetime.py` with `GODOT_MONO` set after the
usual project setup/build. It repeats 36 native generator lifetimes (graceful,
forced free, detach, and pending-stop teardown), checks native retention and
eventual destruction without creating observer references, and runs 12 original
muted-preview Quit paths. `scripts/verify.sh` includes this regression. Set
`LANTERNWAKE_AUDIO_EVIDENCE` to store logs outside the checkout.

For a separate engine-boundary diagnostic, run the qualification scene with
`-- --quit-whole-tree`. This quits immediately with active audio, bypassing the
game's graceful Quit path. Godot can stop its driver before the final mix removes
active playback-list nodes, leaving ObjectDB leak warnings at process exit.
These warnings are preserved as a separate limitation; they are not accepted as
a clean regression pass. Normal Quit and forced node teardown with a continuing
tree must drain without warnings. No timeout inflation, provider calls, models,
or physical-hearing claims are involved.
