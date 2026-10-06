# Route inference feedback — 2026-10-06

Separate successor `feat/route-evidence-feedback-20261006`; authored milestone
`9999f25f23853243d3d70559ed49f3c197113713`.

## Existing requirement and smallest slice

The [accessibility bible](bible/05_PACING_ACCESSIBILITY_QA.md) requires explanatory,
non-punitive wrong-answer feedback. The [readiness backlog](REMAINING-GAMEPLAY-GATES.md)
still recorded 22 activities using generic retries. **A Route Made of Names**
already asks what its reconstruction supports, but its unsupported answers did
not explain their distinct errors.

The existing `ch2_s3a_evidence` now gives Tomas's source-grounded response to
guaranteed survival in an unobserved alternative, and Nessa's response to treating
a visitor map as a tide prediction. Both use already-reached dialogue and return
to the unanswered question with originating keyboard focus. Wrong answers write
no transcript, fact, gate or save. The original supported answer, explanation and
successor remain unchanged. Specific feedback now covers **3 of 24** activities;
21 retain generic retry feedback.

Removing only this one `optionFeedback` property makes the story JSON identical
to frozen family `c8b86bc`: every prior ID/order/text, cast, scene, fact/item,
unlock, activity option/answer/explanation, conversation, family exchange, stage
cue and ending is preserved. Required content remains **35,456 words, 1,439 beats,
41 scenes, 24 activities, 28 model conversation points and one authored exchange**.
Save v2 and its existing v1 migration are unchanged. No new prose gate, model
dependency, relationship score or timing challenge was added.

## Explicit composition and tested source

The branches were composed through normal history, preserving their original refs:

- `54010f50bfb92d4327f417f4282ed71b87804ce9` merges frozen family `c8b86bc`
  with adapter test/docs repair `5fdf0b179f7bec1efbc6b19612bba3202aa60bfb`.
- `9999f25` adds route feedback and its ordinary-player controller.
- `2f138e6540f61804fe65d4b3412fc9e2b7e6e14e` merges preview-oracle repair
  `04b4e3de2182aa7ae88d7a0ee9d025cef1775705` into that route milestone.

Actual cwd: `/workspace/Lanternwake-route-feedback`; .NET **8.0.425**, Godot
**4.6.3.stable.mono.official.7d41c59c4**. Full combined `scripts/verify.sh` on
**2f138e65 exits 0**, including 4,241 core assertions, 63 structure checks, the
22 corrected adapter contracts, audio/build/import, all 1,439 native beats,
completed-save recovery, 24 activity reviews, 28 conversation Returns, content
note, cup continuity, mode-checked family preview plus expected negative launch,
and 13-scene Editor roundtrip. Its aggregate log has no error/warning lines.

## Executed acceptance

- Affected core/structure checks pass: **4,241 assertions, 63 structure checks**.
  The composed adapter's **22 corrected simulated contracts** pass.
- Native question review passes **2,606 checks over all 24 activities and 1,439
  beats**, plus 46 author-preview checks. Exact new feedback, retry/focus,
  save preservation and stale callbacks are exercised by the existing fixture.
- Ordinary Main project passes at **125% with a real frozen v1 save** and
  **150% with a real frozen v2 save** (17 checks each). Both wrong explanations,
  Escape/retry focus, unchanged slot bytes, correct answer without advance,
  Save/Load, existing `ch2_s4_b001` successor and production Quit pass. Their
  question/feedback UI captures were inspected. Fixtures come from actual frozen
  bfa952c/c8b86bc core traversals, rather than reconstructed save history.
- The separate preview repair passes **71 player / 21 preview checks**. Its
  intentionally mismatched launch fails before slot writes, emits no success
  marker and is accepted solely as an expected negative case. The original
  family evidence remains preserved; see [oracle verification](FAMILY-PREVIEW-ORACLE-VERIFICATION.md).

The normal route runs preceded the oracle merge and bind their own native DLL.
That merge changes qualification code only; production scripts/scenes/content
are identical. The combined native suite binds its separately rebuilt binary.

## Evidence and remaining ownership

Source story SHA-256:
`171dd89cc80a4e0de7a452216b5eb5eff0f2908746aee5fc4501db22dfc87c02`.
Final binary after Editor roundtrip SHA-256:
`d94a7dae1654f3244684e126b413ac068367b6709f1a1c9b4ed096f951eba73a`.
The [derived receipt](evidence/route-feedback-20261006.json) binds source, legacy
fixtures, normal receipts/images and combined logs by hash.
Raw logs, fixtures and inspected captures remain local under
`/workspace/lanternwake-route-evidence-20261006`. No raw upload, blocked model
acquisition, Library retry, account/credential change or publication occurred.

Human editorial/voice/pacing/duration, physical-device/accessibility tools,
production art, release platforms and the existing live-model prerequisite remain
open. These checks use synthetic input/software rendering and make no hearing,
microphone, real-model or five-hour claim. Parent owns review, main/PR integration,
merges and publication.
