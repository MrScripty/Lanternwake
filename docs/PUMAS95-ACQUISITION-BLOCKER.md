# Pumas95 managed acquisition checkpoint — 2026-10-06

The real-inference continuation starts from frozen Lanternwake
`bfa952c460c913c163d92a016d5b3eb35589c955` on a separate branch,
`feat/pumas95-live-20261006`. No game, adapter, or authored-content changes are
included in this checkpoint. The observed outcome is **blocked acquisition**,
not live-inference acceptance.

## Actual environment and failures

Shell access works in `/workspace/Lanternwake-pumas95-live`; .NET 8.0.425,
Godot `4.6.3.stable.mono.official.7d41c59c4`, and the previously built Pumas95
binary remain available. Its SHA-256 is
`8dfdc80458ef59659cf512cb7d2e798fa7375a47a08cdceee3bdf5a30992240f`.
The frozen checkpoint's successful build and empty-service checks are unchanged.

Before provisioning, an ordinary shell HTTPS metadata request to
`https://huggingface.co/api/models/Qwen/Qwen2.5-0.5B-Instruct-GGUF?blobs=true`
failed at the HTTPS tunnel with **403 Forbidden**. This was a tunnel failure,
not a returned model-license or model API response. No gated/private metadata
or current publisher file digest was obtained through this environment.
That model acquisition step was stopped. No proxy, certificate, network,
security, or credential setting was changed, and no alternate transfer route
was attempted.

Independently, the actual unmodified Pumas95 binary was started on an owned
ephemeral loopback listener and fresh launcher root. Its normal RPC request:

```json
{"jsonrpc":"2.0","id":"runtime-versions","method":"get_available_versions","params":{"app_id":"llama-cpp"}}
```

made three normal application attempts to the official GitHub release endpoint
`https://api.github.com/repos/ggml-org/llama.cpp/releases?per_page=100&page=1`.
All failed with `error sending request`. The real public result was:

```json
{"jsonrpc":"2.0","error":{"code":-32000,"message":"A required operation is currently unavailable.","data":{"class":"unavailable"}},"id":"runtime-versions"}
```

The public error deliberately does not expose its internal network cause; this
record does not infer that the GitHub request returned the same 403 as the
separate shell Hugging Face tunnel. No further runtime discovery/install retry
was issued after the application exhausted its built-in retries.

Read-only checks then confirmed empty installed/active `llama-cpp` versions,
an empty model map, no served models, and no model downloads. The owned Pumas
process was matched by executable and launcher-root arguments, then stopped
with SIGTERM; its log reported `RPC shutdown completed`. No Godot generation
test was started with missing prerequisites. Existing fallback evidence was
not rerun or recast as generated dialogue.

## Verified candidate sources and licenses

The small proposed model is the official
[Qwen/Qwen2.5-0.5B-Instruct-GGUF](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF),
Q4_K_M. The publisher lists 0.49 billion parameters and this quantization in
its usage example. Its public [LICENSE](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF/blob/main/LICENSE)
is Apache License 2.0, copyright Alibaba Cloud. Those primary pages were read
through the browser tool without an account or agreement click. This verifies
the candidate's published license; it does not replace the blocked environment
metadata/digest check or prove that weights were acquired.

