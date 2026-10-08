# Current player-flow coverage

The authored main path contains five chapters, 41 scenes, 35,485 required-text
words, 1,440 beats, 24 evidence activities, 28 optional model-conversation points
and two optional authored exchanges. Counts are derived from Content/story.json;
optional model replies and authored exchange replies are excluded from required
reading duration. Historical qualification reports retain their original source
and measurement boundaries.

| Area | Implemented behavior | Acceptance boundary |
| --- | --- | --- |
| Story and evidence | Fixed ending, per-option evidence feedback, review/retry, completion and replay. | Automated traversal is not human pacing or comprehension acceptance. |
| Optional agency | Editable suggestions/free input with authored fallbacks; Missing Margin and Six Working Lives retain one chosen authored pair. Declining preserves the fixed continuation. | Authored choices and synthetic fixtures establish no real-model characterization. |
| Saves and resume | Manual/autosave, previous-slot recovery, displayed-snapshot selection, restored knowledge/activity/stage state. History opens at its current/recent paragraph and keeps the full record readable. | Older saves beyond the optional inserted beat load without inventing a choice; forward compatibility with older game readers is not promised. |
| Reading and keyboard | 100/125/150% text, instant/typewriter text, visible scrollbar focus, Page Up/Down and Home/End, content note. | Physical input, assistive tools and localization need device acceptance. |
| Authored scene interactions | Route comparison, cup inspection/persistent states, guided bell lowering and character poses. Speaker names remain in History; live captions depend on the configured optional HUD labels. | Broader art/performance direction and speaker presentation require author/UI decisions. |
| Sound | Independent levels/mute, optional reduced loud/quiet differences, authored textual equivalents for essential sounds. | Native PCM/control tests do not establish hearing or listening preference. |
| AI configuration | Startup/resume menu, independent dialogue/transcription/voice preferences, provider/model selection, secure-key handling and unavailable-state notices. Selected-owner setup and bounded generic audio consumer/capture are implemented. | Installed producer/model interoperability remains unqualified; shipping microphone admission and character voices remain unavailable. |

See [Missing Margin](MISSING-MARGIN-EXCHANGE.md),
[History opening](HISTORY-RESUME-CONTEXT.md),
[reduced audio range](REDUCED-DYNAMIC-RANGE.md),
[Pumas owner reuse](PUMAS-OWNER-REUSE.md) and [speech](SPEECH.md) for contracts and
focused regression commands.

## Automated qualification

The combined scripts/verify.sh suite has passed 4,581 core assertions, full
1,440-beat traversal, all 24 activity reviews and 28 conversation-return flows,
save/UI/audio/lifecycle checks, 44 Missing Margin checks, 47 History checks and the
13-scene editor roundtrip. Debug and ExportRelease managed builds passed with
warnings treated as errors. Focused Linux rendered checks exercised all three
Missing Margin choices and fresh reloads, enlarged sound controls, and fresh/late
History opening, scrollback, keyboard Close, reopen and new story.

Rendered checks used synthetic input on an owned X11 display, Mesa compatibility
rendering and Dummy audio. Existing V-Sync, volumetric-fog and depth-of-field
limitations were observed. These results qualify the source behavior; managed
builds are not standalone executable exports or physical-device qualification.

## Remaining acceptance choices

- Representative readers: chapter comprehension, five-hour target/median, opening
  hook placement, evidence-gate difficulty and repeated Chapter 4 explanations.
  Expand only a specific missing experience supported by reader evidence.
- Author/UI direction: live speaker attribution, composed inserts, materials and
  recorded performances, while retaining textual equivalents and fixed chronology.
- Approved installed producer and model: owner custody, descriptor-bound admission,
  cancellation/reconciliation, spoiler prompts and voice review-before-send.
  Controlled consumer fixtures cannot establish producer cleanup or ASR quality.
- Target devices/locales: physical keyboard/microphone/speaker behavior, assistive
  tools, long translated strings, small windows, performance and executable exports.

No timed human playthrough or production voice/model acceptance is established by
these automated results. Unlimited optional chat does not count as authored hours.
