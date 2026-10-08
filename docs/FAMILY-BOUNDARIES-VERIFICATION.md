# Authored family boundaries — 2026-10-06

The separate `feat/family-boundaries-20261006` successor starts at frozen
`935c8fe27eb4c5a3ef4aeb3f1e547f7f7045c457`. Tested production milestone:
`161c3b9bc6004854e8c90c6cfe90da2c825c5d0e`.

## Chosen gap and consequence

The [story bible](bible/01_STORY_BIBLE.md) permits varied local relational
emphasis within its fixed ending. **Six Working Lives** previously had only
required prose about public evidence and family permissions. At the existing
`ch4_s1a_b035`, Ada can now offer Tomas practical help with permission labels,
respect for private memories, or a question about whether the correction needs
family photographs. Each intention receives a distinct authored response,
retained in the record. Leaving it unspoken remains valid. The responses use
already-reached scene information; they grant no new permission, discovery,
completed archive deposit or future confirmation.

The exchange pauses the passage and returns to its exact reading position.
Reopening displays the retained answer without another selection. It calls no
model and adds no fact, item, score, activity gate, stage event or advancement.

## Compatibility and scope

Removing only this one `exchange` property makes the entire story JSON identical
to frozen 935c8fe: every prior ID/order/text, cast, scene, fact/item, unlock,
activity, conversation, stage cue and ending is preserved. Required content
remains **35,456 words, 1,439 beats, 41 scenes, 24 activities and 28 model
conversation points**, plus one optional authored exchange. `playtested` is false.

Save version remains **2**, with the existing v1/v2 reader and v1 reconstruction
migration. Older saves before/after this moment and at the ending restore without
an exchange migration. New choices retain two plain authored transcript lines,
without generated flags or model-memory scope. The actual frozen 935c8fe core
restores all three chosen saves with byte-equivalent serialized state. See the
[fixture provenance](../tests/Fixtures/family-exchange/provenance.json) and
`integration/save-compat/LegacyReader.csproj` for the external frozen-reader check.

## Executed qualification

Actual cwd: `/workspace/Lanternwake-family-boundaries`; .NET **8.0.425**, Godot
**4.6.3.stable.mono.official.7d41c59c4**. The disconnect callback did not prevent
shell access or these running tests.

- Full `scripts/verify.sh`: exit 0; 4,241 core assertions, 63 structure checks,
  22 simulated Pumas contracts, audio/build/import, all 1,439 native beats,
  completed-save recovery, 24 question reviews, 28 conversation Returns,
  content note, cup states and 13-scene Editor roundtrip. No error/warning lines.
- Dedicated native exchange: 70 player checks and 20 author-preview checks;
  all intentions, cancellation, exact autosave/load, read-only reopening,
  stale/load/shutdown callbacks, paused reveal, 150% small-window focus/scrolling,
  unchanged successor and no preview slot writes.
- Normal Main project at 150%: all three intentions pass (15/18/18 checks),
  including Escape without writes, keyboard selection without advance, exact
  retained response, read-only reopening, Save/Load, unchanged successor and
  production Quit. Reverse Tab/End reaches the last line of both longer replies.
  The intention layout and all three replies/restored replies were inspected.

The first dedicated test controllers used an incorrect autosave caption and
left preview Settings open; correcting those controller assumptions produced
the native passes above. Two normal OCR checks initially dropped an isolated
“I” or misread a wrapped word beside the scrollbar. Actual UI and saved pairs
were correct. Shorter authored OCR phrases and explicit keyboard scroll-to-end
checks pass; both failed runs remain in the local evidence. No production code
changed after 161c3b9. Normal choice 0 used its original controller; choices 1/2
used the respective corrected versions. Each receipt binds its own controller,
source hashes and the same tested native binary.

Story SHA-256: `62c952e3c0d981a16e5b9ab5d6aeb6cd050d4481ef36e0b923f5efd5249e8c13`.
Tested native DLL SHA-256:
`81aa8acd7a30cc9f2f36bc8fefe85ce4f40c6da70a334938e910fc904a40f9ef`.
The [derived receipt](evidence/family-boundaries-20261006.json) binds source,
fixtures, native logs and normal receipts/images by hash. Raw evidence and the
completed resumable checkpoint stay local under
`/workspace/lanternwake-family-evidence-20261006`.

## Remaining gates

Human editorial/voice review, representative pacing and duration, physical input
and accessibility tools, production art and release platforms remain open.
These software-rendered synthetic-input checks make no live-model, hearing,
microphone or human-duration claim. The separate adapter fixture repair is
`5fdf0b179f7bec1efbc6b19612bba3202aa60bfb`; production Pumas code is unchanged.
Frozen adapter/acquisition/reconstruction refs remain preserved. No blocked
acquisition or Library route was retried, and no raw logs/screenshots were
uploaded. Main, PR metadata, merges and publication remain parent-owned.