The runtime owner is official
[ggml-org/llama.cpp](https://github.com/ggml-org/llama.cpp).
Its [MIT license at historical candidate b11352](https://github.com/ggml-org/llama.cpp/blob/b11352/LICENSE)
was read directly. No release asset was selected or downloaded in this run.
Use an actual CPU tag returned by Pumas when discovery works; do not substitute
a guessed tag, an unofficial executable mirror, or the historical archive hash
for a fresh publisher-verified selection.

Neither permissive license requires a new click-through in the reviewed public
pages. No new legal acceptance, payment, access grant, credential, or account
was created. If resumed metadata introduces such a requirement, stop that step.
The small model is a protocol-smoke candidate, not production narrative-quality
approval. Earlier tiny-model characterization failures remain relevant limits.

## Supported acquisition path inspected at Pumas95

All following source links are pinned to
`95a0baad2d0aea4650fc36ad4afd969ac9391bf5`:

- [Runtime version manager](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-app-manager/src/version_manager/mod.rs)
  resolves a llama.cpp install tag from its normal available-release list and
  requires the shared acquisition service.
- [Native installer](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-app-manager/src/version_manager/installer.rs)
  selects the applicable asset, fetches its live publisher identity/digest,
  uses shared acquisition, and records native-output/launcher hashes in a
  durable install receipt before publishing the managed installation.
- [GitHub asset selection](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-core/src/acquisition/github_release.rs)
  requires a stable API asset ID and publisher `sha256:` digest. A legacy
  cache or asset without a digest cannot authorize verified selection.
- [HF download API](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-core/src/api/hf.rs)
  resolves the repository's main revision to an immutable commit, prepares a
  selected artifact, and uses the protected shared-acquisition download path.
- [HF revision resolution](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-core/src/model_library/hf/metadata.rs)
  refuses a response identifying a different repository or lacking a valid
  immutable commit.
- [Download RPC](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-rpc/src/handlers/models/downloads.rs)
  exposes `start_model_download_from_hf`, progress/cancellation, and selected
  artifact identity. Its public request has no caller `revision` field; do
  not invent one or silently claim a historically pinned revision was fetched.

### Resume sequence after an approved working network route exists

1. Use the same unmodified Pumas95 binary and an owned launcher root. Obtain
   normal `get_available_versions` for `app_id: llama-cpp`. Select its CPU tag
   and retain the source release/asset identity and publisher digest.
2. Call `install_version` with `app_id` and that `tag`; observe
   `get_installation_progress`, then confirm `get_installed_versions` and
   `validate_installations`. Record the acquisition manifest/blob digest,
   installed metadata, native tree, and `llama-server` SHA-256. Call
   `switch_version` and confirm the active tag. Do not extract or execute a
   separately fetched archive as a hidden replacement.
3. Verify official HF repository identity, non-gated access, exact selected
   filename, immutable revision, publisher file digest, and license at that
   revision. Stop for new terms/access/payment or a network rejection.
4. Start the normal Pumas model acquisition. The request shape, with the
   filename confirmed against current publisher metadata, is:

   ```json
   {"jsonrpc":"2.0","id":"model-download","method":"start_model_download_from_hf","params":{"repo_id":"Qwen/Qwen2.5-0.5B-Instruct-GGUF","family":"qwen2","official_name":"Qwen2.5-0.5B-Instruct-GGUF","model_type":"llm","quant":"Q4_K_M","filenames":["qwen2.5-0.5b-instruct-q4_k_m.gguf"],"pipeline_tag":"text-generation","license_status":"apache-2.0"}}
   ```

   Preserve returned `download_id` and `selectedArtifactId` (also exposed as
   `artifactId`); poll
   `get_model_download_status` using `download_id`. Retrieve the actual
   registered model ID from the completed Pumas result/library. Verify the
   resolved revision/manifest and local GGUF SHA-256 against the publisher
   evidence before serving. Do not use an external weight downloader followed
   by `import_model` as a substitute for this acquisition acceptance test.
5. Use the existing [CPU profile and serving contract](PUMAS.md):
   `upsert_runtime_profile` → `validate_model_serving_config` → `serve_model`
   → current `get_serving_status`. Require the exact model/alias, provider
   `llama_cpp`, loaded state, and Pumas gateway. Ports must be owned/unoccupied.
6. Run the existing authored-context `integration/pumas/LiveSmoke` against
   that Pumas URL/alias. Success must be real nonempty assistant text with
   canonical state unchanged; fallback does not pass. Then exercise the
   normal Godot typed-dialogue path, generated-history marker, visible Local
   Pumas label, Return/Save/restart, stale-reply handling, and provider
   cancellation. Record exact binary/runtime/model hashes, requests, outcome
   timings, screenshots, and remaining narrative-quality gaps.

These are source-reviewed resume steps, not executed successful provisioning
or positive live acceptance in this blocked run. The required next prerequisite
is a platform-approved working network route for the normal acquisition calls;
this task did not attempt to alter or circumvent the rejected route.

## Evidence and scope

The compact [blocker receipt](evidence/pumas95-acquisition-blocker-20261006.json)
binds the real RPC response, Pumas log, unchanged inventories, and source review
hashes. Local raw evidence stays under
`/workspace/lanternwake-pumas95-live-evidence-20261006`. The prior Library403
restriction remains untouched: no upload, retry, or alternate delivery occurred.
Frozen head `bfa952c4`, main, PR metadata, merging, and publication remain untouched.
