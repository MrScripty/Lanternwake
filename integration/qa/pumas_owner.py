#!/usr/bin/env python3
"""Actual game setup controls with owned CLI/HTTP seams, no installed Pumas or acquisition."""
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    with tempfile.TemporaryDirectory(prefix='lanternwake-owned-pumas-') as temporary:
        root = Path(temporary)
        (root / 'owned-fixture').touch()
        env = os.environ.copy()
        for name, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
            env[name] = str(root / child)
        env.update(LANTERNWAKE_OWNER_FIXTURE=str(root), LANTERNWAKE_PUMAS_MODEL='',
                   DBUS_SESSION_BUS_ADDRESS='unix:path=' + str(root / 'no-keyring'))
        env.pop('OPENROUTER_API_KEY', None)
        command = [env['GODOT_MONO'], '--headless', '--path', str(project), '--audio-driver', 'Dummy', 'res://qualification/pumas-owner.tscn']
        result = subprocess.run(command, cwd=project, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=60)
        print(result.stdout, end='', flush=True)
        if result.returncode or any(word in result.stdout for word in ['ERROR:', 'SCRIPT ERROR:', 'WARNING:']) or 'LANTERNWAKE_OWNER_REUSE_OK' not in result.stdout:
            raise RuntimeError('Owned selected-library game fixture did not pass cleanly.')
    print('PASS owned native selected-library setup, borrowed ownership and local-first acquisition refusal. No installed producer, real model, microphone or inference claim.')


if __name__ == '__main__':
    main()
