#!/usr/bin/env python3
"""Repeat the native generator lifetime regression and the original real Quit path."""
import json
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    evidence = Path(os.environ.get('LANTERNWAKE_AUDIO_EVIDENCE', project / 'artifacts' / 'generator-lifetime'))
    evidence.mkdir(parents=True, exist_ok=True)
    results = []
    cases = ([('lifetime', 'res://qualification/generator-lifetime.tscn', [], 'LANTERNWAKE_GENERATOR_LIFETIME_OK')] * 3 +
             [('preview', 'res://qualification/cup-states.tscn', ['--author-preview-beat', 'ch3_s5_b001'], 'LANTERNWAKE_CUP_STATES_OK')] * 12)
    for index, (kind, scene, arguments, marker) in enumerate(cases):
        with tempfile.TemporaryDirectory(prefix='lanternwake-generator-') as temporary:
            profile = Path(temporary)
            (profile / 'owned-fixture').touch()
            environment = os.environ.copy()
            for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
                environment[key] = str(profile / child)
            environment.pop('OPENROUTER_API_KEY', None)
            environment.update(LANTERNWAKE_PUMAS_MODEL='', LANTERNWAKE_CUP_FIXTURE=str(profile),
                               LANTERNWAKE_CUP_MODE='preview',
                               DBUS_SESSION_BUS_ADDRESS='unix:path=' + str(profile / 'no-desktop-keyring'))
            command = [environment['GODOT_MONO'], '--headless', '--path', str(project), scene]
            if arguments:
                command += ['--'] + arguments
            log = evidence / f'{index:02d}-{kind}.log'
            with log.open('wb') as output:
                # subprocess.run kills/waits only its exact owned child on timeout.
                result = subprocess.run(command, cwd=project, env=environment, stdout=output,
                                        stderr=subprocess.STDOUT, timeout=30)
            text = log.read_text(errors='replace')
            clean = result.returncode == 0 and marker in text and not any(x in text for x in ['ERROR:', 'SCRIPT ERROR:', 'WARNING:'])
            results.append(dict(case=index, kind=kind, returncode=result.returncode, passed=clean, log=str(log)))
            (evidence / 'result.json').write_text(json.dumps(results, indent=2) + '\n')
            if not clean:
                raise RuntimeError(f'Native generator regression failed; inspect {log}')
            print(f'PASS {index:02d} {kind}', flush=True)
    print('PASS 36 generator lifetimes and 12 original muted preview Quit paths; no timeout increase.')


if __name__ == '__main__':
    main()
