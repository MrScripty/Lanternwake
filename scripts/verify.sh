#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
bash scripts/setup.sh
: "${GODOT_MONO:?Set GODOT_MONO to the official Godot .NET executable}"
if ! "$GODOT_MONO" --version | grep -q '\.mono\.'; then
  echo 'Godot .NET is required; ordinary Godot is unsupported.' >&2
  exit 1
fi
python3 -m unittest discover -s integration/audio -v
python3 -m unittest discover -s integration/qa -p 'test_evidence_runs.py' -v
dotnet run --project tests/Lanternwake.Tests.csproj -- Content/story.json
dotnet run --project integration/story-validation/StoryValidation.csproj -- Content/story.json
dotnet run --project integration/pumas/ClientTests/ClientTests.csproj
dotnet run --project integration/pumas/DiscoveryTests/DiscoveryTests.csproj
dotnet run --project integration/speech/SpeechSmoke.csproj
dotnet build Lanternwake.csproj --no-restore
"$GODOT_MONO" --headless --editor --path . --import
for mode in smoke ui-smoke save-isolation-smoke audio-smoke; do
  log="$(mktemp)"
  trap 'rm -f "$log"' EXIT
  LANTERNWAKE_PUMAS_MODEL='' "$GODOT_MONO" --headless --path . -- "--$mode" 2>&1 | tee "$log"
  if grep -q '^ERROR:' "$log"; then echo "Godot $mode emitted an error." >&2; exit 1; fi
  if [[ "$mode" == "ui-smoke" ]] && ! grep -q 'LANTERNWAKE_RECOVERY_UI_OK' "$log"; then
    echo 'Save recovery UI regression did not report success.' >&2; exit 1
  fi
  if [[ "$mode" == "ui-smoke" ]] && ! grep -q 'LANTERNWAKE_READING_SIZE_OK' "$log"; then
    echo 'Reading text-size regression did not report success.' >&2; exit 1
  fi
  if [[ "$mode" == "ui-smoke" ]] && ! grep -q 'LANTERNWAKE_READING_CHAT_OK' "$log"; then
    echo 'Reading chat-size regression did not report success.' >&2; exit 1
  fi
  if [[ "$mode" == "save-isolation-smoke" ]] && ! grep -q 'LANTERNWAKE_SAVE_ISOLATION_OK' "$log"; then
    echo 'Save isolation regression did not report success.' >&2; exit 1
  fi
  if [[ "$mode" == "audio-smoke" ]] && ! grep -q "LANTERNWAKE_AUDIO_OK" "$log"; then
    echo "Audio regression did not report success." >&2; exit 1
  fi
  rm -f "$log"
  trap - EXIT
done
log="$(mktemp)"
trap 'rm -f "$log"' EXIT
timeout 30 "$GODOT_MONO" --headless --path . res://qualification/audio-lifecycle.tscn 2>&1 | tee "$log"
if grep -Eq 'ERROR:|SCRIPT ERROR:|WARNING:' "$log" || ! grep -q 'LANTERNWAKE_AUDIO_LIFECYCLE_OK' "$log"; then
  echo 'Native audio lifecycle regression did not pass cleanly.' >&2; exit 1
fi
rm -f "$log"
trap - EXIT
python3 integration/qa/watch_completion.py
python3 integration/qa/activity_review.py
python3 integration/qa/conversation_return.py
python3 integration/qa/content_note.py
python3 integration/qa/cup_states.py
python3 integration/qa/family_exchange.py
python3 integration/qa/bell_descent.py
python3 integration/qa/character_performance.py
log="$(mktemp)"
trap 'rm -f "$log"' EXIT
timeout 120 "$GODOT_MONO" --headless --editor --path . -- --editor-roundtrip 2>&1 | tee "$log"
if grep -q '^ERROR:' "$log" || ! grep -q 'LANTERNWAKE_EDITOR_ROUNDTRIP_OK' "$log" || ! grep -q 'LANTERNWAKE_STORY_DOCK_OK' "$log" || ! grep -q 'LANTERNWAKE_STORY_DOCK_LAYOUT_OK' "$log" || ! grep -q 'LANTERNWAKE_STORY_DOCK_COLD_SELECTION_OK' "$log" || ! grep -q 'LANTERNWAKE_CHARACTER_AUTHORING_OK' "$log" || ! grep -q 'LANTERNWAKE_PLAYTEST_LAUNCH_OK' "$log"; then
  echo 'Godot Editor roundtrip did not pass cleanly.' >&2; exit 1
fi
rm -f "$log"
trap - EXIT
