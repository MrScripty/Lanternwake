# Pumas-owned discovery and explicit acquisition setup — 2026-10-06

Lanternwake now offers **Settings → Local conversation setup**. Search, repository
inspection and option review are separate actions. Only the explicit **Request
download through Pumas** button requests acquisition. The native C# wrappers use
Pumas's existing loopback `POST /rpc`; Lanternwake has no Hugging Face transport,
model-file transfer, revision resolver, artifact verifier or runtime installer.
No canonical content, conversation prompt, save schema or inference routing was
changed. Runtime installation, serving profiles and model loading remain Pumas
operator setup. The game continues to use its explicitly configured served alias.

## Source and contract ownership

This separate branch starts from verified main
`19495fac93124917d20a1036c6a84d18e01b4f98`. Character-performance
`44f944b8ea157207d43cde1bf47dd109b5580633` and guided-descent review
`ff0f479c0294030c5231c52a98f8a13db47757a9` stay frozen; neither was merged here.
Implementation checkpoint is `6c34a61b53875d16ed194c042f0e7ede0038e5b5`.
Normal UI qualification uses checkpoint `cb340c4` with the same production DLL;
intervening commits only repair the external observation driver. Final hashes,
full checkpoint IDs and individual raw-file hashes are in the
[receipt](evidence/pumas-discovery-setup-20261006.json).

Pumas source is unmodified at
`95a0baad2d0aea4650fc36ad4afd969ac9391bf5`, tree
`3ee66988eb1668188011b2124890b10031403ebd`. Inspection covered:

- `rust/crates/pumas-rpc/src/handlers/models/search.rs`: search parameters and
  outcome; typed download-details handling.
- `rust/crates/pumas-rpc/src/contract.rs`: `GetHfDownloadDetailsParams`,
  `DownloadModelFromHfParams`, `HfDownloadDetailsOutcome`, `DownloadStartedOutcome`.
- `rust/crates/pumas-core/src/models/model.rs`: camelCase model, size, quant and
  file-group records; stored `upstream_revision` field.
- `rust/crates/pumas-rpc/src/handlers/models/downloads.rs` and
  `rust/crates/pumas-core/src/api/hf.rs`: Pumas owns acquisition, resolves the
  immutable revision internally and returns an acceptance receipt.
- `frontend/src/components/ModelManagerRemoteDownload.ts`: developer/family and
  official-name request convention, including developer fallback to repo owner.

The wrappers preserve repository identity, quant or exact grouped filenames,
name, developer/family, pipeline tag, license metadata and release date. They
validate consumed shapes, response IDs, sizes, bounded collections and correlated
repository/receipt fields. The existing transport disables proxies and redirects,
accepts only literal IPv4 loopback, rejects duplicate JSON properties and limits
responses to 256 KiB. Setup has one owned operation, a 30-second deadline, caller
cancellation and no retry. Closing or quitting cancels the wait; stale replies
cannot reopen panels. Shutdown drains owned work before disposing the client.

## Preview provenance and acceptance

The approved download-details response has no immutable revision. UI labels
`main` as mutable and the preview revision as unknown. It does not invent a
revision field in the acquisition request: the approved decoder would reject
unknown fields, and Pumas already resolves the revision internally. A returned
download ID and optional artifact ID mean acceptance only. They establish no
completed download, verified bytes, immutable-revision receipt, installed runtime,
loaded model or dialogue quality. The UI directs the player to Pumas progress
and stored model metadata for those results. An interrupted or unsupported
acquisition response leaves acceptance unknown and is never replayed.

