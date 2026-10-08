# Rendered playability and release qualification

This QA-only milestone is based on production commit
`33aa46899305bcbb4b7a232e0377504b420d1595`, tree
`7fb7d0ce7cd054abefff364c73d5ad771d3c24d9`. It preserves the published
PR7 ancestry and all production story, save, stage, audio and provider source.
The delivery manifest binds the QA commit, binary/PDB checksums, source archive,
native logs and images. No PR, external review request, merge or release is made.

## Completion criteria and current receipts

| Criterion | Evidence and remaining limit |
| --- | --- |
| Complete authored path and evidence gates | Six rendered normal-mode processes visit the exact 1,424 stable beat IDs and 41 scenes. The existing native fixture checks 25,303 assertions, 337 save/resume selections, all 23 wrong-answer retries and recoveries, 93 earlier-state recoveries and the completed-watch resume. All six production Quit exits are clean. Automated callbacks establish deterministic playability, not reader pacing or five-hour duration. |
| Usable rendered native controls | Twelve fresh title/selected-beat processes capture actual rendered pixels. Synthetic X11 mouse/key events open/close title and Settings notes, change the real Master mute and Music volume buses, scroll a small note at 150% text size, submit an editable suggestion to the exact authored fallback, Return to the paused passage, answer final evidence and Finish. No capture action is invoked and no microphone bus is created. All twelve production Quit exits pass. |
| Legible representative composition | Captures cover harbor, four keeper-house cup states, archive, bell before/after lowering, cave and ending. Text, focused native controls and modal return destinations were inspected in the actual images. This is software-rendered inspection; physical display, accessibility-tool and human art acceptance remain open. Broken cup fragments/handle are obscured in the wide composition by foreground actors and the reading panel. The authored evidence remains complete; a bible-directed object insert is an explicit remaining art choice. |
| Reproducible source setup | A pristine archive has no import cache or WAVs. Missing Python exits 1 with actionable guidance and produces no assets. Setup generates seven original deterministic WAVs; a repeat preserves bytes and nanosecond mtimes. Offline restore, Debug, supplemental Release and the actual Godot `ExportRelease` build pass with zero compiler warnings/errors. Official Editor import passes. No pre-existing generated asset is substituted for this fresh-source check. |
| Exported runtime without development tools | **Open.** A real `--export-release` attempt in an isolated source copy exits 1 because both exact `4.6.3.stable.mono` Linux templates are absent. Official release metadata access is blocked by the network proxy (HTTP 403); no alternative route, permission change or guessed storage URL is used. An ephemeral Linux diagnostic preset is evidence, not a supported-platform or distribution commitment. A compiled `ExportRelease` DLL is not an exported executable. |
| Device/provider/editorial acceptance | Physical input/hearing, accessibility tools, low-end hardware, localization, human emotional continuity/duration and approved loaded-model behavior require their own evidence. Speech stays unsupported until Pumas publishes its typed Cohere contract. Owner/coordinator retains platform/distribution decisions and review/integration/release. |

To close the export criterion, obtain the exact official Godot 4.6.3 .NET export
templates through an already approved environment or a supplied Library artifact,
verify identity/provenance, and export a source-bound local diagnostic build.
Check that the PCK contains `Content/story.json`, all referenced scenes/materials
and generated WAVs. Run the resulting binary with isolated userdata and offline
fallback, verify title/Arrival, save/restart/recovery, ending/replay and clean
shutdown without relying on an Editor, SDK, source checkout or Python. Confirm
author-preview entry is rejected by that actual release binary. Until these checks
pass, no exported-runtime or platform-support receipt exists.

## Reproducing the rendered checks

Use Python 3, official Godot .NET 4.6.3, .NET SDK 8.0.425, and already installed
Xorg/dummy, xauth, xdpyinfo, libX11, libXtst and Mesa OpenGL. These tools do not
install packages, modify system displays or enable network listeners. Generate
audio, restore/build Debug and import before running the fixtures. Set
`GODOT_MONO` to the official engine executable and use a **new** private state
directory on each display startup:

```bash
python3 integration/qa/owned_display.py start --state-dir /absolute/owned-qa-display --number 173
python3 integration/qa/graphical_long_session.py --display-state /absolute/owned-qa-display/display.json
python3 integration/qa/graphical_inspection.py --display-state /absolute/owned-qa-display/display.json
python3 integration/qa/owned_display.py stop --state-dir /absolute/owned-qa-display
```

Do not run another client/input fixture concurrently on that display. Runners use
private temporary userdata, an empty model setting, Dummy audio, software OpenGL,
30 FPS and disabled VSync; they reject errors and unexpected warnings. The dummy
driver's single unsupported-VSync warning per process is retained as an
environment limitation. Reports and PNGs are under `artifacts/graphical-*`.
Never archive or share the `Xauthority` credential. Stop verifies exact owned
Xorg arguments before signalling; no shared/operator process is terminated.

The first display attempt passed chapters 1–2 but emitted `BadShmSeg` errors at
chapter 3 and was rejected. Separate sandbox IPC namespaces are the working
explanation; a namespace-identity read was denied and not retried. Disabling the
optional MIT-SHM extension only on this owned display allowed the fresh full
run to pass; rejected logs are retained separately. This is a fixture environment
adapter, not a player-runtime change. Early probe failures also corrected guessed
node paths, embedded-window coordinate mapping and a mistaken disabled-voice
assertion; none required product edits. Production Quit dispatch intentionally
leaves no suspended frame await during native teardown.

The complete previous core/audio/Editor aggregate belongs to the exact frozen
cup-state receipt. This QA-only milestone adds rendered checks and fresh-source
setup; it does not relabel old runs as new full-suite results or claim physical
audio output from the Dummy mixer. Procedural asset provenance is unchanged.
