#!/usr/bin/env python3
"""Qualify the one-time History opening position with actual Main and native keys."""
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    try:
        names = subprocess.check_output(['git', 'ls-files', '-z'], cwd=project, timeout=30).decode().split('\0')
    except subprocess.TimeoutExpired as error:
        raise RuntimeError('History source inventory timed out.') from error
    before = {name: hashlib.sha256((project / name).read_bytes()).hexdigest() for name in names if name}
    with tempfile.TemporaryDirectory(prefix='lanternwake-history-resume-') as temporary:
        root = Path(temporary)
        (root / 'owned-fixture').touch()
        env = os.environ.copy()
        env.update({key: str(root / child) for key, child in [('XDG_DATA_HOME','data'), ('XDG_CONFIG_HOME','config'), ('XDG_CACHE_HOME','cache')]})
        env.update(LANTERNWAKE_HISTORY_FIXTURE=str(root), LANTERNWAKE_PUMAS_MODEL='', DBUS_SESSION_BUS_ADDRESS='unix:path=' + str(root / 'no-keyring'))
        env.pop('OPENROUTER_API_KEY', None)
        try:
            result = subprocess.run([env['GODOT_MONO'], '--headless', '--path', str(project), 'res://qualification/history-resume.tscn'], cwd=project, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=45)
        except subprocess.TimeoutExpired as error:
            if error.stdout:
                print(error.stdout.decode('utf-8', errors='replace') if isinstance(error.stdout, bytes) else error.stdout, flush=True)
            raise RuntimeError('Native History resume timed out; inspect captured output.') from error
        print(result.stdout, end='', flush=True)
        if result.returncode or any(marker in result.stdout for marker in ['ERROR:', 'SCRIPT ERROR:', 'WARNING:']) or 'LANTERNWAKE_HISTORY_RESUME_OK' not in result.stdout:
            raise RuntimeError('Native History resume did not pass cleanly.')
    if any(hashlib.sha256((project / name).read_bytes()).hexdigest() != value for name, value in before.items()):
        raise RuntimeError('History qualification changed tracked source.')
    print('PASS History recent opening, fresh/new story, late Load, manual scrollback, reopen, native keyboard, retired-window and unchanged-save checks.')


if __name__ == '__main__':
    main()
