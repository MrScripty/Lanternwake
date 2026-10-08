#!/usr/bin/env python3
"""Repeated real game startup and audio teardown in a disposable Linux profile."""
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    names = subprocess.check_output(['git', 'ls-files', '-z'], cwd=project).decode().split('\0')
    before = {name: hashlib.sha256((project / name).read_bytes()).hexdigest() for name in names if name}
    with tempfile.TemporaryDirectory(prefix='lanternwake-runtime-lifecycle-') as temporary:
        root = Path(temporary)
        (root / 'owned-fixture').touch()
        environment = os.environ.copy()
        for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
            environment[key] = str(root / child)
        environment.update(LANTERNWAKE_RUNTIME_FIXTURE=str(root), LANTERNWAKE_PUMAS_MODEL='',
                           DBUS_SESSION_BUS_ADDRESS='unix:path=' + str(root / 'no-desktop-keyring'))
        environment.pop('OPENROUTER_API_KEY', None)
        command = [environment['GODOT_MONO'], '--headless', '--path', str(project), 'res://qualification/runtime-lifecycle.tscn']
        result = subprocess.run(command, cwd=project, env=environment, encoding='utf-8', errors='replace',
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=60)
        print(result.stdout, end='', flush=True)
        if result.returncode or any(marker in result.stdout for marker in ['ERROR:', 'SCRIPT ERROR:', 'WARNING:']):
            raise RuntimeError('Native runtime lifecycle failed; inspect the output above.')
        if 'LANTERNWAKE_RUNTIME_LIFECYCLE_OK cycles=4' not in result.stdout:
            raise RuntimeError('Native runtime lifecycle did not report completion.')
    if any(hashlib.sha256((project / name).read_bytes()).hexdigest() != digest for name, digest in before.items()):
        raise RuntimeError('Native lifecycle changed tracked source.')
    print('PASS repeated native Main startup, real New story buttons, location changes, worker/playback teardown and absent streams; tracked source unchanged. Linux headless controls; no physical sound, provider or human playtime claim.')


if __name__ == '__main__':
    main()