The separate [metadata verification at `4b5aa5c8`](https://github.com/MrScripty/Lanternwake/blob/4b5aa5c8d19c5801409f17dbc61ac5a49431cd0b/docs/PUMAS-METADATA-DISCOVERY-20261006.md)
is prior evidence, not this task's network run. It used Pumas source `95a0baad`
and binary SHA-256
`ef367e83a24b8ba6d2018f266a33a0c60cf0a3ee82b0bfc532f9c65ece20ec4a`.
Its successful runtime-version response came from Pumas's disk cache after an
initial fresh fetch exceeded the original client's capture limit. Model-preview
revision stayed unknown. This task did not repeat that external metadata test,
acquire real models/runtimes or introduce an external host. Historical direct-HF
probe evidence retains its original
limits; it is not retroactively relabelled as Pumas-owned.

## Qualification

The pre-run environment checkpoint recorded actual cwd, head, .NET 8.0.425 and
official Godot 4.6.3 Mono. Fresh audio setup, build and Editor import used the
direct Godot project; no standalone export was attempted. Results and limits
are recorded in the receipt. Controlled fixtures are derived from the inspected
Pumas records; their fictitious model and artifact names identify them explicitly.
No real Pumas acquisition or inference is claimed.

| Check | Result |
| --- | --- |
| Fresh Debug build and official Editor import | Exit 0; compiler has zero warnings/errors, import has no error/warning. |
| Native C# discovery/acquisition fixtures | 12 passing scenarios: source parameters, exact selections, unknown sizes, correlated results, local bounds, Pumas rejection, envelope/size/receipt failures, HTTP/redirect/content-type failures, bounded JSON, cancellation and no replay. |
| Existing native C# conversation contracts | 23 scenarios pass; serving/provider/alias admission and generation behavior remain intact. |
| Core and story structure | 4,241 assertions; 1,439 authored beats, five chapters; 63 story checks, zero failures. |
| Native UI smoke | Reading size/chat, save recovery and title/reveal/history/catalogue/settings/conversation/save/load/activity checks pass. |
| Normal Godot, 100% reading text | 29 checks; explicit quant request, Pumas rejection, pending lookup Escape, no replay, exact saved-state preservation and expected explicit Save backup; production Quit exits 0. |
| Normal Godot, 150% reading text | 29 checks with exact grouped filenames and no competing quant; accepted/rejected/cancelled paths, save preservation and production Quit pass. |

Both normal sessions use the same native DLL and external driver. Each records
exactly seven fixture RPCs: two lookups plus one explicit acquisition for acceptance,
the same three for rejection, then one cancelled lookup. Opening setup and reviewing
an option make no acquisition call. Source files remain byte-identical during each
session. Request parameters contain no invented immutable revision. Screenshots of
setup, options, review, acceptance and rejection were inspected, including enlarged
reading text. Native logs contain only the expected dummy-renderer V-Sync warning.

Reproduce the local fixture UI with an owned authenticated display and fresh,
separate `--fixture`/`--output` directories for each size:

```sh
dotnet run --project integration/pumas/DiscoveryTests/DiscoveryTests.csproj
python3 integration/qa/pumas_discovery_input.py --display-state /owned/display/display.json --fixture /owned/normal-100-fixture --output /owned/normal-100 --percent 100
python3 integration/qa/pumas_discovery_input.py --display-state /owned/display/display.json --fixture /owned/normal-150-fixture --output /owned/normal-150 --percent 150
```

The driver starts only its ephemeral IPv4-loopback fixture, injects real X11 input,
observes pixels and real save files, and closes its service. No in-game fixture route
or alternate setup implementation is used. The owned display was stopped afterward.

Three excluded 100% driver attempts remain in local evidence. The first failed
to split repository/quant punctuation into OCR words before clicking inspection;
it sent only search. The second reached the accepted receipt but full-page OCR
dropped the title's first letter. The third completed accepted, rejected and
cancelled flows but incorrectly expected the slot set to stay identical after an
explicit Save. Production Save correctly created the previous-slot backup; its
bytes exactly matched the current save. The driver now reads the inspected title
region, normalizes punctuated tokens, checks untouched files before Save and
checks exact state plus expected backup afterward. No production defect was
inferred from those failed assertions and no production source was changed to
satisfy them.

Raw logs, screenshots, fixture saves and request captures remain local under
`/workspace/lanternwake-pumas-discovery-evidence-20261006`. The public receipt
contains hashes and bounded results. Raw uploads remain separately gated; none
were uploaded. Effective author and committer are MrScripty; original historical
metadata is preserved. Main, PR metadata, merges and publication remain parent-owned.

Real acquisition/completion receipts, verified immutable artifact provenance,
runtime installation, loaded-model wire checks on the approved pin, production
dialogue quality and real-provider cancellation still need separate evidence.
Synthetic X11/software-renderer checks establish no physical-input, accessibility
tool, performance matrix or human reading-duration acceptance.
