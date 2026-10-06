# Review evidence while answering a question

Every evidence question now offers Review known evidence and Read the record.
Back to question, the review window's close control and Escape return to the
same authored prompt and answer options, with focus on the originating review
action. Closing the question itself returns to the current beat without answering.
Opening a question initially focuses Review known evidence, never an answer.

Catalogue and record reuse the existing current-session readers. They reveal only
unlocked objects/facts and the reached transcript; optional generated dialogue
keeps its explicit label. Reviewing or returning does not answer, advance, change
knowledge or write a save. Correct and incorrect answers retain ordinary gameplay
and autosave behavior, including Finish after the final correct answer.

Question/review callbacks check their original window, session, beat and render
generation at use, including cancellation, same-instance save restoration,
replacement and shutdown. Generic modal close callbacks now belong to the window
that raised them. Escape uses that window's dismissal route so it can return from
a review. No story, core, save schema, conversation/provider, speech, audio,
procedural asset, scene or resource changes are included.

## Qualification

Separate branch: `feat/evidence-question-review`, based on frozen completion
`a421937c6a7472c11fe3ee121658574a9acdaa4f`, tree
`356e200434c1032f7735116c4e378fd7317b43fa`. Parent owns coordinated review,
integration, PRs and eventual branch/worktree cleanup. PR7 and release publication
are outside this milestone. Retain this worktree only through parent review.

Official Godot 4.6.3 .NET / .NET SDK 8.0.425, Linux headless, Dummy audio.
`python3 integration/qa/activity_review.py` runs separate native normal/preview
processes in disposable owned userdata with provider transport disabled:

- Normal: 1,424 beats through actual Continue/answer controls; all 23 authored
  evidence activities; 2,511 checks. Exact prompt/options, current catalogue and
  ordered record, every locked fact/object excluded, return/cancel/Escape/focus,
  wrong-answer retry, explicit correct answer/autosave, same-session Load and
  retired callbacks after modal replacement and Continue. Every current/previous
  manual/automatic save byte is compared across review and cancellation.
- Reading text at 150%: every question uses 24-point choices in a 640 x 480 modal;
  actual native layout/focus checks show the last action inside the scrolling
  viewport and Close inside the window. Review restores originating focus.
- Preview: the selected final activity remains unanswered through review; 46
  checks, correct Finish label after answering, no player-save changes.
- A test-only generated conversation exercises record labelling and unchanged
  knowledge. A test-only closing-state probe exercises late review/close/answer
  callbacks; each process also exits through normal production Quit and native
  audio retirement. These probes do not claim production model inference or an
  exhaustive operating-system shutdown race matrix.

The focused runner is required by `scripts/verify.sh`. One final aggregate run
returned zero: five audio asset/setup tests, 4,110 core assertions, 63 story
validation checks, 11 simulated Pumas contracts, unsupported speech, zero-warning
native build, story/UI/save-isolation, 32 audio assertions with 20,480 real mixer
PCM frames (peak 0.024877), completion/replay 1,458/33/7 checks, question review
2,511/46 checks, and 13-scene Editor roundtrip with authoring/selected-beat launch.
No warning/error lines appeared in this aggregate. An earlier development build
failed on a fixture's incorrect record type name; it was corrected before the
successful native runs. Initial focused counts were 2,467/46 before adding the
explicit after-Continue callback checks; they are not the final evidence counts.

Logs are in ignored `artifacts/activity-review/` and `artifacts/watch-completion/`.
Source-bound logs, exact source archive, tested debug DLL/PDB and ancestry-bound
bundle are saved separately for parent review. Native signals/layout and PCM
capture do not establish human visual judgment, physical keyboard/accessibility,
physical hearing, exported-platform behavior or a human five-hour playthrough.
There is no display server in this cloud environment. Production ASR and
microphone capture remain gated on Pumas's unpublished typed Cohere contract.
