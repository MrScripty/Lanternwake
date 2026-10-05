# Direct project-runtime acceptance, 2026-10-05

This follow-up tests the ordinary Godot project runtime and fixes two keyboard
faults found through desktop input. It starts from preserved checkpoint
`d8574fa40df89459d11de99d5b2679fd7503dc52`, tree
`5602169741ab2af976445bba87626a3518841f7b`, on separate branch
`qa/direct-runtime-interruption-20261005`. Remote main remains
`3051855bb0c8f4a6d222081f22d00a19c3298572`; PR12 remains
`05db466507dfbc736d7b0a386724a5fe068554b9`. No existing branch was updated,
no PR was opened or merged, and nothing was published. Repository/ancestor
AGENTS.md and checkout `.agents/skills` were checked; none were present.

On unchanged d8574fa, Arrival starts the first typewriter passage, but one Space
press on focused Continue reaches `ch1_s1_b002` instead of revealing
`ch1_s1_b001`. The focused button and global shortcut can both act on one key.
After correcting that, normal restart testing found that Escape in the focused
conversation LineEdit only releases field focus; the panel remains open and
Continue cannot advance. These failures were missed by tests that call
production methods directly.

`GameView._Input` now handles Space/Enter on focused Continue before GUI
dispatch, consumes both edges, and acts only once on a non-echo press. It also
handles conversation Escape before LineEdit consumes it, using the existing
Return/cancellation path. Godot documents this dispatch order and
`SetInputAsHandled` in its [input-event documentation](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html).
The production change is confined to this input handler. Story prose, scenes,
save format, audio synthesis and provider configuration are unchanged.

The new [external-input controller](../integration/qa/direct_runtime_input.py)
launches the normal project three times with official Godot
`4.6.3.stable.mono.official.7d41c59c4` and .NET SDK `8.0.425`. It supplies no
preview/smoke/alternate-scene/script flag or reflection. Mouse clicks and key
edges enter through X11 XTest. It observes root-screen images, OCR and committed
save files on a private authenticated 1440x900 dummy display with a 1280x800
client, TCP/MIT-SHM disabled, Mesa llvmpipe and Dummy audio. No fast mode or
engine-clock override is used. Automatic advances are not a reading-duration
claim. Static inspected control coordinates cover occasional OCR aliasing;
save identity, exact prose and visible-panel checks validate the effects.

The [compact receipt](evidence/direct-runtime-20261005.json) and local packet
`/workspace/lanternwake-direct-runtime-20261005.tar.gz` retain the final result,
source/binary bindings and evidence hashes. The packet is QA evidence, not an
installable player. It includes final screenshots, native logs, owned test saves,
the original negative, rejected/interim attempts and candidate source. It excludes
Xauthority, credentials, generated WAVs and the large template dependency.

The scenario covers first-passage reveal; focused Enter after visibly closing
History; ordered advancement and exact canonical prose; actual editing and
submission of a suggestion; explicitly labelled authored fallback; an unsent
draft during owned SIGKILL after completed writes; fresh-process explicit manual
Load without rewriting slots; repeated visible History/Catalogue cycles;
one-Escape conversation cancellation; advancement and manual-save rotation;
deliberate damage to only the disposable current manual slot; explicit previous
good recovery; subsequent Save preserving the compatible previous bytes; and
production Settings Quit. No current-player files are used.

The final scenario passes **141 checks**, records **186 external input events**
and retains **16 named root-screen PNGs**. The intentional first-process exit is
SIGKILL/-9; the restart and recovery processes each exit 0 through production
Quit, with no unexpected native diagnostics. The eight History/Catalogue cycles
assert both visible opening and visible closing. Actual save bytes, the exact
21-beat canonical history and typed/authored conversation lines are verified.

