#!/usr/bin/env python3
"""Start/stop an isolated, authenticated dummy Xorg using already installed tools."""
import argparse
import json
import os
from pathlib import Path
import secrets
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['start', 'stop'])
    parser.add_argument('--state-dir', type=Path, required=True)
    parser.add_argument('--number', type=int, default=173)
    args = parser.parse_args()
    state = args.state_dir.resolve()
    authority = state / 'Xauthority'
    config = state / 'xorg.conf'
    metadata = state / 'display.json'
    if args.action == 'stop':
        display = json.loads(metadata.read_text())
        pid = display['pid']
        command = Path(f'/proc/{pid}/cmdline').read_bytes().split(b'\0')
        if (not command or Path(os.fsdecode(command[0])).name != 'Xorg'
                or os.fsencode(str(config)) not in command or os.fsencode(str(authority)) not in command
                or os.fsencode(display['display']) not in command):
            raise SystemExit('Process does not match this owned display; no signal sent.')
        os.kill(pid, 15)
        print('Stopped the matching owned Xorg process:', pid)
        return
    if not 100 <= args.number <= 999:
        raise SystemExit('Use an explicitly owned display number between 100 and 999.')
    if Path(f'/tmp/.X{args.number}-lock').exists() or Path(f'/tmp/.X11-unix/X{args.number}').exists():
        raise SystemExit('Display is already occupied; it will not be replaced.')
    state.mkdir(mode=0o700, parents=True, exist_ok=False)
    config.write_text((Path(__file__).with_name('xorg-dummy.conf')).read_text())
    display = f':{args.number}'
    authority.touch(mode=0o600, exist_ok=False)
    subprocess.run(['xauth', '-f', str(authority), 'add', display, 'MIT-MAGIC-COOKIE-1',
        secrets.token_hex(16)], check=True)
    with (state / 'xorg-console.log').open('wb') as log:
        process = subprocess.Popen(['Xorg', display, '-config', str(config), '-auth', str(authority),
            '-nolisten', 'tcp', '-extension', 'MIT-SHM', '-noreset', '-novtswitch',
            '-logfile', str(state / 'xorg.log')], stdout=log, stderr=subprocess.STDOUT)
    env = os.environ.copy()
    env.update(DISPLAY=display, XAUTHORITY=str(authority))
    for _ in range(50):
        if process.poll() is not None:
            break
        if subprocess.run(['xdpyinfo'], env=env, stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL).returncode == 0:
            metadata.write_text(json.dumps({'pid': process.pid, 'display': display,
                'authority': str(authority), 'authenticated': True, 'tcp': 'disabled',
                'screen': '1440x900', 'driver': 'installed Xorg dummy',
                'mitShm': 'disabled for separate sandbox IPC namespaces'}, indent=2) + '\n')
            print('Owned authenticated display ready:', display, 'pid', process.pid)
            return
        time.sleep(0.1)
    if process.poll() is None:
        process.terminate()
        process.wait(timeout=10)
    raise SystemExit('Owned display unavailable; inspect its logs. No escalation attempted.')


if __name__ == '__main__':
    main()
