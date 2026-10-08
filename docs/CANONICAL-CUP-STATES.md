# Canonical cup stage states

The keeper-house stage now follows the existing object-state direction in
`docs/bible/03_ITEM_STAGE_SOUND_BIBLE.md`: intact blue repaired pencil cup,
floor fragments, a shallow tissue-lined box, then a plain steel replacement mug.
The break is a static authored cut. There is no player click, physics simulation,
real-time deadline, new sound asset or additional plot.

| Existing authored beat | New stage cue | Stage state |
| --- | --- | --- |
| `ch2_s5_b012` | `cup_broken` | Original cup/pencils retire; floor fragments/pencils and handle appear. |
| `ch2_s5_b026` | `cup_boxed` | Body fragments are boxed; the handle remains under the table for measurement. |
| `ch3_s5_b001` | `steel_mug` | Plain steel pencil mug substitutes; boxed fragments remain; floor handle retires. |

These cues use the existing cumulative authored-prefix replay. Earlier saves
restore earlier visibility; later visits reconstruct the current state; a new
watch starts intact. Save version, transcript, stable IDs, facts, items, activities,
conversation contracts and every authored sentence are unchanged. Only three
stage-cue fields were added to story JSON. Historical audio suppression and bell
lowering retain their existing behavior; the new cues trigger no audio effect.

## Native art and provenance

Original Godot primitive resources are authored directly in the keeper-house
scene. The intact cup and four pencils are grouped with identity parent transform;
their names, node IDs, local/world transforms, meshes and material references are
retained. All 359 existing subresource declarations and 340 original node IDs
remain. The root gains Inspector bindings; other original nodes are unchanged.
Existing blue and wood materials and cup/pencil meshes are reused. Added handle,
adhesive seam, fragment, box/tissue and steel-material resources are original
procedural work. There are no external asset downloads or runtime art generation.

Five optional Inspector bindings on `StageScene` own these groups. Other stage
scenes retain their current bindings and artwork. Editor qualification now
snapshots native visibility and cup binding paths as well as prior transforms
and exports; the keeper-house bindings must survive save/reopen.

## Qualification

Separate branch `feat/canonical-cup-states` starts at exact content-note head
`ff048ff2460df590a3cd69aa028a4bc907ed01fe`, tree
`8f2f007424094414b1b95e33a37c73af79542d2c`. Real PR7 ancestry is retained;
PR7–10 and content-note publication are frozen and parent-owned.

The native baseline probe on that production base failed at the exact authored
break: the original intact blue cup remained visible. Baseline source/binary/PDB
and expected-failure log are retained in the evidence packet. The first traversal
fixture reached the mug state but stalled after replay because it omitted an
existing mandatory evidence gate. Only that owned development process was stopped;
the fixture now solves the authored gate and verifies actual progress. This was
a test-harness correction, not another gameplay repair.

The required `python3 integration/qa/cup_states.py` runner passed with 4,265 checks,
1,518 native advances (the full 1,424-beat watch plus replay to the keeper house),
341 keeper-house state observations and six earlier/current save selections.
Four fresh-process resumes passed 20 checks each and four selected author previews
passed 10 checks each. Each uses private userdata and production Quit/audio
retirement. The visibility oracle uses the three canonical beat boundaries
independently of runtime cue values. Later keeper-house visits, reduced-motion
independence, content-note reading, byte-exact saves, transcript/gate equivalence
and new-watch reset pass. Core qualification passed 4,125 assertions, including
15 new event-metadata, restore-replay and unsupported-cue checks; the focused
.NET build has zero warnings/errors.

The full `bash scripts/verify.sh` run exited zero: 4,125 core assertions,
63 structure checks, 11 simulated Pumas contracts, unsupported-speech rejection,
zero-warning/error build, native story/UI/save isolation, 32 audio assertions and
20,480 PCM samples (peak 0.024877, Dummy mixer), completion/replay (1,458/33/7),
question review (2,511/46), conversation Return (3,186/39/61), content note
(56/57/39), this nine-process cup runner and the 13-scene Editor roundtrip plus
authoring/selected-beat launch checks. No warning/error lines occurred in the final
aggregate. PCM proves mixer output, not physical hearing.

This is Linux headless scene/control/persistence evidence. It does not establish
human camera composition or art/readability acceptance, physical input/hearing,
accessibility tools, production model quality, microphone/ASR, exports or duration.
The parent owns native visual review, PR coordination, integration, merge, release
and retained-worktree cleanup. No external review request or merge is made here.
