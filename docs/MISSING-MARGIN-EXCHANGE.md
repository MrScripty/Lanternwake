# The Missing Margin: choosing an approach

In Chapter 2, after Ada and Tomas preserve Ivo's omission note, Speak with Tomas
offers three authored intentions: preserve the wording, bound the inference, or
acknowledge anger without editing the record to suit it. Each receives a distinct
Tomas response. Reopening reads the selected pair rather than offering another
choice. Escape before choosing leaves the passage unspoken; Continue rejoins the
original conversation and subsequent documents whether the player speaks or declines.

The optional interaction at ch2_s4_b019a follows copying the whole page at
ch2_s4_b016 and precedes the unchanged ch2_s4_b020 free-response invitation. It
adds no evidence, score, disclosure gate or alternative ending, grants no forgiveness
and confirms no later testimony. The existing Chapter 2 route comparison remains
available independently.

These choices are authored content. The later free-response conversation retains
its actual capability notice and authored fallback when inference is unavailable.
The existing v2 transcript/save format retains one chosen pair: selection autosaves,
manual reload restores it, and older saves beyond the added beat continue without
backfilling a choice.

## Verification

Core regressions cover all three intentions, exact JSON retention, declining,
fixed continuation and old before/after/completed saves. After setup/build, run:

```sh
python3 integration/qa/missing_margin.py
```

The native fixture checks 44 conditions through cancellation, repeated entry,
stale selectors, autosave/manual reload, same-beat interrupted load, honest
free-response state and Return. Its timeout reports captured output. The full
verification suite retains both route-reconstruction and Missing Margin tests.
The character-performance fixture's expected beat count includes the optional
insertion; its pose/restore assertions are unchanged.

Six rendered Linux Main sessions exercised all three intentions and fresh manual
reloads, actual authored fallback submission, Return, original continuation and
normal Quit. The native checks additionally cover interruption/re-entry and exact
autosave retention. Rendered checks used synthetic input on an owned X11 display,
compatibility rendering and Dummy audio. This establishes control/save behavior,
not production inference, physical hearing or human editorial acceptance.
