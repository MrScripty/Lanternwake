# Cohere Transcribe through Pumas Library

Voice input is temporarily **unsupported**. The requested backend is local Cohere Transcribe owned by Pumas Library. Direct whisper.cpp code, model settings and executable invocation have been removed. Typed replies and editable suggestions work normally.

`PumasSpeechTranscriber` owns this explicit unavailable microphone boundary. `PumasAudioTextClient` now implements the generic consumer described below, with synthetic transport/lifecycle qualification only. The Use voice action explains the missing integration before recording or requesting microphone consent. It does not guess an endpoint, return a fabricated transcript, invoke a vendor CLI, or fall back to another recognizer. Old `LANTERNWAKE_WHISPER_*` settings have no effect.

The retained `SpeechRecorder` passes an operation-local, in-memory stereo sample buffer and the Godot capture sample rate to that typed game boundary. It has no audio-file writer. It clears transferred samples in `finally` after transcription completes or fails, and clears retained samples on disposal. This implementation is gated and establishes no physical capture or recognition result. The generic consumer preserves the actual sample rate and stereo float PCM. PR61 assigns decoding/normalization to the owned Pumas bridge; Lanternwake does not relabel unconverted samples mono 16 kHz.

## Generic consumer contract (Pumas PR61)

Pinned producer: [PR61](https://github.com/MrScripty/Pumas-Library/pull/61), commit
`aa1225b8011e7b16303775614ac8e3423a2069a9`, tree
`bac873ddcb4235117c2a6b754a4991dab3db7293`. The source contract is
`rust/crates/pumas-rpc/src/handlers/model_operations/{types,modality}.rs` and
`rust/crates/pumas-rpc/README.md`. This is consumer preparation against that
exact draft, not a production Pumas pin, release or installed-runtime acceptance.

`PumasAudioTextClient` reads `GET /v1/capabilities?model=...` with the configured
optional profile, accepts contract 1 and the selected model/profile's unambiguous
`speech_to_text` declaration with stereo float PCM input and text output, and
requires available state plus the 512-token option bound. A generic model's
modality label alone cannot establish availability. Unqualified/missing/wrong/
ambiguous selections are rejected before any audio submission. Hosted speech
preferences remain preserved but cannot reroute this local consumer.

The consumer submits only `POST /v1/model-operations`: contract version 1, unique
request ID, selected model and discovered profile, `input.kind=audio`, actual
mix rate, two channels, exact frozen frame count, `pcm_f32le` bytes, text output,
semantic hint from the declaration, explicit language and 512-token bound. It
sends no named `capability` and invents no transcription-specific endpoint.
The 30-second capture/envelope limit and 32 MiB transport bound are retained.
Invalid sample rate, nonfinite/out-of-range floats, excess bytes or duration
fail before submission. Encoding uses owned mutable buffers, avoids an immutable
base64 string, writes no audio file and clears PCM/base64 request bytes after the
original consumer transport settles. Source sample custody remains with the
recorder, which clears its transferred buffer in `finally`.

Results require bounded JSON, unique properties, contract/request correlation,
text kind and observed `stop` or `length`; truncation remains explicit. Typed
errors preserve `not_admitted` versus `unknown`. The 90-second consumer deadline,
Load/close cancellation and disposal cancel the owned HTTP operation. PR61 has
**no public status or cancellation-acknowledgment route**. Lost/malformed replies,
post-submission cancellation, timeout and unknown producer errors therefore
cannot prove producer cessation or cleanup. That client refuses successor
operations after an unknown outcome; it never retries, changes model or invents
a fallback transcript. Recreating a consumer is not proof of producer recovery:
the Pumas owner must establish settlement or exact owning-process drain first.
Disposal retains client/body custody until the original transport settles; no
consumer observation claims native device/artifact release.

Independent speech configuration now retains optional profile and a closed
language choice (`en` for this English story), defaults old configurations
without changing their provider, URL or model, and passes transcription
preferences into the recorder on load/save. Owner scenes, menu layout, dialogue
provider, voice preferences and story/save slots are preserved. The microphone
boundary remains explicitly Unsupported even if a synthetic capability says
available. Actual installed audio qualification must precede capture activation;
preferences or advertised availability are not an override.

`integration/speech/AudioModalityTests.cs` uses controlled HTTP handlers and an
actual local TCP fixture. It checks wire PCM, selection, schema/correlation,
refusals, cancellation/timeout/disposal with deliberately delayed transport
settlement, owned-buffer cleanup and no replay. Synthetic transcript strings
are labelled fixtures and qualify no inference, physical microphone or ASR quality.

## Producer dependency and re-enable conditions

The pinned Pumas source exposes generic audio admission grammar and private ownership foundations, while installed audio remains unqualified and publicly unavailable. The Pumas implementation owner must supply the reusable capability and lifecycle contract before the Lanternwake consumer can be enabled. Coordinate against [Pumas-Library](https://github.com/MrScripty/Pumas-Library), then bind the implementation version in this document.

The bounded integration requires:

1. Pumas discovers a ready local Cohere Transcribe model and explicitly identifies its repository/revision or content digest, native cohere_asr architecture, managed runtime, supported language, input constraints and local execution capability. A generic loaded model is insufficient.
2. Pumas owns bounded mono 16 kHz PCM admission (explicit encoding, sample count and language), decoding and inference through its managed runtime. The native Transformers loader must use local_files_only=True and trust_remote_code=False. Specify the actual request/response schema and route in the producer first; this game currently assumes none.
3. Pumas owns operation cancellation and terminal cleanup. The game retains each request through completion and discards replies after scene advance, load, close or shutdown. Cancellation of a client HTTP request alone is not evidence that inference stopped.
4. After consumer wiring, qualify success, cancellation after observed admission, timeout, malformed/oversized audio or responses, missing/wrong model, close/reopen, retry and shutdown. Use a synthetic fixture first, followed by approved local model execution. Verify the game's in-memory sample cleanup and producer-side audio custody and terminal cleanup; if the producer uses temporary files, qualify their removal separately. Do not infer producer cleanup from client cancellation.
5. Re-enable capture only when the local capability has been verified. Keep the existing explicit microphone consent, 30-second capture limit, operation-local samples, editable transcript and separate Say this action. Physical microphone/device behavior needs its own evidence.

The review trigger is publication of the Pumas transcription contract. Pumas owns inference; Lanternwake owns capture, editable input and stale-result rejection. This milestone is a cutover/removal with an explicit dependency, not completion of speech recognition.

## Official model and acquisition facts

[Cohere Transcribe documentation](https://docs.cohere.com/docs/transcribe) identifies `cohere-transcribe-03-2026`, a 2B Conformer audio-to-text model supporting 14 languages. Language must be selected explicitly. [The official model card](https://huggingface.co/CohereLabs/cohere-transcribe-03-2026) documents local Transformers >=5.4.0 with `CohereAsrForConditionalGeneration` and separately vLLM serving. [The official file listing](https://huggingface.co/CohereLabs/cohere-transcribe-03-2026/tree/main) lists 4.13 GB of weights. License: [Apache 2.0](https://www.apache.org/licenses/LICENSE-2.0).

The same vendor also offers a hosted service. That is a separate data destination and is not configured by this game. No hosted credentials, paid API calls, model downloads or private audio uploads are part of this milestone. The Hugging Face page currently requires contact-sharing acceptance; exact fields and conditions are not visible while logged out. No gate was accepted and no official ungated distribution has been established.

## Evidence

`dotnet run --project integration/speech/SpeechSmoke.csproj` checks the production unavailable boundary, repeated calls, cancellation and rejection of legacy recognizer settings. The Godot UI smoke verifies Use voice shows the explanation without starting capture or disabling typed input. These are cutover checks, not Cohere inference qualification.

Earlier Whisper recognition/process tests remain historical evidence only in Git history and [LIVE-QUALIFICATION.md](LIVE-QUALIFICATION.md). They do not qualify the new backend.
