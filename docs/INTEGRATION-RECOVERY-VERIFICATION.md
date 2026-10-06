# Integration repair verification

The recovery adds keyboard reading to the main passage, corrects the two reviewed
story statements, and reconciles the three documentation issues. The production
change calls the existing `EnableKeyboardReading(_dialogue)` helper in
`GameView.BindInterface`; keyboard ownership, save schema, story IDs, branches,
activities, stage scenes and cues retain their existing implementation.

The remote base was verified as
`17a4cc679ba6b1a2255644c55035d797fab60a44` on
`feat/cup-inventory-insert-20261005`. The repairs are committed as
`038ad35747c3adaff4fea4f6a81b3af63bb6b660` on the separate
`recovery/integration-repairs-20261006` branch. The saved checkout at `65548f97`
was left intact. Existing refs, main and PR12 were preserved; integration and
publication remain parent-owned.

## Environment checkpoint

Before long runs, the active worktree was `/workspace/Lanternwake-recovery` at
the verified `17a4cc6` base. No local `AGENTS.md` or `.agents/skills` instructions
were present. The repository README, architecture, verification scripts and
pushed keyboard/cup receipts were inspected. The saved activation script supplied
official Godot `4.6.3.stable.mono.official.7d41c59c4` and .NET SDK `8.0.425`;
the initial shell did not have `dotnet` on PATH. Matching official Godot packages
were reused. Normal sessions ran directly from the Godot project on owned X11
displays with Mesa llvmpipe and Dummy audio. Accepted external observers used
`OMP_THREAD_LIMIT=1` and `LP_NUM_THREADS=2`.

## Repair scope

- At 125% and 150%, Tab reaches the main passage scrollbar. Arrows, paging,
  Home and End expose the prose while Space/Enter preserve the beat. Tab can
  then reach Continue, whose Return activation advances exactly once.
- `f_full_testimony` says Ivo suppressed the corrected chart but allowed the
  false hold claim to stand. `ch4_s4_b001` adds 23h17m: yesterday 19:58/20:03
  maps to today 19:15/19:20. A parsed comparison proves these are the only two
  changed story values, with IDs and structure preserved.
- Pumas setup now points to approved `95a0baa`, whose source disables ORT
  build downloads. The normal Cargo build needs no ORT environment or download
  suppression. ONNX execution uses a separately provisioned absolute library
  file via `ORT_DYLIB_PATH`, or an adjacent platform-named library, and reports
  a typed `runtime_library` error for missing/invalid runtime. Historical
  `e37bbf4` wire/live evidence retains its original scope. No Pumas build, new
  wire qualification or live inference was performed in this recovery.
- The maintained `docs/bible/drafts/audit_story.py` generator reconciles the
  testimony register and three already committed cup cues. The content contract
  names the validator's actual nonblank coverage; no broad validator was added.

## Executed checks

| Check | Result and scope |
| --- | --- |
| `scripts/verify.sh` | Exit 0; no warnings/errors. 4,125 core assertions, 63 story checks, 11 simulated direct-Pumas scenarios, five audio setup tests; native 1,424-beat traversal, 23 evidence activities, 28 optional conversations, 4,926 complete cup checks and 13 editor scenes. |
| Normal main passage | 44 checks, 67 events, 29 images; both enlarged sizes scroll through Tab and show the exact ending. Reading keys preserve all save bytes and saved state. Tab/Continue advances once; production Quit exits 0. |
| Normal save/restart/recovery | 142 checks, 188 events, 16 images; editable authored fallback, committed-save interruption, fresh load, reader cycles and explicit damaged-primary recovery. Three processes: one intentional owned SIGKILL after completed saves and two production Quit exits 0. |
| Focused normal controls | 84 checks, 125 events, 47 images; native focus, Return/Space press/release and held repeats on Save/History/Talk, real text-field submission and single Continue advance. Production Quit exits 0. |
| Debug build and compiler binding | Zero compiler warnings/errors; matching DLL/PDB identities and source checksums for 143 compiler documents, including all 48 tracked production C# sources. |
| Story and generated bible | Only the specified fact and beat text change; both clock additions checked. Generator passes with five chapters, 1,465 scene/beat IDs, 17 facts and 12 items; new regression Python compiles and Git whitespace checks pass. |

The 125% and 150% opening/ending screenshots were inspected. The keyboard scroll
changed 29,204 and 33,702 prose-mask pixels respectively after six Tab attempts;
earlier attempts differed by at most five background pixels. Prose comparisons
exclude the scrollbar border and tolerate at most 100 mask pixels of rain through
the translucent panel. State checks separately detect unintended advances.

Existing normal-player controllers were left unchanged. Task-local observer
copies add menu readiness, bounded OCR retry and frame settling; they inject no
game script or callback. Their complete source and diffs are retained locally.
Seven incomplete observer attempts remain unqualified: crop/background
calibration, OCR/resource timing, menu readiness and a stale focus frame. Their
raw receipts and images are retained separately from accepted runs. The native
graphical logs contain only the known driver V-Sync warning.

## Evidence and limits

The [compact receipt](evidence/integration-recovery-20261006.json) records source,
DLL/PDB, controller, upstream documentation and raw-evidence hashes. Native runs
used the repaired working tree before its repair commit; all production source,
story and main-controller hashes match that commit. A final Pumas documentation
sentence clarified the setup pin after testing.

Raw evidence is local at
`/workspace/lanternwake-integration-recovery-20261006/`, with a 70,202,015-byte
packet at `/workspace/lanternwake-integration-recovery-20261006.tar.gz`.
Packet SHA-256:
`0ee0567d09a90a660060abfb28e225bad432d5bd71cc5345ab687000fe77e8d0`.
No private Library upload occurred; both owned displays were stopped. Prior
pushed receipts were used as prior evidence, without claiming their absent raw
files were recovered or repeating standalone exports and full source audits.

Synthetic input, software rendering and Dummy mixing do not establish physical
input, assistive-tool, hearing, human editorial/emotional or five-hour duration
acceptance. Production model quality, new-pin Pumas wire/live dialogue, the typed
Pumas/Cohere producer contract, microphone/ASR, standalone/platform/gamepad and
release qualification retain their stated gates. No PR metadata, merge,
publication or account/credential/security action occurred.
