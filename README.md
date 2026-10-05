# Lanternwake

An original stormbound-island mystery built in **Godot 4.6.3 .NET / C#**. A fixed authored five-chapter story unfolds through editable native 3D scenes and a visual-novel interface. Optional local character conversations run through **Pumas Library's real inference-enabled headless gateway**, restricted to llama.cpp provider capability, with a visibly authored fallback.

## Development status

This is an in-development game, not a shipped five-hour product. Five hours is the main-story target, excluding unlimited optional chat. See `docs/bible` for actual authored content, pacing estimates, visual language and remaining playtest requirements. Build success, smoke success, real Pumas inference and real microphone qualification are separate checks; consult the [playable coverage inventory](docs/PLAYABLE-COVERAGE-INVENTORY.md) for accepted non-audio evidence and remaining gates, and [verification record](docs/VERIFICATION.md) for earlier qualification details.

Implemented source: ordered authored story, five editable 3D sets, reusable stylized adult character scenes, dialogue reveal/advance, mandatory evidence questions, unlocked evidence catalogue, history, manual/autosave/load, keyboard controls, editable suggestions and free text, bounded/cancellable local Pumas calls, microphone/review-before-send UI retained for the requested Pumas-owned Cohere Transcribe integration. Voice capture is temporarily unavailable until Pumas exposes that contract.

Real local Pumas/llama.cpp dialogue smoke tests passed; see `docs/LIVE-QUALIFICATION.md` for exact artifacts and the observed tiny-model identity failure. Cohere transcription is not yet implemented or qualified. Direct whisper.cpp support has been removed. Physical microphone input remains unqualified.

Not yet a release promise: production-qualified model responses, real-device speech, relationship/disclosure progression, full accessibility/performance/platform qualification. Human playtesters own five-hour duration qualification.

## Run

Install Python 3, the official Godot **.NET** 4.6.3 build and .NET 8 SDK. The ordinary non-.NET Godot build cannot compile this project.

```
bash scripts/setup.sh  # Generate the seven original WAVs before Godot imports them.
dotnet build Lanternwake.csproj
godot-mono --path . --editor
```

Run setup before opening `project.godot` with the .NET editor, then run. On Windows without Bash, use `py -3 scripts/setup_audio.py`; on other systems, `python3 scripts/setup_audio.py` is equivalent. Setup requires only the Python standard library, performs no downloads, and safely skips already verified audio. Missing Python stops setup with installation guidance; install it before proceeding. Generated WAVs are intentionally not tracked in Git. No LLM is required for the authored main story. Local Pumas setup is in `docs/PUMAS.md`; local speech setup is in `docs/SPEECH.md`.

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
- Suggestions populate editable input; Say this submits
- Return / Escape from optional conversation resumes the paused authored passage; replies remain in the record. Cancelled or retired conversation controls cannot submit hidden drafts. See [conversation return](docs/CONVERSATION-STORY-RETURN.md).
- H: history; E: catalogue; Escape: close panel
- Long record/catalogue text: Tab to the gold-outlined scrollbar, Up/Down or Page Up/Down to read, Home/End for the beginning/end; Tab reaches Close or Back to question. See [keyboard reading](docs/KEYBOARD-RECORD-READING.md).
- Manual Save/Load; separate automatic checkpoint; explicit previous-good recovery choices
- Finish: review the record/evidence, explicitly save the completed watch, or confirm starting a new watch. Starting again preserves manual saves and session reading/sound preferences; automatic checkpoints update as the new watch progresses.
- Evidence questions: review known evidence or read the record, then return to the same unanswered question. Review, return and cancellation preserve the current question and save files. See [question review](docs/QUESTION-EVIDENCE-REVIEW.md) for qualification and limits.
- Reading settings: smaller/larger reading text at 100%, 125% and 150%, reset to 100%, instant text and reduced motion. Reading text size lasts for the current game session; a fresh game starts at 100%. See [reading text size](docs/READING-TEXT-SIZE.md) for the scaled reading/choice surfaces and unchanged navigation/HUD sizes.
- Sound settings: separate music/ambience/effects levels and master mute
- Editable location audio loops and authored bell-release cue; see [audio authoring](docs/AUDIO.md)

All assets currently come from original code and authored text. Source ownership and technical contracts are documented in `docs/ARCHITECTURE.md`.
