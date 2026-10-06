# Combined PR12 release acceptance, 2026-10-05

The selected checkpoint is PR12 head
`05db466507dfbc736d7b0a386724a5fe068554b9`, tree
`540267a7ec1943e2259c5c38cffe3cff7a217988`. Remote/main remains
`3051855bb0c8f4a6d222081f22d00a19c3298572`; no newer remote branch or PR
superseded this checkpoint at startup. Its exact-head hosted CI was already
successful. This follow-up preserves that source and adds QA tools/evidence only
on `qa/combined-release-acceptance-20261005`. It does not update PR12, request a
review, merge, or publish a release. No repository/ancestor AGENTS.md or checkout
`.agents/skills` instructions were present.

Actual local tools: official Godot `4.6.3.stable.mono.official.7d41c59c4`,
.NET SDK `8.0.425`, matching .NET `8.0.31` Linux runtime packs from the ordinary
official NuGet feed, installed Xorg dummy and Mesa llvmpipe. The official editor
archive SHA-256 matches the pinned CI checksum
`702a8a6785060203fd46373adf864586ecf0c377ac685d7107dde66b1eba0a9e`.
The display was private/authenticated, with TCP and optional MIT-SHM disabled,
and was stopped after qualification. Its Xauthority is excluded from delivery.
No fast-mode or engine time-scale override was used. Automatic advances and
synthetic inputs remain automatic tests, without a reader-duration claim.

| Gate | Fresh evidence at this checkpoint |
| --- | --- |
| Complete local aggregate | `scripts/verify.sh` exits 0: 4,125 core assertions; 63 story-structure checks; 11 simulated Pumas contracts; unsupported speech boundary; five Python audio tests; native story/UI/save isolation; 32 mixer assertions and 20,480 PCM frames through Dummy audio; completion/replay, question review, conversation Return, content note, cup state and 13-scene Editor roundtrip. No warning/error lines in the aggregate. The optional Python adapter suite separately passes 19 tests. |
| Combined rendered headers | Both keeper-house views at 100/125/150%, all six PNGs inspected by the assistant. Dark chapter/place backing is readable over both wide geometry and bright floorboards; adjacent controls retain their positions. Body text uses 21/26/32 points and scrolls at the largest size. Pixel samples, exact prose/camera preservation, layout checks and production Quit pass. |
| Repeated rendered gameplay | Six normal-mode processes visit all 1,424 stable beats and 41 scenes. 25,303 assertions, 337 save/resume selections, all 23 wrong-answer retries and recoveries, 93 earlier-state recoveries and completed-watch resume pass. All production Quit exits are 0. These use production callbacks with actual software rendering. |
| Representative native controls | Twelve fresh title/selected-preview processes, 20 captured PNGs, synthetic X11 mouse/key input, no player-save writes, all clean Quit exits. Title/Settings notes, 150% small-window scrolling, Master mute/Music slider, editable authored fallback/Return, cup states, five stages, final evidence/Finish are covered. Assistant inspection includes all six headers and representative title/note/sound/conversation/stage/ending images. |
| Exact source/build identity | Every tracked working file matches the PR12 Git blob. Native Debug and retained ExportRelease DLLs match their portable PDB identities; all 89 compiler documents have verified checksums, including 38 embedded generated documents. All 48 tracked compiled C# sources are represented. Auditor negative tests reject a mismatched PDB and a modified source file. |
| Official build and data export | Pristine Git archive, regenerated seven WAVs, Debug/ExportDebug/ExportRelease builds with warnings as errors, official Editor import/solution build, runtime restore, official .NET publish and `--export-pack` succeed. Native logs and underlying build/publish logs are retained; both issue CSVs are empty. ZIP CRC passes for 57 entries, exact canonical story bytes, 18 scene/resource remaps and seven imported audio samples. A native audit loads the archive's actual binary GameInterface resource and checks the dark PanelContainer, padding and unique label bindings. |
| Standalone exported player | **Blocked.** An actual official `--export-release` attempt exits 1, naming absent `4.6.3.stable.mono/linux_debug.x86_64` and `linux_release.x86_64`. No player executable exists. Neither the data ZIP nor the retained managed library/runtime directory is a playable standalone export. The previously denied official template route was not retried or bypassed. |

Rendered processes retain the dummy graphics driver's unsupported-VSync warning
as an environment limitation; no other warning/error is accepted in final runs.
The first header attempt was rejected after another display client was started
too early. Its log/images remain separate; the entire six-image fixture then
passed with exclusive display use. One export attempt returned 0 but emitted
`EditorSettings not instantiated yet` for `export/android/android_sdk_path` at
teardown and was rejected. The final complete run, with explicit Editor mode,
was clean; this does not establish a root-cause fix for that intermittent engine
diagnostic. Initial auditor failures corrected publish-output retention and
embedded-generated-source handling, without production changes.

Full local evidence is retained in `artifacts/combined-release/`,
`artifacts/header-contrast/after/`, `artifacts/graphical-native/`,
`artifacts/graphical-long-session/` and
`/workspace/lanternwake-combined-export-qualified-20261005/`. The tracked
[compact receipt](evidence/combined-release-20261005.json) contains source,
binary, data-archive and evidence hashes. The local delivery archive additionally
contains source/binaries, native logs, screenshots and rejected-attempt receipts;
it excludes Xauthority and is an evidence packet, not an installable player.
Generated WAVs and binary evidence remain excluded from Git.

Reproduce the export gate after installing the same official engine/SDK:

```bash
python3 integration/qa/combined_export.py \
  --ref 05db466507dfbc736d7b0a386724a5fe068554b9 \
  --output-root /absolute/new-owned-output
```

The runner uses an immutable Git archive and project-local official feeds, rejects
nonzero statuses and warning/error logs, checks archive/source/compiler identity,
and records missing player templates separately. It never downloads templates.
If exact templates are supplied later, a dedicated player-export/runtime runner
must qualify that environment rather than reclassifying this data-only receipt.

Remaining release acceptance: obtain an approved exact official template artifact
or export environment, then run the actual player without Editor/SDK/Python/source
through title/Arrival, save/restart/recovery, ending/replay, author-preview
rejection and shutdown. Human editorial/emotional continuity and five-hour
duration, physical input/hearing, accessibility tools, low-end/platform coverage,
production loaded-model/cancellation and Pumas-owned Cohere/physical microphone
checks remain unperformed. No new prose, chronology, provider API or relationship
mechanic is inferred from these open gates. Parent retains integration, PR/review,
the existing PR7 metadata hold, merge and release decisions.
