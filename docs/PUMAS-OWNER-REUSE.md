# Explicit existing-library owner reuse

Lanternwake model setup now uses an explicitly selected existing Pumas library and
installed `pumas-rpc` observer. Set both paths in AI setup and save, or pass
`--pumas-library ABSOLUTE_FOLDER --pumas-observer ABSOLUTE_EXECUTABLE` to
`scripts/run.py`. Explicit launcher choices override only this process's library
selection; an unrelated AI settings save keeps the saved library selection.
Editing the library fields and saving explicitly replaces that selection for this
process and future launches. Invalid launcher paths leave valid loaded preferences
intact; file-load and launcher-selection errors are reported separately. The
equivalent initial-default
environment names are `LANTERNWAKE_PUMAS_LIBRARY_ROOT` and
`LANTERNWAKE_PUMAS_OBSERVER`. Existing dialogue/provider, keyring, transcription,
character voice and story settings remain separate. Library setup uses the
selected owner's authenticated advertised endpoint, including when dialogue is
configured for OpenRouter; it does not replace that dialogue endpoint.

The launcher starts Godot, never Pumas. Before setup search, inspection, activity
or acquisition, `PumasOwnerClient` executes the supported read-only command
`pumas-rpc --describe-local-http --launcher-root PATH`. The producer authenticates
its private core IPC owner and registered HTTP generation, then reauthenticates
core context. The game additionally compares the full bounded CLI description,
`GET /.well-known/pumas`, and a second CLI description. Redirects/proxies are
refused. The live route must return JSON and `Cache-Control: no-store`; duplicate
keys, incompatible schemas/protocols, wrong roots, changed generations and
malformed types fail closed. Optional build provenance may be omitted; release
strings or compile features do not prove a ready model. The existing client
supports numeric IPv4 `127.0.0.1` only. Canonical absolute root spelling must match;
this managed consumer refuses aliases it cannot compare rather than adding
native filesystem bindings or guessing physical identity.

A successful result is a **point-in-time borrowed observation**, not a physical
store lease or future HTTP admission token. The game pins its core/HTTP owner
context across setup actions. A changed owner requires explicit review and Save
AI settings again. Settings changes apply only after an original setup operation
settles. Close/Escape cancels finite local observation; late results cannot create
a client, submit acquisition, replace a newer window or rewrite preferences.

`PumasAcquisitionGate` sends the existing `intent_query_models` JSON-RPC request
with an upstream-repository selector, `gguf` artifact format, explicit quantization
when available, and `acquisition_policy: local_only`. It never calls get/ensure
operations while observing. File-group previews are not durable typed artifact
IDs: those use a conservative repository/format query. A well-formed empty match
can proceed only from the existing explicit download action and another
same-owner authentication. Any ready, incomplete or unsatisfied local match
blocks another request and directs the player to reuse/repair it in Pumas.
Multiple matches are never silently selected. Errors, unsupported outcomes,
malformed or mixed outcomes are distinct from empty matches and block submission.
The legacy download-start `success:false` response can follow durable admission;
it is not a typed non-admission receipt. An accepted receipt or any uncertain
submitted outcome, including backend refusal, blocks a duplicate of that
selection for the rest of this game session, including after preference changes.
No automatic retries or alternate-owner fallback exist. This session guard does
not invent a durable producer reconciliation receipt across application restarts.
Pumas continues to own transfer, verification, installed artifacts and runtime
admission; an index match or download receipt does not establish serving readiness.

The game owns only its HTTP clients and the transient read-only CLI child process.
Cancellation may terminate that exact child, never a daemon or PID read from an
advertisement. Borrowed service shutdown/reclamation is absent. Tree exit disposes
local observation state and leaves the borrowed service running. There is no
game-owned Pumas daemon in this implementation.

## Producer sources and remaining gaps

Consumer qualification used producer source snapshot
[`5e114f6d`](https://github.com/MrScripty/Pumas-Library/tree/5e114f6d8e4559e0a4d67e56000b423120a0fde0)
and the additive discovery/HTTP/cohort source associated with
[#61](https://github.com/MrScripty/Pumas-Library/pull/61), pinned at
`aa1225b8011e7b16303775614ac8e3423a2069a9`. These are pinned source references,
not a current release or installed-runtime qualification. Relevant contracts:

- [CLI arguments](https://github.com/MrScripty/Pumas-Library/blob/aa1225b8011e7b16303775614ac8e3423a2069a9/rust/crates/pumas-rpc/src/main.rs),
  [authenticated projection](https://github.com/MrScripty/Pumas-Library/blob/aa1225b8011e7b16303775614ac8e3423a2069a9/rust/crates/pumas-rpc/src/discovery.rs),
  [borrowed HTTP negotiation](https://github.com/MrScripty/Pumas-Library/blob/aa1225b8011e7b16303775614ac8e3423a2069a9/rust/crates/pumas-core/src/discovery/http.rs).
- [Build descriptor and optional provenance](https://github.com/MrScripty/Pumas-Library/blob/aa1225b8011e7b16303775614ac8e3423a2069a9/rust/crates/pumas-core/src/build_info.rs),
  [packaging's existing CLI/HTTP verification](https://github.com/MrScripty/Pumas-Library/blob/aa1225b8011e7b16303775614ac8e3423a2069a9/scripts/release/headless_discovery.py).
- [Typed local-only DTOs](https://github.com/MrScripty/Pumas-Library/blob/aa1225b8011e7b16303775614ac8e3423a2069a9/rust/crates/pumas-core/src/intent/types.rs),
  [read-only resolver](https://github.com/MrScripty/Pumas-Library/blob/aa1225b8011e7b16303775614ac8e3423a2069a9/rust/crates/pumas-core/src/intent/resolver.rs).

The CLI returns a descriptor on successful borrowed authentication and an ordinary
error otherwise. It does **not** expose a structured unoccupied-root result or a
cross-language negotiated attach-or-start operation returning an owned HTTP
lifetime handle. Missing registry, incompatible, closing, legacy, unauthenticated
and unreachable owners cannot be distinguished as safe startup authority.
Automatic bundled startup therefore remains unavailable. Library enumeration and
local model snapshots exist as Rust APIs/example, not an installed public CLI
listing contract; the game asks for an explicit root instead of parsing private
registry files. The CLI observation retains no lifetime lease and offers no
HTTP mutation fence tied to its descriptor. These limits prevent an end-to-end
bootstrap/duplicate-exclusion claim. Existing producer atomic ownership and
acquisition rules remain producer responsibilities.

No qualified bundled Pumas distribution or installed producer was exercised here.
An older installed binary without this additive command refuses safely. Consumer
checks use marked owned CLI/HTTP fixtures, actual game controls and independent
review; they do not qualify producer authentication, owned daemon cessation,
physical microphone, model downloads or inference. Microphone installed-runtime
admission remains closed as documented in [SPEECH.md](SPEECH.md).
