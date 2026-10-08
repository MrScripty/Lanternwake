#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python="${PYTHON:-python3}"
if ! command -v "$python" >/dev/null 2>&1; then
  echo 'Audio setup requires Python 3. Install Python 3 from python.org, or set PYTHON to its executable, then rerun bash scripts/setup.sh before opening Godot.' >&2
  exit 1
fi
"$python" scripts/setup_audio.py "$@"
"$python" scripts/setup_music.py "$@"
