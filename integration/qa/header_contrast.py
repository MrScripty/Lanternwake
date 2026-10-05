#!/usr/bin/env python3
"""Before/after rendered wide/insert header inspection at native reading sizes."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile

from non_audio_flows import fingerprint


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--display-state', type=Path, required=True)
    parser.add_argument('--phase', choices=['before', 'after'], required=True)
    args = parser.parse_args()
    display = json.loads(args.display_state.read_text())
    authority = Path(display['authority'])
    if not display['authenticated'] or display['tcp'] != 'disabled' or authority.stat().st_uid != os.getuid() or authority.stat().st_mode & 0o077:
        raise SystemExit('Private owned authenticated display required.')
    executable = os.environ.get('GODOT_MONO')
    if not executable:
        raise SystemExit('Set GODOT_MONO to the official .NET engine.')
    project = Path(__file__).resolve().parents[2]
    before = fingerprint(project)
    output = project / 'artifacts/header-contrast' / args.phase
    output.mkdir(parents=True, exist_ok=True)
    results = []
    for view, beat in [('wide', 'ch2_s5_b011'), ('insert', 'ch2_s5_b012')]:
        target = output / view
        target.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='lanternwake-header-') as temporary:
            fixture = Path(temporary)
            (fixture / 'owned-fixture').touch()
            (fixture / 'display-contract.json').write_text(json.dumps(display))
            env = os.environ.copy()
            env.update(DISPLAY=display['display'], XAUTHORITY=str(authority), LIBGL_ALWAYS_SOFTWARE='1',
                LANTERNWAKE_PUMAS_MODEL='', LANTERNWAKE_GRAPHICAL_FIXTURE=str(fixture),
                LANTERNWAKE_GRAPHICAL_OUTPUT=str(target), LANTERNWAKE_HEADER_PHASE=args.phase)
            for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
                env[key] = str(fixture / child)
            with (target / 'native.log').open('wb') as log:
                result = subprocess.run([executable, '--path', str(project), '--audio-driver', 'Dummy',
                    '--disable-vsync', '--max-fps', '30', '--script', 'res://integration/qa/header_contrast.gd',
                    '--', '--author-preview-beat', beat], env=env, cwd=project, stdout=log, stderr=subprocess.STDOUT, timeout=120)
            text = (target / 'native.log').read_text()
            warnings = [line for line in text.splitlines() if line.startswith('WARNING:')]
            markers = [json.loads(line.split(' ', 1)[1]) for line in text.splitlines() if line.startswith('LANTERNWAKE_HEADER_CONTRAST_OK ')]
            if result.returncode or 'ERROR:' in text or 'SCRIPT ERROR:' in text or 'llvmpipe' not in text or len(markers) != 1 or any('Could not set V-Sync mode' not in line for line in warnings):
                raise RuntimeError(f'Header {args.phase}/{view} failed; inspect {target / "native.log"}.')
            if list((fixture / 'data').rglob('*.json')) or fingerprint(project) != before:
                raise RuntimeError('Header inspection changed player saves or tracked source.')
        results.append({'view': view, 'beat': beat, 'exitCode': result.returncode, 'records': markers[0], 'environmentWarnings': warnings})
        print(f'PASS {args.phase} {view}: 100/125/150%, real renderer, synthetic X11 Settings/Quit', flush=True)
    (output / 'result.json').write_text(json.dumps({'phase': args.phase, 'sourceFingerprint': before, 'cases': results,
        'limits': 'Software-rendered X11 pixels/synthetic input; no physical device or accessibility conformance claim.'}, indent=2) + '\n')


if __name__ == '__main__':
    main()
