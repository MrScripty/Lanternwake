# Guided bell descent — 2026-10-06

Separate successor `feat/guided-bell-descent-20261006`, based on frozen composed
`0eebf4e642485a6e14c62808facbc5c91d6adff7`. The PR13 repair at `fbe51b2` remains
frozen and separate; its three review repairs are not integrated into this branch.

## Player experience and authored chronology

Previously the counterweight changed from suspended to fully lowered when the
arrival cue appeared. The stage bible's guided lowering was not visible. Now
Continue after Nessa's countdown shows the bell descending vertically past guide
marks above its empty cradle, from a fixed elevated release view. The bell itself
and cradle stay above the dialogue panel; the existing cast stays on the landing.
Continue remains available throughout. No time spent reading or paused can change
the operation's outcome.

- `ch5_s3_b004`: existing descent narration; travel to half of the existing offset.
- `ch5_s3_b005`: existing bypass-flow narration; travel to 90% of that offset.
- `ch5_s3_b006`: original `bell_lowered` cue; immediately seat at the original
  `(0, -2.1, 0)` endpoint, matching the arrival narration.
- `ch5_s3_b007`: restore the existing wide story camera and retain the seated bell.

Each animated phase lasts 1.8 seconds by default. Fractions are presentation of
these authored phases, not a physical simulation or a clock the player must obey.
Advance can interrupt a phase. Reduced motion settles each phase immediately.
Loading, preview and recovery settle directly from the authored beat/cumulative
cue; elapsed travel is never serialized. Retired travel is killed when replaced,
when motion is disabled or when the stage exits.

The fixed view makes the mechanism's movement and available cradle visible
without asking the player to search a 3D world. It is a capability of the existing
Continue interaction, not an additional evidence activity. Required text, audio
assets/cues, activity gates, IDs/branches/ending, core and save v1/v2 handling are
unchanged. Canonical story SHA256 remains
`171dd89cc80a4e0de7a452216b5eb5eff0f2908746aee5fc4501db22dfc87c02`.

## Authoring and qualification

`LanternRoom.tscn` owns the guides, marks and optional fixed `BellReleaseCamera`.
StageScene's Inspector group **Optional guided bell descent** exposes the start
and flow beat IDs, camera, optional suspension mesh and travel seconds. BellBody and BellLoweredOffset
remain the existing bindings. Other stages keep empty optional IDs and no release
camera. Artwork/cameras remain authored resources, with no runtime resource write. The
existing suspension pays out by its node transform: its top anchor and radius
stay fixed, its lower end stays connected to the crown, and its mesh resource
stays unchanged. Load/rollback and reduced motion update both pieces together.

Native checks exercise real Main controls and owned save slots: vertical travel
without lateral fall/rotation, unchanged progress while moving, available advance,
Save/Load during travel, retired-tween cleanup, reduced-motion settlement, exact
arrival, wide-camera restoration, persistent seated state, previous-save rollback
and isolated author preview. Compact fixtures derive from actual frozen core
traversal; provenance binds full snapshots and source. Native player/resume/preview
checks pass 35/25/20, including suspension attachment/anchor/resource checks.

Normal Godot project runs use external keyboard/mouse input: 100% with motion and
150% with reduced motion, 19 checks each. One Return reaches each existing beat;
actual rendered passage prefixes are verified in the captures, so frame labels
cannot silently precede their beat. Save/Load preserves the exact canonical record
and solved activities. Production Settings Quit exits 0 and source stays unchanged.
Inspected captures show early travel, settled half travel and the seated cradle,
including the enlarged reduced-motion view. A separate rapid keyboard run
reaches both following beats inside the 1.8-second phase interval, then verifies
that retired travel cannot advance or rewrite slots and that the seated record
survives Save/Load. Its controller explicitly observes instant-text activation
and gives distinct key edges brief frame settlement. An initial synthetic rapid
attempt did not observe the second advance and is excluded from acceptance.
The earlier exploratory captures
preceded some rendered beats and are excluded from acceptance evidence.

Combined suite and Editor results and all source/log/DLL identities are recorded
in [the delivery evidence](evidence/guided-descent-20261006.json).
Raw evidence stays local under `/workspace/lanternwake-bell-evidence-20261006`;
no Library upload or alternate upload occurs. Synthetic X11 input, software
rendering and dummy audio do not establish physical-device, hearing or human
art/accessibility acceptance.

## Remaining gates

The [coverage map](FEATURE-COVERAGE-20261006.md) retains human art/performance,
score/hearing, editorial and measured duration, accessibility/device/platform
acceptance. No loaded-model, speech or five-hour median claim is made. Approved
model/runtime discovery remains blocked and is not retried. Parent owns native
visual review, successor composition with the separate PR13 repair, PR/CI/review
coordination, merges and publication.
