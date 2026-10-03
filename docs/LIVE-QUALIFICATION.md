# Local inference and speech qualification, 2026-10-02

This milestone establishes working local plumbing with a compact test model. It does **not** qualify production dialogue quality, a physical microphone, or a five-hour playthrough.

## Pumas → llama.cpp → production C# client

The official [Pumas v0.7.0 prerelease](https://github.com/MrScripty/Pumas-Library/releases/tag/v0.7.0) full Linux `.deb` was downloaded and checksum-verified, then extracted locally without system installation. Its actual `pumas-rpc` sidecar was used headlessly. Unlike an inference-disabled headless build, this binary initialized the llama.cpp manager and exposed the real serving/gateway routes.

- Package: `PumasLibrary_0.7.0_Setup_x86_64.deb`
- Package SHA-256: `08984d1c4284027e612b39d6855577bf8fb1926c7450f59b0b1171dc4a753f47`
- Sidecar SHA-256: `faaf57ad46101fa479506f78781b17e467cf0365b6632966546fd0cef1693130`
- Runtime installed/activated **through Pumas**: [llama.cpp b11352 CPU](https://github.com/ggml-org/llama.cpp/releases/tag/b11352), Pumas tag `b11352+cpu`
- Official runtime archive SHA-256: `9ddab20c8f93d408f3d30ed6d9ba260edaf4f78437ed49fb31c5c64f6aa32e16`
- Model: official Qwen2.5-0.5B-Instruct-GGUF Q4_K_M, pinned revision `2ed9be962c95f7625f4963ff51ed472e4538187a`
- Model SHA-256: `74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db`
- Dedicated CPU profile, zero GPU layers, 4,096 context tokens; process affinity restricted to two CPUs

Observed sequence: `install_version` → installed-version check → `switch_version` → `import_model` → `upsert_runtime_profile` → `validate_model_serving_config` → `serve_model` → `get_serving_status`.

The real snapshot reported schema1, provider `llama_cpp`, load state `loaded`, the exact registered model ID and alias, and `endpoint_mode=pumas_gateway`. The unmodified production `PumasClient` then called that Pumas gateway and received a nonempty real response in **1,601ms**. No direct llama.cpp request substituted for Pumas.

The release binary was qualified by its actual behavior and artifact hash. The separate source inspection at e37bbf4 does not make this release byte-equivalent to that later source commit.

## Native Godot path and observed quality limit

The compiled native Godot game ran against the same real Pumas profile. Its ordinary `SendReply` path returned a generated response, stored it in history, left the canonical beat unchanged, and visibly displayed the “Local Pumas conversation” label. The real rendered viewport was captured as `artifacts/captures/live-pumas.png`; it is separate from the title screenshot.

**Quality finding:** the 0.5B model addressed the player using the speaking character's name. This is a failed characterization sample, not a polished dialogue demonstration. Context was repaired to name the speaking character and Ada, the player, separately, with regression assertions. That prompt repair is not evidence that the small model is now reliable. Larger-model selection, adversarial spoiler/identity tests and narrative evaluation remain open. No additional model budget was used.

## Historical, removed whisper.cpp adapter

This section records the previous implementation at `a53e4937e355074d5aaf1fbb70cbea41c0c4f3bd`. It is no longer the production path. The current Cohere/Pumas status is in [SPEECH.md](SPEECH.md); none of the following evidence qualifies Cohere.

- Official [whisper.cpp v1.9.4](https://github.com/ggml-org/whisper.cpp/releases/tag/v1.9.4), CPU build with official CMake4.4.4
- `whisper-cli` SHA-256: `b7ffc161b9ecb8bad27a418d0e517961eafab059cf16d379164f5056fd47a099`
- Official-script `tiny.en` model SHA-256: `921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f`

The actual production `LocalSpeechTranscriber` was run against the bundled known speech sample. It passed recognition and temporary-file cleanup, 48kHz input conversion, and a successful independent retry after cancellation. The test exposed and fixed integer multiplication overflow during resampling; conversion now promotes the rate calculation before multiplying.

Subprocess cancellation is checked separately through the same production adapter with a task-owned Linux executable fixture. The fixture publishes its PID and remains running. The test observes that PID alive before requesting cancellation, then asserts it has exited when the adapter completes cancellation and checks temporary-file cleanup. This replaces the earlier timer-only assertion, which could cancel during WAV preparation without starting a subprocess. The strengthened test passed alongside actual whisper.cpp recognition and retry; it qualifies the adapter's process ownership, not recognizer-specific cancellation behavior or physical microphone capture.

Reproduce after provisioning the official executable/model:

```
dotnet run --project integration/speech/SpeechSmoke.csproj -- /absolute/whisper-cli /absolute/ggml-tiny.en.bin /absolute/jfk.wav /absolute/empty-test-temp
```

The audio-input device remains unavailable in this cloud desktop (dummy audio driver after no ALSA card). A file fixture is not a microphone. Real permission/recording quality, hardware denial and device changes remain unqualified. Transcripts are still editable and never auto-submitted.

## Resource and setup findings

A single-job source build hit a refused ONNX binary mirror, then a memory kill compiling Pumas's main library. A Microsoft-official matching ONNX dependency was tested as the supported source-build alternative; no TLS protection was disabled. The final successful run used the verified official prebuilt sidecar instead of repeating OOM builds.

Task-local HOME/XDG directories were needed to keep Pumas's configuration/cache writable and isolated. Integration commands owned server startup, client checks and shutdown. No sandbox/network protections, credentials or security settings were changed. Runtime/model files and build caches are ignored and are not repository deliverables.
