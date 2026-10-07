# Audio integration and authoring

## Ownership and editing

`Scenes/Audio/AudioDirector.tscn` owns four editable `AudioStreamPlayer` nodes.
Its Inspector exposes the five location WAVs and crossfade duration. For custom audio, put an original or appropriately licensed loop outside
`Assets/Audio` (for example `Assets/CustomAudio`) and assign it in the Inspector.
Only generated `Assets/Audio/*.wav` is ignored; hand-authored WAVs elsewhere
remain visible to Git. The director makes a runtime-only loop copy; it does not mutate
imported resources. Repeated beats and same-location scene changes retain the
playhead. Rapid location requests retire the superseded transition; only the
latest location remains active after the fade.

`default_bus_layout.tres` owns Master, Music, Ambience and Effects routing and
starting gains. Music and ambience start at the title. The single authored
`bell_lowered` beat triggers the quiet release effect. Loading or previewing an
existing beat also stops the old timeline’s one-shot and never replays a historic
cue, including when loading the same cue beat. Text already
carries the clue; no clue requires hearing the effect.

Settings → Sound settings offers independent music, location ambience and
effects levels, plus master mute. Keyboard Tab selects controls, arrow keys
adjust sliders, and Space toggles mute. Changes apply immediately and last
only for the current process; this milestone adds no settings-file format.
There is no voiced dialogue audio asset or dialogue-volume control yet.

## Assets and provenance

All seven WAVs in `Assets/Audio` are new original procedural placeholders,
materialized by `bash scripts/setup.sh` (or `python3 scripts/setup_audio.py`)
using only Python's standard library. The seven WAV files are generated
outputs, not Git-tracked inputs. They are not recovered stems from the previously delivered 58-second
sampler. That sampler has been preserved separately and is not sliced or
looped as ambience. The source generator and manifest describe their exact
sample counts, rate, hashes and levels. The six 16-second loops use integer
periods and a periodic envelope; the release cue is a five-second one-shot.
No external recordings, paid services, model inference or microphone input
are used to generate or play these assets.

Run setup before opening the Godot editor or playing a fresh checkout. On
Windows use `py -3 scripts/setup_audio.py`. `scripts/verify.sh` runs setup
first, then imports assets before native tests; CI also has an explicit setup
step before any Godot invocation. No synthesizer runs during gameplay.
Missing Python is a setup failure; install Python 3 from python.org or set
`PYTHON` to its executable when using the shell wrapper.

Setup checks the committed provenance manifest first and leaves matching
files (including modification times) unchanged. If files are missing, it
generates all candidates in a temporary directory, verifies every expected
hash, then atomically installs each missing output. A failed generation or
hash mismatch preserves existing assets. A modified generated WAV is refused;
preserve custom work elsewhere, then explicitly use `--regenerate` to replace
it. Publication is per file; interruption can leave a subset missing, and a
rerun fills those missing files. The authoring-only generator can update the
source manifest deliberately; ordinary setup never changes it.

These restrained tone/noise beds establish functioning routing, transitions
and controls. They are not a finished felt-piano score, realistic harbor field
recording, original song performance, or a substitute for listener review.
Dynamic-range reduction, richer effects, music spotting and final mastering
remain production work.

## Verification

`--audio-smoke` selects disposable test storage, just like the existing UI
smoke. Contradictory/malformed launch flags fail closed. The test checks the
actual players, imported-resource preservation, looping, rapid transition
replacement, same-location continuity, bus gains/mute, historical cue
suppression, settings visibility, production control wiring, reopen retention
and stopping every owned player. Each started playback has one owned managed
handle. Finished or stopped playbacks are released only when the reference
count falls to that sole owned reference. Shutdown waits for this native
ownership condition, not a guessed fixed sleep; a two-second deadline emits
an error and exits if a backend cannot retire its handles. The native
[Godot 4.6.3 implementation](https://github.com/godotengine/godot/blob/4.6.3-stable/servers/audio/audio_server.cpp)
queues stop/deletion on the mixer and releases its references during
main-thread cleanup. Engine upgrades must revalidate this boundary. `scripts/verify.sh` includes this mode.

Playback acquisition first checks that the player has a native playback and
retains only a non-null handle. Missing streams therefore cannot put null into
per-frame retirement or tree-exit disposal. This does not replace setup: absent
location assets still report the Inspector assignment failure, with fresh-checkout
setup commands for Linux and Windows. The
native `qualification/audio-lifecycle.tscn` regression covers missing music and
effects, naturally finished effects, live loop retention, interrupted transitions,
repeat shutdown and direct tree exit. `scripts/verify.sh` runs it after the audio smoke.

An AudioEffectCapture on the real Master mixer observes nonzero bounded PCM.
This is native Godot mixing evidence, not proof of physical speaker output,
subjective sound quality, production voice recognition, model quality, or
five-hour gameplay duration. If a backend produces no capture frames, the
smoke reports PCM unavailable rather than fabricating a pass.

Native cloud desktop inspection found and fixed an initially hidden control
container. The corrected UI visibly showed all three channels and mute;
Space/Tab/Right changed mute and music from 35% to 40%, and closing/reopening
preserved those values. The full story, stage art and existing save format
remain unchanged.
