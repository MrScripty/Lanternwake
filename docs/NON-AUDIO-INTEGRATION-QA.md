# Non-audio integration qualification

Candidate branch: `qa/non-audio-playable-authoring`. Exact real PR7 base:
`65548f97e1c203884864bf4dcc1d1849db4cbcd1`. Product composition commit:
`e4b00c34859bbf1c405876c094a1a91b9fa33fe3`.

This isolated candidate combines the published validation, authoring navigation
and reading-size milestones. Original branches remain frozen. Each source commit
was cherry-picked with `-x`, retaining its original identity in the commit message;
there is no synthetic recovery root in this ancestry. All cherry-picks applied
cleanly. The only automatic shared-file merges were the verification script and
authoring checklist. No product changes were added while combining these sources.

| Original published commit | Candidate commit |
| --- | --- |
| `b5dfd00d67b388a22d20702d50dce004a393fffc` | `bd9e00ec471d3f196aba4e7d40f12b2eb6593ef8` |
| `7cacd52ea173094a478c7c3936effc37a17f582a` | `710141ce4ed1d7c3773029c6b4f4cb41b6e1edfd` |
| `742691292e2a604b034526e789f143585b58d3d1` | `5567a238a99cb4c048b88202c8f19079d295daaf` |
| `c4da1209da03f3940df0835c400e0865c29323fd` | `401590684e71ed2e744f3900d934774ac713a4a0` |
| `92548140522f09cc4446aa1a07909bff67bc78ec` | `853b75b1e83dfb4c5047c6547f071058c81800b1` |
| `d84199a849689cd2e40365be66380fa28dfa0329` | `e4b00c34859bbf1c405876c094a1a91b9fa33fe3` |

## Checks performed

Qualification used official Godot 4.6.3 .NET and .NET SDK 8.0.425 on Linux,
with a local pinned-package NuGet configuration and separate task storage.

- The complete `bash scripts/verify.sh` returned zero: 4,107 core assertions,
  63 story validation checks, 11 simulated Pumas client contracts, explicit
  unsupported-speech checks and a native build with zero warnings/errors.
- Native headless gameplay traversed all 1,424 authored beats. UI checks passed
  reading size/reset/reopening/reflow/focus, settings toggles, the actual Quit
  route, history/catalogue/chat/activity/save/load and save-recovery markers.
  Save isolation preserved real-slot sentinels.
- The real Editor suite passed cold/rapid selection and pending-callback teardown,
  dock layout/focus, character and story authoring, repeated native selected-beat
  launch/exit, and an 11-scene roundtrip with defaults preserved.
- `python3 integration/story-validation/verify_aggregate_failure.py` passed. Its
  disposable invalid chapter title reached the real validation harness; status
  134 propagated and later dotnet checks did not run. The preceding core command
  alone was simulated to exercise that otherwise unreachable failure gate.
- `python3 integration/qa/non_audio_flows.py` observed a live child carrying the
  selected-beat preview argument, deliberately killed its owned Editor process
  group, verified the observed child stopped executing and confirmed tracked
  source was unchanged. Then two Editor and three UI workflows completed with
  exit zero, all required markers and unchanged tracked files. No warning/error
  lines appeared in these five repeated-run logs. Temporary XDG storage is removed
  by the fixture; no normal player storage is used.
- `git diff --check` and Python compilation of the new fixture passed.

The interruption fixture uses Linux process-parent metadata because this cloud
procfs omits `task/*/children`. It reads command arguments only for children of
its own Editor. It records PID start times, bounds waits and terminates only
its own process groups. The initial observer version failed before it observed
a child; that tooling failure was diagnosed and corrected before the passing run.
Hard interruption verifies restart/source isolation, not graceful application
teardown. The five subsequent normal exits and existing native smoke fixtures
provide the separate normal-exit evidence.

Initial aggregate runtime passes emitted existing invalid-UID warnings with text
path fallback before Editor import; these warnings are not represented as a
warning-free aggregate. Subsequent repeated workflows used the imported project.
Qualification logs are local task evidence; the commands above reproduce checks
without assuming another executor's filesystem paths.

## Preserved contracts and limits

`Content/story.json`, `Theme.tres` and `project.godot` are byte-identical to PR7.
Conversation/speech sources, `StorySession`, `SaveStore` and `SaveRecovery` are
unchanged. Final `Story.cs` matches the published required-text validation source;
the final reading and selection-visibility sources match their published commits.
All aggregate gates from the input branches are retained. Text sizing remains
session-only, with no save-schema or persistence migration.

This candidate contains no audio checkpoint or audio changes. The pending audio
integration must preserve its Sound settings action when moving the old settings
method into `GameView.ReadingSettings.cs`, as documented in
[READING-TEXT-SIZE.md](READING-TEXT-SIZE.md). No PCM capture, playback retirement,
15-exit audio result or 13-scene audio result is established here. The original
audio checkpoint could not be materialized due to download DNS resolution failure;
its contents, CRC and recovery metadata remain unverified by this worker.

Speech stays unsupported before microphone capture, with no proposed Pumas
endpoints, production ASR or microphone activation. No physical display/audio
device or X11 preview was available. Headless control geometry does not establish
graphical appearance, physical keyboard behavior or human-playthrough acceptance.
Independent graphical review of the frozen authoring/reading sources is pending.
The branch is published for coordinated review only; PR7 is not updated or merged,
and no external review request is sent.
