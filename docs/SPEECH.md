# Local voice-to-text

The game implements Godot microphone capture followed by the separately owned, tested LocalSpeechTranscriber whisper.cpp process boundary. It does not send audio to a hosted service. This path requires real-device qualification before a release claim.

Install whisper.cpp from its official project and provision a compatible local speech model yourself or via an explicitly approved install. Configure these environment variables before starting Godot:

- `LANTERNWAKE_WHISPER_CLI`: absolute path to the executable `whisper-cli`
- `LANTERNWAKE_WHISPER_MODEL`: absolute path to the compatible model file

The installed model and executable are checked before offering capture. Choose **Use voice**, accept the explicit microphone prompt, speak, then choose **Stop**. Recording stops at 30 seconds and capture memory is bounded to 30 seconds of stereo samples. Audio is converted to 16 kHz mono PCM WAV in a unique temporary path. The CLI runs without a shell using `-m model -f input.wav -otxt -of prefix -nt`. The process is reaped on cancellation. Temporary WAV/text files are deleted in a finally block after success or failure. An application/OS crash can leave temporary files; no crash-cleanup claim is made.

The resulting text appears in the ordinary input field. Review and edit it; only **Say this** submits a conversation. The operating system may separately ask for microphone permission. Empty audio, failed recognizer, missing model and timeout produce visible errors, not fabricated transcripts.

No recognizer binary/model is bundled. Until a real microphone test passes, voice input is implemented but unqualified. Typed dialogue and editable suggestions are available without it.

## Executed file-transcription evidence

The production adapter passed actual whisper.cpp v1.9.4/tiny.en sample transcription, 48 kHz conversion, cancellation/reaping, cleanup and retry tests. See [LIVE-QUALIFICATION.md](LIVE-QUALIFICATION.md) for exact hashes and the resampling overflow repair. This does not qualify a physical microphone.
