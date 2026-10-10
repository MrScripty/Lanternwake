# Local Cohere transcription through Pumas

Lanternwake supports the **explicit experimental local Cohere CPU** path in Pumas
main `ad31e391dcd94c204b3fcd748cc98d27b3281e65`. It is an experiment,
not production-qualified ASR. Real-model accuracy, memory requirements and
physical microphone behavior have not been measured in this cloud instance.
The user supplies the real model and runs those checks locally.

The default runtime mode is Off, including migrated older settings. Enabling the
microphone checkbox alone does not admit capture. AI setup → Transcription lets
you select Experimental local Cohere CPU, the local Pumas URL, the indexed model
ID returned by Pumas import and an explicit loaded runtime profile. Linux
x86_64 with Landlock ABI 6 or newer is required. A read-only host preflight
refuses unsupported systems; Pumas remains responsible for all runtime/model
checks and confinement. No environment variable or capability JSON overrides
the user's mode selection or host refusal. Hosted preferences remain saved but
cannot route microphone audio away from local Pumas.

## Prepare your local runtime

Use the [Pumas installed audio session contract and local workflow](https://github.com/MrScripty/Pumas-Library/blob/ad31e391dcd94c204b3fcd748cc98d27b3281e65/docs/contracts/installed-audio-session.md).
Build/run the normal inference-enabled `pumas-rpc` with an explicit library root;
keep one owner for that root. Preview and install its trusted `cohere-asr` CPU
runtime, wait for terminal installation success, switch to the installed version,
and create a managed CPU profile (for example `cohere-local`). Use
`import_local_cohere` with your existing model directory, then
`serve_experimental_local_cohere` with its returned indexed ID and that profile.
Proceed only after `loaded: true` and an experimental report with
`experimental: true`, `production_available: false`.

Lanternwake attaches to that loaded selection. It does not download weights,
run model Python, start an interpreter, change runtime permissions, import/load
models automatically, or replace Pumas's closed ordinary shipping policy.
The runtime is borrowed: closing Lanternwake cancels its requests but leaves
Pumas running. Use Pumas's `unserve_model` or captured-generation
`stop_runtime_profile_if_generation` to stop and join the original child. A lost
or cancelled HTTP request alone is not evidence of producer drainage.

## Test without entering the game

1. From the title menu, open **AI setup → Transcription**. Enable microphone
   transcription, choose **Experimental local Cohere CPU (Pumas)** and enter
   your URL (for example `http://127.0.0.1:18743/`), indexed model ID and profile.
2. Choose **Record test clip…**. The selected capability is checked before the
   consent dialog; **Start recording** rechecks the consent-bound selection.
   Speak a short phrase. No audio is sent while recording.
3. Choose **Stop recording**, then **Transcribe recording**. Stop releases the
   microphone; transcribe sends the retained clip to Pumas. The 30-second or
   buffer limit also stops without sending. Read and edit the visible result.
4. Repeat; try **Cancel / discard recording**, change tabs, close/reopen settings,
   and disconnect Pumas to check errors. Confirm capture ends on navigation and
   that a cancelled/late response never replaces a newer result. Test results
   are never placed in game input, history or save state.
5. **Save AI settings** when idle to use this selection in the game. In an optional
   conversation, **Use voice** asks for consent; **Stop and transcribe** fills the
   editable draft. Only **Say this** submits it as dialogue.

The test uses the current form draft without saving. Its result disappears when
settings close. Configuration changes are locked while a test owns consent,
capture, a clip or a request. Repeated clicks cannot start a successor operation
until the original settles. No audio files are written. Device failure,
starvation, overflow, invalid PCM and navigation discard retained clips and
restore controls. Each capture owns a uniquely named muted bus, stream,
playback and effect; disposal resolves the bus by name, stops input, clears unread
frames and releases resources. Sample arrays and mutable PCM/JSON/base64 bytes
are cleared after their original operation settles, never while transport owns
them.

If Pumas completion is unknown after timeout, cancellation or malformed/lost
response, voice input remains quarantined across settings and game use. The
consumer has no public per-request cancellation/drain receipt. Confirm the
original Pumas generation stopped and drained before restarting Lanternwake;
restarting the game alone does not prove producer cleanup.

## Current consumer contract

The source-pinned producer defines the closed request/response grammar in
`rust/crates/pumas-rpc/src/handlers/model_operations/types.rs` and the generic
facade in `modality.rs`. The consumer reads
`GET /v1/capabilities?model=...&profile=...`, requires version 1, matching selection,
unambiguous `audio_transcription` / `speech_to_text`, available state, `pcm_f32le`
input, text output, non-streaming behavior and a 512-token option bound.
Unloaded, ambiguous, unavailable and incompatible selections are rejected
before consent or submission.

`POST /v1/model-operations` uses the **closed named capability** form:
`capability: audio_transcription`, version 1, unique request ID, selected
model/profile, actual capture mix rate, two channels, frozen sample count,
`input.kind: audio`, float little-endian PCM, `output: text`, explicit language
(`en`) and 512 output tokens. Pumas owns native input conversion and inference.
Responses require bounded unique JSON, request correlation, text kind and
`stop` or `length`; a truncated result remains visible. Errors preserve
`not_admitted` versus `unknown`. Requests have a 90-second consumer deadline,
no retry/fallback recognizer and a 32 MiB envelope limit.

## Cloud evidence and local acceptance

Source identities, checks and limits are recorded in
[PUMAS-TRANSCRIPTION-VERIFICATION.md](PUMAS-TRANSCRIPTION-VERIFICATION.md).

`dotnet run --project integration/speech/SpeechSmoke.csproj` exercises the real
consumer using controlled HTTP responses and a local TCP server: current closed
wire shape, PCM, correlated results, refusal/error paths, cancellation,
timeout, delayed transport custody, byte clearing and unknown-outcome quarantine.
It also checks explicit mode/profile/local selection, platform branches and
migration/persistence. These fixtures are not model inference.

`python3 integration/qa/speech_capture.py` runs real Godot controls and a marked
owned audio generator, never a microphone. It checks the startup settings test,
consent cancellation/stale confirmation, repeat record/stop/transcribe, editable
result isolation, tab/close discard, pending probe reopen, device errors, held
POST cancellation/reopen/stale result rejection, shared quarantine, sample
zeroing, and the retained game voice flow. The fixture permit cannot authorize
`AudioStreamMicrophone`. `scripts/verify.sh` includes these checks.

For local acceptance, retain the Pumas experimental load, transcription and
unload/captured-generation stop receipts. Record an actual microphone clip,
check the words and editable result, repeat/cancel/navigate while recording and
transcribing, and verify Pumas child drainage independently. Record the OS,
Landlock ABI, model/artifact and runtime digests, profile generation and observed
errors. A successful build, fixture, import or load does not establish real ASR.
