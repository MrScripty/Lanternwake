# Reading text size

Reading settings now offer smaller/larger text and reset, at 100%, 125% and 150%
of each control's authored font size. Default/reset is 100%; the existing shared
theme remains 21-point prose and 16-point buttons. A size change applies in place,
keeps settings open and moves focus to Reset if a focused size action reaches a
disabled limit. Instant text, reduced motion and Quit retain their existing routes.

Dialogue, history/catalogue/modal prose, evidence choices, suggestions and editable
reply text scale. Navigation buttons and compact HUD labels keep their authored
sizes. Suggestions and modal actions have scrolling containers with focus-following
so enlarged or long choices stay reachable. Modal prose retains its own scrollbar.
Reset restores the original inherited font or an existing per-control override.
Changes do not compound, and newly opened reading surfaces use the selected size.

This preference lasts for the current game session, matching the existing reading
settings. A fresh game uses 100%. Nothing is added to schema-1 saves or written to
preference files; cross-launch persistence is not claimed. Scaling preserves the
current beat, revealed character count, text, draft/caret and dialogue scrollbar
offset. Reflow can change line breaks; an unchanged scrollbar offset is not a
promise that exactly the same glyph occupies the top pixel after reflow.

## Qualification

Source base: exact PR7 `65548f97e1c203884864bf4dcc1d1849db4cbcd1`.
Branch: `feat/reading-text-size`. Provisioned .NET 8.0.425 / Godot 4.6.3 .NET.

One complete `scripts/verify.sh` execution returned zero: 4,107 core assertions,
11 simulated Pumas contracts, unsupported-speech checks, native build, authored
story traversal, UI/save-isolation and Editor authoring/launch/11-scene roundtrip.
After adding explicit existing-setting/quit-route assertions and removing action
index assumptions, a final native build and UI smoke passed. No source changes
followed those checks other than documentation. The final build reported zero
warnings/errors; the final UI run had no warning/error lines. The aggregate's
earlier fresh-worktree runtime passes emitted known invalid-UID path-fallback
warnings before Editor import, which are not hidden as warning-free evidence.

The aggregate now requires both new runtime markers:

- `LANTERNWAKE_READING_SIZE_OK`: min/max, repeated changes, reset, reopening,
  authored overrides, unchanged reveal/scroll/state/source/save, Unicode reflow,
  enlarged choices in a 640 x 480 modal, focus and reachable Close.
- `LANTERNWAKE_READING_CHAT_OK`: enlarged scrolling suggestions, focus, unchanged
  editable draft/caret and reachable Return.

These are actual Godot control/signal/layout checks using disposable test storage
and temporary presentation strings. They do not establish graphical appearance,
physical keyboard or accessibility-tool coverage, an entire small-window platform
matrix or a human playthrough. Native visual/default-appearance review remains
separate. No audio playback, microphone or production ASR claim is made.

## Deferred combination with reviewed branches

This independent branch does not include the audio candidate or the reviewed
story-validation/authoring branches. Do not replace their commits with this tree.
The shared story theme, canonical JSON, core/session/save code, `RenderBeat`, load,
production shutdown and speech implementation are unchanged.

Audio's known conflict is its added `("Sound settings", ShowAudioSettings)` action
in the old `GameView.ShowSettings`. Preserve that entry when combining, placing it
in `GameView.ReadingSettings.cs`'s action list. Reading controls are identified by
their labels rather than list indices, so the sound action can retain its intended
order. Do not change audio partials, gain/mute state or its resource IDs `7_audio`
and `8_audio_settings`. No new Main scene export/resource ID is introduced here.

Other deliberate integration points: `GameView` binding, suggestion creation,
generic modal creation and UI-smoke hooks; `GameInterface`'s Suggestions parent;
`ModalWindow`'s ModalActions parent; and the two added aggregate success-marker
gates. Unique control names are preserved. Deferred layout callbacks capture their
original window and ignore retired/non-reading windows, avoiding accidental work
on a replacement audio modal. Preserve the aggregate branch's additional 63-check
story-validation invocation when composing `scripts/verify.sh` later. Independent
combined-tree build, runtime, audio and visual qualification is still required.
