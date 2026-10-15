# Run Lanternwake from source

[Back to the README](../README.md)

## Requirements

Use Python 3, the complete official Godot .NET 4.6.3 distribution and the .NET 8 SDK. A .NET runtime alone is insufficient. Godot's ordinary non-.NET build cannot compile this C# project.

The launcher checks the exact Godot version and mono build marker. It locates the GodotSharp package feed beside the executable, or inside the macOS app bundle, and refuses an incomplete distribution. It does not install prerequisites.

From the repository directory:

```sh
python3 scripts/run.py --godot "/path/to/Godot.NET/executable"
```

Windows:

```powershell
py -3 scripts/run.py --godot "C:\Tools\Godot\Godot_v4.6.3-stable_mono_win64_console.exe"
```

Use the Windows console executable so preparation errors are visible. `GODOT_MONO` or a suitable executable on PATH can replace `--godot`. Pass `--dotnet` an executable when the SDK is outside PATH.

## What preparation does

The launcher verifies bundled MIDI music, generates/verifies the ambience and effect WAVs, restores C# packages from the installed Godot package feed, builds Debug and imports assets. It uses Python's standard library and downloads no software, packages, model weights or export templates.

The project-local package cache, generated WAVs, imports and builds are ignored by Git. Matching generated WAVs are retained byte-for-byte with unchanged modification times. A modified generated WAV is refused instead of silently overwritten; keep custom audio outside `Assets/Audio`.

Preparation fails before launch when a prerequisite or step fails. Each restore/build/import step has a five-minute limit. Use `--setup-timeout SECONDS` on slow machines. Expiry or Ctrl+C stops the owned preparation process tree. The limit does not apply to audio generation, game/editor sessions or model requests.

## Editor and setup-only modes

Append `--editor` to prepare the checkout and open the .NET editor with the chosen SDK and local/offline package configuration retained.

Append `--setup-only` to prepare and exit. Rerun the launcher with `--editor` to edit or without `--setup-only` to play. These settings apply to launched processes, not your shell or global configuration. Opening `project.godot` independently requires your own working dotnet/NuGet setup.

See [authoring](AUTHORING.md) for locations, character layouts, UI and the Story Text dock.

Optional Godot arguments go after `--`. For example:

```sh
python3 scripts/run.py -- --headless -- --audio-smoke
```

Run `python3 scripts/run.py --help` for all options. Bash setup helpers remain available for audio-only preparation: `bash scripts/setup.sh`; on Windows, use `py -3 scripts/setup_audio.py` and `py -3 scripts/setup_music.py`.

## Optional Pumas selection

Model setup can use `--pumas-library ABSOLUTE_FOLDER --pumas-observer ABSOLUTE_EXECUTABLE`. Both must point to existing paths. These select a borrowed Pumas owner; the launcher never starts Pumas. The same selection is available in AI setup. Read [owner reuse](PUMAS-OWNER-REUSE.md) before configuring it.

## Local helper, checks and exports

When this checkout has Godot in `.toolchain/`, `bash run-game.sh`, `bash run-game.sh --editor` and `bash run-game.sh --verify` are local conveniences. This helper keeps saves in `.toolchain/user-data/`; preserve that folder to retain its progress.

The main launcher has been exercised on Linux. Windows/macOS execution remains unqualified. Playing from source needs no export templates. Exporting requires the matching official .NET player templates and platform runtime prerequisites; see [export qualification](EXPORT-SOLUTION.md).

For the wider local suite, set `GODOT_MONO` and run `bash scripts/verify.sh`. Tests and smoke checks do not establish real model quality, physical microphone behavior or human play duration.

## Optional AI and speech

The authored main story needs no model. Optional text dialogue can use Pumas-managed llama.cpp or OpenRouter. Local setup needs an explicitly selected existing library and authenticated owner; Lanternwake never starts Pumas automatically. See [provider setup](PUMAS.md) and [owner selection](PUMAS-OWNER-REUSE.md).

OpenRouter sends dialogue and bounded story context to its service and upstream provider and may incur charges. Saved keys use the Linux desktop Secret Service keyring; other platforms and unavailable keyrings have no plaintext persistence fallback.

Microphone transcription is an off-by-default experimental local Pumas/Cohere CPU path. It needs Linux x86_64, Landlock ABI 6+, a prepared runtime/model and separate local testing. Real-model accuracy and physical microphone behavior remain unverified; character-voice providers are not implemented. See [speech setup](SPEECH.md). Historical inference smoke tests, current producer compatibility, controlled fixtures and real-device testing establish different things; see the linked provider/test records for their scope.

## Player reference

Manual saves, a separate automatic checkpoint and explicit previous-good recovery choices let you resume. At the finish, review the record/evidence, explicitly save the completed watch or confirm a new watch. Starting again preserves manual saves and session reading/sound preferences; automatic checkpoints update as the new watch progresses.

- [Keyboard reading](KEYBOARD-RECORD-READING.md), [History context](HISTORY-RESUME-CONTEXT.md) and [question evidence review](QUESTION-EVIDENCE-REVIEW.md): read long records and return to the pending question.
- [Route comparison](PLAYABLE-ROUTE-MODEL.md), [The Missing Margin exchange](MISSING-MARGIN-EXCHANGE.md) and [canonical cup states](CANONICAL-CUP-STATES.md): authored activities and their boundaries.
- [Conversation return](CONVERSATION-STORY-RETURN.md) and [watch completion](WATCH-COMPLETION.md): return, finish, saves and recovery.
- [Reading text size](READING-TEXT-SIZE.md) and [reduced dynamic range](REDUCED-DYNAMIC-RANGE.md): session settings and fresh-process defaults.
- [Content note](PLAYER-CONTENT-NOTE.md): available before starting and through Settings without advancing the story.
- [Playable coverage](PLAYABLE-COVERAGE-INVENTORY.md) and [dated feature coverage](CURRENT-FEATURE-COVERAGE-20261008.md): what existing checks establish, with their limits.
