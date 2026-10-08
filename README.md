# Lanternwake

An original stormbound-island mystery built in **Godot 4.6.3 .NET / C#**. A fixed authored five-chapter story unfolds through editable native 3D scenes and a visual-novel interface. Optional local character conversations run through **Pumas Library's real inference-enabled headless gateway**, restricted to llama.cpp provider capability, with a visibly authored fallback.

## Development status

This is an in-development game, not a shipped five-hour product. Five hours is the main-story target, excluding unlimited optional chat. See `docs/bible` for actual authored content, pacing estimates, visual language and remaining playtest requirements. Build success, smoke success, real Pumas inference and real microphone qualification are separate checks; consult the [playable coverage inventory](docs/PLAYABLE-COVERAGE-INVENTORY.md) for accepted non-audio evidence and remaining gates, and [verification record](docs/VERIFICATION.md) for earlier qualification details.

Implemented source: ordered authored story, five editable 3D sets, reusable stylized adult character scenes, dialogue reveal/advance, mandatory evidence questions, unlocked evidence catalogue, history, manual/autosave/load, keyboard controls, editable suggestions and free text, bounded/cancellable local Pumas calls, microphone/review-before-send UI retained for the requested Pumas-owned Cohere Transcribe integration. Voice capture remains unavailable until the installed Pumas audio runtime is independently qualified.

Earlier real local Pumas/llama.cpp dialogue smoke tests passed; see `docs/LIVE-QUALIFICATION.md` for their exact source/artifacts and the observed tiny-model identity failure. Those historical runs do not qualify the current selected-owner producer deployment. The generic audio consumer and bounded capture path are implemented with synthetic qualification; installed Cohere transcription and physical microphone behavior remain unqualified, and shipping microphone admission stays closed. Direct whisper.cpp support has been removed. Physical microphone input remains unqualified. See [current player-flow coverage and remaining inputs](docs/CURRENT-FEATURE-COVERAGE-20261008.md) for the reconciled status.

Installed model interoperability, real-device speech, accessibility, performance and target-platform exports require separate acceptance. Representative readers qualify duration and comprehension.

Model setup now requires an explicitly selected existing Pumas library and authenticated owner. See [existing-library reuse](docs/PUMAS-OWNER-REUSE.md) for UI/launcher selection, local-first checks and remaining bootstrap contract gaps. The game never starts Pumas automatically.

## Run

Install Python 3, the official Godot **.NET** 4.6.3 build and .NET 8 SDK. The ordinary non-.NET Godot build cannot compile this project.

From the checkout, run one command. On Linux/macOS:

```sh
python3 scripts/run.py --godot "/path/to/the/Godot.NET/executable"
```

On Windows (PowerShell or Command Prompt, no Bash required):

```powershell
py -3 scripts/run.py --godot "C:\Tools\Godot\Godot_v4.6.3-stable_mono_win64_console.exe"
```

Use your installed executable's path; on Windows choose the `_console.exe` variant so preparation errors are visible. You can omit `--godot` when `GODOT_MONO` names it or a Godot .NET executable is on PATH. If dotnet is outside PATH, pass `--dotnet` its executable. If `python3`/`py` is missing, install Python 3 from python.org first.

The launcher checks installed prerequisites, verifies bundled MIDI music and generates/verifies location ambience and effect WAVs, restores C# packages from the installed Godot distribution, builds Debug, imports assets and runs the game directly from source. It uses only Python's standard library, installs no prerequisites, and downloads no software, packages, models or export templates. Its local package cache, generated WAVs, imports and builds are ignored by Git. A second run keeps matching WAV bytes and modification times; a modified generated WAV is refused rather than overwritten. Keep custom audio outside `Assets/Audio`. Preparation failures stop before launch and show the next action.

To edit, add `--editor` to the same launcher command. This opens Godot with the selected dotnet path, project-local package cache and offline feed retained for editor builds. `--setup-only` prepares assets/builds and exits; afterward rerun the launcher with `--editor` to edit or without `--setup-only` to play. Opening `project.godot` independently requires configuring your own dotnet and NuGet environment; preparation does not change your shell or global settings.

Restore, build and import each have a five-minute limit; slow machines can increase it with `--setup-timeout SECONDS`. Expiry or Ctrl+C stops the owned preparation process tree. This setting does not time audio generation, game/editor sessions or model requests.

Pass optional Godot arguments after `--`; for example, `python3 scripts/run.py -- --headless -- --audio-smoke` runs the native audio check after setup. `bash scripts/setup.sh` (Windows: `py -3 scripts/setup_audio.py` and `py -3 scripts/setup_music.py`) remains available when you only need to generate audio. No executable export is needed to play from source. The shared launcher has been exercised on Linux; Windows/macOS execution remains unqualified.

