# Pacing, accessibility, and narrative acceptance

## Timing model

The compiler counts only required beat text as main-path prose. It excludes optional model replies, authored fallback replies, suggested player input, static item/fact entries, and this bible. Consequently the count understates total available authored reading but does not inflate the main story with optional material.

Current measured main path: **35,408 words, 41 scenes, 1,438 beats**. The 23 evidence activities are real runtime gates with objective multiple-choice answers, gentle retries, no timer, and authored explanation. Conversation is optional and never a duration requirement.

| Reading pace | Required text alone |
| --- | ---: |
| 140 words/minute | 253 minutes |
| 150 words/minute | 236 minutes |
| 180 words/minute | 197 minutes |
| 220 words/minute | 161 minutes |

Planning allowance, not measured performance: the 23 evidence reviews may add roughly12–30 minutes depending on recall and inspection; navigation, scene settling, and reflection may add15–35 minutes. The ranges must not be treated as guaranteed additive runtime: some reflection happens while reading. A reflective first pass at140–150wpm is near the five-hour target; a quick reader will finish considerably sooner. Market language should say "a substantial five-chapter mystery" until representative playtesting establishes a median. Never count unlimited chat as authored hours.

The existing native runtime verification performed by the game worker exercises UI and representative interactions, not a complete timed reading playtest. A working build and a schema test do not establish literary pacing or a five-hour median.

## Scene purpose and pacing

Chapter1 moves from public orientation to private inventory, then extraordinary observation, then ordinary recorded grief. The extra object work establishes Ada's competence and keeps the house from becoming a pile of plot tokens.

Chapter2 alternates archival reasoning with tangible geography. The route model is a meaningful limit on counterfactual certainty. Cup anticipation should be tense but not a real countdown; players may pause without causing a different event.

Chapter3 deliberately gives the player a physical model, emotional disagreement, and a bounded experiment before offering the complete historical answer. The discussion of preserving an active machine must not become a lecture detached from the rising pressure.

Chapter4 is the densest evidentiary chapter. Break the deposition with space for grief, ordinary crew lives, publication boundaries, and practical evacuation. The climax's "no one" must feel earned by named people and checks, not by a poetic line alone.

Chapter5 resolves the mechanism early enough to allow a lived aftermath. The safe return, debrief, song, family call, and final catalogue are not sequel hooks. They show what a corrected record permits people to do next.

## Honest expansion criteria

If representative median completion falls materially below4.5hours and the requested target remains5hours, add content only where readers identify a missing experience: a substantive evidence reconstruction, a conflicting interpretation resolved by a source, or a relationship decision whose current transition feels abrupt. New content must have a concrete dramatic question and payoff, respect the knowledge gate, and earn its reading time. First remove duplicated explanation before expanding another area.

Do not: slow text below user preference; force unskippable pauses; require an arbitrary number of free-chat turns; make players repeat evidence questions already mastered; insert irrelevant chores; add an unrelated late villain; obscure correct answers merely to extend play; claim the upper end of a timing range as an observed average.

## Accessibility requirements

These are acceptance requirements; verify against the runtime before claiming implementation.

- Entire plot understandable from text. Every essential sound, handwriting feature, gauge reading, diagram relation, and visual state is described in authored words.
- Keyboard access to advance, suggestions, input, evidence options, menus, and return. Visible focus order must be logical. No mouse-only tiny targets.
- Adjustable text size, wrapping, readable contrast, and backgrounds sufficient for all five set palettes. Test at small window sizes and long localized lines.
- No reaction-time challenges. Timed events are fictional chronology, not player deadlines. Pause/save during the operation does not create casualties.
- Optional typewriter, instantaneous reveal, and user-controlled advance. Disable screen shake, camera drift, and flashing through reduced-motion preferences where present.
- Hearing access: speaker-labeled transcript, all archive content in text, captions for clue-bearing environmental sounds. Audio channel comparisons must have a textual equivalent.
- Color independence: diagrams name A/B and routes; correct/incorrect responses use text; threshold labels supplement yellow/red markings.
- Voice input is optional. Recognized text must be visible/editable before submission. If unavailable, typed input and editable suggestions retain every story capability. No claim of bundled/offline speech recognition unless actually supplied.
- Wrong evidence answers receive explanatory feedback and retry without shame, penalty, lost trust, or resource loss. The answer must be deducible from earlier required material, not a hidden memory test.
- Conversation can be skipped. Model/network failure yields a safe authored fallback. No accessibility path loses a clue.
- Save/load restores beat, known evidence, activity state, and the bell-lowered visual cue. Backlog remains readable after an emotionally heavy scene.
- Content note: bereavement, historical adult deaths by drowning (described non-graphically), institutional concealment, storm danger, difficult family memories. No child injury, gore, self-harm storyline, or jump scare is authored.

## Main-path narrative test

Play once fully offline with no model. Read every required beat, choose an incorrect activity option before the correct one in each chapter, skip all optional conversations, and reach the ending. Then repeat selected scenes with model enabled and ask deliberately premature questions. Confirm that the main-path answer set is unchanged.

At each scene transition, check the location against the five-stage vocabulary and the current actor list. Archival voices must not appear as living actors. The tower's bell remains lowered after its cue on reload and in the morning. The destroyed active effect does not restart when revisiting a scene.

Ask a reader after each chapter to summarize only what the story has established. If they can solve a later clue by inference, that is fair; if the model or a default description confirmed a later fact early, that is a leak. If they cannot name the current question, simplify the transition rather than adding more exposition.

## Canon acceptance questions

1. Was Ivo murdered? No. The documented natural cardiac death is stable canon.
2. Does the machine send Ivo's voice? No. All voices are ordinary recordings.
3. Can Ada dial1998 or branch history? No supported capability.
4. Who writes the two notes? Ada, at the documented next-day input times.
5. What does "holding" describe? Water level on measurement Channel A, not a boat order.
6. Why does the corrected chart matter? It shows the sheltered west approach absent from the issued rescue route and public exhibit.
7. Is Ivo solely responsible for every failure? No; his specific actions are documented alongside coordinator and institutional failures.
8. Does discovering the truth guarantee a legal conclusion or instant healing? No; independent review and personal grief continue.
9. Why can the active machine be lost? Evidence is independently preserved and safety takes priority over continued operation.
10. What causes the bell to fall? A tested controlled gravity-release procedure, not a supernatural force or reckless leap into the shaft.
11. Did anyone drown in the present-day release? No; all people are accounted for and zones are clear.
12. What does the blank strip mean? No recorded trace. Nothing more is established.

## Editorial regression checklist

- Preserve exact trace wording, relative-day times, and23h17m arithmetic.
- Ensure all ordinary new details stay independent of the central conspiracy; not every object is a clue.
- Do not redeem Ivo by inventing a sacrifice or a perfect secret plan.
- Do not turn Nessa's age-nineteen distribution work into a concealed command role.
- Do not require Tomas to forgive or Ada to stay.
- Do not overclaim Sera's experiment as a complete physical theory.
- Keep public record access distinct from personal-family permissions.
- Keep the final agency with living people and the catalogue's capacity for correction.
