# Lanternwake

An original stormbound-island mystery built in **Godot 4.6.3 .NET / C#**. A fixed authored five-chapter story unfolds through editable native 3D scenes and a visual-novel interface. Optional local character conversations run through **Pumas Library's real inference-enabled headless gateway**, restricted to llama.cpp provider capability, with a visibly authored fallback.

## Development status

This is an in-development game, not a shipped five-hour product. Five hours is the main-story target, excluding unlimited optional chat. See `docs/bible` for actual authored content, pacing estimates, visual language and remaining playtest requirements. Build success, smoke success, real Pumas inference and real microphone qualification are separate checks; consult `docs/VERIFICATION.md` for the latest evidence and blockers.

Implemented source: ordered authored story, five editable 3D sets, reusable stylized adult character scenes, dialogue reveal/advance, mandatory evidence questions, unlocked evidence catalogue, history, manual/autosave/load, keyboard controls, editable suggestions and free text, bounded/cancellable local Pumas calls, microphone/review-before-send UI retained for the requested Pumas-owned Cohere Transcribe integration. Voice capture is temporarily unavailable until Pumas exposes that contract.

Real local Pumas/llama.cpp dialogue smoke tests passed; see `docs/LIVE-QUALIFICATION.md` for exact artifacts and the observed tiny-model identity failure. Cohere transcription is not yet implemented or qualified. Direct whisper.cpp support has been removed. Physical microphone input remains unqualified.

Not yet a release promise: production-qualified model responses, real-device speech, relationship/disclosure progression, full accessibility/performance/platform qualification. Human playtesters own five-hour duration qualification.

## Run

Install the official Godot **.NET** 4.6.3 build and .NET 8 SDK. The ordinary non-.NET Godot build cannot compile this project.

```
dotnet build Lanternwake.csproj
godot-mono --path . --editor
```

Open `project.godot` with the .NET editor, then run. No LLM is required for the authored main story. Local Pumas setup is in `docs/PUMAS.md`; local speech setup is in `docs/SPEECH.md`.

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
- Stay and talk: optional conversation panel
- Suggestions populate editable input; Say this submits
- H: history; E: catalogue; Escape: close panel
- Manual Save/Load; separate automatic checkpoint
- Settings: instant text and reduced motion

All assets currently come from original code and authored text. Source ownership and technical contracts are documented in `docs/ARCHITECTURE.md`.