The startup menu offers story, load/resume, reading and sound settings, and independent AI provider configuration. Music plays from bundled MIDI through MeltySynth and an acoustic instrument bank; no rendered music WAVs are needed.

For this checkout with Godot in `.toolchain/`, `bash run-game.sh`, `bash run-game.sh --editor`, and `bash run-game.sh --verify` remain convenient local shortcuts. This helper keeps saves under `.toolchain/user-data/`; preserve that folder to retain local progress.

No LLM is required for the authored main story. Local Pumas setup is in `docs/PUMAS.md`; local speech setup is in `docs/SPEECH.md`.

`Lanternwake.sln` tracks the Godot C# build configurations `Debug`, `ExportDebug` and `ExportRelease`. Executable export also requires the matching official .NET player templates; current partial export evidence and limits are recorded in [export qualification](docs/EXPORT-SOLUTION.md).

To adjust the game, see [the editor authoring guide](docs/AUTHORING.md). Locations, characters, camera, lighting and interface styling live in normal Godot scenes/resources. The Story Text dock edits the canonical story JSON without changing C#.

## Verify

```
dotnet run --project tests/Lanternwake.Tests.csproj -- Content/story.json
dotnet build Lanternwake.csproj
godot-mono --headless --path . -- --smoke
godot-mono --headless --path . -- --ui-smoke
```

Set GODOT_MONO to the official .NET executable and use scripts/verify.sh for the complete local suite. Core tests are a package-free console project. They test actual production core files. Runtime smoke traverses authored beats, solves evidence activities at their canonical answer, reopens a save and constructs every stage. Neither test substitutes for playing the game or actual model/microphone qualification.

## Controls

- Click or Space/Enter: reveal/advance
- Content note: available before Arrival and through Settings during play. Uses the existing bible's note; reading it does not start or advance the story. See [content-note qualification](docs/PLAYER-CONTENT-NOTE.md).
- Keeper-house cup states follow the existing canonical break, boxing and steel-mug beats, including save/load and replay. See [cup-stage qualification and visual limits](docs/CANONICAL-CUP-STATES.md).
- Stay and talk: optional conversation panel
- The Missing Margin: **Speak with Tomas** retains one of three authored approaches to Ivo's omission note. Escape leaves it unspoken; Continue rejoins the original story. See [the local exchange](docs/MISSING-MARGIN-EXCHANGE.md).
- Suggestions populate editable input; Say this submits
- Return / Escape from optional conversation resumes the paused authored passage; replies remain in the record. Cancelled or retired conversation controls cannot submit hidden drafts. See [conversation return](docs/CONVERSATION-STORY-RETURN.md).
- H: History opens at current/recent context while retaining full scrollback; E: catalogue; Escape: close panel. See [History opening](docs/HISTORY-RESUME-CONTEXT.md).
- Long record/catalogue text: Tab to the gold-outlined scrollbar, Up/Down or Page Up/Down to read, Home/End for the beginning/end; Tab reaches Close or Back to question. See [keyboard reading](docs/KEYBOARD-RECORD-READING.md).
- Manual Save/Load; separate automatic checkpoint; explicit previous-good recovery choices
- Finish: review the record/evidence, explicitly save the completed watch, or confirm starting a new watch. Starting again preserves manual saves and session reading/sound preferences; automatic checkpoints update as the new watch progresses.
- Evidence questions: review known evidence or read the record, then return to the same unanswered question. Review, return and cancellation preserve the current question and save files. See [question review](docs/QUESTION-EVIDENCE-REVIEW.md) for qualification and limits.
- Chapter 2 route question: explore the labelled route model, compare supported approaches and read their source passages before answering. The optional model stops at documented decision points and preserves the pending question. See [playable route comparison](docs/PLAYABLE-ROUTE-MODEL.md).
- Reading settings: smaller/larger reading text at 100%, 125% and 150%, reset to 100%, instant text and reduced motion. Reading text size lasts for the current game session; a fresh game starts at 100%. See [reading text size](docs/READING-TEXT-SIZE.md) for the scaled reading/choice surfaces and unchanged navigation/HUD sizes.
- Sound settings: independent channel levels, master mute and optional [reduced loud/quiet differences](docs/REDUCED-DYNAMIC-RANGE.md), off in a fresh process.
- Original MIDI score with environment and character themes, a developing five-chapter journey, live MIDI acoustic layers, speech EQ/ducking, editable location loops and bell-release cue; see [audio authoring](docs/AUDIO.md)

Artwork, story text and MIDI compositions are original project work. The bundled synth and instrument bank retain their separate licences; see [audio provenance](docs/AUDIO.md#provenance-and-verification). Source ownership and technical contracts are documented in `docs/ARCHITECTURE.md`.
