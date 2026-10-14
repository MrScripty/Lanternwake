# Application acceptance and bounded repairs — 2026-10-11

Acceptance branch starts at Lanternwake `60d02ebc9ea7be207429f17cda9373183e00dc2e`
(draft transcription PR #37), based on main
`76b8428ee6bbe0c03d80b7df0520370df1a1b3a7`. It does not change PR #37 or the
separate native screenshot PR #38. Tools: official Godot 4.6.3 .NET, .NET SDK
8.0.425, Linux cloud instance. No repository AGENTS.md or local skill files were
present. All native qualification storage is disposable and owned by its runner.

## Repairs verified by failing then passing native regressions

Conversation suggestions and Send/TextSubmitted callbacks could edit/submit an
underlying draft while Reading settings or microphone consent owned the UI.
Suggestions also remained enabled during recording and could overwrite its
locked draft. A covered Use voice callback could probe Pumas and strand the
disabled button. Return could close an underlying conversation under a modal.

Callbacks now check conversation editability, modal/consent ownership and current
request state. Suggestions share the draft lock during capture and submission;
uncovered controls recover when their owner settles. Tests check exact draft,
story and save-byte preservation, no covered capability request, usable controls
after closing the modal, and capture cleanup. Existing generation/token guards
still reject stale speech results. The settings test additionally covers consent
confirmation after its preparation deadline expires, followed by cancellation.

No runtime admission, Cohere selection, producer isolation, story content,
inference policy or save format was changed. Test transcripts remain separate
from conversation/history/save state.

The broad qualification fixtures had assumptions predating the startup menu,
AI-off default, staged actor representation, authored wrong-answer explanations
and phased bell descent. They now use visible New story/Load game controls,
verify AI-off authored provenance, compare the exact staged cast, return from
authored source feedback without advancing, and check partial bell positions
with reduced motion selected through Reading settings. Dedicated bell tests
continue to exercise animated movement and cancellation.

## Actual cloud evidence

| Check | Result and scope |
| --- | --- |
| `scripts/verify.sh` | Passed: core/story, protocol, native settings/recovery, lifecycle/cancellation, all activity/history/bell paths and Editor authoring. Core: 4,581 assertions, 1,440 beats, five chapters. |
| `integration/qa/conversation_return.py` | Passed: complete traversal 3,215 checks at all 28 conversation points; closed 51; preview 73. |
| `integration/qa/speech_capture.py` | Passed: 136 native checks, two controlled POSTs, owned generator, no physical microphone or inference. Repeated clicks, interrupted probe/consent/capture/transcription, release, editable result isolation, stale-result rejection and quarantine. |
| `integration/speech/SpeechSmoke.csproj` | Passed: 141 audio contract, 19 buffer and 12 admission/platform checks. Actual consumer HTTP/TCP wire shape and current closed Pumas grammar, controlled responses only. |
| `integration/qa/optional_conversations.py` | Passed: 80,065 checks, 1,440 beats, 28 conversation points, 84 authored choices, 280 explicit submissions, 120 loads, 56 cancellations, 308 empty no-ops, 62 budget trims; six fresh processes, actual Quit exit 0. |
| `integration/qa/long_session.py` | Passed: 25,678 checks, 1,440 beats, 41 scenes, 344 resumes, all 24 wrong-answer/recovery paths and 95 rollback recoveries; six fresh processes, complete ending and actual Quit exit 0. |
| Build and hygiene | Debug, ExportDebug and ExportRelease warnings-as-errors builds passed with zero warnings/errors; credential scan and whitespace checks passed. Exact-head CI runs the full suite, all three build configurations and official Editor build. |

The full suite preceded the final fixture-only refinements; focused speech and
extended story/conversation runs cover those refinements. The runners check
tracked-source fingerprints and exact canonical story order. Story SHA-256:
`dabfe352a969dcb8c646065865bad352370263945dfa242108156344ea81c2db`.
These are accelerated headless callback tests. They do not certify physical
input, rendering, listening, human session duration, model quality or resource
usage. PR #38's original native screenshots document its own main-based source,
not this repair branch or real transcription.

## Deferred local producer and microphone acceptance

Pumas-Library `ad31e391dcd94c204b3fcd748cc98d27b3281e65` and its
[installed audio session contract](https://github.com/MrScripty/Pumas-Library/blob/ad31e391dcd94c204b3fcd748cc98d27b3281e65/docs/contracts/installed-audio-session.md)
were inspected as source, not executed. The user's unpublished local producer
fixes are absent here. No gated weights, real model, physical microphone, paid
API, user device/catalog or producer generation was accessed. Controlled
consumer success is not an import/load/transcribe/drain receipt.

After identifying the producer revision containing those fixes, use the detailed
[local workflow](SPEECH.md) and retain its observations:

1. On Linux x86_64, record the read-only Landlock ABI result (6+), actual producer
   revision, model/artifact/runtime/manifest digests and loaded profile generation.
   Import the existing local Cohere model and use the explicit experimental CPU
   serve path. Require `loaded: true`, `experimental: true`,
   `production_available: false`; do not weaken shipping admission/isolation.
2. In **AI setup → Transcription**, select that local indexed model/profile.
   **Record test clip → Start recording → Stop recording → Transcribe recording**.
   Speak a known short phrase, check the actual words and edit the result. Repeat,
   cancel and navigate during capture/request; check device errors and the clip
   limit. Confirm test text never enters game/history/save state. Record physical
   permission/device behavior, accuracy, latency and resource observations.
3. Save settings when idle; in a conversation, voice fills an editable draft.
   Only **Say this** submits it. Independently retain `unserve_model` with
   `unloaded: true` or `stop_runtime_profile_if_generation` for the captured
   generation with `stopped: true`, confirming child and diagnostic-reader join.
   A false, cancelled, timed-out or lost stop is not a drain receipt. Do not stop
   a successor generation. After unknown outcomes, confirm original drainage
   before restarting Lanternwake; restarting alone proves no producer cleanup.

Real installed-producer and physical-microphone acceptance remains deferred
until those local receipts exist. The application repairs and controlled
consumer checks above are reviewable independently. No PR was merged or deployed.
