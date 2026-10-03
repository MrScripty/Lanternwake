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
