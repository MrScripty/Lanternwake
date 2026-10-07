# Reached History navigation

Verified source `fa65570eeebf18df3ed285e9ffe6103896f8d391` on the new local branch
`feat/history-navigation-f5d50d5`, based on fetched main
`f5d50d5ed16f02978faa1cf521c7fcd888f369df` (PR23). Repo-local author identity is
MrScripty <TheEnvironmentGuy@protonmail.com>. No public write or export was made.
The available workspace `.agents` directory was empty; no repository AGENTS.md
or SKILL.md was found. Existing authoring, UI, architecture and QA guidance was read.

## Gap and behavior

Pinned main's `ShowHistory` displayed one `HistoryText()` block through
`ShowWindow`, beginning at the top. There was no search, latest-entry action or
chapter selector. This feature adds all three to ordinary History, question
review and ending review, retaining their close/return destinations.

Search is a trimmed, literal, case-insensitive substring search over actual
recorded transcript text and displayed speaker names. It includes recorded
selected exchanges and optional conversations; generated dialogue retains its
optional label. Authored future prose, unused options, activity answers and
character dossiers are not search inputs. No results has an explicit message
and reached-entry count.

Latest clears the filter and scrolls to the latest recorded entry. Chapter
choices use canonical chapter titles mapped from recorded BeatIds, and jump to
the first recorded entry of the selected chapter. Only represented chapters
appear. Missing association never invents a chapter. Opening creates a new
transcript projection, so an earlier Load removes later entries and chapter
links. Empty history disables navigation and focuses Search.

Controls are editable scene nodes in `Scenes/UI/ModalWindow.tscn`, hidden in
other readers. Reading size also scales the native chapter popup rows. Tab and
Shift-Tab reach search, latest, chapters, prose and Close; controller Down reaches
chapters/prose and Right leaves the reading scrollbar for Close. The existing
arrow/Page Up/Page Down/Home/End reader behavior is retained. Jump continuations
check window ownership, render revision and shutdown before scrolling or moving
focus.

Story JSON, StorySession, save/load implementation, save schema, choices, evidence
and inventory rules remain unchanged. Reading controls do not advance or save.

## New feature evidence

The [receipt](evidence/history-navigation-20261007.json) records hashes and paths.
`HistoryNavigationTests.cs` is new coverage for this feature: reached/unchosen/
future boundaries, recorded optional and selected text, Unicode/literal search,
authoritative chapter offsets, sparse metadata, earlier restore, serialized fresh
session and state/unlock preservation. The complete core suite passes 4,552
assertions over 1,439 authored beats and five chapters.

`HistoryNavigationQualification.cs` and `integration/qa/history_navigation.py`
drive the actual native controls with private slots. Preparation genuinely
advances through prerequisite questions, saves an earlier manual slot at the
start of chapter 2, then reaches chapter 3 for a later autosave. Both the first
process and a fresh process load the later slot, restore the earlier slot and
exercise search/latest/chapter boundaries, repeated reopen, no results, competing
search/retired jumps, shutdown invalidation, empty history, actual keyboard and
joypad input events, returned focus, 640x480 layout and reading size at 100%/150%.
Exact snapshots, facts, inventory and every save byte are checked. The final
strict aggregate feature run passes **112 preparation / 111 fresh-process checks**
without warnings or errors. Evidence is under
`artifacts/history-navigation/1791375193185376567`.

The separate normal native player run uses external X11 input and observed
pixels/files, with both genuine prepared slots. It passes **48 checks**, with
296 recorded input edges/events and 41 retained full captures/crops, at 100%/150%
with two repetitions each. Latest visibly shows the earlier restored entry;
typed search, no-results, first-chapter jump and the popup's exclusion of later
chapters are observed in the record itself. It exits through normal Settings
Quit with code 0. Every slot byte and all tracked source hashes remain exact.
All new feature source files and the native DLL match the source manifest.
Evidence: `/workspace/history-navigation-input-evidence12/receipt.json`.

The complete `bash scripts/verify.sh` exits 0 in
`/workspace/history-navigation-aggregate-candidate.log`, including the new strict
History checks, existing 24-question review, completion, conversation, save,
inventory/audio/staging regressions and 13-scene native Editor roundtrip.
The compiled production/fixture source is unchanged by subsequent desktop-observer
commits. Independent review separately inspected source, native receipts and
graphical captures and independently reran the core suite. Its local report is
`/workspace/history-navigation-independent-review.txt`.

## Preservation, reproduction and limits

Older tracked evidence and saved-environment evidence remain intact. Earlier
attempt directories/logs are retained separately, including stale audio/import
setup, corrected test-oracle assumptions, OCR segmentation and frame-settlement
observations. Intermittent WAV/playback warnings came from abrupt qualification
exit; the fixture now uses the player's existing drained audio shutdown. The
strict runner rejects warnings. Intermediate runs are not counted as additional
accepted scenarios. Retained guided-descent tests are regression coverage and
do not implement or establish acceptance of this feature.

Use the installed official Godot 4.6.3 .NET engine and .NET 8 environment:

```sh
source /workspace/.lanternwake-tools/activate.sh
bash scripts/verify.sh
```

The graphical observer is `integration/qa/history_navigation_input.py`; its CLI
accepts an owned display state, new private fixture/output directories, and the
actual earlier/later saves from native preparation. Set `OMP_THREAD_LIMIT=1`
and `LP_NUM_THREADS=2` for the retained software-rendered run. It polls bounded
reader/title/popup observations rather than reactivating controls while a native
frame is still settling. No game time scale is accelerated.

Source archive and the source/DLL manifest are retained locally; the final
evidence packet is `/workspace/lanternwake-history-navigation-20261007.tar.gz`.
No generated audio, display authorization cookie, credentials or model download
is included. Parent review owns any later publication.

Synthetic X11/joypad events do not qualify physical keyboard/controller hardware.
Software rendering and test geometry do not qualify performance, every platform
or assistive tools. Real model/voice/microphone behavior and human editorial,
accessibility and duration acceptance remain separate; no release promise is
inferred from these results.
