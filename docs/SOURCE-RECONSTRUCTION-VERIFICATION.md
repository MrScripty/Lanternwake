# Authored source reconstruction — 2026-10-06

Two playable milestones are saved on `feat/source-reconstruction-20261006`,
descending from frozen `bfa952c460c913c163d92a016d5b3eb35589c955`:

- `68ffc960757d9c89360a54abdb44cf06ca11c0b1`: source comparison, specific retry
  reader, author context, and legacy-save compatibility.
- `a09f264cc95332774ba830dd88a3594e4d05a467`: specific evacuation-identity
  feedback and normal-control coverage.
- `3691e538369536300de12c0a75bc490ae8b31285`: retain five generated script UIDs
  so the cached project imports cleanly.

## Chosen commitments and consequences

The [story bible](bible/01_STORY_BIBLE.md) asks the player to distinguish what
a record supports from an interpretation. The existing fourteen-beat source
reconstruction in **No Single Keeper** was passive prose. The new required
`ch4_s2_reconstruction_evidence` asks Ada to check the correction card against
the omission note, public summary, measurement log, and complete deposition.
Withholding the corrected chart and leaving the false hold claim in print are
different acts. Neither suppressing both nor asserting every editor's knowledge
can advance a current run. Tomas and Sera explain the particular source error;
the supported answer rejoins Tomas's existing checked-headings payoff before
the verified deposit. No additional confession or legal conclusion is invented.

The [accessibility bible](bible/05_PACING_ACCESSIBILITY_QA.md) calls for explanatory
wrong-answer feedback. The existing `ch4_s3a_evidence` now gives distinct
Nessa/Tomas responses to deleting a row or assuming similar names identify one
person. Both preserve the two household references until identities and arrivals
are checked. Its original options, answer, explanation and stable ID are unchanged.

Optional `optionFeedback` opens the shared keyboard-readable source-review
window and returns to the unanswered question with focus on the originating
option. Retired callbacks cannot answer, close or replace it. Wrong selections
change no transcript, facts, solved gates or save bytes. The author context/search
exposes the saved rationales; beat editing preserves them. Other activities retain
their established generic retry behavior. Specific rationales currently cover
**2 of 24** activities.

## Save and canon preservation

Snapshots now use **version 2**; versions 1 and 2 are readable. The only legacy
carry-forward is the new source gate when a v1 save is already past it and has
no transcript entry for it. A v1 save before it encounters it normally. A current
save cannot skip it; no original gate is waived. Restore remains transactional.
Loading preserves source/backup bytes; an explicit subsequent save writes v2.
The frozen v1 runtime cannot read v2 files: retain original slots when reverting.

Fixtures were produced by traversing the actual frozen v1 core. Compact test
fixtures retain its last six valid transcript entries; full snapshots were kept
locally and used for normal Godot Load. Before-insert, after-insert and completed
saves preserve exact position/history. See [fixture provenance](../tests/Fixtures/source-reconstruction/provenance.json).

All **1,438 original beat IDs, order and authored values** are preserved, with
only the new optional feedback property on the old headcount activity. Cast,
facts, items, scene metadata, unlocks, conversations, existing gates, stage cues,
chronology, and fixed ending are unchanged. One 48-word activity transition adds
a real source check: **35,456 main-path words, 1,439 beats, 41 scenes, 24 activities,
28 conversations**. `playtested` remains false. No human duration is established.

## Executed checks and evidence boundaries

The actual project ran with .NET **8.0.425** and Godot
**4.6.3.stable.mono.official.7d41c59c4** in
`/workspace/Lanternwake-story-successor`.

| Check | Observed result |
| --- | --- |
| Full `scripts/verify.sh` on a09f264c | Exit 0; 4,193 core assertions, 63 structure checks, 22 simulated Pumas contracts, audio/build/import, all 1,439 native beats, completion/recovery, 24 question reviews, 28 conversation Returns, content note, cup states and 13-scene Editor roundtrip. |
| Full optional-dialogue/save traversal on a09f264c | Six fresh native processes; 1,439 beats, 28 conversations, 84 choices, 280 exact authored fallback replies, 120 loads, 56 cancels, completed 1,999-line record reopened. |
| Normal source comparison at 150% on a09f264c | Both wrong accounts show their distinct feedback; keyboard return, unchanged saved bytes, supported answer, existing payoff, Save/Load and normal Quit pass. |
| Normal headcount at 150% on a09f264c | Both wrong accounts, feedback/keyboard return, unchanged saved bytes, original answer, existing successor and Save/Load/Quit pass. |
| Normal v1 after-insert recovery on 68ffc960 | Byte-preserving Load, explicit v2 Save with exact history/position, and original successor pass. Production C# is identical in a09f264c. |
| Normal completed-v1 recovery at 150% on a09f264c | Exact completed history/position retained, v2 Save, visible ending controls and normal Quit pass. |
| Metadata cleanup on 3691e53 | Clean import, UI/recovery smoke and 13-scene Editor roundtrip; after the Editor rebuild, UI and all 24 question reviews pass again without warning/error lines. |

The normal sessions use the ordinary Main project and real X11 input/observed
files/pixels, with owned private save roots; they do not use an author-preview or
qualification scene. Screenshots of both questions, all four explanations, the
source payoff and recovered ending were inspected. They establish the selected
software-rendered layout and interaction, not physical-device, assistive-tool,
human art or reading-duration acceptance. Empty model selection makes no live
provider or generated-dialogue claim.

The initial aggregate exposed a qualification constant expecting 23 activities;
both relevant fixtures now derive the count from content. The first completed
normal controller looked for an unauthored caption; its corrected check observes
the actual ending copy. These exploratory failures remain recorded. The full
aggregate then passed but reported five missing-UID cache warnings after cleanup;
retaining their generated IDs resolved them in the clean follow-up checks.
A transient environment-starting handoff retained files and running tests but
lost tool session handles; the actual harness logs/PASS markers were inspected.

Normal UI binary SHA-256:
`1605647e3e7bb7ac30ad9b6717f02761f16c8ff5af09877b53efcf983cb0c3de`.
After the metadata/Editor rebuild, the final native binary is:
`c7858f868492b76d8ceacefed8324d3338a87a6faaa0b1c592fd0fea82e87385`.
The clean follow-up native checks bind that latter binary. Story SHA-256:
`de4cf1b809e83565e1139c83b13a14cd22ef6c5e9fe61c8e4c4392468069b562`.

The [compact receipt](evidence/source-reconstruction-20261006.json) binds source,
fixtures, native logs and normal-session receipts/images by hash. Raw evidence
and its sealed archive stay local under `/workspace/lanternwake-story-evidence-20261006`.
No raw logs/screenshots are uploaded to Library or another destination.

## Remaining gates and ownership

Human editorial review of the new authored choices/voices, representative pacing
and duration, physical input/accessibility tools, production art and release
platform coverage remain open. The Pumas95 adapter `bfa952c4` and verified
acquisition blocker `e1418374` remain frozen; no discovery/provisioning retry,
external service, account/credential/security change or Library retry occurred.
Main, PR metadata, merge and publication remain parent-owned.
