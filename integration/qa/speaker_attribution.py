#!/usr/bin/env python3
"""Canonical live/recorded attribution through real game and reading controls."""
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    names = subprocess.check_output(['git', 'ls-files', '-z'], cwd=project).decode().split('\0')
    before = {name: hashlib.sha256((project / name).read_bytes()).hexdigest() for name in names if name}
    with tempfile.TemporaryDirectory(prefix='lanternwake-speaker-attribution-') as temporary:
        root = Path(temporary)
        (root / 'owned-fixture').touch()
        env = os.environ.copy()
        env.update({key: str(root / child) for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]})
        env.update(LANTERNWAKE_SPEAKER_FIXTURE=str(root), LANTERNWAKE_PUMAS_MODEL='', DBUS_SESSION_BUS_ADDRESS='unix:path=' + str(root / 'no-keyring'))
        env.pop('OPENROUTER_API_KEY', None)
        result = subprocess.run([env['GODOT_MONO'], '--headless', '--path', str(project), 'res://qualification/speaker-attribution.tscn'],
                                cwd=project, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=60)
        print(result.stdout, end='', flush=True)
        if result.returncode or any(marker in result.stdout for marker in ['ERROR:', 'SCRIPT ERROR:', 'WARNING:']) or 'LANTERNWAKE_SPEAKER_ATTRIBUTION_OK' not in result.stdout:
            raise RuntimeError('Native speaker attribution failed; inspect output above.')
    if any(hashlib.sha256((project / name).read_bytes()).hexdigest() != digest for name, digest in before.items()):
        raise RuntimeError('Speaker attribution changed tracked source.')
    print('PASS visible live/recorded speaker names, narration, relative reading size, authored-label reuse, Load/Return and native teardown. Headless controls/layout; no human hearing, provider or accessibility-tool qualification.')


if __name__ == '__main__':
    main()
