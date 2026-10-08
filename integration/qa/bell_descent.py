#!/usr/bin/env python3
"""Owned native guided descent, rollback and author-preview checks."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fixtures', type=Path)
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[2]
    fixtures = args.fixtures or project / 'tests/Fixtures/bell-descent'
    evidence = project / 'artifacts/bell-descent'
    evidence.mkdir(parents=True, exist_ok=True)
    for mode in ['player', 'resume', 'preview']:
        with tempfile.TemporaryDirectory(prefix='lanternwake-bell-') as temporary:
            root = Path(temporary); (root / 'owned-fixture').touch()
            environment = os.environ.copy()
            for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
                environment[key] = str(root / child)
            environment.update(LANTERNWAKE_PUMAS_MODEL='', LANTERNWAKE_BELL_FIXTURE=str(root), LANTERNWAKE_BELL_MODE=mode)
            saves = root / 'data/godot/app_userdata/Lanternwake'; saves.mkdir(parents=True)
            shutil.copy2(fixtures / ('before.json' if mode == 'player' else 'descent.json'), saves / 'save.json')
            before = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in saves.glob('*.json')}
            command = [os.environ['GODOT_MONO'], '--headless', '--path', str(project), 'res://qualification/bell-descent.tscn']
            if mode == 'preview': command += ['--', '--author-preview-beat', 'ch5_s3_b004']
            log = evidence / (mode + '.log')
            with log.open('wb') as output:
                result = subprocess.run(command, cwd=project, env=environment, stdout=output, stderr=subprocess.STDOUT, timeout=120)
            output = log.read_text(errors='replace')
            marker = [l for l in output.splitlines() if l.startswith('LANTERNWAKE_BELL_DESCENT_OK ')]
            if result.returncode or '\nERROR:' in '\n' + output or len(marker) != 1 or json.loads(marker[0].split(' ', 1)[1])['mode'] != mode:
                raise RuntimeError(f'Native {mode} failed; inspect {log}')
            if mode == 'preview' and before != {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in saves.glob('*.json')}:
                raise RuntimeError('Preview changed owned player slots.')
            print(marker[0], flush=True)
    print('PASS native guided bell presentation; no human art/hearing/duration claim.')


if __name__ == '__main__': main()
