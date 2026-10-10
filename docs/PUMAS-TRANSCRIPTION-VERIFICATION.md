# Experimental Pumas transcription consumer verification — 2026-10-10

Base: Lanternwake `76b8428ee6bbe0c03d80b7df0520370df1a1b3a7`.
Producer contract inspected: Pumas-Library
`ad31e391dcd94c204b3fcd748cc98d27b3281e65`,
`docs/contracts/installed-audio-session.md`,
`rust/crates/pumas-rpc/src/handlers/model_operations/{types,modality,projection}.rs`
and `model_operations.rs`.
Tools: official Godot 4.6.3 .NET and .NET SDK 8.0.425, Linux cloud instance.

Implemented an explicit local Cohere CPU mode, indexed model/profile fields and
settings-only record/stop/transcribe/cancel controls using the game recorder and
transcriber. Default/migrated preferences keep admission disabled. Pumas owns
runtime preparation, model selection validation, native conversion, confinement
and child drainage. The game borrows the user's already loaded experiment.

Focused verification on the final implementation:

- `dotnet run --project integration/speech/SpeechSmoke.csproj`: 141 controlled
  audio contract assertions, 19 sample-buffer assertions, 12 explicit admission/
  platform/configuration checks. Also retains default refusal, legacy recognizer
  rejection and pre-cancellation checks. The actual consumer's loopback TCP
  request is checked against the current closed named `OperationRequest` field
  set and capability/PCM grammar. Correlated success/length/error, malformed and
  oversized replies, cancellation/timeout/disposal, held transport custody,
  zeroed byte buffers and unknown-outcome quarantine are exercised.
- `python3 integration/qa/speech_capture.py`: 103 native checks using the actual
  settings/game controls, a marked owned generator and controlled HTTP replies.
  Includes startup access, 640px consent wrapping, consent cancellation, repeated
  recording/stop/transcribe without overlapping POSTs, editable result isolation,
  tab/close discard, stale consent, held-probe reopen, device failure, held-POST
  cancellation/reopen, late result rejection, sample zeroing, shared quarantine
  into game use, and game capture ending when another modal opens.
- Native `--ui-smoke`: AI settings, startup menu, reading sizes/chat,
  recovery and workflow success markers passed.
- Debug, ExportDebug, ExportRelease solution builds with `--no-restore
  --warnaserror`: zero warnings/errors. Official Editor `--build-solutions
  --quit`: passed.
- Python Pumas adapter suite: 19 passed. Repository credential check and
  `git diff --check`: passed.

These are controlled consumer tests and native Godot controls/capture tests,
not an installed Pumas producer execution or ASR qualification. No physical
microphone was opened by the fixtures. No gated weights were acquired. The
host admission branches are tested with simulated platform observations; the
shipping path performs its own read-only Landlock version query.

Run `scripts/verify.sh` for the complete repository suite. CI checks the exact
PR head and runs that suite, all solution configurations and the official Editor
build. CI results belong to that exact commit, not a model-quality certificate.

Local acceptance still requires the user's real-model load/transcribe/drain
receipts, physical microphone/permission/device checks, accuracy and actual
resource measurements. The concise workflow and interrupted-flow checks are in
[SPEECH.md](SPEECH.md). Unknown producer outcomes stay quarantined: a cancelled
HTTP transport never serves as a child-drain receipt, and recreating the game
alone does not establish recovery.
