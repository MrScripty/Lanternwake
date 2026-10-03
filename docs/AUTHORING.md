# Adjusting Lanternwake in Godot

Open `project.godot` with **Godot 4.6.3 .NET**, build the C# project once, and open `Scenes/Main.tscn`. Save your scenes/resources before running. The runtime loads these files; it does not rebuild artwork or layouts from C#.

If the Story Text tab is hidden behind the Inspector tabs, choose **Editor → Editor Docks → Story Text**. Scene, resource and story edits need no C# rebuild. If external C# rebuilding hits Godot's assembly-reload error, close and reopen the Editor after the build.

## Locations, lighting and camera

Open a location under `Scenes/Stages/`. Its ordinary scene tree contains meshes, materials, lights, a `Camera3D`, a `WorldEnvironment` and a cast origin. Use Godot's move/rotate/scale tools and Inspector. Select the camera to adjust its transform and orthographic size; select a light for its color and energy. Edit the environment resource for ambient light and fog.

Select `StoryCamera` and enable the viewport's **Preview** checkbox to see the game's framing while arranging the set. The free editor camera otherwise opens independently of the game camera.

The stage root exposes optional dawn/dusk environment resources and key-light variations. Edit the look used by the story's time of day, or clear an optional environment to use the default. Runtime time selection is explicit here, rather than hidden in C# constants. Existing day/night changes are preserved.

`Scenes/Stages/StageDirector.tscn` assigns the five location scenes and character scenes. Keep these references assigned. A location's cast origin moves the ensemble; its exported spacing and facing settings control grouping. The bell offset is an authored story cue and should remain connected to the correct bell node.

## Characters and motion

Open a character scene under `Scenes/Characters/` to change its silhouette, mesh dimensions, clothing materials or base transform. Instances use that scene wherever the character appears. Character IDs and historical speakers remain defined in story JSON; historical speakers are not living actors.

Small `StageMotion` components animate their parent's authored transform, rather than replacing it with a hardcoded pose. Their Inspector controls expose motion kind, phase, amount and speed. The player’s reduced-motion setting disables ambient animation. Keep a motion component attached to a compatible 3D node.

## Interface

Open `Scenes/UI/GameInterface.tscn` in the 2D editor for margins, containers, typography and dialogue/chat layout. Its shared Theme resource controls colors and button/panel styling. `ModalWindow.tscn` provides the reusable window shell; history, evidence and settings populate it at runtime.

Select the Main scene root for exported scene references, story path and text-reveal settings. Keep referenced controls assigned when reorganizing the UI. Runtime code owns events and changing content; scenes own layout and appearance.

## Story text

`Content/story.json` is the **single authoritative story**. The enabled **Story Text** editor dock offers a scene selector, beat list, speaker selector and text field. Choose a beat, change the line, then **Save beat**. Saves validate the complete story and preserve stable IDs, unlocks, activity gates, conversation metadata and unknown fields. **Reload / discard** loads the file again and discards the unsaved edit. Save or reload before changing selection.

The dock refuses to save if another editor changed the file since loading. Use one writer at a time; it is not a collaborative document service. A successful save uses same-directory staged replacement. Do not edit the same file concurrently in another application. Scene/resource changes use normal Godot Save; the Story Text dock's Save beat is separate.

For larger structural edits, use a text editor on the same JSON. Keep existing chapter, scene, beat and activity IDs stable for save compatibility. Do not renumber existing beats just because you insert a line. Facts must be unlocked before a conversation uses them and must belong to its character's knowledge. Mandatory activities retain their options, valid correct answer and explanation. Story state and gates remain deterministic; generated dialogue cannot rewrite them.

After story changes:

```
python3 scripts/story_metrics.py
dotnet run --project tests/Lanternwake.Tests.csproj -- Content/story.json
```

The old manuscripts are historical reference. Their former compiler now stops without changing files, so it cannot erase editor-authored changes. Derived metrics describe the current JSON. Five hours remains a design target; human playtesters exclusively assess duration.

## Check an edit

Save, close and reopen the edited scene/resource or reload the Story Text dock, then run the game. Use the existing debug stage preview (`--stage-preview`, F10 for the next location) for location checks. Run `scripts/verify.sh` with `GODOT_MONO` configured after structural changes. Core and engine tests protect story/save behavior; visual checks establish that your artistic changes read as intended.
