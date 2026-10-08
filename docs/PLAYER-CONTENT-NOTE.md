# Player-visible content note

The existing content note in `docs/bible/05_PACING_ACCESSIBILITY_QA.md` is now
available before Arrival and through Reading settings during play. Its text is
preserved exactly, apart from sentence capitalization and a paragraph break.
There is no new plot content, answer hint, acknowledgment gate or save field.

The title control opens a read-only note and returns to the title with Arrival
focus. It hides once the story starts or a save is loaded; a retired title
callback cannot open it during play. The Settings route returns to Settings,
including while an optional conversation and unsent editable draft remain open.
Opener/return callbacks belong to their originating window; retired callbacks
cannot replace a newer window. Existing story, reveal, conversation and audio
authorities are retained.

The note follows the current 100/125/150% reading preference. Its scrollbar accepts
keyboard focus; Up/Down retain that focus while scrolling, and Tab reaches Close.
Close names its destination. The title control uses a 48-pixel minimum height and
normal keyboard focus. Other modal scrollbars and the existing top bar are unchanged.

## Qualification

Source base: accepted Return repair `630f76575db622d344db6ea42d37e5e8d8b8168a`,
tree `dce5cec938277761819ac116f1331475a023a076`. Separate branch:
`feat/player-content-note`. PR7–10 and their metadata are parent-owned and frozen.
The task worktree is retained for parent review/integration; parent owns cleanup.

Official Godot 4.6.3 .NET / .NET SDK 8.0.425, Linux headless. The owned native runner
`python3 integration/qa/content_note.py` uses the bible as an independent copy
oracle and disposable userdata:

- Normal title/play: 56 checks, Arrival, 20 actual advances to an authored
  conversation, exact note copy, title close/Escape/focus, hidden/retired callbacks,
  Settings return, unchanged progress and every primary/previous save byte.
- Fresh-process resume: 57 checks, note access before Load, selected manual save,
  hidden title control, note access during restored play and preserved save bytes.
- Author preview: 39 checks at the selected conversation, Settings-only access,
  unchanged author-preview state and every player-save byte.
- At 150% text in a 480 x 320 note window, actual viewport Down-key input scrolls
  the exact prose without leaving the scrollbar; Tab reaches the visible Close.
  Open editable chat drafts remain intact. Each process uses production Quit/audio retirement.

The first development pass caught Down moving focus from the scrollbar to Close
after scrolling; local vertical focus neighbors now keep arrow keys on that
scrollbar. No focus trap is introduced: the native Tab assertion passes.
The runner is required by `scripts/verify.sh`.

The full `bash scripts/verify.sh` run exited successfully: 4,110 core checks,
63 story checks, 11 simulated Pumas checks and the unsupported-speech gate;
zero-warning/error .NET build; native story/UI/save isolation; 32 audio checks
and 20,480 captured PCM samples through the Dummy mixer; completion/replay
(1,458/33/7 checks), question review (2,511/46), conversation Return
(3,186/39/61), this note (56/57/39), and 13-scene Editor roundtrip plus
authoring and selected-beat launch checks. PCM capture establishes native mixer
output, not physical speaker playback.

These are headless native control/input-dispatch and persistence checks. They do
not establish physical keyboard, human appearance, accessibility-tool coverage,
physical hearing, exported platforms, production model quality or ASR. Existing
microphone/producer-contract and human editorial/duration gates remain open.
