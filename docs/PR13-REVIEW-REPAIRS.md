# PR13 review repairs — 2026-10-06

Separate successor `fix/pr13-review-20261006`, based on reviewed
`0eebf4e642485a6e14c62808facbc5c91d6adff7`. The guided bell descent remains
separate unfinished work; this successor contains only the three confirmed
review repairs and their qualification.

## Changes

- LiveSmoke supplies `conversation.CharacterId`, matching GameView's
  `chat.CharacterId`. The parity regression runs the actual LiveSmoke entry point
  against an owned simulated Pumas and compares its complete serialized requests
  with the production identity/context call. A display-name mutation fails the
  payload comparison. This establishes transport parity, not loaded-model quality
  or new Pumas wire qualification.
- The current testimony reconstruction controller now follows 14 prose beats and
  the existing `ch4_s2_reconstruction_evidence` gate. It rejects the unsupported
  suppression claim, solves only the supported correction, verifies Save/Load,
  then reaches `_014` and the original `ch4_s2_b007`. Historical receipts remain
  untouched. The existing 125%/150% keyboard reading checks are retained.
- `Save` returns its outcome. Authored exchange Return carries a failed autosave
  warning in the captured reading state; successful autosave retains the original
  reading status. Both paths restore the paused passage and preserve the selected
  pair without advancing. A real filesystem failure in an owned slot tests the
  response, Return, read-only reopening, Escape and successful manual retry.
  Removing the warning propagation fails the native regression.

## Evidence and limits

The combined `scripts/verify.sh` exited 0: 4,241 core assertions, 63 structure
checks, 23 simulated client contracts, 1,439 native beats, 24 activity reviews,
28 conversation Return paths and 13 Editor scenes. Native exchange player/preview
checks pass at 82/22, including real failure and successful status restoration;
the mismatched preview launch still rejects before slot writes. The exchange
checks pass again after Editor qualification. Normal-player failure and full
reconstruction runs pass 19/50 checks with clean Quit and source unchanged.
The pre/post-Editor DLL hash is identical.

Qualification identities and actual results are in
[evidence/pr13-review-20261006.json](evidence/pr13-review-20261006.json).
Raw logs and screenshots remain local under
`/workspace/lanternwake-pr13-review-evidence-20261006`; no Library upload or
alternate upload occurred. Early compilation/test-harness corrections are retained
there and excluded from passing evidence. Normal-player captures use synthetic
X11 input, a software renderer and dummy audio, not physical devices or human
hearing/accessibility acceptance.

Story, core/save format, canonical IDs/branches/ending and Pumas production
transport are unchanged. The approved model/runtime discovery blocker is not
retried. Production dialogue, speech producer contract, human art/editorial and
measured duration, platform/device acceptance remain open. Parent owns PR13
advancement, comments, review cooldown, merges and publication; no new review,
PR metadata action or merge is requested here.
