# Cohere Transcribe through Pumas Library

Voice input is temporarily **unsupported**. The requested backend is local Cohere Transcribe owned by Pumas Library. Direct whisper.cpp code, model settings and executable invocation have been removed. Typed replies and editable suggestions work normally.

`PumasSpeechTranscriber` owns this explicit unavailable microphone boundary. `PumasAudioTextClient` now implements the generic consumer described below, with synthetic transport/lifecycle qualification only. The Use voice action explains the missing integration before recording or requesting microphone consent. It does not guess an endpoint, return a fabricated transcript, invoke a vendor CLI, or fall back to another recognizer. Old `LANTERNWAKE_WHISPER_*` settings have no effect.

The retained `SpeechRecorder` now connects explicitly consented Godot capture to the generic
adapter. Shipping installed-runtime admission remains independently closed;
capability JSON, saved preferences and the debug fixture cannot authorize a
microphone. Once independently admitted, `Use voice` first probes the selected local contract. The existing
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

Pinned producer source snapshot associated with [PR61](https://github.com/MrScripty/Pumas-Library/pull/61), commit
`aa1225b8011e7b16303775614ac8e3423a2069a9`, tree
`bac873ddcb4235117c2a6b754a4991dab3db7293`. The source contract is
`rust/crates/pumas-rpc/src/handlers/model_operations/{types,modality}.rs` and
`rust/crates/pumas-rpc/README.md`. This consumer is tied to that exact source snapshot, not the current PR status,
a production Pumas release or installed-runtime acceptance.

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
fail before submission. Encoding uses fixed, owned mutable JSON/base64 buffers,
clears both on success or failure, and avoids the writer's pooled base64 scratch
and an immutable base64 string. It writes no audio file and clears PCM/request
bytes after the original consumer transport settles. Source sample custody remains with the
recorder, which clears its transferred buffer in `finally`.

Results require bounded JSON, unique properties, contract/request correlation,
text kind and observed `stop` or `length`; truncation remains explicit. Typed
errors preserve `not_admitted` versus `unknown`. The 90-second consumer deadline,
Load/close cancellation and disposal cancel the owned HTTP operation. The pinned
snapshot has **no public status or cancellation-acknowledgment route**. Lost/malformed replies,
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

The pinned producer snapshot defines the generic audio grammar used by this
consumer. The bounded capture path and adapter are implemented and qualified
against owned synthetic sources. Shipping installed-runtime admission remains
closed. Re-enabling it requires an approved installed producer version, actual
capability/lifecycle verification and physical-device acceptance; document that
version and evidence separately from this source snapshot.

The bounded integration requires:

1. Pumas discovers a ready local Cohere Transcribe model and explicitly identifies its repository/revision or content digest, native cohere_asr architecture, managed runtime, supported language, input constraints and local execution capability. A generic loaded model is insufficient.
2. Pumas owns bounded audio admission, decoding/resampling and inference through its managed runtime. This consumer sends the pinned stereo PCM grammar above; the approved producer must establish the selected recognizer's native input requirements and local execution rather than expecting the game to invoke a loader or guess another route.
3. Pumas owns operation cancellation and terminal cleanup. The game retains each request through completion and discards replies after scene advance, load, close or shutdown. Cancellation of a client HTTP request alone is not evidence that inference stopped.
4. Installed acceptance must qualify success, cancellation after observed admission, timeout, malformed/oversized audio or responses, missing/wrong model, close/reopen, retry and shutdown. Use a synthetic fixture first, followed by approved local model execution. Verify the game's in-memory sample cleanup and producer-side audio custody and terminal cleanup; if the producer uses temporary files, qualify their removal separately. Do not infer producer cleanup from client cancellation.
5. Re-enable capture only when the local capability has been verified. Keep the existing explicit microphone consent, 30-second capture limit, operation-local samples, editable transcript and separate Say this action. Physical microphone/device behavior needs its own evidence.

The review trigger is an approved installed Pumas transcription capability and lifecycle contract. Pumas owns inference; Lanternwake owns capture, editable input and stale-result rejection. This milestone implements bounded capture and generic consumer wiring, while installed inference and physical microphone qualification remain separate dependencies.

## Model selection and acquisition

The intended local recognizer is Cohere Transcribe through Pumas. Model identity,
language, supported encoding, licensing, storage, acquisition terms and installed
runtime compatibility must be verified for the approved deployment before enabling
capture. [Cohere's documentation](https://docs.cohere.com/docs/transcribe) and the
[official model card](https://huggingface.co/CohereLabs/cohere-transcribe-03-2026)
are references for that review, not installed-runtime acceptance receipts.

A hosted speech service would be a separate data destination. This local consumer
does not reroute audio to one; saved hosted preferences cannot authorize capture.

## Evidence

`dotnet run --project integration/speech/SpeechSmoke.csproj` checks the production
unavailable boundary, legacy rejection, generic synthetic contract assertions
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
