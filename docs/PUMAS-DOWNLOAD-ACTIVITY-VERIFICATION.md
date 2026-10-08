# Pumas-owned download activity — 2026-10-06

The existing setup candidate stopped at acquisition acceptance. It had no progress
query, completion/failure view, explicit cancellation control, or way to recover
the view after closing it. Pumas already supplies the lifecycle state and owns
its transfer, persistence, revision resolution and cleanup. This candidate adds
only the missing native activity view and three typed RPC wrappers.

## Scope and source ancestry

The separate `feat/pumas-download-activity-20261006` branch starts from frozen
setup `973a23ae59baaf0882d71f8e1804670c6aefeb68`. Its implementation checkpoint is
`35463c7dd359a62dc4ff4106b62202253c611269`. The qualified external driver checkpoint
is `b0850b7dc93e0ddec9e863b68f80b5740e24369e`; only observation code changed.
The frozen setup,
character-performance `44f944b8` and guided-descent review `ff0f479c` heads are
preserved. The character and descent candidates are not ancestors of this branch.
Parent merged setup PR14 into main `9019fe1569d664759d84d866fee0af9c0f8aa5ec`
while qualification was running. Its tree `cddb8d9bcfca0069eb79e0966531a4d5c78ae099`
is byte-identical to frozen setup. After both UI runs exited, that approved main
was merged into this feature at `e5c8fad9f1e35032337eaab89d2cd11235554ef6`.
The merge changes no tracked bytes from the qualified checkpoint (tree
`a38e73fe087d449996cc603ff1978bca548f61a7`). This feature does not mutate main,
PR metadata or publication. Parent monitors the setup postmerge CI separately;
neither that merge nor its CI is real-model/acquisition qualification.

Pumas source remains unmodified at
`95a0baad2d0aea4650fc36ad4afd969ac9391bf5`, tree
`3ee66988eb1668188011b2124890b10031403ebd`. Source inspection covered:

- `pumas-rpc/src/contract.rs`: flattened `DownloadStatusOutcome`, camelCase
  `DownloadProgressOutcome`, list and mutation outcomes, `DownloadIdParams` and
  method dispatch. Requests use `download_id`; response records use `downloadId`.
- `pumas-rpc/src/handlers/models/downloads.rs`: status/list delegation and explicit
  cancellation through Pumas API ownership.
- `pumas-core/src/models/model.rs`: all eight states and nullable progress,
  library/repository/artifact identities, numeric and retry evidence.
- `pumas-core/src/api/state_hf.rs` and `model_library/hf/download.rs`: reads
  reconcile Pumas state; Pumas lists tracked active/paused/terminal requests,
  owns persistence restore and launches cancellation finalization before returning
  acknowledgement. A successful cancellation command is not terminal proof.
- `frontend/src/hooks/useModelDownloads.ts`: Pumas's own startup view recovers
  through `list_model_downloads` and separates command outcome from backend status.

These Rust paths are relative to `rust/crates/`. Full source/evidence hashes and
the consumed Pumas source hashes are in the
[receipt](evidence/pumas-download-activity-20261006.json).

## Implemented behavior

**View this request** on the accepted receipt queries its exact download ID.
**View Pumas download activity** in setup lists all tracked Pumas requests; it
does not guess that they all belong to Lanternwake. Selecting an exact ID reads
the current backend snapshot. **Refresh** is explicit. Reopening the activity
view obtains a new Pumas list without a local download journal, new acquisition,
transfer resume or automatic command replay.

The typed wrappers are `ListModelDownloadsAsync`, `GetModelDownloadStatusAsync`
and `CancelModelDownloadAsync`. They reuse the strict loopback-only transport,
shared setup-operation ownership, 30-second deadline, response-ID validation,
bounded JSON and caller cancellation. The list is bounded to 256 activities and
rejects duplicate IDs; a status response must match the selected ID. Unknown
states, malformed shapes, invalid numeric/boolean evidence and unsafe integers
fail closed. Nullable metadata stays unknown. All eight backend states remain
distinct; 100% progress cannot create completion or readiness.

Status displays progress, transferred bytes and exact Pumas library/artifact IDs
when returned. Backend `error` is a successfully observed failed download, not a
successful transfer. Completion directs the player to Pumas provenance/runtime
setup; it does not load a model or configure an alias. Existing serving selection
and inference are unchanged. Preview `main` remains mutable, and this status
contract adds no immutable revision receipt.

