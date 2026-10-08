# The Missing Margin: choosing an approach

In Chapter 2, after Ada and Tomas preserve Ivo's omission note, **Speak with
Tomas** offers three authored intentions: preserve the wording, bound the
inference, or acknowledge anger without editing the record to suit it. Each
receives a distinct Tomas response. Reopening reads the selected pair; it does
not offer a second choice. Escape before choosing leaves the passage unspoken.
Continue rejoins the original conversation and subsequent documents, whether
you speak or decline.

This develops the existing `ch2_s4_b020` topic and suggestions into a local
retained choice at `ch2_s4_b019a`, using the story bible's fixed action spine,
Ada's professional approach and Tomas's source-grounded role. The whole page
has already been copied at `ch2_s4_b016`. No intention hides evidence, releases
it publicly, grants forgiveness, invents a reason for Ivo's action or reveals
later testimony. There is no morality score or alternative ending.

The original optional free-response conversation follows unchanged. Its actual
AI capability notice and authored fallback remain visible when inference is
off or unavailable. These written choices are authored content, never fixture
AI or generated dialogue. They use the existing v2 transcript/save format:
selection autosaves, manual save/reload retains the exact pair, and old saves
already beyond the added optional beat continue without backfilling it.

Core regression tests cover each intention, exact JSON retention, no extra
evidence or stage changes, fixed continuation, declining and old before/after/
completed saves. `python3 integration/qa/missing_margin.py` exercises actual
native controls for cancellation, repeated entry, stale selectors, autosave,
manual reload, same-beat interrupted load, the honest free-response state and
Return. `scripts/verify.sh` includes this fixture. Rendered normal-game controls
are qualified separately; headless controls do not establish physical input.

Initial qualification passed 27 new core assertions (4,564 total across 1,440
beats), 44 native flow checks, the full verification suite including the
13-scene headless editor roundtrip, and all three Godot build configurations
with warnings treated as errors. Six rendered normal sessions exercised all
three intentions, interruption/re-entry, fresh manual/automatic reloads,
actual authored-fallback submission, Return, original continuation and clean
Quit. They used an owned authenticated X11 display, explicit OpenGL
compatibility and Dummy audio. No production inference, physical hearing or
graphical editor-play acceptance is claimed. The character-performance
fixture's exact beat count is updated by one for the added optional beat;
its pose/restore assertions remain intact.

This candidate starts from main `af93c24e3f8e0ca64af8d4a46847536462c39867` and
requires neither PR28 nor PR29. The earlier route model in PR29 can coexist
without a code dependency. Existing owner UI scenes, startup menu, AI provider
configuration and runtime exchange implementation are unchanged. Human
editorial/pacing review, production model quality, speech, audiovisual and
platform acceptance remain separate work toward the intended game.
