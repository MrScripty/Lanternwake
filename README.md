# Lanternwake

An original stormbound-island mystery built in **Godot 4.6.3 .NET / C#**. A fixed authored five-chapter story unfolds through editable native 3D scenes and a visual-novel interface. Optional local character conversations run through **Pumas Library's real inference-enabled headless gateway**, restricted to llama.cpp provider capability, with a visibly authored fallback.

## Development status

This is an in-development game, not a shipped five-hour product. Five hours is the main-story target, excluding unlimited optional chat. See `docs/bible` for actual authored content, pacing estimates, visual language and remaining playtest requirements. Build success, smoke success, real Pumas inference and real microphone qualification are separate checks; consult the [playable coverage inventory](docs/PLAYABLE-COVERAGE-INVENTORY.md) for accepted non-audio evidence and remaining gates, and [verification record](docs/VERIFICATION.md) for earlier qualification details.

Implemented source: ordered authored story, five editable 3D sets, reusable stylized adult character scenes, dialogue reveal/advance, mandatory evidence questions, unlocked evidence catalogue, history, manual/autosave/load, keyboard controls, editable suggestions and free text, bounded/cancellable local Pumas calls, microphone/review-before-send UI retained for the requested Pumas-owned Cohere Transcribe integration. Voice capture is temporarily unavailable until Pumas exposes that contract.

Real local Pumas/llama.cpp dialogue smoke tests passed; see `docs/LIVE-QUALIFICATION.md` for exact artifacts and the observed tiny-model identity failure. Cohere transcription is not yet implemented or qualified. Direct whisper.cpp support has been removed. Physical microphone input remains unqualified.

Not yet a release promise: production-qualified model responses, real-device speech, relationship/disclosure progression, full accessibility/performance/platform qualification. Human playtesters own five-hour duration qualification.

## Run

Install Python 3, the official Godot **.NET** 4.6.3 build and .NET 8 SDK. The ordinary non-.NET Godot build cannot compile this project.

For a local checkout with Godot .NET installed in `.toolchain/`, the launcher uses the installed .NET SDK:

```sh
bash run-game.sh           # Build and play
bash run-game.sh --editor  # Build and open the Godot editor
bash run-game.sh --verify  # Run the repository verification suite
```

The launcher keeps build caches and game saves under `.toolchain/`; preserve `.toolchain/user-data/` to retain progress. The authored story works without an AI provider. Optional model conversations require the setup below; transcription and character voice playback remain unavailable until their providers are integrated.

```
bash scripts/setup.sh  # Verify bundled MIDI music and generate location ambience/effects before import.
dotnet build Lanternwake.csproj
godot-mono --path . --editor
```

Run setup before opening `project.godot` with the .NET editor, then run. On Windows without Bash, run both `py -3 scripts/setup_audio.py` and `py -3 scripts/setup_music.py`; on other systems use `python3` for the equivalent commands. Setup uses Python's standard library, performs no downloads, and safely skips verified audio. Music plays directly from bundled MIDI through the MIT synth and acoustic instrument bank; it needs no rendered music WAVs. The .NET 8 game build packages the synth dependency. Generated ambience/effect WAVs are intentionally not tracked in Git. No LLM is required for the authored main story. Local Pumas setup is in `docs/PUMAS.md`; local speech setup is in `docs/SPEECH.md`.

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
- Manual Save/Load; separate automatic checkpoint; explicit previous-good recovery choices
- Reading settings: smaller/larger reading text at 100%, 125% and 150%, reset to 100%, instant text and reduced motion. Reading text size lasts for the current game session; a fresh game starts at 100%. See [reading text size](docs/READING-TEXT-SIZE.md) for the scaled reading/choice surfaces and unchanged navigation/HUD sizes.
- Sound settings: separate music/ambience/effects levels and master mute
- Original MIDI score with environment and character themes, a developing five-chapter journey, live MIDI acoustic layers, speech EQ/ducking, editable location loops and bell-release cue; see [audio authoring](docs/AUDIO.md)

All assets currently come from original code and authored text. Source ownership and technical contracts are documented in `docs/ARCHITECTURE.md`.
