# Header contrast over bright stage geometry

The accepted floor insert at `852792c` put bright floorboards behind the gold
chapter label and gray scene label. The main reading panel and clues remained
clear. This follow-up wraps only the two header labels in the interface theme's
existing dark PanelContainer style, with a small padding container. It retains
the label colors, fonts, authored text, unique-name bindings, adjacent controls,
stage cameras, story, saves and audio. No new palette or runtime behavior is added.

Native before/after captures cover the keeper-house wide view and floor insert
at 100%, 125% and 150% reading sizes. The setting changes the main text to
21/26/32 points; existing header fonts remain 21/15 points. The fixture uses real
software-rendered pixels and synthetic X11 Settings/Quit input, checks exact
prose/header/camera preservation and label/control fit, and samples the actual
dark backing. Six before and six after images are inspected separately from the
layout/pixel assertions. Both views exit through production Quit without an
unexpected warning or teardown leak. The dummy driver's unsupported-VSync warning
is an environment limitation, retained in each log.

Focused native UI smoke and 13-scene Editor save/reopen checks qualify the scene
change. The evidence packet records exact source/tree, source/DLL/PDB bindings,
captured PNGs, native logs and source fingerprints. No new full core/audio run
is claimed for this scene-only change. This is rendered readability evidence,
not physical-display or accessibility-standard conformance certification.

Reproduce on an owned authenticated display after normal setup/build/import:

```bash
python3 integration/qa/header_contrast.py --display-state /absolute/owned-display/display.json --phase after
```

`--phase before` records the accepted source before this scene edit; it does not
require a backing that was absent. Recovery uses the exact accepted parent Git
commit; no synthetic history, PR, review request, merge or release is created.
