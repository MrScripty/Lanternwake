# Pumas headless conversation integration

## What is implemented and what is verified

The game uses **native C# `HttpClient` directly against Pumas**. Python, an
OpenAI account, cloud inference, and generated UniFFI bindings are not runtime
requirements. `Scripts/Conversation/PumasClient.cs` accepts character, observed
world context, and player text, and returns only `PumasReply(Success, Text,
ErrorCode)`. It has no game-state reference, save-file access, tool execution, or
quest/inventory mutation surface.

The optional **Settings → Local conversation setup** panel uses typed native C#
wrappers in `PumasClient.Discovery.cs`: `search_hf_models`,
`get_hf_download_details`, and `start_model_download_from_hf`. Search and inspection
only request metadata. Selecting an option opens a review panel; only **Request
download through Pumas** sends the acquisition RPC. Grouped options preserve the
exact filenames Pumas returns. A download ID is an acceptance receipt, not proof
of completion, verification, runtime installation, loading, or dialogue quality.

All setup transport uses the same strict loopback-only, no-proxy/no-redirect
boundary as conversation. Lanternwake does not contact Hugging Face, transfer
model bytes, verify artifacts, resolve revisions, or acquire runtimes. Pumas owns
these operations. Preview `main` is mutable; immutable revision is unknown in the
approved preview response. Pumas resolves the revision during acquisition and
stored model metadata can expose `upstream_revision`. Never promote `main` or a
download ID into an immutable-revision receipt. Closing cancels Lanternwake's
wait; it does not undo a request already accepted by Pumas. An interrupted or
unsupported acquisition response has unknown acceptance and is never replayed.
Check Pumas before sending another request.

