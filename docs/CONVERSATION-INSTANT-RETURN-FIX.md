# Instant text selected during optional conversation

Independent review of `da39b24938bb6df44bde976297bfe4c01049647c` found that
Return restored the pre-chat partial reveal even after the player enabled instant
text through Settings. This contradicted that setting's explicit reveal action.

The instant-text action now updates the current paused passage using the same
session/beat/generation/visible-chat/shutdown checks as its owner. Enabling reveals
the passage; disabling does not hide already revealed text. Return retains the
selected-setting status. Without an instant-text action, captured reveal count,
timer and status stay exact. Return's existing callback fences are unchanged.
There is no new save field or preference persistence.

The old partial-reveal fixture forced seven visible characters while instant text
was already enabled. It has been replaced: normal-mode native Load renders the
current conversation beat in actual typewriter mode, real process frames reveal
part of it, and the fixture pauses processing only after observing that progress.
Author preview starts with the real typewriter mode at its selected conversation.
No reveal count or timer is fabricated by these probes.

The revised native fixture fails on exact `da39b24` at the in-chat instant-enable
Return assertion. Original failing logs, probe source and tested DLL/PDB are
retained separately. The repaired focused runner passes:

- Complete: 3,186 checks across all 28 conversations and 1,424 authored beats.
- Closed controls: 39 checks, preserving retired Send/Return/suggestion fences.
- Author preview: 61 checks, preserving every player-save byte.

New cases cover unchanged actual typewriter state, in-chat sizing without an
instant change, explicit instant enable, the next native frame, enable/disable
without a rewind, setting status, and first Continue revealing a paused passage
without advancing. Existing transcript/save preservation, same-beat Load,
cancel/reopen, late Return at all 23 evidence gates and normal Quit remain covered.

Source base: `da39b24938bb6df44bde976297bfe4c01049647c`, tree
`06aa80fc0b8ec30103c019e064f2c440dcb393cc`. Separate branch:
`fix/conversation-instant-text-return`. Original completion, evidence-review,
conversation-return and PR7 refs remain frozen. Parent owns PR/review/integration,
release and the retained worktree's cleanup.

Environment: official Godot 4.6.3 .NET / .NET SDK 8.0.425, Linux headless, Dummy
audio. Model selection is empty, rejecting before provider transport. These are
native control/reveal/persistence checks, not human appearance, physical input,
accessibility tools, physical hearing or genuine in-flight provider cancellation.
Production model quality, microphone and ASR gates remain unchanged.

One final `scripts/verify.sh` run returned zero: five audio asset/setup tests,
4,110 core assertions, 63 story checks, 11 simulated Pumas contracts, unsupported
speech, zero-warning/error build, native story/UI/save isolation, 32 audio checks
with 20,480 mixer PCM frames (peak 0.024877), completion 1,458/33/7, question review
2,511/46, revised Return 3,186/39/61, and 13-scene Editor roundtrip including native
authoring/selected-beat launch. No warning/error lines appeared in this aggregate.
Source-bound evidence includes the legitimate failing probe, final source archive,
tested DLL/PDB, ancestry-bound bundle and native logs. Original da39b evidence is
retained unchanged; its earlier synthetic partial test does not qualify this
settings interaction.
