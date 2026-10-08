# Visible dialogue attribution

The story's canonical character names identify living speakers and archival
recordings. The current authored interface omits `SpeakerLabel`; retaining its
text only in `CurrentSpeakerText` and History left the primary passage unlabelled.
The normal rendered game reproduced this on main `b8434b0`, including Ivo,
the station operator and inquiry clerk. The bible requires a speaker-labelled
transcript (`docs/bible/05_PACING_ACCESSIBILITY_QA.md`).

When the authored control is absent, GameView inserts a runtime Label immediately
before the existing prose. It inherits the owner's theme and participates in
session reading-size adjustments. It wraps, ignores mouse input and adds no Tab
stop. Narration collapses the fallback label. An authored `SpeakerLabel` is reused
with its own font and visibility. Neither the scene nor the exact passage text,
story, save format, optional dialogue rules or ending changes.

Run `python3 integration/qa/speaker_attribution.py` after preparing a Debug build
with `scripts/run.py --setup-only`, using the installed official Godot .NET.
`scripts/verify.sh` includes it. The fixture exercises both omitted and supplied
speaker controls, all seven canonical names, 100/150/100 percent reading sizes,
layout bounds, recorded-dialogue Load, repeated authored fallback/Return and all
three authored intentions with autosave reload and read-only reopening. It checks
that fallback replies remain authored rather than generated and drains native
audio before freeing each real Main instance. A marked disposable profile is
required; provider selection is cleared and no model/provider request is made.

Native checkpoint selection uses canonical snapshots through production rendering
and controls; it is distinct from real X11 input and pixel inspection. Rendered
evidence is retained separately outside Git. Automated operational traversal does
not establish representative reading time, editorial pacing, screen-reader
support, physical hearing or model inference.

The earlier chapter-four full-route controller failure was an OCR filter error:
the visible Settings text was recognized with zero confidence and discarded.
The repaired controller retains exact visible caption matches within the header
and sends actual XTest mouse/key input, then checks the visible next state.
The prior 1,037-beat, 32-wrong-answer, three-resume partial evidence remains intact
and is not described as a completed route.
