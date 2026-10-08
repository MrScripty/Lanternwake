# First inventory: repaired cup insert

Branch `feat/cup-inventory-insert-20261005` starts at preserved keyboard-reading
checkpoint `f4838546eb6da5fd9269349084e54a668bdee9fa`, tree
`7d414030dbe40f55deb0fe45770fecba2ff48f82`. Remote main remains
`3051855bb0c8f4a6d222081f22d00a19c3298572`; PR12 remains
`05db466507dfbc736d7b0a386724a5fe068554b9`. Parent owns integration.

## Selected feature and acceptance criterion

The [item/stage bible](bible/03_ITEM_STAGE_SOUND_BIBLE.md) calls for close views
of catalogued objects in the keeper's house. Ada's first table inventory,
`ch1_s2_b008`, previously used the wide room view: the cup was small and its repair
could not be read. This bounded sequence makes the ordinary repaired cup visible
before its later prophecy and break, using existing art and materials.

Acceptance: wide view at `ch1_s2_b007`, a static close view at `ch1_s2_b008` with
cup body, repaired handle, adhesive seam and pencils above the reading panel,
then wide view at `ch1_s2_b009`. Loading the inventory restores the insert;
recovering its preceding save restores the wide view. Larger reading text, reduced
motion, record/catalogue, question progression and the later cup-break insert work.

## Implementation

KeeperHouse owns an editable orthographic `CupInventoryCamera` and the explicit
inventory beat ID. GameView forwards the current beat ID through StageDirector;
StageScene chooses the close view only for that current intact-cup beat. The
existing broken-cup insert retains priority and the default camera handles all
other beats. Cumulative object state does not prolong the inventory shot.

The existing adhesive-seam marker was entirely inside the cup body. Moving its X
position from 0.27 to 0.305 exposes it at the upper handle joint. No mesh, material,
dialogue, save schema, input/reading handler, sound or external integration is added
or changed. The procedural prop still depicts four pencil meshes against the
authored six-pencil description; this existing stylization is outside this insert's
scope. The scene's existing subresources and node IDs are preserved.

## Verification and limits

The [external normal-player controller](../integration/qa/cup_inventory.py) uses
the bare Godot project, synthetic X11 input and observed pixels/save files. It loads
the actual preceding player's unanswered first-question save, answers that question
through the keyboard and advances to the inventory through ordinary controls.
It observes wide/insert/wide, opens both readers, checks 150% prose and reduced
motion, loads the inventory, recovers its preceding wide view, and repeats the
insert. Blue-cup pixels remain identical across settings/load/repeat; settings,
readers and recovery preserve every save byte. A separate fresh normal process
loads the actual inventory manual save and advances to the following wide view.
The final normal process passes 38 checks with 62 input events; the separate fresh
process passes nine checks with 17 events. Both exit through production Settings
Quit with code 0 and clean native logs. The actual inventory manual save retains
the preceding 97-entry history exactly and appends eight canonical beats, with
only the first reached question solved. Final hashes are in the
[receipt](evidence/cup-inventory-20261005.json).

Selected author-preview checks use an injected QA script and are separate from
normal-player evidence. Native projection checks place every AABB corner of the
seven existing cup/handle/seam/pencil meshes above the dialogue panel. Raw native
PNGs were inspected for the actual repair pixels; projection alone does not prove
visibility. The later broken-cup preview is retained. Headless cup qualification
checks current-beat camera choice across the full authored path, recovery and
replay, and checks that the seam reaches beyond the cup surface.

Debug, ExportDebug and ExportRelease are managed builds, with DLL/PDB compiler
checksums bound to all 48 tracked compiled C# sources. They are not standalone player
exports. `bash scripts/verify.sh` exits 0: 4,125 core assertions, 63 story checks,
11 simulated Pumas cases, unsupported speech, native full story/UI/save/reading/
completion/question/return/content-note/cup checks, and 13 Editor scene roundtrips.
The complete cup fixture passes 4,926 checks, including 341 keeper-house beat
observations, plus the separate fresh recovery/preview states. These are synthetic
native checks, not a timed playthrough. Aggregate import has five known warnings
for recreated untracked UID sidecars; the subsequent focused Editor import has no
warnings/errors. Only those five generated sidecars are removed afterward.
Runs use normal engine time, official Godot
4.6.3 .NET, .NET 8, Mesa software rendering, Dummy audio and authored model fallback.

Baseline wide-view and rejected first-camera images remain diagnostic evidence:
the first camera enlarged the cup but left the seam buried. A pre-final normal run
used a DLL built before a comment-only source edit; its controller also preceded
unused-import cleanup. The final normal/fresh runs and final assemblies are the accepted source
checkpoint. Historical `productionBaseline` in preview JSON identifies the original
runner's provenance; its source fingerprint and this receipt identify this candidate.

Human art direction/editorial/duration, physical devices, hearing, accessibility
tools, real-model behavior, microphone/voice, other platforms, standalone export
and full five-hour playthrough remain unqualified. Remaining character poses,
other object inserts and detailed bell staging are not closed by this one sequence.
No Library upload, PR action, merge, account change or live publication occurs.

Reproduce the normal/fresh checks with an owned display from
[owned_display.py](../integration/qa/owned_display.py), `GODOT_MONO` and the
desktop prerequisites from the preceding acceptance report. Pass `--display-state`,
fresh `--fixture` and `--output`, and `--seed` for an actual prior normal save;
`--fresh` expects the actual inventory save. Run `bash scripts/verify.sh` for the
headless aggregate. Generated WAVs, templates and private display authority stay
outside Git and the local evidence packet.
