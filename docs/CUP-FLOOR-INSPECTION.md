# Visible fallen cup and a bounded object insert

Rendered inspection of frozen production source `33aa468` found the fallen cup
unreadable in the keeper-house wide view. Native bounds over that exact scene
then confirmed a geometry error: the floorboards top at approximately `0.095`,
while all nine fallen meshes end below it (shards `0.0625`, pencils `0.053315`,
handle `0.057990`). Visibility flags alone had not caught that occlusion.

This separate fix follows the QA-only milestone `40d71d1`. It raises only the
four new shards, four fallen pencil instances and floor handle to the existing
floor surface. All 369 subresources and 340 existing unique node identities are
preserved. Original intact cup/pencil transforms, materials and mesh sources,
room furniture, cast placement, procedural audio, authored prose/stable IDs and
save schema remain exact. No physics, player-triggered break or new plot event
is introduced.

The item/stage bible already calls for a close object insert and a safe viewing
angle. An optional editor-authored `CupFloorCamera` supplies a static cut only
at the existing `cup_broken` beat (`ch2_s5_b012`). `GameView` passes the current
beat's existing cue separately from cumulative object-state cues. Other beats,
earlier/later recovery, replay and scenes without the optional camera use their
original StoryCamera. Historical break state cannot pin later beats to the
insert. This introduces no camera drift, interpolation, shake or timed pause;
the cut works identically with reduced motion enabled.

The native cup qualification now checks the current camera at every keeper-house
observation and checks all fallen mesh bounds against the floor. Existing full
path, earlier/current Load, four fresh-process saves/previews, later visits,
content-note overlays and replay checks remain in place. Rendered qualification
also checks the nine mesh centres project above the reading panel at the break,
captures the actual pixels, and exercises production Settings/Quit with synthetic
X11 input. Before/after images and exact baseline scene bytes are in the evidence
packet. Mesh-centre projection alone is not an occlusion proof; the resulting
image is inspected separately.

```bash
bash scripts/verify.sh
python3 integration/qa/graphical_inspection.py --display-state /absolute/owned-display/display.json --case house-broken
```

The first camera draft had unsuitable orientation and was rejected after image
inspection despite passing input checks. The corrected static camera shows blue
fragments, fallen pencils and the detached handle above the reading panel. That
draft and the failed too-early baseline probe are retained as rejected fixture
evidence; their errors are not counted as successful qualification. The accepted
native baseline probe defers until the scene is inside the tree.

The delivery manifest records final checks, exact source/tree, source/binary/PDB
bindings and honest limits. Software-rendered scene inspection and Dummy mixer
PCM do not establish human art/accessibility acceptance or physical hearing.
Export remains blocked by the missing exact Godot .NET templates recorded in
[the release qualification](GRAPHICAL-RELEASE-QUALIFICATION.md). Provider quality,
speech and owner platform/distribution decisions retain their existing gates.
