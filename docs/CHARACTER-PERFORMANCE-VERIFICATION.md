# Authored character performance — 2026-10-06

Separate successor `feat/character-performance-20261006` starts from frozen guided-descent review head `ff0f479c0294030c5231c52a98f8a13db47757a9`. That review branch stays unchanged for independent closure. The implementation checkpoint is `e4a38ba4e21393feeb97baa8eaa3eb313a842165`; parent owns main, PR/review coordination, merges and publication.

## Selected gap and player-visible acceptance

The coverage plan and stage bible leave working/listening/resting character performances incomplete. The existing sculptures had one fixed posture and breathing. This bounded first pass adds three editable pose families for each living character and authored direction in six scenes: house inventory (`ch1_s2`), cave measurement (`ch3_s1`), archive work/playback (`ch3_s2`), full testimony (`ch4_s1`), controlled release (`ch5_s3`) and final catalogue (`ch5_s5`).

- Live speakers in the first three scenes use explaining/working poses; the audience turns toward the living speaker. Explicit narrated work identifies its worker by beat ID.
- Testimony, pressure and quiet catalogue use restrained resting poses, with explicit work exceptions. Breathing is disabled throughout these three still scenes, including work exceptions. Recorded speakers remain speaker-labelled recordings, never staged bodies.
- Pose changes are cuts. Reduced motion, author preview, Load and recovery show the appropriate current-beat pose immediately. Reading or pausing adds no timer, outcome, gate or automatic advance.
- Feet, cast positions and the safe landing remain fixed. Mesh/material resources and existing cameras remain authored resources. Undirected scenes restore their neutral sculptures.

This makes existing work, attention and restraint visible. It does not add prose, a puzzle, relationship scoring, a new branch or elapsed animation state. The gesture families do not claim an exact caliper/brake contact simulation or bespoke recorded acting. Broader choreography and human performance/composition acceptance remain open.

## Authoring and runtime

Each character prefab now has editable torso/head/shoulder/elbow pivots. Original neutral mesh bases and global positions are retained through reparenting; hand-held props follow their forearms. `CharacterPose` resources provide additive joint angles; twelve `.tres` resources author the three families for all four characters. No runtime mesh or pose-resource mutation occurs.

`StoryPerformance.tres`, exposed on StageDirector, contains explicit scene/still/rest sets and seven existing work-beat/actor bindings. Location resources bind an existing relevant object as a fixed performance focus. StageDirector reads scene ID, beat ID and speaker from the actual authored session. It applies bounded torso/head turns and static pose offsets without moving actor roots. Other scenes retain their original neutral posture. This is a first direction pass for six scenes, not complete choreography of every scene.

`GameView.RenderBeat` applies this direction alongside existing stage cues. No input, core, storage, model or voice code changes. The canonical story remains SHA256 `171dd89cc80a4e0de7a452216b5eb5eff0f2908746aee5fc4501db22dfc87c02`, with 35,456 required words, 1,439 beats, 41 scenes, 24 evidence activities, existing IDs/branches and fixed ending. Save v1/v2 remain unchanged. Poses derive from the restored authored beat; elapsed breathing or animation phase is not a restore claim.

## Qualification

Exact source/DLL identities, results and raw-evidence hashes are bound in [the delivery receipt](evidence/character-performance-20261006.json). Raw logs/screenshots stay local under `/workspace/lanternwake-character-performance-evidence-20261006`. No Library or alternate upload occurs.

The native fixture instantiates real Main with normal owned storage. Seventy-four checks cover all four characters' distinct hand placements, repeated-application stability, fixed feet/roots, unchanged mesh resources, all explicit work bindings, speaker/listener role changes, resting/still scenes, no automatic progress, actual Save/Load of full live state and pose, reduced-motion stability and neutral restoration. Core traversal creates setup fixtures and checks all 1,439 canonical positions; it is not a timed reading playtest.

Nine real rendered author-preview cases cover all six scenes and representative speaker/work/rest transitions, with synthetic X11 controls, production Quit, no player-slot writes and unchanged source. Inspected house, cave, archive, testimony, release and catalogue images show the intended gestures and living cast. The existing release view retains its prior partial actor framing under the dialogue panel; this work does not claim broader camera composition acceptance.

The affected checks pass: 4,241 Core assertions, 63 structure checks, the complete native route/ending/resume, 24 activity reviews, 28 optional conversation Return paths, the retained exchange (82/22 plus rejected mismatched preview), guided descent (44/32/25) and 13 Editor scenes with profile/dock/playtest-launch verification. The performance fixture passes again after Editor. All accepted native/rendered runs use the same tested DLL.

Normal 100% house input passes 23 checks and 150% reduced-motion catalogue input passes 19, both with production Quit exit 0 and unchanged source. The house captures show the roles switching from Ada speaking/Nessa listening to Nessa speaking/Ada listening. Catalogue captures show work, resting, loaded resting and earlier working recovery. Both accepted controllers and all three qualification styles share the same tested DLL. Detailed results are recorded in the delivery receipt. These launch real Main through the passive DEBUG observation wrapper with normal storage and external X11 input. The observation file is outside player saves and the observer never restores state or reads/writes slots. Full live-state Load/recovery comparisons cover transcript, facts, inventory, solved activities and derived snapshot fields independently from slot bytes.

An initial render attempt omitted the owned display contract; the input guard rejected it. The first normal house capture still showed the Load panel although the live beat was already loaded, and the next attempt did not observe the closely spaced second Return. Both are retained and excluded. The accepted controller waits for the actual rendered passage before captures and between those keyboard inputs. No production restore/input defect is inferred from these controller attempts.

## Remaining gates and identity

Synthetic X11, software rendering and dummy audio do not establish physical input/hearing, assistive-tool, human editorial/performance/composition or measured-duration acceptance. Five hours remains a human playtesting target. Automatic beat traversal, generated fixtures and optional chat are never counted as authored human playtime. Score/sound production, broader scene direction, loaded-model/voice and platform evidence remain open.

Effective author and committer are verified as `MrScripty <TheEnvironmentGuy@protonmail.com>` before each commit. The pre-hold `cafee825`/`3f580361` metadata stays intact; no history rewrite or force-push occurs. The separate Pumas metadata worker owns network testing. This feature performs no discovery/download test and does not widen network access.
