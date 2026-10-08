#!/usr/bin/env python3
"""Exercise the Missing Margin authored exchange through real native controls in owned storage."""
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile


def run_native(command, *, cwd, env):
    try:
        return subprocess.run(command, cwd=cwd, env=env, text=True,
                              stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=45)
    except subprocess.TimeoutExpired as error:
        # TimeoutExpired can contain bytes even when the subprocess requested text.
        for captured in (error.stdout, error.stderr):
            if captured:
                print(captured.decode('utf-8', errors='replace') if isinstance(captured, bytes) else captured,
                      end='', flush=True)
        print(f'\nNative Missing Margin timed out after {error.timeout} seconds.', flush=True)
        raise RuntimeError('Native Missing Margin timed out; inspect captured output above.') from error


def main():
    project = Path(__file__).resolve().parents[2]
    names = subprocess.check_output(['git', 'ls-files', '-z'], cwd=project).decode().split('\0')
    before = {name: hashlib.sha256((project / name).read_bytes()).hexdigest() for name in names if name}
    with tempfile.TemporaryDirectory(prefix='lanternwake-missing-margin-') as temporary:
        root = Path(temporary); (root / 'owned-fixture').touch()
        env = os.environ.copy()
        env.update({key: str(root / child) for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]})
        env.update(LANTERNWAKE_MARGIN_FIXTURE=str(root), LANTERNWAKE_PUMAS_MODEL='', DBUS_SESSION_BUS_ADDRESS='unix:path=' + str(root / 'no-keyring'))
        env.pop('OPENROUTER_API_KEY', None)
        result = run_native([env['GODOT_MONO'], '--headless', '--path', str(project), 'res://qualification/missing-margin.tscn'],
                            cwd=project, env=env)
        print(result.stdout, end='', flush=True)
        if result.returncode or any(marker in result.stdout for marker in ['ERROR:', 'SCRIPT ERROR:', 'WARNING:']) or 'LANTERNWAKE_MISSING_MARGIN_OK' not in result.stdout:
            raise RuntimeError('Native Missing Margin did not pass cleanly.')
    if any(hashlib.sha256((project / name).read_bytes()).hexdigest() != digest for name, digest in before.items()):
        raise RuntimeError('Missing Margin changed tracked source.')
    print('PASS Missing Margin authored exchange, three retained choices, interruption/reentry, autosave/manual reload, honest capability notice and original continuation. Linux native controls; no provider or invented historical outcome.')


if __name__ == '__main__':
    main()
