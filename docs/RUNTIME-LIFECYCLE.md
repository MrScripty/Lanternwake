# Repeated native game lifecycle

Run `python3 integration/qa/runtime_lifecycle.py` after preparing a Debug build with
`scripts/run.py --setup-only`; set `GODOT_MONO` to the installed official Godot .NET
4.6.3 executable. `scripts/verify.sh` includes this check.

The runner starts the real `Scenes/Main.tscn` four times in one native process,
using a disposable Linux profile. Each instance displays the authored startup
menu and starts the story through its real New story button. Canonical snapshots
select seven rapid location changes per instance through production `RenderBeat`.
They cover all five locations, interrupted ambience fades and the continuous MIDI
score. Reflection selects checkpoints and observes ownership; it does not replace
the audio player, synthesis worker, menu or scene implementation.

Two instances retire gracefully (including repeated stop), one is queued for
deletion while audio is active, and one is detached and freed. The last instance
has absent music and effect streams, covering the original null-playback cause
through the complete game root. All cases check worker termination and shared EQ
restoration before the next startup. No saves or AI settings are written, and the
runner verifies tracked source hashes remain unchanged.

The fixture refuses to instantiate Main unless its user directory is inside the
marked disposable profile. It clears inherited model selection and API-key input
for its child process, and uses an unavailable owned desktop-bus address so the
test cannot reach an existing keyring. It sends no provider or microphone request.
The speech-ducking envelope is a teardown stress signal, not synthesized speech
or a claim of Pumas audio support.

This is Linux native regression coverage, not physical hearing, model inference,
human playtime, or Windows/macOS qualification. Existing audio smoke checks cover
captured MIDI/ambience PCM separately. The runtime check emits
`LANTERNWAKE_RUNTIME_LIFECYCLE_OK cycles=4 checks=105` on success. The settled
ambience assertion compares harbor PCM and format rather than resource identity,
and requires finite full local gain. Native silent and wrong-location probes
confirm that each condition rejects playback which the original playing-count
assertion would accept.

Initial qualification uses main
`af93c24e3f8e0ca64af8d4a46847536462c39867`, which includes the merged launcher,
owner startup menu/AI settings, adaptive MIDI score and scene work. No production
UI, story, AI configuration or audio behavior changes are part of this regression.

The initial full suite passed with 103 new lifecycle checks, 18 existing
missing-stream/playback checks, 259 adaptive-audio checks and the 13-scene
headless editor roundtrip. Two additional cold rendered normal launches used
actual X11 New story, advance, Escape/menu and Quit controls and exited zero.
Those runs used explicit OpenGL compatibility, llvmpipe and Dummy audio on an
owned authenticated display. V-Sync, Forward+ volumetric fog and depth blur are
unavailable in that environment.

A graphical editor attempt failed during startup with a native Godot signal-11
crash before its F6 check could run; the display lacks Vulkan surface support.
That remains a separate graphical-editor/environment blocker, not a passing
in-editor play result. No owner renderer or project setting was changed to hide
it. Fresh public CI and independent review still gate merging this regression.
