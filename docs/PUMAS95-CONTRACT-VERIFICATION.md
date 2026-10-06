# Pumas95 contract and testimony reconstruction — 2026-10-06

Work started from merged main `77b7e3d93339099993f962467cf37d6fed1e831e`
in a separate development worktree. The compact receipt is
[evidence/pumas95-reconstruction-20261006.json](evidence/pumas95-reconstruction-20261006.json).
It binds the source checkpoints, compiled game, upstream source, and local raw
receipts. Main, PR metadata, merging, and publication remain parent-owned.

## Current Pumas evidence

The approved source pin is
[`95a0baad2d0aea4650fc36ad4afd969ac9391bf5`](https://github.com/MrScripty/Pumas-Library/commit/95a0baad2d0aea4650fc36ad4afd969ac9391bf5),
tree `3ee66988eb1668188011b2124890b10031403ebd`.
The serving handler, serving DTOs, runtime-profile DTOs, and dialogue gateway
are byte-identical to historical `e37bbf4`. The current HTTP router and local
request admission were also inspected; native IPv4 loopback requests remain
supported. Source hashes are in the receipt.

An isolated Rust 1.92.0 installation built the unmodified pinned source with:

```sh
cargo build --locked --manifest-path rust/Cargo.toml -p pumas-rpc --release -j 2
```

The build passed in 15m08s. No ORT environment/download-suppression setting was
needed. The default RPC Cargo feature graph contains neither ORT
`download-binaries` nor `copy-dylibs`. No native ORT, model weights, or llama
runtime were acquired. The local RPC binary SHA-256 is
`8dfdc80458ef59659cf512cb7d2e798fa7375a47a08cdceee3bdf5a30992240f`.
This source-built binary is distinct from the historical v0.7.0 package.

`integration/pumas/current_source_probe.py` started this binary on its own
ephemeral loopback port and fresh launcher root. Actual HTTP responses showed:

- `/health`: `status: ok`.
- `get_serving_status`: successful JSON-RPC, serving schema 1, no served models.
- `get_models`: empty model map.
- `get_installed_versions` for `llama-cpp`: empty versions.
- `get_active_version` for `llama-cpp`: empty active version.
- Runtime profiles: schema 2, the default Ollama profile stopped; no llama profile.

The production C# client used `StorySession.ConversationContext()` at
`ch1_s1_b021`, not a fabricated setting. It returned `model_unavailable` with
canonical state unchanged. A normal Godot process, controlled through X11,
submitted a typed turn to the same real empty Pumas: exact authored fallback,
visible `model_unavailable` label, unchanged beat/solved gates/prior history,
Return, Save, and production Quit all passed (13 checks, 33 events, 3 images).
Both owned services and the owned display terminated cleanly. One Pumas run
logged a file-watcher-disconnected warning during shutdown; its exit was 0.

**No loaded-model response or real inference on Pumas95 was qualified.** This
environment has no registered GGUF or installed/active managed llama.cpp
runtime. Real dialogue requires those prerequisites, a matching Pumas-owned
CPU profile/served alias, and current loaded-model/native dialogue acceptance.
Historical inference in [LIVE-QUALIFICATION.md](LIVE-QUALIFICATION.md) remains
historical. Model quality, spoiler resistance, native in-flight cancellation,
and Cohere transcription remain separate gates.

## Adapter repair

The previous adapter combined alias and model-ID matches, incorrectly rejecting
a unique alias when another library ID happened to equal it. It now follows
the inspected gateway's alias-first selection among loaded models with current
router observations. Duplicate selected aliases/base IDs remain unavailable;
malformed/duplicate router observations fail closed. Completion text must have
assistant role.

The package-free .NET harness passes **22 simulated contract scenarios**,
including current/pending/stale routers, dedicated profiles without router
observations, unrelated stale profiles, alias precedence, duplicate aliases,
ambiguous base IDs, unloaded states, assistant role, cancellation during a held
response, busy ownership, and no automatic replay. The baseline adapter from
merged main fails the new alias-precedence fixture; the repaired adapter passes.
These fixtures are explicitly source-derived simulations, not real inference.
`LiveSmoke` now uses the game's exact authored context and emits structured
outcomes with context hash and canonical-state preservation.

## Authored content and combined acceptance

Fourteen new beats in `ch4_s2` reconstruct the relation between the Channel A
log, public summary, corrected chart, and Ivo's omission note. The passage
distinguishes suppressing the corrected chart from allowing the false vessel-
hold claim to remain public, and leaves other editors' knowledge open to review.
All old IDs, fact/item unlocks, activities, conversations, stage cues, branches,
and ending remain intact. One later bag reference was corrected to match the
earlier decision to leave it behind. No new mechanic or required gate was added.

The normal Godot check passed **40 checks, 52 events, 11 images**: all 14 exact
new beats, stable solved gates, old pre-insert save loading, and rejoining
`ch4_s2_b007`. Manual screen inspection found the whole opening fits at 125%;
at 150%, Tab/End reaches the native scrollbar and shows its exact ending.
Return/Space there preserve every saved byte. Production Quit exits 0.

`bash scripts/verify.sh` passed: 4,153 core assertions, 63 story checks, 22
simulated Pumas scenarios, 5 audio setup tests, all 1,438 authored beats,
23 activity reviews, 28 conversation points, complete cup-state checks,
13 editor scenes, and Dummy-driver PCM output. The DLL/PDB source audit matched
143 compiler documents. Both accepted normal-player sessions used the same
game DLL SHA-256 `eb9c11f991d9c766ec447fde7d8313c2e67076d2a5a512552dce361d68f4a34b`.

A targeted late corrupt-manual-save case passed preparation/recovery/verification
in three fresh native processes with 1,438-line late history and unchanged
archived checkpoint bytes. The historical full 12-fault matrix was not repeated.
Domain checks restored old pre-insert, post-insert, and completed snapshots
from merged main with exact old history and equivalent derived facts/items/cues.

Metrics are now **35,408 main-path words, 41 scenes, 1,438 beats**, with
23 activities and 28 conversations. This is 471 additional measured words,
not measured play time. Human editorial, accessibility, physical-input/hearing,
and five-hour duration acceptance remain open.

## Evidence limits and retained failures

The initial combined run reached the new ending but failed the qualification's
fixed 1,424-beat expectation. Three qualification checks now derive expected
length from the authored timeline; the complete rerun passed. Separate rejected
graphical attempts retained a controller assumption that instant-text Settings
stayed open, an assumption that this passage overflowed at 125%, and a typed
capital-letter expectation without a Shift event. Corrected controllers passed;
rejected receipts are excluded from acceptance. They remain local for audit.

Raw logs/screenshots are local, not uploaded. The separately held Library
tunnel-403 blocker was not retried or bypassed. No model-license, credential,
account, system-loader, or security changes were made. Direct Godot was used;
standalone export was not a requirement.
