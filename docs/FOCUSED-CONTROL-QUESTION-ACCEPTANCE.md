# Focused controls and first evidence question, 2026-10-05

Separate successor branch: `qa/caption-interaction-acceptance-20261005`, based on
preserved `dcb0e0b1933b0a29a03a514093d5a7b6e0cfb6c5`, tree
`c6a5aae7f925a79daf27fbfaa32021fdcc8c818e`. Remote main/PR12 and the earlier
QA branches were inspected and remain unchanged. No repository/ancestor AGENTS.md
or checkout `.agents/skills` instructions were present. Parent owns integration;
this work opens no PR, merges nothing and publishes no release.

The production fix addresses a specific pre-existing keyboard fault confirmed by
independent review and then reproduced in the normal game. On dcb0e0b, a fully
revealed passage at `ch1_s1_b021` has Save focused through Tab. Enter key-down
advances to `ch1_s1_b022` and creates its autosave; key-up activates Save and writes
that unintended next beat. A native capture independently shows the Save focus
underline. The failed receipt, original DLL identity, keyboard log and resulting
manual/automatic slots are retained as the negative regression.

Global Space/Enter advance now runs only when the viewport has no focused GUI
control. Focused controls retain their ordinary GUI activation. Continue still
uses the earlier single-activation `_Input` path, and conversation Escape retains
the earlier before-LineEdit handling. Text-field Space and Enter submission are
preserved. No scene, prose, answer, save format, provider, audio, gamepad mapping
or unrelated UI behavior is changed. The unfocused global branch is retained in
source; the new full-game assertions concern focused controls, text entry and
Continue rather than claiming a separate unfocused-device test.

## Caption finding corrected

The earlier report described incomplete captions in root PNGs after inspecting
their displayed previews. That interpretation was wrong: the original saved
files contain the complete glyphs. OCR and original-detail inspection show all
three suggestions, Return and Continue. Compared with a complete native capture,
the older disputed root image has identical bright-text masks in all five
regions; raw RGB differences are only 1–2 channel levels, not missing letters.
The earlier root-capture/game-rendering defect claim is retracted.

The minimal [caption comparison](../integration/qa/caption_compare.py) repeatedly
opens a normal saved conversation, types actual text, submits its labelled
authored fallback and surrounds the existing Debug F12 native viewport capture
with two independent desktop root captures. The caption strings remain stable;
the changed F12 status line and animated scenery are excluded from comparison.
On unchanged dcb0e0b, 18 native/36 root captures have zero differing mask pixels
in the five caption regions. On the input-fix candidate, 9 native/18 root captures
also match exactly. Adjacent frames are not claimed to be simultaneous, and
this evidence does not establish every frame or physical GPU/platform behavior.
No font, renderer or capture-source production change is justified by these data.

## Direct interaction evidence

The [focused-control regression](../integration/qa/focused_control_input.py)
loads a byte-bound checkpoint produced by the earlier ordinary player session.
It tests Tab-focused Save, History and Talk with both Enter and Space, short
press/release and held keys that exercise X11 server autorepeat. Each target's
native gold focus underline is checked. Press/held-repeat phases preserve save
bytes; Save also preserves its modification timestamp until release. Release
actually writes the intended manual slot without changing beat/history or
creating an advance autosave. History/Talk open and close without progress/save
changes. Real text-field spaces, one Enter-submitted authored fallback and one
focused Continue advance pass. Raw X11 key-event logs substantiate delivered
repeat events; these are synthetic desktop events, not physical input or gamepad
acceptance. The final run passes 83 checks with 124 injected events; its independent
X11 observer records 204 key presses and 280 releases, including delivered repeats.

The useful planned test gap is the first mandatory evidence question,
`ch1_s1a_evidence`: all-question callback coverage already existed, but the desktop
keyboard/default-focus/retry sequence was uncovered. The
[normal desktop question test](../integration/qa/question_desktop.py) makes 74
actual Continue transitions from the verified conversation checkpoint, checking
every reached ID and exact authored prose. At 150% reading size, initial Enter
reviews known evidence instead of answering. Three catalogue/return cycles,
record review and question cancellation preserve the exact unanswered slots.
A deliberately wrong choice shows the existing gentle retry copy without saving
or advancing. The canonical correct choice displays its authored explanation,
solves only that question in autosave, preserves the explicitly unanswered manual
save and enables exactly `ch1_s2_b001` next. This run passes 259 checks.

The [fresh-process check](../integration/qa/question_resume.py) copies that actual
unanswered manual save into new owned userdata. Explicit Load preserves its
bytes; the new session starts at 100% despite the preceding session's 150%.
Default Enter still reviews without answering, Escape returns to the same
question, and the explicit correct answer/progression work with the manual
snapshot retained unchanged. It passes 18 checks. This is a genuine normal
project process, not a selected author preview or forged story checkpoint.

All accepted processes use official Godot `4.6.3.stable.mono.official.7d41c59c4`,
.NET SDK `8.0.425`, normal engine time, a private authenticated owned dummy Xorg
display, Mesa llvmpipe, 30-FPS frame cap and Dummy audio. They supply no smoke,
alternate-scene or injected Godot script flag and invoke no production reflection.
The [desktop observer](../integration/qa/normal_player.py) reads pixels, OCR and
files, emits X11 inputs and restores any pre-existing Debug capture bytes.
Automatic advancing is not human reading or duration evidence. Every accepted
process exits through production Settings Quit with status 0.

The native aggregate and Debug/ExportDebug/ExportRelease builds also cover this
production candidate. The clean aggregate exits 0 without warning/error lines,
including the 13-scene Editor roundtrip and 32 Dummy mixer assertions with 20,480
PCM frames. All three managed configurations compile with warnings as errors.
Compiler-document checks bind each retained DLL/PDB to the
working source, including all 48 tracked compiled C# files. Managed builds are
not standalone players. The compact [receipt](evidence/focused-question-20261005.json)
and local `/workspace/lanternwake-caption-question-20261005.tar.gz` packet bind
source, assemblies, raw captures, actual saves and native logs to the final Git
checkpoint. Generated WAVs and binary evidence stay out of Git; the large cached
templates and private Xauthority are excluded from the packet.

Calibration is distinguished from product failure: full-page OCR omitted a
visibly complete 100% dialog title in one fresh-process attempt; a narrowly
observed title-region read corrected the observer, and the repeat passed. The
initial native import regenerated five missing, untracked UID sidecars and
logged warnings; that attempt is retained separately from the clean repeat.
An initial comparison used a deprecated Pillow getter; its exact controller is
retained, and the delivered version uses the installed supported getter. None of
these observer/import changes is presented as a production UI repair.

Reproduce with the official engine/SDK on PATH and installed Xorg dummy,
ImageMagick, Tesseract, Pillow, Xlib/Xtst and xev. Start the owned display with
`integration/qa/owned_display.py`, then run the selected controller with
`--display-state`, fresh `--fixture`/`--output` paths and `--seed` pointing to the
byte-verified, genuinely produced normal-session save. The packet retains those
seed/snapshot bytes. Stop the owned display afterward. Raw full-resolution PNGs
and their pixel measurements take precedence over an image preview interpretation.

Remaining acceptance includes human five-hour/editorial/emotional playtesting,
physical input/display/accessibility and hearing, real loaded-model inference
and in-flight cancellation, microphone/ASR, platform/low-end and gamepad coverage.
No gamepad bindings are added or claimed. Standalone export remains deferred;
cached official templates were not used. No Library upload occurred; separate
approval is still pending. Parent retains merge/release decisions.
