# Guided descent review repair — 2026-10-06

Separate repair branch `fix/guided-descent-review-20261006` starts from the remote-verified feature delivery `be3056b640656276b6741d08f651061e3c853ba0`. Main, PR metadata, merges and publication remain parent-owned.

## Painted marks

The high mark was at installation Y3.0, above the rim's initial top Y2.40. The rim only descends, so it could not pass that mark. The three mark centres now sit at installation Y1.95, Y1.55 and Y0.80. Their entire 0.055-unit slabs lie below the initial rim bottom Y2.22 and above the final rim top Y0.30. The half-travel phase passes high/middle and the 90% flow phase passes low, before the existing arrival cue. Native checks read the actual loaded mesh bounds and node transforms. Bell endpoints, suspension, cameras, authored beats, narration and timing are unchanged.

## Restored live state

The previous normal controllers' “exact Save/Load” assertions reread the unchanged manual file. That established file persistence, not full restored runtime state. Those historical assertions must be read with this limitation; no production restore defect was established.

The corrected controllers launch real Main through a passive DEBUG-only observation scene, enforce normal storage and an owned fixture, and act through external X11 input. The observer serializes the actual `StorySession` into an atomic file outside the player save directory. It never reads or writes player slots and performs no player actions or restores. Production restore code is unchanged.

Each Load check captures the live state before Save, then activates Continue to leave it while confirming the manual file stays byte-identical. After actual Load, the live state must equal the captured state: every SaveData field, transcript and solved activity, full known fact and inventory records, chapter/scene, cumulative stage cues, progress, ending/advance availability and authored exchange selection. Loading the seated beat after the following beat also removes that beat's newly unlocked fact and item. Slot bytes are checked independently. Native qualification uses the same full live-state representation and advances away before restoring.

The contract remains authored phase restoration. Neither observation nor equality includes elapsed animation time; loading still settles the authored phase.

## Evidence and limits

Focused native player/resume/preview runs pass 44/32/25 checks. Normal 100% motion and 150% reduced-motion runs pass 23 checks each; the isolated rapid run passes 14, reaching both following beats in 1.215 seconds. All normal sessions exit 0 through production Quit, preserve source and use the same tested DLL. Inspected descent, flow and enlarged seated captures show the marks and rim beside the guide.

Source/DLL/log/image hashes are bound in [the review receipt](evidence/guided-descent-review-20261006.json). Raw logs, captures and owned fixtures remain local under `/workspace/lanternwake-bell-review-evidence-20261006`. The retained broad suite on `be3056b` remains historical; this narrow repair reruns the affected native and graphical qualification.

Initial graphical attempts sent Return while Save held keyboard focus, so they activated Save again and did not reach the perturbation beat. They are retained and excluded from acceptance. The corrected controller activates the visible Continue control. The first rapid timing attempt, concurrent with both graphical sessions, exceeded its 1.8-second capture-inclusive threshold at 1.948 seconds and is also retained/excluded; it was rerun alone without source changes.

Synthetic X11, software rendering and dummy audio do not establish human art/accessibility, physical input, hearing, editorial or measured-duration acceptance. Loaded-model/voice and platform gates remain open; blocked runtime discovery is not retried. No Library upload occurs.

Effective author and committer were verified as `MrScripty <TheEnvironmentGuy@protonmail.com>` before the repair commit. Pre-hold `cafee825` and `3f580361` retain their original Jeremy metadata; no history is rewritten.
