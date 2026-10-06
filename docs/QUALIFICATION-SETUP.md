> Historical setup record: the Whisper setup below was superseded by the requested Cohere Transcribe through Pumas integration. Do not use it to configure current voice input. See [SPEECH.md](SPEECH.md).

# Proposed local inference and speech qualification bundle

No model weights or additional inference/speech tools were installed during the initial build. This document is a bounded setup proposal, not execution evidence.

## Reuse

Use the already provisioned Rust 1.92.0 toolchain/Cargo cache, g++/make/ffmpeg, and official Godot 4.6.3 .NET/.NET 8.0.425 SDK. Keep all game-specific tools, models, state, build outputs and test recordings in a dedicated workspace. Do not change other projects' runtime profiles or delete their caches.

## Sources and small smoke candidates

- Historical Pumas wire qualification: [e37bbf4](https://github.com/MrScripty/Pumas-Library/commit/e37bbf4b964a0e2aadf25f80ab71edd8fa6b3eb3). For new source setup use approved [95a0baad2d0aea4650fc36ad4afd969ac9391bf5](https://github.com/MrScripty/Pumas-Library/commit/95a0baad2d0aea4650fc36ad4afd969ac9391bf5); this does not establish fresh Lanternwake wire or live-inference qualification. Build with `cargo build --locked --manifest-path rust/Cargo.toml -p pumas-rpc --release` and default `inference-plugins`. This build does not download ONNX Runtime and needs no ORT environment variables or download-suppression flags. ONNX execution requires a separately provisioned library: `ORT_DYLIB_PATH` is an absolute library file, or the platform-named library is adjacent to the executable. Missing/invalid runtime returns the typed `runtime_library` backend error when ONNX execution is requested. See [PUMAS.md](PUMAS.md) for current setup; this historical bundle is not an automatic dependency-download recommendation.
- llama.cpp: official [ggml-org releases](https://github.com/ggml-org/llama.cpp/releases), Linux x64 CPU runtime selected and managed by Pumas. Pin the exact release and verify the downloaded artifact before use. No CUDA runtime is needed for the CPU smoke.
- LLM smoke weights: official [Qwen2.5-0.5B-Instruct Q4_K_M GGUF](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF/blob/2ed9be962c95f7625f4963ff51ed472e4538187a/qwen2.5-0.5b-instruct-q4_k_m.gguf), approximately 491 MB. This tiny model can qualify plumbing, not adequate characterization, truthfulness or product quality.
- Speech engine: official [whisper.cpp](https://github.com/ggml-org/whisper.cpp), CPU-only `whisper-cli` built from a pinned revision.
- Speech weights: `tiny.en`, approximately 75 MiB, acquired through whisper.cpp's official model-download script from its documented model source. Use its bundled `samples/jfk.wav` first. This tests file transcription; a physical microphone test remains separate.
- If no reusable CMake installation exists, provision an official [Kitware CMake release](https://github.com/Kitware/CMake/releases) locally. Do not install a system-wide package by default.

## Disk and stopping boundary

These are planning estimates, not measured build outputs:

- Pumas compilation + new native dependencies: 1.2–2.5GB peak
- CPU llama.cpp runtime: 100–250MB
- Qwen GGUF: 491 MB
- whisper.cpp source/build: 100–200 MB, plus 75 MiB weights
- CMake if needed: 100–200 MB

Allow roughly 2.1–3.8 GB new peak space, plus existing artifacts. Start the full Pumas build only with at least 4 GB free and preserve an 800 MB free-space floor; stop rather than consuming another project's safety margin. Use one build job, no incremental build, stripped debug data, and a task-owned target directory. Measure usage between stages. Remove only documented reproducible outputs owned by this qualification task after they are no longer needed; never remove user data or another worker's files.

## Required evidence

1. Build and start real inference-enabled Pumas on loopback; register the actual licensed GGUF; configure a dedicated CPU profile via Pumas; validate and serve it.
2. Confirm `get_serving_status` reports exactly the configured model/alias, `llama_cpp`, loaded state and current routing.
3. Run `dotnet run --project integration/pumas/LiveSmoke/LiveSmoke.csproj` with `LANTERNWAKE_PUMAS_URL` and `LANTERNWAKE_PUMAS_MODEL`. Record real result, latency and source/runtime/model versions. No authored fallback passes this check.
4. Launch Godot with the same configuration and submit an editable/free-text reply. Confirm local result label and history, cancel/reopen behavior and canonical-state invariants.
5. Build whisper.cpp and transcribe its known sample. Verify transcript, failures, cancellation/reaping and temp-file cleanup.
6. Only with an actual audio-input device: explicit consent, ≤30-second capture, editable transcript, no autosubmit, denial/cancel/reopen and shutdown during work. The cloud GUI currently falls back to a dummy audio driver because no ALSA card is present; file transcription cannot satisfy microphone qualification.

Record these separately from the existing simulated protocol tests, headless Godot tests and visual screenshots. Do not describe a smoke-sized model as the final dialogue model.
