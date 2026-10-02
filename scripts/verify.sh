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
dotnet build Lanternwake.csproj --no-restore
for mode in smoke ui-smoke; do
  log="$(mktemp)"
  trap 'rm -f "$log"' EXIT
  LANTERNWAKE_PUMAS_MODEL='' "$GODOT_MONO" --headless --path . -- "--$mode" 2>&1 | tee "$log"
  if grep -q '^ERROR:' "$log"; then echo "Godot $mode emitted an error." >&2; exit 1; fi
  rm -f "$log"
  trap - EXIT
done
