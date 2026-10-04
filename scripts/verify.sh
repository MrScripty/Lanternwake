#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
: "${GODOT_MONO:?Set GODOT_MONO to the official Godot .NET executable}"
if ! "$GODOT_MONO" --version | grep -q '\.mono\.'; then
  echo 'Godot .NET is required; ordinary Godot is unsupported.' >&2
  exit 1
fi
dotnet run --project tests/Lanternwake.Tests.csproj -- Content/story.json
dotnet run --project integration/pumas/ClientTests/ClientTests.csproj
dotnet run --project integration/speech/SpeechSmoke.csproj
dotnet build Lanternwake.csproj --no-restore
for mode in smoke ui-smoke save-isolation-smoke; do
  log="$(mktemp)"
  trap 'rm -f "$log"' EXIT
  LANTERNWAKE_PUMAS_MODEL='' "$GODOT_MONO" --headless --path . -- "--$mode" 2>&1 | tee "$log"
  if grep -q '^ERROR:' "$log"; then echo "Godot $mode emitted an error." >&2; exit 1; fi
  if [[ "$mode" == "ui-smoke" ]] && ! grep -q 'LANTERNWAKE_RECOVERY_UI_OK' "$log"; then
    echo 'Save recovery UI regression did not report success.' >&2; exit 1
  fi
  if [[ "$mode" == "save-isolation-smoke" ]] && ! grep -q 'LANTERNWAKE_SAVE_ISOLATION_OK' "$log"; then
    echo 'Save isolation regression did not report success.' >&2; exit 1
  fi
  rm -f "$log"
  trap - EXIT
done
log="$(mktemp)"
trap 'rm -f "$log"' EXIT
timeout 120 "$GODOT_MONO" --headless --editor --path . -- --editor-roundtrip 2>&1 | tee "$log"
if grep -q '^ERROR:' "$log" || ! grep -q 'LANTERNWAKE_EDITOR_ROUNDTRIP_OK' "$log" || ! grep -q 'LANTERNWAKE_STORY_DOCK_OK' "$log" || ! grep -q 'LANTERNWAKE_STORY_DOCK_LAYOUT_OK' "$log" || ! grep -q 'LANTERNWAKE_STORY_DOCK_COLD_SELECTION_OK' "$log" || ! grep -q 'LANTERNWAKE_CHARACTER_AUTHORING_OK' "$log" || ! grep -q 'LANTERNWAKE_PLAYTEST_LAUNCH_OK' "$log"; then
  echo 'Godot Editor roundtrip did not pass cleanly.' >&2; exit 1
fi
rm -f "$log"
trap - EXIT
