# Late-session damaged-primary recovery

Local qualification branch: `qa/non-audio-late-recovery`, directly based on frozen
`595ef701c708e5689ac54d3877f97cd1ccbc754f` / tree
`a1d92d87c46e7cd776031bb7f6453b383130a7c8`. That input retains graphically accepted
`21a8eaeeaf9bf42cc6fd99f1b5a40af1850673f6` / tree
`25c480438aa37617d89a453b35473417f84e097a` and real PR7 ancestry
`65548f97e1c203884864bf4dcc1d1849db4cbcd1`.

This candidate adds an explicitly selected Debug qualification scene, its runner
and documentation. Existing production/gameplay/save sources, scenes, resources,
project settings, prior fixtures and canonical content are unchanged. The frozen
input branches are not updated. No production defect was demonstrated, so no
production repair was made.

## Known checkpoint provenance

Each process derives exact snapshots through the production `StorySession`
timeline, solving only actual preceding evidence gates and skipping optional chat.
This is deterministic fixture setup, not a second native all-beat traversal or a
human reading playtest. The previous milestone supplies the complete native path.

| Snapshot | Beat | Transcript entries | Meaning |
| --- | --- | ---: | --- |
| A | `ch5_s3_b005` | 1,232 | Before the bell cue |
| Cue | `ch5_s3_b006` | 1,233 | After one legitimate Continue from A |
| B | `ch5_s3_b007` | 1,234 | Original primary after the bell cue |
| C | `ch5_s5_evidence` | 1,424 | Full late transcript; final activity unsolved |

Normal production `SessionStorage.Write` publishes A, then B in the target slot,
creating a compatible previous A. Before rotation, owned A bytes include a UTF-8
BOM and an unknown qualification metadata field; the backup must preserve them
exactly. The independent other slot contains C. An immutable fixture archive
preserves the original published bytes across all three processes.

The actual Main scene runs with `SessionMode.Normal` inside temporary XDG userdata.
The controller requires the runner's owned marker and verifies the user directory
lies beneath that root before instantiating Main or editing any save. Faults touch
only these fixture slots. Native buttons and displayed modal actions perform every
tested load, cancel, subsequent manual save, evidence completion and Continue.
Reflection observes private state for assertions; it does not force session state
or call the unused legacy private Load method.

## Fault matrix and result

Official Godot 4.6.3 .NET / SDK 8.0.425 on Linux, headless Debug:

| Case | Slots tested | Expected actual UI behavior |
| --- | --- | --- |
| Primary missing | Manual and auto | Show absence; offer valid previous A |
| Primary malformed | Manual and auto | Show unreadable JSON; offer valid previous A |
| Primary truncated halfway | Manual and auto | Show unreadable JSON; offer valid previous A |
| Primary and previous both malformed | Manual and auto | Offer neither damaged candidate; other current C remains selectable |
| Primary is an owned empty directory | Manual and auto | Show read error; valid previous A remains selectable; failed save preserves slots |
| Backup replaced after menu inspection | Manual | Selected action restores the captured A even though its backing file has become invalid |
| All four slots malformed | All | No load actions; cancel preserves progress; a fresh watch can be explicitly saved |

All **12 cases** passed across **36 fresh native processes**, each ending through
the actual Settings Quit control. There were **191 actual load selections** and
**48 cancellations**. Two intentional read-error saves surfaced `Could not save`
in the application while preserving every slot. The captured-candidate replacement
case passed. Native logs had no warning/error lines.

The 55,249 assertion count includes repeated domain setup checks; it is not a count
of distinct faults or user scenarios. The complete matrix took 62.90 seconds here,
which is neither representative reading time nor a performance benchmark.

Each case runs prepare, recover and verify in separate processes:

1. Prepare enters C through the real Load menu, introduces its owned fault, opens
   and cancels the chooser twice, and quits with damaged disk state intact.
2. Recover starts at the normal title without silently loading a backup. The real
   chooser shows the failure, omits invalid actions and survives two cancellations.
   Valid previous A and independent current C are repeatedly selected. Each load
   restores the exact full transcript, solved gates, derived facts/items and cue
   history, without advancing, appending duplicate text or modifying slot bytes.
3. A later explicit production save rehabilitates the unusable primary. Manual
   recovery saves A; auto recovery advances once to Cue and autosaves it. A bad old
   primary never rotates over the original good backup. For the directory fault,
   the failed write preserves all slots; the fixture removes only its owned empty
   directory, then retries through the real control path. The auto retry first
   explicitly reloads A, preventing duplicate progression.
4. Verify starts another native process, repeatedly loads the new primary and valid
   previous A, and proves original good backup bytes remain intact. All processes
   preserve the immutable fixture archive and canonical source.

For both-invalid pairs, selecting intact other C is an explicit choice, not an
automatic fallback. A subsequent manual save or final evidence completion creates
a valid target primary while retaining the existing damaged previous bytes. With
all four slots invalid, the chooser has no recovery action. A fresh process can
explicitly start and save the first authored beat, with one transcript entry and
zero solved activities; it does not invent late progress or overwrite other slots.

Native bell position is checked against the authored origin plus exactly one
lowered offset. Repeated A/C/Cue recovery rebuilds cue state idempotently and does
not replay or cumulatively apply that visual event. No audio-event claim is made.

## Offline validation and reproduction

The warning-free native build, **4,107 core assertions**, **63 story structure
checks** and real Editor suite passed. Editor markers include cold/rapid selection,
character/story authoring, selected-beat launch and the **11-scene roundtrip**.
The source digest remains
`0e08db51fb0b4d8c90dab1d816ab5e14245ad23216f0e9115ffe3bbf7f7f83f7`.
Python compilation and `git diff --check` also passed.

Restore/build using the already approved matching official offline packages, set
`GODOT_MONO`, then run:

```sh
"$GODOT_MONO" --headless --editor --path . --import
python3 integration/qa/late_recovery.py
dotnet run --no-restore --project tests/Lanternwake.Tests.csproj -- Content/story.json
dotnet run --no-restore --project integration/story-validation/StoryValidation.csproj -- Content/story.json
"$GODOT_MONO" --headless --editor --path . -- --editor-roundtrip
```

Each fault gets a new owned temporary root shared by only its three processes.
The runner fingerprints tracked source and original checkpoint archives between
phases, bounds native waits, and cleans only owned process groups. It removes its
temporary data after each case; logs/summary remain under ignored
`artifacts/late-recovery/`. The diagnostic scene is not referenced by Main and adds
no production launch flag. All assertions exercise the exact source recorded above.

## Limits and handoff

This task uses no actual player data, microphone, provider or network client. The
offline checks do not constitute a new full aggregate run: the aggregate's loopback
client contracts retain their prior milestone scope. No inference configuration,
credential, permission or network setting is changed. No audio source or frozen
audio publication is touched.

Headless button/signal/focus and native scene checks do not establish human visual
accessibility, physical keyboard/audio behavior, a five-hour human reading time,
power-loss durability or multi-writer persistence. The faults are deterministic
owned fixture mutations, not corruption of user storage. Optional long-conversation
transcript qualification remains the next existing runnable non-audio gap.

The candidate stays local under this task's no-network constraint, with a real
parented commit and review bundle/patch for coordinated integration. No remote
publication, PR7 update, merge or external review request is performed.
