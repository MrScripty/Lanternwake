# Family preview launch oracle — 2026-10-06

Separate review repair `fix/family-preview-oracle-20261006`, based on preserved
`c8b86bcf71a0f698ae6c84ce3df7a1eddd79cc35`. Production scripts, content,
scenes, save behavior and the original family evidence are unchanged.

The native fixture now checks the explicitly expected player/preview mode before
loading or choosing. The Python runner requires exactly one success marker and
its parsed `preview` boolean must match the requested mode. It also verifies
the preview run preserves every JSON slot byte in the owned player fixture.

Executed with .NET 8.0.425 and Godot 4.6.3 mono:

- Build/import pass, with no build warnings/errors.
- Player passes 71 checks; actual author preview passes 21 checks, with matching
  parsed mode markers and unchanged preview slot bytes.
- A third launch deliberately omits preview arguments while expecting preview.
  It exits nonzero at the mode assertion before loading/selection, emits no
  success marker, and preserves every owned JSON slot byte. The runner accepts
  this only as the expected negative result.

The earlier reported 20-check preview result remains consistent with correct
launch behavior; this repairs its test oracle rather than a production failure.
See the [derived receipt](evidence/family-preview-oracle-20261006.json).
Raw logs remain local in
`/workspace/lanternwake-family-preview-review-evidence-20261006`.
No acquisition/Library retry or raw upload occurred. Parent owns main, PRs,
merges and publication; the reviewed source refs remain frozen.
