# Finished watch and replay

The production Finish control now opens an ending menu with the completed record,
established evidence, explicit manual save, confirmed replay and normal Quit.
Record/catalogue views can return to that menu. Closing it retains the last beat.

Starting another watch requires a separate confirmation, initially focused on
Keep this watch. It resets in-memory transcript, evidence gates and stage cues,
then renders the first authored beat. It preserves the process's reading and
sound preferences. Starting again writes no save files; subsequent progression
updates ordinary autosaves. The manual slot changes only through explicit Save.
The confirmation explains this distinction and recommends saving the finished
watch before replacing the in-memory record. Save failures remain visible.

Completion/confirmation callbacks belong to their originating window and session.
Retired callbacks cannot save or reset a replacement watch. Restart uses the
existing load-style rendering discontinuity to suppress historical audio effects.
Author preview offers no finished-save or player-replay action. The fixed story,
schema-1 saves, procedural assets, generated-audio setup and Pumas/speech boundaries
are unchanged.

## Verification

Base: exact current draft PR7
`9209b3f0366dfa14795e81e774915f231f238255`, tree
`c724af1dbb7388762a7570a90cb1bb327a60ba21`. Remote main remains the initial commit;
this feature is a separate proposal for parent-coordinated integration.

Official Godot 4.6.3 .NET / .NET SDK 8.0.425, Linux headless, Dummy audio driver.
`python3 integration/qa/watch_completion.py` uses owned normal-mode userdata:

- Complete: 1,424 authored beats through actual Continue/evidence/Finish controls,
  1,458 checks, finished manual save and visible failure feedback, complete record
  and catalogue, cancel/close/retired callbacks, replay, preserved settings/slots,
  a freely typed authored fallback, and reopening the exact completed manual save.
- Resume: a second native process reopens that completed save and repeats the
  completion/replay flow, 33 checks.
- Preview: a third process solves the selected final activity before Finish,
  exposes no player save/replay actions and preserves every player-save byte,
  7 checks.

Independent review of the initial feature commit `2a3b0a32` found that solving
the final evidence activity left the advance button labelled Continue. The
post-answer label now uses the same ending condition as ordinary rendering.
A native final-activity label assertion failed before the correction, and both
full-watch and author-preview paths now assert Finish before triggering it.
The original source-bound logs remain a separate Library evidence package;
their success markers alone did not establish the previously unchecked label.

The focused runner is part of `scripts/verify.sh`. The final aggregate returned
zero: 4,110 core assertions, 63 story-validation checks, 11 simulated Pumas
contracts, unsupported speech, zero-warning/error build, native story/UI/save
checks, 32 audio assertions with 24,576 mixer PCM frames (peak 0.024877), and
13-scene Editor roundtrip including authoring/selected-beat launch checks.
No final warning/error lines were emitted. Earlier development runs caught
fixture compilation errors and an incorrect assumption that final-beat author
preview had already solved its activity; these were corrected before this result.

Evidence is in `artifacts/watch-completion/` (not tracked). Tests drive real Godot
controls/signals and persistence; they do not establish graphical appearance,
physical keyboard/hearing/accessibility, production model quality, microphone
capture, exported-platform behavior or a human five-hour playthrough. Those gates
remain separate. Speech stays unavailable pending the typed producer contract.

The task-created worktree is retained at
`/workspace/audio-qualification/gameplay-current` on
`feat/watch-completion-replay` for parent review/integration. The parent owns its
final integration and cleanup disposition; accepted PR7/QA worktrees are untouched.
