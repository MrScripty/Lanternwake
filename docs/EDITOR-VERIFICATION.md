# Editor authoring verification

Verified in Godot 4.6.3 .NET with .NET SDK 8.0.425 on the cloud Linux desktop (Mesa llvmpipe, compatibility renderer). These checks establish editor authoring and runtime consumption; they do not qualify production model quality, a physical microphone or human playtime.

## Native Editor workflow

Opened `Scenes/Stages/Harbor.tscn` in the actual Godot Editor and made these temporary changes through its Inspector and Story Text dock:

| Authoring surface | Saved change |
| --- | --- |
| StoryCamera | Orthographic size 19.5 → 21.5 |
| Moonlight | Light energy 1.1 → 1.4 |
| CastOrigin | Position X 1.3 → 2.3 |
| LanternwakeTheme.tres | Default font size 21 → 25 |
| Story Text dock | First beat text → `editor proof` |

The serialized files contained all five changes. The story dock reloaded its saved line. Closed the Editor, reopened the project, confirmed the cast position remained 2.3 in the Inspector, and ran the game with F5. The game displayed the altered framing, lighting, cast placement and larger typography; advancing from the title displayed `editor proof` as the first authored line. No C# changes were needed for these adjustments.

Stopped the test game, closed the Editor, and restored every temporary authoring edit from its pre-test copy. The original story hash remains `0fae051ce545f7e5c8cea248edac0e3fac89cb0a1ac8aa3f20a113f1ef99adce`.

During setup, rebuilding C# while the Editor was open triggered Godot's assembly-unload failure. A normal Editor restart restored operation. Scene, resource and story edits themselves require no C# rebuild. Close the Editor before external C# builds if this engine issue occurs; do not infer that a failed hot reload is a successful build activation.

## Automated regression coverage

`scripts/verify.sh` exercises the production core, direct Pumas adapter, native build and both Godot smokes. Its native resource test changes camera, light and cast settings on a scene copy, saves it with `ResourceSaver`, reloads without cache, initializes it in the scene tree and checks the resulting runtime values. It separately saves/reloads a Theme and verifies its font size on an attached runtime Label. Temporary test resources are removed.

Core authoring tests check that a text edit reaches the runtime parser, preserves every beat ID and mandatory activity, retains unknown fields, restores initial and completed saves, and rejects invalid speakers, blank lines, stale snapshots and future character knowledge. Existing story traversal, save, conversation scope and cancellation ownership tests remain in the suite.

The initial scene conversion also saved/reloaded each of the five locations and four character scenes with matching node counts and valid typed Inspector references. The one-time exporter is not part of the runtime or authoring workflow.

## Editor-only default preservation

Review of the first manual save found an editor-only defect: non-tool C# placeholders serialized omitted exported value types as null. The initial runtime-only smoke did not detect that boundary. Presentation components now use `[Tool]` so the Inspector reads actual typed defaults, with explicit `Engine.IsEditorHint()` guards preventing gameplay startup, input, camera switching and animation during authoring.

The verification script now also runs the normal Godot Editor lifecycle with `--editor-roundtrip`. Across all eleven Main/director/location/character scenes, it snapshots exported values and transforms, verifies that editor frames do not mutate them or start gameplay, saves and reopens, and compares every observed value. Null value-type exports, lost spacing/colors/motion settings, missing success output, timeout and engine errors fail the check. Runtime smoke separately proves rain moves when enabled and stays fixed under reduced motion.

After this repair, a fresh graphical Editor session saved Harbor, Main, Nessa and StageDirector. None emitted null exports. Harbor's Inspector showed Pair Spacing 3.1, and the scene was closed and reopened. The final native suite consumed these Editor-saved files and observed pair spacing 3.1, dusk light multiplier 1, rain enabled and rain height 13. It also verified two visible actors remain separated, active rain changes position, and reduced motion stops it. The normal Editor regression exits cleanly, without the resource-leak diagnostics produced by the earlier custom-main-loop test attempt.

## Story search and context regression

The normal `--editor-roundtrip` lifecycle also exercises the actual Story Text controls/signals against a disposable `user://` story copy. It searches a stable ID, opens a late-story evidence beat, verifies its read-only context, blocks search/scene/beat navigation while dirty, saves and reparses the edited line, and retains the selection. It then injects malformed external content, verifies failed reload preserves the draft and last valid snapshot, verifies Save refuses to overwrite the changed file, restores valid content and reloads successfully. Missing results and clearing search are checked too. The canonical story is never a test write target. `scripts/verify.sh` requires dock, narrow-layout and scene-roundtrip success markers.

Core tests independently cover complete canonical index/order, search coordinates for every beat, blank/missing searches, case-insensitive IDs, multi-term metadata intersection, evidence prompts, answers and authored fallback context. A separate real-engine layout regression sizes the scrollable dock to 320×600, checks horizontal fit and scroll-to-action reachability. These checks establish navigation, layout constraints and persistence behavior. Graphical screenshot review was blocked in this workspace: no display server is attached to the execution environment, and cloud-desktop input failed with an unavailable AT-SPI provider. Visual/editor usability review remains open.

## Character dossier and context-preview regression

Core tests cover optional/backward-compatible profile loading, exact single-field mutation, nested unknown-field preservation, stale saves, field-size limits, legacy and completed-save compatibility, and rejection of viewpoint/recorded characters as live targets. Unique secret sentinels replace every author-only field on every character. Across all 28 authored conversations, tests check that no sentinel enters the runtime context, each included fact is exactly in the triple-gated intersection, context stays within 12,000 characters, and an isolated preview equals the production builder without changing an existing session. Altering writer dossiers alone cannot change runtime context.

The actual headless Editor control/signal smoke saves and reopens a dossier on its disposable story copy, checks author-only/runtime labels, verifies cross-surface draft locks and non-discarding saves, exercises invalid-length and external-change rejection, reloads, and checks the preview against the production builder. The main verification script requires `LANTERNWAKE_CHARACTER_AUTHORING_OK` alongside the existing dock, layout and eleven-scene roundtrip markers. These checks are not graphical pixel inspection or model-quality qualification.

Optional dossier/style fields are the only additions to the canonical JSON: removing those fields yields the exact pre-milestone data, including all beats, facts, items, knowledge lists and gates. Authored-main-path metrics remain unchanged.

## Selected-beat author playtest

Core launch tests cover ordinary/test/preview mode selection, missing/duplicate/conflicting flags and explicit release-build rejection. All 23 evidence activities are previewed at their own beat: each current activity remains unsolved and exactly its preceding gates are solved. Late-story preview retains the authored bell cue.

The normal headless Editor regression calls the actual launch method after checking dirty-beat, dirty-dossier and external-snapshot-conflict rejection. It launches the current .NET executable twice, with a late-story activity selected, and awaits each headless child. Each child verifies the exact beat/text, visible preview header, disabled player save/load, unsolved current activity and authored knowledge/cues, then exits through the normal window-close notification handler. Persistent project settings and canonical story bytes are compared before/after. The script requires `LANTERNWAKE_PLAYTEST_LAUNCH_OK`; captured child errors, missing markers, nonzero exits and timeouts fail the check. Owned headless children are killed and reaped in the test finally block on timeout or any earlier failure, and on plugin shutdown; this cleanup never force-closes an interactive author preview. This is native launch/state/lifecycle coverage, not a graphical pixel review.
