# Return from optional conversation to the authored story

## Remaining-gap inventory and selection

Inventory source: frozen PR9 head `10966adb37cf74f563f78072d417a6a6436a7528`,
tree `7aa77b71984667d5a986baea6f2261bb3a399fa6`, with PR8 completion head
`a421937c6a7472c11fe3ee121658574a9acdaa4f` and PR7
`9209b3f0366dfa14795e81e774915f231f238255` preserved. The parent reports independent
review and hosted CI acceptance for PR8/9; this inventory does not replace that
review with a new release claim.

The bible's [player agency](bible/01_STORY_BIBLE.md),
[accessibility and main-path acceptance](bible/05_PACING_ACCESSIBILITY_QA.md),
and [coverage inventory](PLAYABLE-COVERAGE-INVENTORY.md) separate deterministic
story capability from authored production decisions and required-real evidence.

| Area | Actual state at this base | Disposition |
| --- | --- | --- |
| Required story, evidence and ending | Fixed complete five-chapter path; 23 retryable gates; save/recovery; completed-watch replay and question/catalogue/record return implemented. | Preserve reviewed behavior. No new story, branch, timer or relationship score is justified. |
| Return after optional conversation | Hides chat but leaves the optional reply and its speaker in the main passage. A closed Send/TextSubmitted callback can submit an unsent cancelled draft and autosave. | Selected gameplay defect: recover the paused authored view and retire conversation controls. Native failing probes establish both consequences. |
| Player-visible content note | Bereavement, historical adult drowning, concealment, storm danger and difficult family memories are documented in the bible; the title/settings UI does not expose that note. | Concrete remaining player-facing gap for a separate milestone, using existing authored copy. Not mixed into this repair. |
| Wrong-answer feedback | Text retry feedback and a correct-answer explanation exist; the contract has no separate per-option authored rationales. | Preserve retry. More specific prose requires an editorial/content decision, not invented hints or new answer rules. |
| Stage production intent | Required events remain fully described in text; procedural stages implement the cumulative lowered bell. Detailed cup/fragments/mug changes, working poses and guided-release staging are not fully represented. | Art/staging work against the existing bible; not evidence of a missing required interaction or permission to rewrite chronology. |
| Relationship/disclosure progression | No hidden trust score; fixed authored relationships and optional local expression. VERIFICATION names additional mechanics as production design work. | Parent/author design decision. Do not invent scores, romance branches or model-dependent gates. |
| Human editorial/pacing/duration | Authored content and automated progression exist; no representative human median or full editorial acceptance follows from them. | Human playtest/editorial owners. Do not expand content or slow reading to manufacture five hours. |
| Production model and ASR | Offline fallback and simulated contracts are qualified. Production characterization/spoiler resistance/cancellation need real-provider evidence; typed Pumas/Cohere transcription is unpublished. | Keep current boundaries; no endpoint wiring, microphone activation or production inference/ASR claim. |
| Platforms/accessibility/release | Session reading preferences and selected native controls are implemented. Full physical input/accessibility tools, human visual/hearing review, localization, exports and low-end platform coverage remain open. | Separate release/device evidence. Parent owns release decisions and PR7 metadata hold. |

The Return defect takes priority because an optional exchange can replace the
current required passage, and a cancelled control can mutate the transcript and
disk. The fix uses existing Return, Escape, record and save paths; it adds no
optional feature scope.

## Resulting behavior

Opening optional conversation captures the current speaker, passage, revealed
character count, reveal timer and status as presentation-only state. Return or
Escape restores that view and story keyboard focus. Replies remain in the record.
Partial passages resume at their captured reveal count; the next Continue reveals
the passage before advancing. Return does not render a beat, replay cues, unlock
facts, solve a gate or write saves.

The view belongs to its original session, beat and generation. Load, replay and
scene replacement keep their existing rendering authority. Closed
Send/TextSubmitted and Return controls are inert; retired suggestion rows cannot
edit a later draft. Repeated Talk cannot reset an open draft; Talk cannot open
behind a modal or during shutdown. Existing request cancellation and owned async
draining are retained, with main-thread engine continuations.

Story JSON, stable IDs, core/save schema, conversation/provider code, ASR, audio,
procedural art, scenes/resources and reviewed completion/question partials are
unchanged. Branch: `feat/conversation-story-return`. Its worktree at
`/workspace/audio-qualification/conversation-return` is retained for parent
review/integration; parent owns PRs, integration and final cleanup.

## Qualification

Official Godot 4.6.3 .NET / .NET SDK 8.0.425, Linux headless, Dummy audio.
Two native probes failed on the exact reviewed base plus the owned test scene:
Return did not restore the authored passage/speaker, and hidden Send submitted
the cancelled draft. Original logs, probe source and tested DLL/PDB are retained
separately from corrected evidence.

`python3 integration/qa/conversation_return.py` uses private normal/preview
userdata and an empty model selection, rejecting before provider transport:

- Complete: 2,598 checks, all 28 conversation points and all 1,424 required beats;
  native fallback submission, Return/Escape, exact reveal/timer and focus, record
  retention, same-beat pre-chat Load, repeated Talk, retired suggestions, hidden
  Send/TextSubmitted/Return, cancellation after Send and all 23 evidence gates
  after late Return. Every current/previous manual/automatic save byte is checked
  across return/cancellation. Reading text is 150%.
- Closed controls: a separate native process, 39 checks, first conversation
  reached through 21 required beats.
- Author preview: 42 checks at the selected first conversation, including exact
  player-save isolation. Each process exits through production Quit/audio retirement.

The runner is required by `scripts/verify.sh`. One final aggregate returned zero:
five audio asset/setup tests; 4,110 core assertions; 63 story-validation checks;
11 simulated Pumas contracts; unsupported speech; zero-warning/error native build;
story/UI/save isolation; 32 audio assertions with 20,480 real mixer PCM frames
(peak 0.024877); completion/replay 1,458/33/7; question review 2,511/46; Return
2,598/39/42; and 13-scene Editor roundtrip including authoring/selected-beat launch.
No warning/error lines appeared. An earlier focused pass reported 2,547/38/41
before adding retired-Return checks; those are not the final evidence counts.

Logs are in ignored `artifacts/conversation-return/`, `artifacts/activity-review/`
and `artifacts/watch-completion/`. Source-bound evidence includes exact Git
identity, source archive, tested DLL/PDB, original failing probes and final logs.
These are headless native control and persistence checks. The after-Send probe
uses the fast unavailable-model path; it does not qualify genuine in-flight live
provider cancellation. No physical keyboard, accessibility tool, human visual or
hearing, exported platform, model quality, microphone or production ASR acceptance
is claimed. There is no display server in this cloud environment.
