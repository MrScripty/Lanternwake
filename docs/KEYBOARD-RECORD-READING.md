# Keyboard reading of long records and catalogues

Successor branch `feat/keyboard-record-reading-20261005`, based on preserved
`43b8b8a9b1ba2fd1d92db96fd80fb8ece93254b4`, tree
`3834caafe5dcc432a95d6a2368ce961c14cd2c05`. Main, PR12 and prior branches
were inspected before work. Parent owns PR integration and publication.

## Remaining feature map and selected gap

The bible requires keyboard access and a readable backlog. The fixed authored
story, all evidence gates, completion review/replay, saves and optional conversation
are already implemented. Initial living-character pose families, additional object
inserts and detailed guided bell staging remain production art direction. Genuine
model characterization/cancellation requires loaded-model evidence; voice is blocked
by Pumas's unpublished typed transcription contract. Human editorial/duration,
physical input/display/hearing, accessibility tools and platform qualification remain
acceptance work. No relationship score, new puzzle or story branch is inferred.

A concrete keyboard-reading defect was selected: the content note made its prose
scrollbar keyboard-focusable, but ordinary record/catalogue and evidence-review
readers did not. In a normal player with 95 genuinely reached beats, five Tab cycles
with Page Down and Down left the record at its opening paragraph. Original PNG prose
masks matched exactly, while the Close button retained focus. The retained baseline
diagnostic exits through production Quit. It establishes the missing capability;
it is not a passing keyboard-reading acceptance run.

The bounded criterion is to read long reached record/catalogue text using Tab,
arrows, Page Up/Down and Home/End at 100% and 150%, both from ordinary menus and an
unanswered question, then close/return without answering, advancing or changing saves.

## Implementation

All shared modal prose now uses the existing content-note focus behavior. Native
arrows remain on the focused scrollbar; Tab can leave it for the action or Close.
Page Up/Down moves by the visible page and Home/End reaches the beginning/end, only
while that scrollbar owns focus. Values are clamped to the readable extent. A shared
editable gold scrollbar-focus style makes the reading focus visible; other controls
retain their own keys. The content note reuses the shared behavior instead of a
private duplicate. Godot's [scrollbar contract](https://docs.godotengine.org/en/4.6/classes/class_scrollbar.html)
documents native focused-arrow behavior and the `scroll_focus` theme style.

Story content, model/speech/audio integration and save schema are unchanged.
The accepted Continue/Save/History/Talk input repair remains intact.

## Evidence and limits

The external [desktop controller](../integration/qa/keyboard_reading.py) runs the
normal bare project through X11 inputs and observed pixels/files. It starts from
an actual preceding-player save, reaches sufficient further authored evidence via
ordinary reveal/advance and prerequisite answer controls, and explicitly saves an
unanswered question. It does not forge a late checkpoint or inject a game script.
Reading checks then compare every save byte, opening-prose pixels and reader-only
OCR of the exact reached final line/fact. Screenshot crops are derived observations;
raw full-resolution images are retained. All engine execution uses normal time,
Mesa software rendering and Dummy audio, with no loaded model or microphone.

The final accepted fresh process passes 129 checks with 120 injected events and 87
retained captures/crops, exiting through production Settings Quit with code 0.
An earlier fresh process also passes 129 checks; counts are not added as distinct
scenarios. Clean normal preparation passes 112 checks and makes 97 actual authored
transitions from the preceding 95-beat save. Its saved 192-beat prefix has exact
ordered canonical IDs/prose, only the three prerequisite activities solved, and
bytes identical to both fresh-reading seeds. Preparation also quits with code 0.
The selected question is `ch1_s3_evidence`, after the three earlier activities
are solved. End is verified against the exact reached final record line and
the final established fact inside the reader, excluding the main passage.

The first question's ordinary catalogue fits on one page, so expecting it to scroll
was an invalid fixture assumption. A later genuinely reached question supplies the
long catalogue. Initial rejected controller attempts and intermediate native logs
are retained separately from accepted final runs. These include a start-click
that had not visibly settled, input sent too soon after location construction,
a gold-border threshold that missed its antialiased 177/148/103 pixels, and a
full-page title check matching the separate question action "Read the record".
The delivered observer uses settled state, complete reveal before opening a
prerequisite question, direct window-title OCR and measured focus pixels. These
observer corrections are not additional production defects. Automated progression/reveal is
not human reading or a timed playthrough. Synthetic keyboard/pixel evidence does
not qualify physical devices, assistive tools, hearing, real-model/voice behavior,
all platforms or a five-hour median. Managed builds are not standalone exports.
No Library upload, PR action, merge, account change or release publication occurs.

Debug, ExportDebug and ExportRelease managed builds pass with warnings as errors;
retained DLL/PDB compiler documents bind all 48 tracked compiled C# sources. An
aggregate attempt after ExportRelease failed because its Debug `--no-restore`
build reused the export dependency graph without Editor API references. A normal
Debug restore/build repairs the local build cache; no build configuration or
script change is included. That failed log and the earlier UID import warnings
remain distinct from final accepted qualification.

The final `bash scripts/verify.sh` exits 0 with no warning/error lines: 4,125
core assertions, 63 story checks, 11 simulated Pumas cases, unsupported speech,
full native story/UI/save/reading/completion/question/return/content-note/cup
checks and the 13-scene Editor roundtrip. Dummy mixer checks capture 20,480 PCM
frames; they do not qualify hearing. The compact [receipt](evidence/keyboard-record-20261005.json)
and local `/workspace/lanternwake-keyboard-record-20261005.tar.gz` packet bind
actual saves, controllers, captured pixels, logs, source archives and assemblies.
Generated WAVs, templates and private Xauthority stay outside the packet/Git.

Reproduce with the official Godot .NET engine in `GODOT_MONO`, .NET 8 on PATH
and the external desktop prerequisites listed in the
[preceding acceptance report](FOCUSED-CONTROL-QUESTION-ACCEPTANCE.md).
Start an [owned display](../integration/qa/owned_display.py), then run
`integration/qa/keyboard_reading.py` with `--display-state`, fresh `--fixture`
and `--output` directories, and `--seed` pointing to an actual owned save.
The default target is `ch1_s3_evidence`; an earlier genuine seed uses ordinary
keyboard prerequisite answers/reveal/advance before the reading checks. A seed
at that unanswered question tests fresh Load directly. The packet retains both
actual seeds. Stop the owned display afterward. This is an explicit desktop QA
runner, not a new platform dependency for the game or headless verification.
