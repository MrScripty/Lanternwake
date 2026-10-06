# Lanternwake: narrative production set

**Full spoilers throughout this directory.** `Content/story.json` is the single authoritative editable story. Use the Godot **Story Text** dock for beat text/speakers, or a text editor for structural story changes; see [the authoring guide](../AUTHORING.md). The manuscripts under `drafts/` are historical references from the initial writing pass, not a second editable source. The retired compiler refuses to overwrite the story. Run `python3 scripts/story_metrics.py` after edits to refresh derived statistics, then run the core tests.

This is an original adult-cast mystery, not an adaptation. The story has a complete fixed ending. Player conversation changes local expression, interpretation, rapport, and the amount of already-permitted detail requested; it cannot rewrite the authored past, kill a character, create an unplanned romance, or create an alternate ending. Suggested lines are editable invitations, not hidden plot branches.

## What is actually authored

- Five chapters and 41 staged scenes
- 35,408 main-path words by `scripts/story_metrics.py`, including short conversation invitations and activity transitions; count excludes optional LLM output, fallback replies, suggested player lines, item descriptions, and this bible
- 1,438 sequential authored beats, 28 optional conversation points, and 23 implemented objective evidence activities
- Seven named voice IDs: four living adults, plus Ivo, the inquiry clerk, and a station operator heard only in archival recordings
- Five reusable 3D locations; the final bell state is an authored stage cue
- All essential clues and the complete solution on the authored main path, independently of model availability

Exact machine-readable measurements are in `content_metrics.json`. These counts are generated, not an estimate based on file size.

## Duration honestly stated

**Design target: approximately five hours for a reflective first playthrough. No five-hour playtest has been completed.** At 150 words/minute, the current main text takes about 236 minutes to read; at 180, about 194; at 220, about 159. The 23 actual evidence activities add decision/review time, and scene changes, UI progression, and contemplation add time that must be measured rather than assumed. A reasonable pre-test planning envelope is roughly 3.25–5 hours depending on reading and investigation pace. Optional model conversation is additional and is never counted as required padding. Do not advertise a guaranteed five hours or report this as playtested.

A five-hour median requires validation and may require additional meaningful investigation content after reader testing. The expansion priorities and stop conditions are in `05_PACING_ACCESSIBILITY_QA.md`; do not lengthen the game with forced waits or repeated prose.

## Documents

1. `01_STORY_BIBLE.md`: creative thesis, cast, canon, chronology, chapter causality, ending
2. `02_SCENE_KNOWLEDGE_GATES.md`: generated per-scene unlocks and strict model-access boundaries
3. `03_ITEM_STAGE_SOUND_BIBLE.md`: object meanings, locations, staging, animation, audio language
4. `04_CHARACTER_DIALOGUE_CONTRACT.md`: voice, adaptive response behavior, secrecy, fallback design
5. `05_PACING_ACCESSIBILITY_QA.md`: timing, interactive coverage, accessibility, editorial acceptance tests

The staging document distinguishes production intent from current generated art. Reading a direction in this bible is not evidence that a corresponding animation, audio asset, or accessibility feature is implemented.
