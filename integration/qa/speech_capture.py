#!/usr/bin/env python3
"""Owned synthetic Godot capture and generic HTTP fixture; never activates a microphone."""
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    with tempfile.TemporaryDirectory(prefix='lanternwake-owned-speech-') as temporary:
        root = Path(temporary)
        (root / 'owned-fixture').touch()
        env = os.environ.copy()
        for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
            env[key] = str(root / child)
        env.update(LANTERNWAKE_SPEECH_FIXTURE=str(root), LANTERNWAKE_PUMAS_MODEL='', LANTERNWAKE_SPEECH_RENDERED='0')
        command = [env['GODOT_MONO'], '--headless', '--path', str(project), '--audio-driver', 'Dummy', 'res://qualification/speech-capture.tscn']
        result = subprocess.run(command, cwd=project, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=60)
        print(result.stdout, end='', flush=True)
        if result.returncode or any(marker in result.stdout for marker in ['ERROR:', 'SCRIPT ERROR:', 'WARNING:']) or 'LANTERNWAKE_SPEECH_CAPTURE_OK' not in result.stdout:
            raise RuntimeError('Owned synthetic capture did not pass cleanly; inspect output above.')
    print('PASS owned synthetic capture and generic adapter lifecycle. No physical microphone, permissions, Pumas runtime or inference claim.')


if __name__ == '__main__':
    main()