This integration was historically source-qualified against Pumas commit
[`e37bbf4b964a0e2aadf25f80ab71edd8fa6b3eb3`](https://github.com/MrScripty/Pumas-Library/commit/e37bbf4b964a0e2aadf25f80ab71edd8fa6b3eb3),
whose Cargo package version is **0.7.0** and Git description is
**v0.7.0-117-ge37bbf4b**. It is not a claim of binary compatibility with every
0.7.0 release, later main revision, or provider. Requalify changed Pumas wire
contracts before upgrading.

Evidence at implementation time:

- 19 Python loopback-adapter contract tests passed. They use an explicitly fake
  Pumas service and prove only the modeled HTTP contract and rejection paths.
- A package-free .NET direct-client contract harness is supplied. Its execution
  depends on a .NET 8 SDK; see the project's verification record for latest run.
- **Real local Pumas + llama.cpp + pinned GGUF inference passed**, through the production C# client and native Godot UI. Exact release artifacts, hashes, latency and limitations are in [LIVE-QUALIFICATION.md](LIVE-QUALIFICATION.md). The tiny model failed a characterization sample; this is plumbing evidence, not production model-quality approval. Authored fallback remains visibly labeled when inference is unavailable.

## Actual upstream API, not an assumed service shape

The current source review uses approved Pumas `95a0baad`. The serving handler,
serving/runtime records, and inference gateway are byte-identical to historical
`e37bbf4`; the HTTP router/admission changes were reviewed separately. Current
checks and their limits are in [PUMAS95-CONTRACT-VERIFICATION.md](PUMAS95-CONTRACT-VERIFICATION.md).
The following source-owned routes and records were inspected:

- [RPC server router](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-rpc/src/server.rs):
  `POST /rpc` and, only with `inference-plugins`, `POST /v1/chat/completions`.
- [Serving handler](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-rpc/src/handlers/serving.rs):
  JSON-RPC `get_serving_status`, `validate_model_serving_config`, `serve_model`.
- [Serving records](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-core/src/models/serving.rs):
  response `result.success`, `result.snapshot.schema_version = 1`,
  `served_models`, `router_profiles`.
- [Runtime records](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-core/src/models/runtime_profile.rs):
  provider `LlamaCpp` serializes as **`llama_cpp`**. The version-manager app ID
  is **`llama-cpp`**; these identifiers are not interchangeable.
- [Pumas inference gateway](https://github.com/MrScripty/Pumas-Library/blob/95a0baad2d0aea4650fc36ad4afd969ac9391bf5/rust/crates/pumas-rpc/src/handlers/openai_gateway.rs):
  resolves served model ID/alias, rewrites the provider model identity, and
  proxies to the Pumas-owned runtime. Its OpenAI-compatible path is an actual
  Pumas API. Lanternwake does not contact OpenAI or bypass Pumas for llama.cpp.

Every conversation first calls `get_serving_status`. It follows Pumas alias-first
routing: one current loaded alias wins before a base model ID is considered.
The match must be unambiguous. Its instance must have provider `llama_cpp`,
a runtime profile, and a current router observation if one is present. Unknown
schema versions, stale routers, mismatched response IDs, missing models, and
other providers fail before generation. Pumas independently checks routing
again at generation admission, so its later rejection is preserved.

Generation uses `stream: false`, `max_tokens: 220`, `temperature: 0.7`, and one
system/user message pair. Only one nonempty string in an assistant-role
`choices[0].message.content` is accepted. Extra Pumas fields are forward-
compatible only when they do not change the consumed projection; duplicate
JSON properties are rejected. No output is parsed as a game instruction.

## Required runtime and setup

1. Supply an **inference-enabled** `pumas-rpc`. The full official v0.7.0 Linux package sidecar was exercised successfully; see `LIVE-QUALIFICATION.md` for its exact artifact/hash. The approved source pin below now has source-contract and real empty-service checks; loaded-model wire and live dialogue acceptance remain open.
   The audited upstream headless release archive is explicitly built with
   `--no-default-features` and cannot generate dialogue. A GUI-less process
   and an inference-disabled build are different choices.
2. Install/select a compatible llama.cpp runtime through Pumas. Serving code
   requires an active `llama-cpp` version; merely starting an unrelated
   `llama-server` does not satisfy the Pumas managed-runtime contract.
3. Register a licensed, instruction-capable GGUF in the Pumas model library,
   or explicitly request acquisition through the optional game setup panel.
   Preserve the actual model ID returned by Pumas. The adapter does not
   download models, install runtimes, or change profiles automatically.
4. Configure and serve that model with a `llama_cpp` profile. CPU placement is
   the least hardware-specific starting point; real latency and memory still
   need measurement. Use a unique alias such as `lanternwake-dialogue`.
5. Start the game with the exact URL and alias below.

For new source builds, use the approved Pumas revision
[`95a0baad2d0aea4650fc36ad4afd969ac9391bf5`](https://github.com/MrScripty/Pumas-Library/commit/95a0baad2d0aea4650fc36ad4afd969ac9391bf5)
after its prerequisites are provisioned. The default RPC release build, current
source projection, empty serving response, production-client unavailable result,
and normal Godot fallback were checked on this pin. **Loaded-model responses and
real dialogue inference on this pin remain unqualified.** The historical live
evidence remains tied to the exact artifacts in `LIVE-QUALIFICATION.md`; it is
not reused as a successful `95a0baad` inference run. Build/start commands:

```sh
cargo build --locked --manifest-path rust/Cargo.toml -p pumas-rpc --release
./rust/target/release/pumas-rpc --host 127.0.0.1 --port 8080 --launcher-root /absolute/pumas-root
```

The source pins Rust **1.92.0**. At the approved setup pin, Cargo does not
download ONNX Runtime: `ort` defaults and download/copy features are disabled.
The normal default-feature build above needs no ORT environment variables or
download-suppression flags. ONNX execution loads a separately provisioned native
library at runtime: set `ORT_DYLIB_PATH` to an absolute library **file**
(`.so`/`.dylib`/`.dll`), or place the platform-named library beside the executable.
A missing or invalid runtime returns a typed `runtime_library` backend error when
ONNX execution is requested. Build success alone does not qualify ONNX execution
or Lanternwake dialogue. Account for compiler cache and runtime/model storage;
process management and GPU monitoring remain default inference dependencies.

To create a dedicated CPU profile through the actual headless RPC after the
runtime and model have been provisioned:

```sh
curl --fail-with-body http://127.0.0.1:8080/rpc \
  -H 'Content-Type: application/json' --data '{
  "jsonrpc":"2.0","id":"game-profile","method":"upsert_runtime_profile",
  "params":{"profile":{"profile_id":"lanternwake-cpu","provider":"llama_cpp",
    "provider_mode":"llama_cpp_dedicated","management_mode":"managed",
    "name":"Lanternwake CPU","enabled":true,"port":8081,
    "endpoint_url":"http://127.0.0.1:8081","device":{"mode":"cpu","gpu_layers":0},
    "scheduler":{"auto_load":false}}}}'
```

Then call `validate_model_serving_config` followed by `serve_model`, replacing
`ACTUAL_LIBRARY_MODEL_ID` with the registered ID. Both accept this `params`:

```json
{"request":{"model_id":"ACTUAL_LIBRARY_MODEL_ID","config":{
  "provider":"llama_cpp","profile_id":"lanternwake-cpu","device_mode":"cpu",
  "gpu_layers":0,"context_size":4096,"keep_loaded":true,
  "model_alias":"lanternwake-dialogue"}}}
```

Wrap it in the usual `{"jsonrpc":"2.0","id":"unique-id","method":"serve_model","params":...}`
envelope. Inspect `result.success` and the domain validation/load outcome;
HTTP 200 alone is not proof of a loaded model. These setup calls mutate Pumas
configuration and load a model; they are operator setup, never game dialogue.

Launch the game in a terminal inheriting:

```sh
export LANTERNWAKE_PUMAS_URL=http://127.0.0.1:8080/
export LANTERNWAKE_PUMAS_MODEL=lanternwake-dialogue
```

`LANTERNWAKE_PUMAS_URL` defaults to `http://127.0.0.1:8080/`. There is no guessed
default model: an unset/empty model returns `model_unavailable`. Only HTTP with
a literal `127.0.0.1`, no credentials, query, fragment, or extra path is accepted.
HTTP proxies and redirects are disabled. The endpoint must be a trusted local
Pumas process; loopback is not a security boundary against other local users.
Do not put private real-world information in fictional dialogue.

For a small initial smoke model, the official
[Qwen2.5-0.5B-Instruct Q4_K_M GGUF](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF/tree/2ed9be962c95f7625f4963ff51ed472e4538187a)
listing is approximately 491 MB and Apache-2.0 licensed. This is an untested
smoke candidate, not a demonstrated dialogue-quality recommendation. A tiny
model may invent facts despite prompting; game-state isolation must remain
structural. No weights are included in this project.

## Lifecycle and UI outcomes

The C# client permits one generation at a time, uses a 5-second connect budget,
a 50-second overall request budget, and a caller cancellation token. It does
not automatically retry. Responses are bounded to 256 KiB and visible text to
4,000 Unicode scalar values. Input bounds: character 160, context 12,000,
player text 2,000. The UI should bound player input before calling it.

The caller owns operation cancellation and stale-reply suppression: cancel
when leaving the conversation, await the invocation's terminal result, and
apply a successful reply only if its original conversation is still active.
Cancel and await outstanding work before disposing `PumasClient`. The client
uses context-free continuations because it touches no engine state; the UI
must resume on Godot's main thread before rendering text, and disable markup
interpretation for model output.

Cancellation closes this client's HTTP request. It does not certify that a
native provider has already stopped all computation. No canonical state can be
changed by a late response, and the application must not replay it.

Suggested presentation of stable `ErrorCode` values:

- `model_unavailable`: “Choose and load a llama.cpp dialogue model in Pumas.”
- `wrong_provider`: “This conversation needs a Pumas llama.cpp model.”
- `pumas_contract`: “Pumas inference is disabled or its API is incompatible.”
- `pumas_unavailable`: “Pumas is unavailable or rejected the request.”
- `invalid_response`: “Pumas returned an unsupported dialogue reply.”
- `invalid_request`: “Shorten or correct the conversation text.”
- `busy`: “The previous conversation is still finishing.”
- `timeout`: “The local model took too long. You can try again.”
- `cancelled`: normally no error toast; the player closed or changed the chat.

Never present any of these outcomes as a successful live-model reply.

## Verification commands and optional adapter

```sh
dotnet run --project integration/pumas/ClientTests/ClientTests.csproj
dotnet run --project integration/pumas/DiscoveryTests/DiscoveryTests.csproj
python3 -m unittest discover -s integration/pumas -v
```

The .NET harness exercises the actual native game client against a simulated
Pumas server. The Python suite exercises the optional Python adapter only.
Neither establishes real model availability, latency, llama.cpp compatibility,
quality, or engine main-thread behavior.

For tools that require the simpler `{character, context, player_text}` →
`{text}` HTTP contract, the optional Python standard-library adapter is in
`integration/pumas/bridge.py`. **The game does not need or use this bridge.**

```sh
python3 integration/pumas/bridge.py --pumas-url http://127.0.0.1:8080 --model lanternwake-dialogue --check
python3 integration/pumas/bridge.py --pumas-url http://127.0.0.1:8080 --model lanternwake-dialogue
```

Its `POST http://127.0.0.1:8787/conversation` returns exactly `{"text":"..."}`
on success; failures are non-200 `{"error":{"code":"...","message":"..."}}`.
`GET /health` reports readiness only after querying real Pumas serving state.
It accepts no extra request fields or browser Origin, limits requests to
24,576 bytes, admits at most four connections and one generation, and joins
owned workers on shutdown. Its upstream socket timeout is a **45-second idle
budget**, not a total generation deadline. Caller disconnection does not
immediately cancel upstream work; the adapter neither retries nor claims
provider cleanup. It retains no dialogue logs or state.

Broader production acceptance procedure, still required: record exact Pumas commit/binary
hash, llama.cpp version, model revision/hash, OS/CPU, and model profile; observe
loaded status; send a real game conversation; observe its displayed reply;
close during generation and verify no late UI/state update; disconnect Pumas
and verify a clearly labeled unavailable result. Compare the deterministic
world state before and after free chat. Only that run can qualify live use.
