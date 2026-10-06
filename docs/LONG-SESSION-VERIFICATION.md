# Offline story save/resume qualification

Branch: `qa/non-audio-save-resume`, based on exact published non-audio QA commit
`21a8eaeeaf9bf42cc6fd99f1b5a40af1850673f6`, tree
`25c480438aa37617d89a453b35473417f84e097a`, with real PR7 ancestry
`65548f97e1c203884864bf4dcc1d1849db4cbcd1`.

The parent reported genuine native graphical acceptance for that exact input:
cold/reloaded narrow-dock selection, rapid navigation and draft guard, reading
sizes/reset/fresh-session defaults, required reading surfaces, keyboard close/Quit
and unsent chat draft preservation. This worker did not perform that visual review.
This follow-up changes qualification fixtures and documentation only. Existing
production source, scenes, resources, project settings and canonical content are
unchanged; original QA and feature branches remain frozen.

## Gap selected from the existing plan

The narrative acceptance in `docs/bible/05_PACING_ACCESSIBILITY_QA.md` calls for a
complete offline main path, wrong answers before correct answers, optional chat
skipped, save/resume without narrative penalties and preserved stage cues.
Existing core/smoke tests traverse all beats chiefly in one domain instance and
save at the end; the normal UI smoke covers representative early interactions.
The selected gap was therefore full production-button progression with continuous
autosaves, repeated inspected loads, earlier-checkpoint recovery and fresh native
processes through terminal completion.

The story has one deterministic authored timeline. The exercised alternatives
are wrong-answer/retry, earlier-snapshot recovery and current-snapshot continuation;
this does not imply alternate authored endings or a new relationship mechanic.

## Executed result

Official Godot 4.6.3 .NET and .NET SDK 8.0.425, Linux headless, debug build:

| Fresh native process | Required beats | UI/fresh-domain resumes | Wrong retries | Earlier-beat recoveries |
| --- | ---: | ---: | ---: | ---: |
| Chapter 1 | 327 | 71 | 5 | 20 |
| Chapter 2 | 289 | 69 | 5 | 19 |
| Chapter 3 | 315 | 64 | 4 | 18 |
| Chapter 4 | 257 | 66 | 5 | 18 |
| Chapter 5 | 236 | 60 | 4 | 16 |
| Reopen completed watch | 0 | 1 | 0 | 0 |
| Total | **1,424** | **331** | **23** | **91** |

All 41 authored scenes were reached. There were also 23 explicit previous-manual
recoveries from solved to unsolved versions of the same evidence gate. The fixture
performed 25,191 assertions; this is a repeated integration assertion count, not
25,191 distinct scenarios. All six processes exited zero through the actual
Settings Quit action. The final six logs contained no warning/error lines.
No reachable production defect was demonstrated, so no production repair was made.

Each required beat advances through the actual Continue signal and production
`GameView` callback. The fixture checks exact rendered text, canonical transcript
order, derived facts/items, scene label, actual instantiated stage and living cast.
Every Continue's real autosave is read and compared with current state. Every
evidence gate is manually saved, reopened, answered incorrectly with unchanged
state/save bytes, then answered correctly and checked for solved-state autosave.
Its previous unsolved manual snapshot is explicitly selected through the real
Load menu, followed by the current solved autosave.

At scene boundaries, every 50th beat, the bell cue and chapter handoff, manual
save/resume exercises the full growing transcript. Distinct previous manual beats
are selected before returning to the current checkpoint, verifying removal of
later solved gates, facts and items. Every selected snapshot is also restored in
a newly constructed production `StorySession`. Loads and menu inspection preserve
all four primary/previous save files byte-for-byte. Actual bell position is checked
against the authored origin and lowered offset, including earlier recovery across
`ch5_s3_b006` and subsequent return to the lowered state.

Long History/Catalogue windows are opened and closed while preserving progress.
All 28 optional conversation points are skipped. Chapter handoffs persist at the
first beat of the next chapter, then another native process loads that exact
manual snapshot. Fresh process reading size starts at 100%, then the existing
125% and instant-reading controls are exercised. The final gate is solved before
the actual Finish route; ending state is stable, all 23 solved gates remain, and a
sixth process reopens and finishes the completed watch again.

Canonical JSON SHA-256 remains:
`0e08db51fb0b4d8c90dab1d816ab5e14245ad23216f0e9115ffe3bbf7f7f83f7`.
The runner fingerprints all tracked files between processes. Successful saves
leave no pending staging files. Normal `SessionStorage` mode uses its real primary
and previous-slot policy inside runner-owned temporary XDG storage, which is
removed afterward. No existing player files are addressed.

The existing complete aggregate and deliberate validator-failure probe also passed:
4,107 core assertions, 63 story structure checks, 11 simulated Pumas contracts,
unsupported-speech checks, native build/runtime/UI/save isolation, Editor authoring,
selected-beat launch and 11-scene roundtrip. The final build had zero warnings/errors;
the final aggregate had no Godot warning/error lines. The deliberate failure probe
propagated the real validator's status 134 and prevented downstream dotnet checks.

An initial version passed the shorter 148-resume path, with fresh-worktree UID
fallback warnings and one fixture nullability warning. After Editor import and
correcting that fixture warning, the final expanded 331-resume run above passed.
The final run took 91.94 seconds in this environment. This is accelerated automated
progression, not representative reading time or a performance benchmark.

## Reproduce

Restore/build the Debug project with the matching official Godot .NET packages,
set `GODOT_MONO` to the official executable, then run:

```sh
"$GODOT_MONO" --headless --editor --path . --import
python3 integration/qa/long_session.py
bash scripts/verify.sh
python3 integration/story-validation/verify_aggregate_failure.py
```

`qualification/long-session.tscn` is an explicitly selected diagnostic scene,
not the main scene or a new production launch flag. Its C# controller is Debug-only
and requires the runner's owned-fixture marker and temporary user path before it
instantiates Main. It observes private fields by reflection for assertions and
drives public Godot buttons/modal actions; it does not rewrite production state or
skip gates. Direct domain calls are fresh-instance restore and a terminal no-advance
assertion. Python bounds each process, cleans only its owned process groups and
keeps logs/summary under ignored `artifacts/long-session/`.

## Remaining existing-plan work

- Next runnable non-audio path: longer optional **authored fallback** conversations,
  edited/Unicode inputs, repeated save/resume of transcript provenance and earlier
  loads that remove later optional memory. This run intentionally skipped optional
  chat; existing short UI/core tests are not that complete native path.
- Another useful deterministic combination: late-session damaged-primary recovery
  with the full transcript through the real displayed Load candidate, rather than
  only isolated corruption/fault fixtures and representative short recovery UI.
- Production model characterization/spoiler resistance remains separate. The 11
  contract cases are simulated; this run makes no inference request or quality claim.
- Representative reading/editorial playtests, human duration assessment, physical
  input/accessibility tools, platform/export/localization and low-end performance
  remain existing release-plan work. Neither this run nor the parent graphical
  milestone establishes a human five-hour median or a release platform matrix.
- Audio stays frozen pending parent-owned publication approval. No transfer retry,
  audio edit, PCM playback or physical audio acceptance is part of this branch.
  Speech stays gated on the published typed Pumas/Cohere producer contract; no
  endpoint, microphone, recognizer, credential or permission change is introduced.

The branch is for coordinated review only. No PR7 update, merge or external review
request accompanies this qualification.
