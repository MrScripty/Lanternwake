# Cohere Transcribe through Pumas Library

Voice input is temporarily **unsupported**. The requested backend is local Cohere Transcribe owned by Pumas Library. Direct whisper.cpp code, model settings and executable invocation have been removed. Typed replies and editable suggestions work normally.

`PumasSpeechTranscriber` owns this explicit unavailable boundary. The Use voice action explains the missing integration before recording or requesting microphone consent. It does not guess an endpoint, return a fabricated transcript, invoke a vendor CLI, or fall back to another recognizer. Old `LANTERNWAKE_WHISPER_*` settings have no effect.

The retained `SpeechRecorder` passes an operation-local, in-memory stereo sample buffer and the Godot capture sample rate to that typed game boundary. It has no audio-file writer. It clears transferred samples in `finally` after transcription completes or fails, and clears retained samples on disposal. This implementation is gated and establishes no physical capture or recognition result. The required producer contract must specify conversion/admission to the mono 16 kHz input below; that conversion is not implemented by the unavailable transcriber.

## Producer dependency and re-enable conditions

Pumas currently supports text/image/embedding inference, but does not expose a transcription loader or audio transcription gateway. Its unsupported-route tests include audio. The Pumas implementation owner must supply the reusable capability and lifecycle contract before the Lanternwake consumer can be enabled. Coordinate against [Pumas-Library](https://github.com/MrScripty/Pumas-Library), then bind the implementation version in this document.

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
