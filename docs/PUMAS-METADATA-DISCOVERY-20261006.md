# Pumas-owned metadata discovery — 2026-10-06

Metadata discovery passed through the existing Pumas loopback JSON-RPC setup
path. The [receipt](evidence/pumas-metadata-discovery-20261006.json) records
source hashes, responses, file metadata, process ownership and raw evidence
hashes. This qualifies discovery only; no model or runtime was acquired.

Normal `git fetch origin main` obtained Lanternwake
`19495fac93124917d20a1036c6a84d18e01b4f98`. Verification used a separate local
clone; the saved checkout remained clean at
`65548f97e1c203884864bf4dcc1d1849db4cbcd1`. Neither fetched tree contains
`AGENTS.md` or `.agents/skills`; `/workspace/.agents` is empty. The requested
environment-version label is `cecfgver_6ac4b26339608192af8e57f6369e3db7`; no
independent environment-version introspection was available.

## Source, binary and request ownership

The existing integration pins Pumas to
`95a0baad2d0aea4650fc36ad4afd969ac9391bf5`, tree
`3ee66988eb1668188011b2124890b10031403ebd`. Unmodified source built with Rust
1.92.0 and default inference features:

```sh
cargo build --locked --offline --manifest-path rust/Cargo.toml -p pumas-rpc --release -j 2
```

Build time was 16m25s. The new binary SHA-256 is
`ef367e83a24b8ba6d2018f266a33a0c60cf0a3ee82b0bfc532f9c65ece20ec4a`.
Official Rust/crates.io sources supplied isolated dependencies before the
offline build. The inspected Cargo feature graph contains neither ORT
`download-binaries` nor `copy-dylibs`.

The probe sent only literal-loopback HTTP to Pumas, using the same `POST /rpc`
JSON-RPC pathway as `Scripts/Conversation/PumasClient.cs` and
`integration/pumas/current_source_probe.py`. Exact Pumas source paths are:

- `get_hf_download_details`: `pumas-rpc/src/handlers/mod.rs:779` →
  `handlers/models/search.rs:51` → `pumas-core/src/api/hf.rs:221` →
  `model_library/hf/search.rs:715` → `hf/metadata.rs:255`. Pumas's own
  `HuggingFaceClient` requests
  `https://huggingface.co/api/models/Qwen/Qwen2.5-0.5B-Instruct-GGUF/tree/main?recursive=true`.
- `get_available_versions`: `pumas-rpc/src/handlers/mod.rs:975` →
  `handlers/versions/release.rs:8` →
  `pumas-app-manager/src/version_manager/mod.rs:830` →
  `pumas-core/src/network/github.rs:707,1113`. Pumas's own `GitHubClient`
  requests `https://api.github.com/repos/ggml-org/llama.cpp/releases?per_page=100&page=1`.

These crate paths are relative to Pumas's `rust/crates` directory. Source
hashes are in the receipt. `strace -f` records execution of the identified
binary and outbound connections by its threads. Pumas debug logs identify
both logical metadata hosts and the successful release fetch. Pumas also
runs its existing startup connectivity HEAD probe to `api.github.com`, from
`pumas-core/src/api/builder.rs:373`. Ambient proxy/authentication settings
were preserved. No direct Lanternwake or probe HTTP request to either remote
metadata service was made.

## Observed metadata

Both metadata RPCs returned local HTTP **200**, matching JSON-RPC envelopes
and `success: true`. Successful Pumas source branches imply upstream 2xx;
the RPC does not expose the exact upstream HTTP status.

The model tree contains nine GGUF LFS entries and three regular-file names.
`qwen2.5-0.5b-instruct-q4_k_m.gguf` is **491,400,032 bytes**, with advertised
LFS SHA-256
`74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db`.
All file sizes and advertised hashes are retained. No file bytes were fetched
to verify those hashes. The revision selector is `main`; the existing
metadata-only RPC and its cache expose no immutable repository commit.

Pumas fetched **100 releases**, projected into **693 platform variants**.
The first CPU variant is `b11435+cpu`, published `2026-10-06T06:16:04Z`,
marked prerelease. Its `llama-b11435-bin-ubuntu-x64.tar.gz` metadata size is
**17,692,966 bytes**. Archive URLs were recorded as metadata only. No
additional redirect host or network denial was naturally exposed.

The first client's 4 MiB capture limit rejected the 4,505,618-byte expanded
runtime RPC response after Pumas had fetched and cached the release listing.
That failed receipt is preserved. A second owned Pumas run returned the
cached listing with `force_refresh: false` and a 16 MiB client capture limit;
the release-list network request was not repeated. Both processes exited 0.
The file watcher logged a disconnected-channel warning during shutdown.

Before/after model inventories and installed llama.cpp versions were empty.
The isolated launcher contains metadata, caches, locks and databases only.
No weights, runtime archives, native ONNX/ORT libraries, serving or inference
were requested. No credentials, authentication, host policy or shared main
changes were made. Raw evidence and the loopback-only probe remain under
`/workspace/lanternwake-metadata-verification/evidence`, including
`initial-rejected`; none were uploaded.

## Remaining limits

Immutable model-revision evidence needs a Pumas-owned metadata contract that
exposes it. Acquisition was not invoked to obtain that revision. Lanternwake's
production C# client currently implements serving-status and inference only;
it has no discovery/acquisition wrapper. This run verifies the existing Pumas
RPC setup pathway, and does not claim a new game discovery feature, installed
runtime, loaded model or real inference.
