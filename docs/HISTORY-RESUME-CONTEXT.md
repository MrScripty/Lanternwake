# History opens at current context

Opening History after loading a late save shows the current/recent paragraph of
the complete chronological record. The History button and H shortcut set this
opening position once, after the existing modal has laid out its text. A long
final paragraph opens at its beginning; a short one can share the page with
preceding context.

The scrollbar remains freely readable. Page Up/Down, Home/End and arrows retain
their existing behavior; Home reaches the original arrival. Closing and reopening
History applies the recent opening again. Fresh/new stories have only their first
passage and start at the beginning. The shared catalogue, question-review and
completion readers retain their existing opening behavior.

The deferred callback ignores a closing game, a replaced modal or a retired
native window. Busy History is inert. This position change preserves the full
record, selection behavior, manual scrollback, current story state and save bytes;
it enables no ongoing scroll-following policy.

## Verification

After normal project setup/build, run:

```sh
python3 integration/qa/history_resume.py
```

The Debug fixture instantiates Main, loads a legitimate late snapshot through the
existing Load action and checks 47 conditions at 100% and 150% reading size. It
covers fresh story, current-context opening, the complete record, manual scrollback,
reopen, native keyboard focus/Close, exact session/save preservation, replacement
panels, busy input and freeing Main before deferred dispatch. The headless fixture
alone embeds dialogs and supplies a usable client area.

Four separate rendered Linux Main processes exercised fresh story at 100%,
ch4_s1a_b035 at 100%, ch5_s3_b006 at 150% and ch5_s5_evidence at 150%, using actual
normal-route manual snapshots and default modal geometry. Synthetic X11 keyboard
input checked recent opening, scrollback, reopen, new story, catalogue opening,
unchanged saves and normal Quit. Compatibility-renderer limits were observed.

The runner is included in scripts/verify.sh. Automated/native/rendered checks do
not establish physical-device, assistive-tool, hearing, model or human-duration
acceptance. Generated captures, audio, builds and private userdata are excluded
from Git.
