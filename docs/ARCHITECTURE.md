# Runtime architecture and qualification

## Ownership

- `Content/story.json` is the authored story authority. Schema 1, five ordered chapters, scenes and beats. No generated dialogue can supply, reorder or unlock a canonical beat.
- `StorySession` owns position, established facts, collected items, mandatory evidence activities and transcript. Facts are derived by replaying authored unlocks, including after a load.
- Native scenes under `Scenes/Stages`, `Scenes/Characters` and `Scenes/UI`, plus their resources, own editable artwork, lighting, camera, character placement and interface styling. The initial procedural geometry was serialized once; runtime construction was removed.
- `StageDirector` selects and instantiates authored scenes. `StageScene` binds location settings and story cues; `StageMotion` adds animation to authored base transforms. These components do not consume conversation output or own story state.
- `GameView` owns the Godot presentation, modal lifecycle, textbox, history/catalogue, editable suggestions, submit/cancel and stale-response rejection.
- `PumasClient` owns the local HTTP boundary and provider verification. See PUMAS.md for exact upstream contract and qualification.
- `SpeechRecorder` owns consent-triggered microphone capture and transfers an operation-local PCM buffer to `PumasSpeechTranscriber`, which currently reports unsupported because Pumas has no transcription API. Capture is gated before touching audio devices. The intended local Cohere runtime and operation lifecycle belong to Pumas; the game has no direct recognizer process. Transcription fills the editable input, never submits it.
- `SessionStorage` owns the normal/author-preview/test persistence policy and slot paths. `SaveRecovery` validates snapshots against the current story, publishes compatible previous bytes before replacing a primary, and supplies explicit inspected recovery candidates. `SaveStore` remains the low-level schema serializer/reader. Only schema 1 is supported; recovery never silently rewrites files. See `VERIFICATION.md` for failure ordering and durability limits.

## Conversation invariants

Prompts include only the intersection of authored currently unlocked facts, this conversation's allowed facts and this character's knowledge. Full future chapters are never sent. Player text is bounded to 1,000 characters. Follow-up context includes at most six prior transcript lines for this character in this exact scene, each limited to 600 characters. The complete serialized context has a 12,000-character ceiling, trimming oldest prior lines first. Saved transcript provenance rejects later beats, wrong scenes and wrong characters. Prior conversation is labeled noncanonical; it is never promoted to an established fact. Response rendering is plain text, not executable markup. An optional response can be wrong: the model is not a truth oracle. UI labels generated dialogue as optional interpretation; the catalogue remains canonical.

A model has no state-mutation API. It cannot award evidence, solve activities, advance scenes, write saves, or change the ending. Closing conversation or loading invalidates in-flight output by generation token and cancels work. Authored fallback dialogue is visibly identified when local inference is unavailable.

Trust progression is deliberately not implemented as a reward for using suggestions. Free typing and edited suggestions are equivalent. Relationship-state design requires explicit authored intents, equivalent input paths, bounded effects and narrative tests before it can gate disclosures. Current optional LLM speech has scene-specific context and voice, but no advertised trust mechanic.

## Save lifecycle

Manual and auto saves are separate local files under Godot `user://`. Manual save is explicit. Advancing and completing an evidence activity autosaves. Schema1 stores beat ID, transcript with source-beat/scene/character provenance, and solved activity IDs. Load validates all activity references and rejects skipping an earlier unsolved activity before changing session state. Position-based facts/items are replayed from source. Schema changes before release require explicit migration or rejection policy. Concurrent instances are not a supported save workflow; each may replace the same slot. No cloud sync is claimed.

## Acceptance boundaries

1. Core tests: canonical sequence, locked context, chat immutability, activity gates, ending, save roundtrip/replacement/version rejection, earlier-save reset.
2. Godot .NET build: actual API/type correctness.
3. `--ui-smoke`: actual Godot callbacks for title/start, reveal, modal open/close, editable suggestion, fallback reply/history, conversation reopen, manual save/load and activity gating. Headless integration only, not perceptual proof.
4. `--smoke`: full authored canonical traversal, save/reopen, construct five actual 3D sets under Godot. This is a smoke, not a five-hour playtest.
5. GUI review: title, multiple locations/characters, text, evidence/activity flows, save/load and cancellation. Screenshots and actual observed results must be recorded.
6. Pumas required-real: inference-enabled Pumas with loaded llama.cpp model; provider-qualified request yields response. Mock tests do not satisfy this.
7. Speech required-real: permission, actual microphone, local recognizer/model, editable transcript, submit only on command, temp deletion/cancel. Compilation does not satisfy this.
8. Duration: five hours remains the design target. Human playtesters own duration qualification; assistant-run timed playthroughs are not an acceptance blocker. Optional chat cannot pad the main-story target.

## Editor authority and compatibility

`Content/story.json` is now directly editable through the lightweight Story Text dock or a text editor. Manuscripts are historical reference and the retired generator refuses to overwrite JSON. `scripts/story_metrics.py` reads this authority and updates only derived counts. Runtime schema and save format remain unchanged; the dock edits text/speaker without modifying IDs or story gates. The same domain validator runs before a dock save and at game startup, including conversation fact availability and character knowledge.

The dock owns a loaded snapshot, unsaved text and one explicit save. It rejects observed external file changes, validates before publication and replaces through a same-directory staged file. One editor/writer at a time is the supported workflow; no multi-process transaction guarantee is claimed. Godot owns normal scene/resource persistence. See [AUTHORING.md](AUTHORING.md) for practical editing steps.

## Standards

Reviewed the checked-out MrScripty Coding-Standards Core/Router, implementation, verification, build, code-design, contracts/evolution, persistence and C# async guidance. The write set is this new repository only. Local procedural art needs no third-party asset download. Documentation distinguishes source implementation, tests, real-runtime qualification and release acceptance.
