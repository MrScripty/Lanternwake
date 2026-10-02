# Verification record

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
- **Physical microphone:** actual whisper.cpp file transcription, 48 kHz conversion, cancellation/reaping and cleanup passed using the production adapter. The cloud GUI has no ALSA input card, so physical capture, permissions and device behavior remain unqualified.
- **Duration:** five hours is a target. Use the authored-word counts and reading-speed estimates in `docs/bible/content_metrics.json`; no timed human full playthrough was completed, and optional chat is not counted as padding.
- **Relationship mechanics:** no hidden trust score rewards suggestion use; explicit authored relationship/disclosure progression remains a production design task.
- **Release quality:** no exported distribution package, platform matrix, localization, full screen-reader coverage, low-end performance benchmark or complete editorial/playtest pass is claimed.

## Reproduction

Use the official .NET Godot build and SDK, set `GODOT_MONO` to the engine executable, restore/build the project, then run `scripts/verify.sh`. For visual review, run normally. Debug builds support `-- --stage-preview`, F10 to inspect the next authored location and F12 to save the current viewport image. Preview mode is diagnostic and does not automatically save merely for visiting a stage.

## Hosted milestone evidence

Authored-game commit `13d3722b7f854b2760b7d603e45a0eade1896e06` passed every hosted check in [run37074218895](https://github.com/MrScripty/Lanternwake/actions/runs/37074218895). Subsequent qualification changes need their own exact-head run; a prior green check does not qualify a later commit.
