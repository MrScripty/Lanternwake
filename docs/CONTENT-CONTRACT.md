# Authored content schema 1

`Content/story.json` is the sole authored authority. Producer and C# consumer evolve together. It is not a model-output schema.

Root: `schemaVersion:1`, `title`, `characters`, `facts`, `items`, `chapters`.

- Character: `id`, `name`, `role`, `voice`, `color` (hex), `knowledge` (fact IDs).
- Optional Character `authoringProfile`: `history`, `personality`, `motivations`, `speakingStyle`, `knowledgeNotes`, `sources` (strings, at most 8,000 characters each). Writer-only; never sent to the model or treated as unlocks.
- Optional Character `dialogueStyle`: spoiler-free behavior guidance, at most 600 characters; the only new profile text sent to runtime context. Blank/absent preserves the original role/voice card. Author review is required for semantic spoiler safety. No schema/save migration is needed. Viewpoint Ada and recorded Ivo/operator/clerk cannot be live conversation targets.
- Fact: `id`, `text`.
- Item: `id`, `name`, `description`.
- Chapter: `id`, `title` (nonblank text), `scenes`.
- Scene: `id`, `title`, `location`, `timeOfDay` (nonblank text, not an enumerated vocabulary), `characterIds`, `beats`.
- Beat: `id`, `speaker` (character ID or `narrator`), `text`, optional `unlockFacts`, `unlockItems`, `conversation`, `activity`, `stageCue`. Supported stage cues: `bell_lowered`, `cup_broken`, `cup_boxed`, `steel_mug`; cumulative authored cues replay on load. The cup cues occur at the existing canonical break, boxing and mug-substitution beats; they change native stage visibility without new interactions, timers or save fields.
- Conversation: `characterId`, `prompt`, `suggestions` (editable strings), `fallback` (authored response), `allowedFacts` (fact IDs). Available facts are further intersected with current unlocks and character knowledge before prompt assembly.
- Activity: `prompt` (nonblank text), `options` (at least 2, each nonblank text), `correctIndex` (zero-based), `explanation`. Correct selection unlocks advance; incorrect selection gives retry feedback without penalty.

Required text is rejected when missing, null, empty or entirely whitespace. Valid
text is retained exactly, including Unicode and surrounding whitespace; validation
does not invent replacement labels or normalize authored prose. Diagnostics identify
the chapter, scene or beat ID and the invalid field (including an option's zero-based index).

Locations: `harbor`, `keeper_house`, `archive`, `lantern_room`, `tide_cave`.

Exactly five chapters are required. The player character has reserved ID `ada`; prompts name Ada separately from the speaking character. IDs are nonempty and unique per entity kind. All references must resolve. Scenes must contain beats; text cannot be empty. The runtime validates content before play. Optional conversation never blocks advance; a mandatory activity does. Save files reference stable beat and activity IDs, not numeric positions. The final ending is fixed and authored.