The full native `scripts/verify.sh` aggregate is also run on this production
candidate and exits 0 with no warning/error lines: 4,125 core assertions, 63
story-structure checks, 11 simulated Pumas scenarios, unsupported speech
boundary, five Python audio tests, native story/UI/save isolation, 32 mixer
assertions and 20,480 PCM frames through Dummy audio, plus completion/replay,
activity review, conversation Return, content note, cup states and 13-scene
Editor roundtrip. Debug, ExportDebug and ExportRelease compile with warnings as
errors. These managed configurations are not standalone exports.
This is distinct from the external-input scenario: its all-1,424-beat
completion and other native checks remain synthetic/headless. The earlier
[combined rendering/data-export receipt](COMBINED-RELEASE-ACCEPTANCE.md) qualifies
its earlier source only; it does not establish a new exported player for this
keyboard change. The retained new native DLL/PDB is checked against compiler
document checksums, including all 48 tracked compiled C# files and embedded
generated documents. Matching DLL/PDB checks cover 143 Debug, 137 ExportDebug
and 89 ExportRelease compiler documents, with 51 disk documents in each.

Calibration failures are retained separately: startup readiness, OCR aliases,
and an incorrect controller assumption that optional dialogue lacks a beat ID.
Optional dialogue actually carries its origin beat plus conversation scope; the
canonical-history assertion now filters by that scope. One aggregate reported
the already-observed intermittent Godot teardown error about uninitialized
`export/android/android_sdk_path`; that attempt is rejected. A clean repeat does
not establish a root-cause fix for the engine diagnostic. The dummy driver's
unsupported-VSync warning is retained as an environment limitation in rendered
runs. A controller run stopped to add visible-modal assertions is also labelled
interim; it does not count as acceptance.

One attempt observed History absent immediately after its key event and was
rejected. The final fresh-display run waits for visible changes and passes;
this does not establish the cause of that earlier observation. Several external
root-screen captures contain missing portions of button captions. A separate
normal-runtime diagnostic uses the existing Debug F12 shortcut to capture the
actual viewport; that image and neighboring root captures show complete labels.
The comparison did not reproduce the earlier incomplete glyphs, so their
capture/renderer cause remains unresolved. Those partial PNGs are retained but
do not qualify complete-label appearance. The native diagnostic exits 0 via
production Quit and preserves all tracked sources and any pre-existing capture.
Assistant inspection covers arrival/reveal, editing, fallback, Escape return,
explicit damaged-current recovery, recovered passage and the native viewport;
it remains software-rendered evidence, not physical display acceptance.

The exact official .NET export-template asset was downloaded from the ordinary,
accessible [Godot release asset](https://github.com/godotengine/godot-builds/releases/download/4.6.3-stable/Godot_v4.6.3-stable_mono_export_templates.tpz)
before the user's steering to prioritize gameplay. Its published SHA-256 is
`6436f474ee085fb0a94d3d296b9821736bdd663b6e91b2228fdf77066f1d1400`;
SHA-512 matches the official `SHA512-SUMS.txt`; all 27 ZIP members pass CRC and
the version is `4.6.3.stable.mono`. It remains cached outside Git at
`/workspace/.lanternwake-tools/export-templates-4.6.3/`. No template installation
or new standalone export followed that steering. The shell archive-page route
returned proxy CONNECT 403 and was not retried; the browser download-page route
redirected to published object storage that its fetcher could not open. The
accessible published GitHub asset returned HTTP 200. These routes and checksum
details are recorded in the packet; no denied route or security control was
bypassed.

With the official engine/SDK and installed Xorg dummy, ImageMagick, Tesseract,
libX11 and libXtst, reproduce on fresh owned paths:

```bash
python3 integration/qa/owned_display.py start --state-dir /tmp/lanternwake-display --number 176
python3 integration/qa/direct_runtime_input.py \
  --display-state /tmp/lanternwake-display/display.json \
  --fixture /tmp/lanternwake-player-fixture \
  --output /tmp/lanternwake-runtime-evidence
python3 integration/qa/owned_display.py stop --state-dir /tmp/lanternwake-display
```

`GODOT_MONO` must name the official .NET executable and the SDK must be on PATH.
The controller requires new fixture/output directories. `--arrival-only` limits
the run to the first-passage regression. Stop the owned display even if a run
fails; the stop helper verifies its process identity before signalling it.

Remaining acceptance: human editorial/emotional continuity and five-hour
duration, physical input and accessibility tools, actual hearing/microphone,
real loaded-model inference and in-flight cancellation, and platform/low-end
coverage. SIGKILL here is after completed saves, not during an atomic write.
Packaging is deferred at the user's request; an actual standalone export remains
unqualified. Parent retains integration, PR/review, merge and release decisions.
No raw evidence was uploaded to Library; that separate approval is pending.
