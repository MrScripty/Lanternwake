# Cohere Transcribe through Pumas Library

Voice input is temporarily **unsupported**. The requested backend is local Cohere Transcribe owned by Pumas Library. Direct whisper.cpp code, model settings and executable invocation have been removed. Typed replies and editable suggestions work normally.

`PumasSpeechTranscriber` owns this explicit unavailable microphone boundary. `PumasAudioTextClient` now implements the generic consumer described below, with synthetic transport/lifecycle qualification only. The Use voice action explains the missing integration before recording or requesting microphone consent. It does not guess an endpoint, return a fabricated transcript, invoke a vendor CLI, or fall back to another recognizer. Old `LANTERNWAKE_WHISPER_*` settings have no effect.

The retained `SpeechRecorder` now connects explicitly consented Godot capture to the generic
adapter. Shipping installed-runtime admission remains independently closed;
capability JSON, saved preferences and the debug fixture cannot authorize a
microphone. `Use voice` first probes the selected local contract. The existing
consent dialog wraps inside a 640px viewport and starts capture only after
`Start recording`. Its second probe must preserve the consent-bound model/profile;
a changed selection requires fresh consent. There is no background recording,
automatic permission acceptance, direct recognizer or non-Pumas audio destination.

The existing muted `AudioStreamPlayer`/`AudioEffectCapture` path is owned by
`GodotSpeechCaptureSource`. Each capture owns a uniquely named bus, stream,
playback and effect. Stop resolves the bus by name rather than a stale index,
stops microphone playback/input, clears unread capture frames and releases owned
resources. Device start failure, read failure, capture overflow or three seconds
without frames discards the clip and restores typed input. Native mixer ownership
may settle on later frames; app shutdown also drains authored audio.

`SpeechSampleBuffer` retains at most 30 seconds or the transport's PCM budget,
with envelope headroom at high sample rates. It validates finite normalized
stereo floats and zeroes entire replaced/disposed arrays. Temporary source arrays
are also zeroed. Either the sample bound or 30-second elapsed limit stops capture
without sending. The user must choose `Stop and transcribe` or, after the limit,
`Transcribe recording`. Return, Load and close discard the clip. Transcription
moves its sample buffer into the original operation; cancellation retains it
until that transport settles, then zeroes it in `finally`. Pending remains true
through this cleanup. No audio file is written. Only the editable entry receives
a current-generation transcript; `Say this` remains a separate user action.
Consent window identity and generation checks reject queued callbacks and awaited
rechecks after Load, Return or close. Unknown producer outcomes remain blocked
across conversation reopen and settings changes.

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
provider, voice preferences and story/save slots are preserved. The shipping microphone
boundary remains explicitly Unsupported even if a synthetic capability says
available. Actual installed audio qualification must precede capture activation;
preferences or advertised availability are not an override.

`integration/speech/AudioModalityTests.cs` uses controlled HTTP handlers and an
actual local TCP fixture. It checks wire PCM, selection, schema/correlation,
refusals, cancellation/timeout/disposal with deliberately delayed transport
settlement, owned-buffer cleanup and no replay. Synthetic transcript strings
are labelled fixtures and qualify no inference, physical microphone or ASR quality.

## Producer dependency and re-enable conditions

The pinned Pumas source exposes generic audio admission grammar and private ownership foundations, while installed audio remains unqualified and publicly unavailable. The Pumas implementation owner must supply the reusable capability and lifecycle contract before shipping microphone admission can be issued. The generic consumer and bounded capture path are now implemented and qualify only against owned synthetic sources. Coordinate against [Pumas-Library](https://github.com/MrScripty/Pumas-Library), then bind the implementation version in this document.

The bounded integration requires:

1. Pumas discovers a ready local Cohere Transcribe model and explicitly identifies its repository/revision or content digest, native cohere_asr architecture, managed runtime, supported language, input constraints and local execution capability. A generic loaded model is insufficient.
2. Pumas owns bounded mono 16 kHz PCM admission (explicit encoding, sample count and language), decoding and inference through its managed runtime. The native Transformers loader must use local_files_only=True and trust_remote_code=False. Specify the actual request/response schema and route in the producer first; the implemented generic consumer uses the exact pinned PR61 grammar above.
3. Pumas owns operation cancellation and terminal cleanup. The game retains each request through completion and discards replies after scene advance, load, close or shutdown. Cancellation of a client HTTP request alone is not evidence that inference stopped.
4. After consumer wiring, qualify success, cancellation after observed admission, timeout, malformed/oversized audio or responses, missing/wrong model, close/reopen, retry and shutdown. Use a synthetic fixture first, followed by approved local model execution. Verify the game's in-memory sample cleanup and producer-side audio custody and terminal cleanup; if the producer uses temporary files, qualify their removal separately. Do not infer producer cleanup from client cancellation.
5. Re-enable capture only when the local capability has been verified. Keep the existing explicit microphone consent, 30-second capture limit, operation-local samples, editable transcript and separate Say this action. Physical microphone/device behavior needs its own evidence.

The review trigger is publication of the Pumas transcription contract. Pumas owns inference; Lanternwake owns capture, editable input and stale-result rejection. This milestone implements bounded capture and generic consumer wiring, while installed inference and physical microphone qualification remain separate dependencies.

## Official model and acquisition facts

[Cohere Transcribe documentation](https://docs.cohere.com/docs/transcribe) identifies `cohere-transcribe-03-2026`, a 2B Conformer audio-to-text model supporting 14 languages. Language must be selected explicitly. [The official model card](https://huggingface.co/CohereLabs/cohere-transcribe-03-2026) documents local Transformers >=5.4.0 with `CohereAsrForConditionalGeneration` and separately vLLM serving. [The official file listing](https://huggingface.co/CohereLabs/cohere-transcribe-03-2026/tree/main) lists 4.13 GB of weights. License: [Apache 2.0](https://www.apache.org/licenses/LICENSE-2.0).

The same vendor also offers a hosted service. That is a separate data destination and is not configured by this game. No hosted credentials, paid API calls, model downloads or private audio uploads are part of this milestone. The Hugging Face page currently requires contact-sharing acceptance; exact fields and conditions are not visible while logged out. No gate was accepted and no official ungated distribution has been established.

## Evidence

`dotnet run --project integration/speech/SpeechSmoke.csproj` checks the production
unavailable boundary, legacy rejection, 106 generic synthetic contract assertions
and 19 sample-buffer assertions. `python3 integration/qa/speech_capture.py` runs a
marked disposable profile and real Godot generator through the capture bus and
generic adapter, using a controlled HTTP handler. It covers consent cancellation,
stale confirmation and held recheck after Load, shifted buses, an injected elapsed
limit with no POST, editable transcript/save preservation, device failure,
starvation/read failure, cancellation and timeout with deliberately held transport
custody, sticky unknown outcomes, and active-source disposal. The fixture permit
cannot authorize a microphone. Rendered runs display **OWNED SYNTHETIC AUDIO ·
NO MICROPHONE · NO INFERENCE** and qualify actual controls and the elapsed limit;
they do not qualify an installed Pumas model, OS permissions or physical capture.
The full `scripts/verify.sh` includes the native capture gate. Real hardware and
producer-owned device/artifact cleanup need their own evidence.
