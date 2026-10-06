# Long authored-fallback conversation qualification

Branch: `qa/non-audio-optional-conversations`, directly based on exact late-recovery
commit `d53452245bd952d5af132491415ce8451b784121` / tree
`00467e34ccc03f6c84887636b13381fd3f059093`. Earlier accepted sources retain their
identities; see [PLAYABLE-COVERAGE-INVENTORY.md](PLAYABLE-COVERAGE-INVENTORY.md).
This candidate adds only qualification fixtures and documentation. Existing
production source, scenes/resources, prior fixtures and canonical content are
unchanged. No production defect surfaced, so no production repair was made.

## Executed path

Official Godot 4.6.3 .NET / SDK 8.0.425, Linux headless Debug, isolated normal
userdata, empty Pumas model and no configured URL. Production `GenerateAsync`
reports `model_unavailable` before transport. The actual UI displayed exactly
`Authored reply · local Pumas unavailable (model_unavailable)` for every tested
reply. No live model, provider request, audio or microphone is involved.

| Native process | Required beats | Conversation points | Authored choices | Submitted fallback replies |
| --- | ---: | ---: | ---: | ---: |
| Chapter 1 | 327 | 6 | 18 | 60 |
| Chapter 2 | 289 | 6 | 18 | 60 |
| Chapter 3 | 315 | 6 | 18 | 60 |
| Chapter 4 | 257 | 5 | 15 | 50 |
| Chapter 5 | 236 | 5 | 15 | 50 |
| Reopen completed watch | 0 | 0 | 0 | 0 |
| Total | **1,424** | **28** | **84** | **280** |

Every authored suggestion was clicked and submitted unedited, then clicked again
and freely edited with a unique marker and Unicode before explicit Send. Each
conversation also submitted one deliberate repeated suggestion and three freely
typed Unicode follow-ups. Thus the 280 replies comprise 84 raw suggestions,
84 edited suggestions, 28 deliberate repeats and 84 Unicode free-text submissions.
Each reply is the exact authored fallback; player text is local fixture input,
not claimed to be authored story prose.

Before submission, clicking all choices only populated editable focused drafts.
An unsent draft was cancelled through Return without history or save changes.
Continue while the panel was open did not advance the plot. A whitespace Send
and a second cleared-input click after every completed Send produced **308 no-op
submissions**, adding no history or saves. Return after completed outcomes cancelled
only a new unsent draft. There were **56 draft cancellations** in total.

One explicit Send appended exactly one player/fallback pair. Both lines remained
`Generated=false` with exact stable beat, scene and conversation-character IDs.
Fallback text/speaker matched the authored conversation. Canonical beat, solved
activities, facts, inventory and cues were unchanged across raw, edited, repeated
and free inputs. Native dialogue rendered the fallback as plain text; autosaves
contained the entire exact outcome and left explicit manual checkpoints unchanged.

Each conversation saved a pre-outcome manual snapshot and then its accumulated
post-outcome snapshot. Real Load actions recovered the previous pre-outcome watch,
removed later optional memory, and repeatedly restored current manual and auto
snapshots once without duplication. A reopened History window contained the
authored fallback. **120 load selections** passed across same-process recovery and
fresh chapter/completed-watch processes, preserving save bytes during inspection
and load. Required text remained in exact canonical order throughout.

Every new conversation excluded earlier scenes' optional memory. Follow-up context
contained only the current scene/character's bounded suffix: at most six lines,
at most 600 characters per line, and at most 12,000 characters for the complete
serialized context. Unicode escaping forced **62 observed budget trims**. These
are context admission checks, not live-model characterization or spoiler testing.

All 23 mandatory gates were solved through real evidence buttons and the actual
Finish route remained available. Six fresh processes exited zero through Settings
Quit. The completed watch reopened with **1,984 transcript lines**: 1,424 required
lines and 560 optional lines, all with `Generated=false`. Repeated loading and
Finish appended no additional events. The bell cue remained exactly one authored
offset on native stages after reload, with no cumulative application.

Final native logs contained no warning/error lines. There were 80,016 repeated
fixture assertions, including accumulated provenance scans; this is not 80,016
distinct scenarios. Runtime was 87.29 seconds here, an accelerated deterministic
run rather than a human reading duration or performance benchmark.

## Reproduction and evidence

Set `GODOT_MONO` to the official .NET executable and restore/build Debug from the
approved matching offline packages:

```sh
"$GODOT_MONO" --headless --editor --path . --import
python3 integration/qa/optional_conversations.py
dotnet run --no-restore --project tests/Lanternwake.Tests.csproj -- Content/story.json
dotnet run --no-restore --project integration/story-validation/StoryValidation.csproj -- Content/story.json
"$GODOT_MONO" --headless --editor --path . -- --editor-roundtrip
```

The native build had zero warnings/errors. Existing **4,107 core assertions**,
**63 story validation checks**, authoring/selected-beat launch and the real
**11-scene Editor roundtrip** passed on this candidate. These are offline checks;
prior simulated-client aggregate results retain their earlier source scope.

The Debug-only controller requires the runner's owned marker and verifies its
normal user directory lies within temporary XDG storage before instantiating Main.
It drives actual buttons/modal actions and observes private state by reflection;
it does not bypass gates or directly insert chat outcomes. Direct domain reads
check context/provenance, and stored snapshots feed the actual Load menu. The
runner bounds native processes and fingerprints tracked source between chapters.
Temporary userdata is removed afterward. An explicitly synthetic completed save,
logs and summary remain under ignored `artifacts/optional-conversations/` for
independent restore/review. No existing player data is exported.

Canonical JSON SHA-256 remains
`0e08db51fb0b4d8c90dab1d816ab5e14245ad23216f0e9115ffe3bbf7f7f83f7`.
Main, shared theme, procedural asset provenance and all production/save/speech
interfaces retain their accepted bytes. No PR7/audio update, merge or external
review request accompanies the qualification.

## Limits

The empty-model fallback completes without a live inference operation. Cancellation
coverage is therefore unsent drafts, completed outcomes and load/Return retirement;
it does not establish cancellation of an in-flight provider or native inference
retirement. No delay, invented endpoint or recognizer was substituted. Headless
focus/control geometry establishes no human accessibility or physical input claim.
Human reading/editorial assessment, release platform/accessibility/performance,
production model quality and the separately gated audio/speech work remain outside
these completed deterministic non-audio paths. No further feature work is included.
