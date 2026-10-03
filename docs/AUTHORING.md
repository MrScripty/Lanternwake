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

Use **Search story** to find beats across all chapters by line text, speaker, scene/beat ID, location, evidence prompt or conversation metadata. Whitespace-separated terms all have to match (case-insensitive). Results stay in story order; selecting a match navigates to its original scene and beat. The result count includes all matches; clearing search hides the list. Save or discard a draft before selecting a result.

The read-only **Author context** panel shows the scene/cast, stable beat ID, fact/item unlocks, stage cue, mandatory evidence answer/explanation and optional conversation prompt, allowed facts, suggestions and authored fallback. It contains spoilers for authors, not player-visible knowledge. Editing a beat still changes only its speaker and text. Successful reloads retain the selected stable beat ID when it still exists; a failed reload keeps the last valid selection and unsaved draft intact.

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

## Character dossiers and runtime context

**Show character dossiers** opens a character selector and one-field-at-a-time editor in the Story Text dock. History, personality, motivations, speaking-style notes, knowledge/disclosure notes and source references are **AUTHOR ONLY**. They may contain full spoilers and never enter model context. The four living-character dossiers are excerpts from the existing story bible and response contract, with source references. They introduce no new story facts. Recorded characters may have writer notes but cannot become live conversation targets; Ada remains the player viewpoint.

The separate **RUNTIME · dialogueStyle** field adds at most 600 characters of spoiler-free behavior guidance to the existing role/voice card. Use it for sentence style, handling uncertainty or responding to emotion. Do not copy history, arc outcomes, hidden motives or additional facts into it. The engine enforces the size and separation of author-only fields; it cannot determine whether prose pasted into the runtime field is spoiler-free. Authors must review that text. Clearing it restores the previous role/voice-only behavior. Optional profiles require no save/schema migration. Each author-only field is limited to 8,000 characters.

**Save selected dossier field** validates the whole story and updates only that character field, preserving IDs, gates and unknown JSON fields. A beat draft locks dossier editing; a dossier draft locks beat editing. Save the active draft before switching fields/characters/beats. Both Reload buttons discard all drafts. Stale external content is not overwritten. Saving one surface cannot silently discard an edit on the other.

**Show runtime context preview** displays the exact production character-context string at the selected conversation beat. Its state is an isolated replay of authored progress, with prior mandatory activities solved and **no optional conversation history**. It does not inspect or modify a player's save, call a model or represent a player's actual progress. The transport's system wrapper and the player's message are not shown. Preview uses the same `StorySession.ConversationContext` method as gameplay and updates from saved content, not unsaved drafts. Non-conversation beats show no model request.

Facts remain the intersection of authored progress unlocks, the selected conversation's allowed facts and the character's knowledge ceiling. Author-only knowledge notes grant no access. The 12,000-character context budget, six same-scene transcript-line limit and 600-character per-line bound are unchanged. More descriptive guidance is not evidence of improved model quality; live characterization and spoiler-resistance evaluation remain open. No fine-tuning dataset is generated.

## Play the selected saved beat

Choose a beat in the Story Text dock and select **Play saved beat · separate window**. This is a debug-only, isolated author session, visibly labeled **AUTHOR PREVIEW** in the game header. It replays prior authored unlocks, inventory and stage cues; prior mandatory activities are solved only to reach the selection. If the selected beat itself is an activity, it remains unsolved so you can test it. Optional conversation history starts empty. You can continue playing normally from that point, but no player save can be read or written.

Save or discard any beat/dossier draft first. The launcher also rejects a story changed externally since loading. Save native scenes/resources separately before testing: the launched game consumes saved files. A second launch is refused while that dock's preview is still running. Close the separate game window normally before launching another; ordinary cancellation/shutdown handles any pending conversation. Closing the Editor releases its process handle but does not force-close the separate game.

The launcher invokes the current Godot executable directly with a stable beat-ID argument; it does not modify `project.godot`, persistent run arguments or player saves. CLI equivalent: `godot-mono --path . -- --author-preview-beat <stable-beat-id>`. Invalid/missing IDs, contradictory test/preview flags and release-build preview requests fail rather than falling back to normal play. The existing `--stage-preview` also disables player save/load. Start the game normally to resume real saves.
