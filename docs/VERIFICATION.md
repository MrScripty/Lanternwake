# Verification record

The [integration repair verification](INTEGRATION-RECOVERY-VERIFICATION.md)
records the recovery from remote `17a4cc6`, enlarged main-passage keyboard reading,
the scoped story/documentation corrections and their native delta evidence.

The [playable coverage inventory](PLAYABLE-COVERAGE-INVENTORY.md) reconciles the
accepted source, long save/resume, late recovery and optional authored-fallback
qualification, with exact identities and remaining human/platform/audio gates.

The separate [late-session damaged-primary qualification](LATE-RECOVERY-VERIFICATION.md)
records actual Load/control recovery, owned faults, repeated cancellation and fresh
processes on the later non-audio QA source.

The separate [offline long-session save/resume qualification](LONG-SESSION-VERIFICATION.md)
records the later non-audio QA source, all-beat native button progression, six fresh
processes and the remaining acceptance limits. Earlier evidence below retains its
original source scope.

Status: development build, 2026-10-02. This is not a release declaration or a five-hour playtest.

## Executed environment

- Official Godot `4.6.3.stable.mono.official.7d41c59c4`, Linux x86_64
- Official .NET SDK `8.0.425`, target framework .NET 8
- Godot NuGet packages from the matching official editor archive
- Native GUI: X11, OpenGL 4.5 compatibility renderer, Mesa 25.0.7 llvmpipe software rendering
- Installed tools are local to the development workspace; no toolchain/model files are committed
- Coding-Standards reference inspected at `dcc56f26e884ade260770beceba2501d3746200d`

## Automated evidence

Final frozen-content run: **3,243 core assertions passed across 1,424 beats and five chapters**. Content has 34,937 main-path words, 41 scenes, 23 activities and 28 optional conversation points. SHA-256 of Content/story.json: `0fae051ce545f7e5c8cea248edac0e3fac89cb0a1ac8aa3f20a113f1ef99adce`. The `scripts/verify.sh` suite executes:

1. Production core tests: authored canonical order, every activity gate, ending, chat/state isolation, current-fact filtering, scene/character conversation-memory boundaries, serialized Unicode budget, save/provenance rejection, earlier-load reset, save replacement/size limits and pending-operation ownership.
2. **11 direct C# Pumas-client contract scenarios passed** against a simulated loopback Pumas service. These use the actual production client, but no real model.
3. Godot C# build: **0 warnings, 0 errors** on the reviewed build.
4. `--smoke`: full authored traversal, mandatory-answer selection, final activity, save/reopen and construction of all five actual procedural stages.
5. `--ui-smoke`: Godot title/start, reveal-before-advance, history/catalogue/settings, modal keyboard-focus return, editable suggestion, submitted authored fallback/history, chat close/reopen, manual save/load and evidence gating. Headless callbacks are integration evidence, not visual or hardware proof. The script fails on Godot `ERROR:` diagnostics even if the process exits 0.

Also executed: **19 Python diagnostic-adapter contract tests passed**, 12.060 seconds. Python is optional and is not used by the game. These simulated tests do not qualify live inference.

## Manual native GUI evidence

The actual compiled Godot game was launched through the cloud desktop's application launcher and inspected using CUA. No browser recreation or mock screenshot was substituted.

Observed:

- Rendered title and harbor/lighthouse/boat/rain composition
- All five original procedural locations: harbor, keeper house, archive, lantern room, tide cave
- Canon-aligned adult character sculptures, readable dialogue, current scene/chapter labels
- Mouse start/advance and keyboard reveal/advance
- Catalogue and settings, including Escape dismissal
- Suggested reply populated the input; its text was replaced with freely typed `please explain`; explicit Send produced the visibly labeled authored fallback because no model was configured
- Missing local speech setup displayed an unavailable message and did not start recording
- Incorrect evidence selection left advance blocked; correct selection enabled Continue
- Manual Save, advance into another scene, then manual Load restored the prior scene and its solved gate
- In-game Quit closed only the game cleanly

Visual review identified and repaired lighthouse framing, over-tall header buttons, modal Escape routing, modal exclusivity during rapid replacement and keyboard focus restoration after modal closure. The final focus invariant is also asserted in the headless UI suite.

Actual in-engine screenshots were written under ignored `artifacts/captures/`. Developer stage previews are explicitly labeled; their presence is not a timed playthrough.