Cancellation requires a separate exact-ID review and explicit send action.
Closing that confirmation sends no command. Queued, downloading, pausing, paused
and failed snapshots offer cancellation; completed, cancelled and already
cancelling snapshots do not. Pumas handles stale-state rejection and cleanup.
The acknowledgement view requires a fresh status read before presenting terminal
cancellation. Cancellation callbacks retain their owning confirmation window;
late or repeated callbacks cannot issue a command from a replacement panel.

Closing a pending lookup only cancels Lanternwake's wait. Closing a pending
cancellation wait also leaves its outcome unknown locally: Pumas may already have
accepted it. Reopening queries Pumas and never resends cancellation or acquisition.
Late results cannot reopen dismissed views. Normal Quit cancels/drains owned
operations before disposing the client. Player story/transcript/save state is not
part of these contracts.

Pause/resume and interrupted-transfer recovery stay in Pumas. At this pin,
`recover_download` and `list_interrupted_downloads` are method-not-found;
`resume_partial_download` requires a Pumas-issued model ID and recovery token.
This candidate does not invent those tokens, routes, revision resolution or
download mechanics. Its recovery is recovery of the activity view from existing
backend records, not authorization to resume a transfer.

## Qualification and limits

The pre-run checkpoints record actual cwd/head, .NET 8.0.425 and official Godot
4.6.3 Mono. Fresh source audio setup, Debug build and Editor import pass. Controlled
contract tests include 12 existing discovery/acquisition scenarios plus 14 new
lifecycle scenarios; all 26 pass. All 23 existing conversation-client scenarios
and the native UI smoke pass. No old full-route/core suite is relabelled as a new
result; canonical story bytes and core source are unchanged.

The production setup UI is tested with real X11 input, observed pixels and real
save files against an ephemeral, source-derived, literal-loopback RPC fixture.
The fixture identifies its fictitious model, download and artifact IDs explicitly.
Backend state transitions are controlled fixture responses; they are not real
transfer, verification, persistence-restore or cleanup evidence. No real Pumas
binary, weights/runtime download, external host or credential change is involved.

Both normal 100% and 150% sessions pass 62 checks each with clean production Quit,
source preservation and exact saved-state preservation. They use the same
native DLL and driver. Each records 28 fixture RPCs: one search, one detail lookup,
one explicit acquisition, eight activity-list reads, fourteen selected-status
reads and three explicit cancellation commands. The cancellations target exactly
`fixture-download-002`, then `fixture-download-003` for rejection, then
`fixture-download-003` with its acknowledgement held and dismissed. Reopening
queries Pumas; it does not repeat acquisition or cancellation.
Scenarios cover accepted-request status, 100%-but-still-downloading, close/reopen
list recovery, a dismissed held status response, backend completion, cancellation
confirmation Escape, acknowledgement versus cancelling versus terminal cancelled,
backend failure, rejected cancellation, missing status, and recovery after a held
cancellation acknowledgement is dismissed. Prose is read directly when visible;
Tab/End and native Up reach it when scrolling is needed. Save is checked before and after an
explicit repeat save, including the expected identical previous-slot backup.

The first 100% driver run passed and remains under `prior-driver-100`. The first
150% run reached backend completion but failed its reading assertion: End
correctly scrolled to the footer and left the first line of the completion
paragraph partly above the viewport. The `excluded-end-assertion` receipt and
pixels are retained. The driver was repaired to use small native Up steps after
End, then both sizes were repeated with the same repaired driver. Enlarged
completion/cancelling/failure prose and cancellation confirmation/result screens
were inspected. No production code was changed to satisfy the assertion and
no production defect was inferred from it. Native logs contain only the expected
software-renderer V-Sync warning. The owned display was stopped afterward.

Reproduction uses the built/imported direct project, official `GODOT_MONO`, an
owned authenticated display and fresh separate fixture/output directories:

```sh
dotnet run --project integration/pumas/DiscoveryTests/DiscoveryTests.csproj
python3 integration/qa/pumas_activity_input.py --display-state /owned/display/display.json --fixture /owned/activity-100-fixture --output /owned/activity-100 --percent 100
python3 integration/qa/pumas_activity_input.py --display-state /owned/display/display.json --fixture /owned/activity-150-fixture --output /owned/activity-150 --percent 150
```

Raw screenshots, logs, fixture save files and exact request captures stay local
under `/workspace/lanternwake-pumas-activity-evidence-20261006`. Raw upload requires
separate approval; none occurs. Source author and committer remain MrScripty;
historical metadata is preserved.

Real transfer/completion/cleanup and immutable artifact receipts, Pumas runtime
installation, loaded-model wire/inference qualification on the approved pin,
production dialogue quality, provider cancellation, physical accessibility/input
and human reading-duration acceptance remain separate evidence gates. The
independent activity review and parent-owned integration/publication gates remain.
