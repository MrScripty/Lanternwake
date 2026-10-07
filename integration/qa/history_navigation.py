#!/usr/bin/env python3
"""Owned native history navigation, genuine control progression and fresh-process restore."""
import json
import os
from pathlib import Path
import subprocess
import time


def main():
    project = Path(__file__).resolve().parents[2]
    root = project / 'artifacts/history-navigation' / str(time.time_ns())
    root.mkdir(parents=True, exist_ok=True)
    fixture = root / 'fixture'
    fixture.mkdir(exist_ok=False)
    (fixture / 'owned-fixture').touch()
    env = os.environ.copy()
    env.update(LANTERNWAKE_HISTORY_FIXTURE=str(fixture), LANTERNWAKE_PUMAS_MODEL='')
    for variable, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
        env[variable] = str(fixture / child)
    runs = []
    for name, prepare in [('prepare', '1'), ('fresh-process', '0')]:
        env['LANTERNWAKE_HISTORY_PREPARE'] = prepare
        log = root / (name + '.log')
        command = [env['GODOT_MONO'], '--headless', '--path', str(project), 'res://qualification/history-navigation.tscn']
        with log.open('wb') as output:
            result = subprocess.run(command, cwd=project, env=env, stdout=output, stderr=subprocess.STDOUT, timeout=300)
        text = log.read_text()
        if result.returncode or '\nERROR:' in '\n' + text or '\nWARNING:' in '\n' + text or 'LANTERNWAKE_HISTORY_NAVIGATION_OK' not in text:
            raise RuntimeError(f'{name} failed; inspect {log}')
        marker = next(line for line in text.splitlines() if line.startswith('LANTERNWAKE_HISTORY_NAVIGATION_OK'))
        runs.append({'name': name, 'exitCode': result.returncode, 'result': marker})
        print(marker, flush=True)
    (root / 'receipt.json').write_text(json.dumps({'runs': runs, 'limits': 'Headless native controls and layout; graphical keyboard/controller input is a separate check. No model, voice or human acceptance.'}, indent=2) + '\n')
    print('Evidence:', root, flush=True)


if __name__ == '__main__':
    main()
