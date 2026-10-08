#!/usr/bin/env python3
"""Exercise the playable route model through real native controls in owned storage."""
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    names = subprocess.check_output(['git', 'ls-files', '-z'], cwd=project).decode().split('\0')
    before = {name: hashlib.sha256((project / name).read_bytes()).hexdigest() for name in names if name}
    with tempfile.TemporaryDirectory(prefix='lanternwake-route-model-') as temporary:
        root = Path(temporary); (root / 'owned-fixture').touch()
        env = os.environ.copy()
        env.update({key: str(root / child) for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]})
        env.update(LANTERNWAKE_ROUTE_FIXTURE=str(root), LANTERNWAKE_PUMAS_MODEL='', DBUS_SESSION_BUS_ADDRESS='unix:path=' + str(root / 'no-keyring'))
        env.pop('OPENROUTER_API_KEY', None)
        result = subprocess.run([env['GODOT_MONO'], '--headless', '--path', str(project), 'res://qualification/route-model.tscn'],
                                cwd=project, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=45)
        print(result.stdout, end='', flush=True)
        if result.returncode or any(marker in result.stdout for marker in ['ERROR:', 'SCRIPT ERROR:', 'WARNING:']) or 'LANTERNWAKE_ROUTE_MODEL_OK' not in result.stdout:
            raise RuntimeError('Native route model did not pass cleanly.')
    if any(hashlib.sha256((project / name).read_bytes()).hexdigest() != digest for name, digest in before.items()):
        raise RuntimeError('Route model changed tracked source.')
    print('PASS playable route model, bounded blocks, source review, keyboard/150-percent small-window layout, save preservation and explicit canonical continuation. Linux native controls; no provider or invented historical outcome.')


if __name__ == '__main__':
    main()
