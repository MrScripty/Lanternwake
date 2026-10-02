# Authored content schema 1

`Content/story.json` is the sole authored authority. Producer and C# consumer evolve together. It is not a model-output schema.

Root: `schemaVersion:1`, `title`, `characters`, `facts`, `items`, `chapters`.

- Character: `id`, `name`, `role`, `voice`, `color` (hex), `knowledge` (fact IDs).
- Fact: `id`, `text`.
- Item: `id`, `name`, `description`.
- Chapter: `id`, `title`, `scenes`.
- Scene: `id`, `title`, `location`, `timeOfDay`, `characterIds`, `beats`.
- Beat: `id`, `speaker` (character ID or `narrator`), `text`, optional `unlockFacts`, `unlockItems`, `conversation`, `activity`, `stageCue`. Supported stage cue: `bell_lowered`; cumulative authored cues replay on load.
- Conversation: `characterId`, `prompt`, `suggestions` (editable strings), `fallback` (authored response), `allowedFacts` (fact IDs). Available facts are further intersected with current unlocks and character knowledge before prompt assembly.
- Activity: `prompt`, `options` (at least 2), `correctIndex` (zero-based), `explanation`. Correct selection unlocks advance; incorrect selection gives retry feedback without penalty.

Locations: `harbor`, `keeper_house`, `archive`, `lantern_room`, `tide_cave`.

Exactly five chapters are required. The player character has reserved ID `ada`; prompts name Ada separately from the speaking character. IDs are nonempty and unique per entity kind. All references must resolve. Scenes must contain beats; text cannot be empty. The runtime validates content before play. Optional conversation never blocks advance; a mandatory activity does. Save files reference stable beat and activity IDs, not numeric positions. The final ending is fixed and authored.