## Still unqualified / remaining work

- **Production dialogue quality:** real Pumas/llama.cpp plumbing and native Godot display passed; the small smoke model confused speaker/player identity in one observed reply. Explicit identity context is regression-tested, but production characterization, spoiler resistance and model selection remain open. See `LIVE-QUALIFICATION.md`.
- **Speech:** Cohere Transcribe through Pumas is blocked on the producer transcription API. Direct Whisper was removed; its historical tests do not qualify Cohere. Physical capture, permissions and device behavior also remain unqualified.
- **Human duration assessment:** five hours is a design target. Human playtesters exclusively own duration qualification; no assistant-run timed playthrough is required. Authored-word counts and reading-speed estimates remain in `docs/bible/content_metrics.json`; optional chat is not counted as padding.
- **Relationship mechanics:** no hidden trust score rewards suggestion use; explicit authored relationship/disclosure progression remains a production design task.
- **Release quality:** no exported distribution package, platform matrix, localization, full screen-reader coverage, low-end performance benchmark or complete editorial/playtest pass is claimed.

## Reproduction

Use the official .NET Godot build and SDK, set `GODOT_MONO` to the engine executable, restore/build the project, then run `scripts/verify.sh`. For visual review, run normally. Debug builds support `-- --stage-preview`, F10 to inspect the next authored location and F12 to save the current viewport image. Preview mode is diagnostic and does not automatically save merely for visiting a stage.

## Hosted milestone evidence

The editor-first conversion passed 3,253 core assertions, 11 direct Pumas contract scenarios, a warning-free native build, native story/UI/resource roundtrip smokes and 19 optional adapter tests. Actual Editor save/reopen/play checks are recorded in [EDITOR-VERIFICATION.md](EDITOR-VERIFICATION.md). That editor-first milestone preserved the then-current inference boundaries. The subsequent speech cutover removes Whisper; see SPEECH.md for the current blocked Cohere/Pumas dependency.

