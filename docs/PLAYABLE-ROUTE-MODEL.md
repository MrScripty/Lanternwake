# Playable route comparison

At the unanswered Chapter 2 question after **A Route Made of Names**, choose
**Explore route model**. Move the rescue block through the issued eastern route,
compare the corrected western approach, retrace or reset the blocks, and read
the supporting passages. Back to question restores the same unanswered activity.
The existing correct answer and fixed story continuation remain unchanged.

This implements the labelled-block comparison described in
`docs/bible/03_ITEM_STAGE_SOUND_BIBLE.md` and the canonical `ch2_s3a` scene. The
west workboat stays at its documented mooring. The other unlocated reference
blocks stay off the chart. The issued route stops at the exposed turn; the
corrected approach stops at a conditional decision point. No scale, exact rescue
departure, fatal moment, safe arrival or guaranteed alternative ending is invented.

The model is optional and has no timer. Its position is temporary: exploration
does not advance a beat, solve a gate, add a transcript/fact/item, call a model,
or write a save field. Loading or reopening starts a fresh comparison. Source
reading preserves the current comparison until returning to the question.

Buttons work with mouse or Tab/Enter. The board labels inherit the owner's theme
and reading-size setting. The action scrollbar accepts Home/End/Page Up/Page Down
to read the board above the controls at small sizes. Focus moves to an enabled
control when a route reaches its last supported position.

After preparing a Debug build, run `python3 integration/qa/route_model.py` with
`GODOT_MONO` pointing to the installed official Godot .NET executable. The native
fixture uses the real question/model/source controls, 150% reading size and a
640x480 window. It checks bounded movement, source text, cancellation/load,
retired callbacks, exact snapshot/save preservation and ordinary continuation.
Core tests cover route rules, availability and v2 snapshot compatibility.
`scripts/verify.sh` includes both the new fixture and the existing all-question
review, gameplay, save, audio and editor checks.

Initial qualification on main `af93c24e3f8e0ca64af8d4a46847536462c39867` passed
17 new core assertions (4,552 total across 1,439 beats), 27 native model checks,
the complete verification suite and Debug/ExportDebug/ExportRelease builds with
warnings treated as errors. A rendered normal game on an owned authenticated
X11 display passed actual Load, model movement/comparison, Tab/Enter source
reading, Escape return, the original correct answer, Continue and Quit input.
The manual save stayed byte-identical; an autosave appeared only after answering
and continuing. That run used explicit OpenGL compatibility and Dummy audio;
it qualifies normal rendered controls, not Forward+ graphical editor play.

The owner startup menu, UI scenes, AI settings and canonical story bytes are
unchanged. Provider inference, Pumas audio, physical hearing and graphical editor
play are separate qualifications; this feature requires none of them.