Authored-game commit `13d3722b7f854b2760b7d603e45a0eade1896e06` passed every hosted check in [run37074218895](https://github.com/MrScripty/Lanternwake/actions/runs/37074218895). Subsequent qualification changes need their own exact-head run; a prior green check does not qualify a later commit.

## Save isolation

Normal sessions retain `user://save.json` and `user://autosave.json`. Author preview uses a non-persisting policy: save/load controls are disabled, and the storage boundary rejects reads and writes even if called directly. Both stage advance and evidence completion can be exercised without touching player slots. Preview is visibly labeled in the chapter header and is rejected by release builds.

Automated story/UI/live-dialogue checks use distinct temporary save directories owned by each session and removed on disposal. They never point at real player slots. Core sentinel tests cover preview denial, independent test directories, cleanup, and unchanged normal save/load. `--save-isolation-smoke` exercises the actual native advance/activity/manual-save/load paths against disposable player-slot sentinels and requires `LANTERNWAKE_SAVE_ISOLATION_OK`. The sentinels themselves are created only in a test-owned temporary directory, never in an existing player's directory.

## Explicit previous-good recovery

Normal filenames and save schema remain unchanged. `save.previous.json` and `autosave.previous.json` hold the prior compatible primary for each slot. **Compatible** means that the snapshot deserializes and passes `StorySession.Restore` against the currently loaded story, including schema/title, stable IDs, activity gates and transcript provenance. Valid JSON alone is insufficient. Reads use one opened handle and consume at most 16 MiB plus one excess-detection byte; file growth or path replacement cannot bypass the bound. Candidate file time is captured from that same handle, not a later path lookup. Only genuine missing-file/directory exceptions count as absence. A corrupt, unsupported or incompatible primary never rotates over the existing backup; unexpected read/access failures abort the save instead of guessing. A compatible prior primary is copied byte-for-byte, including unknown fields.

The write sequence validates and bounds the new snapshot, inspects the old primary, flushes a new-primary staging file, flushes any compatible previous staging file, publishes previous first, then publishes current. Every staging file is unique and same-directory. An exception before backup publication leaves both old slots unchanged; failure after backup publication but before current publication leaves the old current and its duplicate previous snapshot. After current publication, the new current and prior snapshot are available. The implementation assumes one game writer. It does **not** claim multi-file power-loss atomicity, directory-entry durability, multi-process serialization or automatic cleanup/adoption of staging left by a crashed process. Only each operation's own staging is cleaned on ordinary completion/failure.

The Load chooser lists current and previous manual/autosave candidates with chapter, scene, stable beat and UTC file time. Missing, malformed, unsupported, incompatible and read-failed candidates have explicit reasons and no load action. Choosing **Recover previous …** replaces in-memory progress with the exact inspected snapshot. It does not silently recover, promote a backup or rewrite either slot. Closing changes nothing; later ordinary saves may update slots.

Owned-path regressions inject failures after current staging, previous staging, previous publication and current publication; verify the resulting recoverable states; exercise a real exclusive-read failure and failed backup rename; reject oversized/invalid new snapshots before publication; preserve good backups behind unusable primaries; and ignore abandoned staging. Native UI coverage verifies explicit recovery, candidate identity despite later disk replacement, no slot rewrite, and cancellation. `scripts/verify.sh` requires `LANTERNWAKE_RECOVERY_UI_OK`. Preview cannot inspect or load these candidates; automated tests keep all slots/backups in their owned disposable directories.

## Audio integration recovery, 2026-10-04

Recovered all 136 blobs at PR #7 (`65548f97e1c203884864bf4dcc1d1849db4cbcd1`)
with exact Git blob hashes; recovered tree
`a8bbd2b1de62795a2188c5cf9fceea3e27521881` matches the published receipt.
The prior sampler was preserved; unpublished mixer source was unavailable.
The new mixer and procedural placeholder assets are documented in `AUDIO.md`.

Local official Godot 4.6.3 .NET and .NET SDK 8.0.425:
- C# build: zero warnings/errors.
- Core: 4,110 assertions, 1,424 authored beats; story metrics unchanged.
- Pumas protocol client suite and explicit unavailable speech contract: passed.
- Scene, UI/recovery and save-isolation smoke: passed their success markers.
- Audio: 29 focused assertions; latest actual Master capture 20,480 stereo frames,
  observed peak 0.024877 after cancelling the old timeline effect, Dummy output driver. No physical listening claim.
- Editor: character authoring, Story Text, 320×600 dock, selected-beat launch,
  and 13-scene roundtrip markers passed in a separate complete invocation.
- Python adapter: 19 tests passed.
- Native GUI: controls visible, keyboard mute/volume, close/reopen retention.

An early aggregate invocation was interrupted at the Editor stage; a later
complete aggregate invocation passed. This is local evidence only; no hosted result or
external review for these new changes is claimed. An initial shutdown regression retained two WAV playback handles on immediate
scene-smoke exit (3/10 repeated runs; exact PR7 baseline clean). Fixed by
retaining explicit playback handles and waiting until native references are
released, with a two-second failure deadline. Fifteen subsequent real Godot
exits (five each: scene, audio, save isolation) completed without errors,
ObjectDB leaks or timeout warnings. This covers the tested Godot/platform;
other output backends still require qualification. Model, microphone, five-hour duration and
finished sound design qualification remain open.

## Source-only generated audio setup, 2026-10-04

Generated `Assets/Audio/*.wav` files are intentionally untracked and ignored;
local copies of the prior audio candidate were preserved. The generator,
committed hash/provenance manifest, import settings and native audio scenes
remain source controlled. WAVs elsewhere are not hidden from Git.

The normal setup command is `bash scripts/setup.sh` (or
`python3 scripts/setup_audio.py`; Windows: `py -3 scripts/setup_audio.py`).
Verification invokes setup before Godot and explicitly runs Editor import
before runtime checks; CI has an explicit generation step before Godot setup.
No synthesis is performed in gameplay. Missing Python produces a setup failure
with actionable installation guidance and does not start Godot.

A fresh copy containing only tracked source, with no WAVs and no `.godot`
cache, generated all seven WAVs matching the unchanged committed manifest.
A repeated setup changed no files. Five asset/setup tests cover missing-file
repair, unchanged modification times, refusal to overwrite modified output,
explicit regeneration, failed generation custody and hash rejection.
The complete aggregate then exited 0: 4,110 core assertions, 29 native audio
assertions, native scene/UI/storage checks and 13-scene Editor roundtrip.
Native Master capture observed 20,480 frames with peak 0.024877 on Dummy;
this remains mixer evidence, not physical listening qualification.
