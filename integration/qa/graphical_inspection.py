#!/usr/bin/env python3
"""Rendered native states and synthetic X11 input on an explicitly owned display."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile

from non_audio_flows import fingerprint


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--display-state', type=Path, required=True, help='Metadata from this run\'s owned authenticated Xorg display.')
    parser.add_argument('--case', action='append', help='Run only named cases; repeat for a focused check.')
    args = parser.parse_args()
    display = json.loads(args.display_state.read_text())
    if not display['authenticated'] or display['tcp'] != 'disabled':
        raise SystemExit('An owned authenticated display with TCP disabled is required.')
    authority = Path(display['authority'])
    if authority.stat().st_uid != os.getuid() or authority.stat().st_mode & 0o077:
        raise SystemExit('Display authority must be owned by this user and private.')
    os.kill(display['pid'], 0)
    executable = os.environ.get('GODOT_MONO')
    if not executable:
        raise SystemExit('Set GODOT_MONO to the official Godot .NET executable.')
    project = Path(__file__).resolve().parents[2]
    evidence = project / 'artifacts' / 'graphical-native'
    evidence.mkdir(parents=True, exist_ok=True)
    cases = [('title', None), ('harbor', 'ch1_s1_b001'), ('conversation', 'ch1_s1_b021'), ('house-inventory', 'ch1_s2_b008'), ('house-intact', 'ch2_s5_b011'),
        ('house-broken', 'ch2_s5_b012'), ('house-boxed', 'ch2_s5_b026'), ('house-mug', 'ch3_s5_b001'),
        ('archive', 'ch1_s4_b001'), ('tower-before', 'ch5_s3_b005'), ('tower-lowered', 'ch5_s3_b006'),
        ('cave', 'ch3_s1_b001'), ('ending', 'ch5_s5_evidence')]
    if args.case:
        if set(args.case) - {label for label, _ in cases}:
            raise SystemExit('Unknown graphical case.')
        cases = [(label, beat) for label, beat in cases if label in args.case]
    before = fingerprint(project)
    results = []
    for label, beat in cases:
        output = evidence / label
        output.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='lanternwake-rendered-') as temporary:
            fixture = Path(temporary)
            (fixture / 'owned-fixture').touch()
            (fixture / 'display-contract.json').write_text(json.dumps(display))
            env = os.environ.copy()
            env.update(DISPLAY=display['display'], XAUTHORITY=str(authority), LIBGL_ALWAYS_SOFTWARE='1',
                LANTERNWAKE_PUMAS_MODEL='', LANTERNWAKE_GRAPHICAL_FIXTURE=str(fixture),
                LANTERNWAKE_GRAPHICAL_OUTPUT=str(output))
            for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
                env[key] = str(fixture / child)
            command = [executable, '--path', str(project), '--audio-driver', 'Dummy', '--disable-vsync',
                '--max-fps', '30', '--script', 'res://integration/qa/graphical_probe.gd']
            if beat:
                command += ['--', '--author-preview-beat', beat]
            with (output / 'native.log').open('wb') as log:
                process = subprocess.run(command, env=env, cwd=project, stdout=log, stderr=subprocess.STDOUT, timeout=120)
            text = (output / 'native.log').read_text(errors='replace')
            warnings = [line for line in text.splitlines() if line.startswith('WARNING:')]
            unexpected = [line for line in warnings if 'Could not set V-Sync mode' not in line]
            if process.returncode or 'ERROR:' in text or 'SCRIPT ERROR:' in text or unexpected or 'LANTERNWAKE_GRAPHICAL_PROBE_OK' not in text or 'llvmpipe' not in text:
                raise RuntimeError(f'Native rendered {label} failed; inspect {output / "native.log"}')
            if list((fixture / 'data').rglob('*.json')):
                raise RuntimeError(f'{label} wrote player saves despite title/author-preview isolation.')
            if fingerprint(project) != before:
                raise RuntimeError(f'{label} changed tracked source.')
        images = {p.name: {'bytes': p.stat().st_size, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in output.glob('*.png')}
        if not images:
            raise RuntimeError(f'Native rendered {label} produced no image.')
        results.append({'case': label, 'beat': beat, 'exitCode': process.returncode, 'images': images,
            'environmentWarnings': warnings, 'unexpectedWarnings': 0, 'playerSaveWrites': 0})
        print('PASS rendered', label, 'native synthetic input and production Quit', flush=True)
    report = {'productionBaseline': '33aa46899305bcbb4b7a232e0377504b420d1595', 'sourceFingerprint': before,
        'display': display['display'], 'mitShm': display.get('mitShm', 'not recorded'),
        'authentication': 'private Xauthority; TCP disabled', 'renderer': 'Mesa llvmpipe OpenGL compatibility',
        'audio': 'Dummy; no microphone', 'maxFPS': 30, 'cases': results,
        'limits': ['Software-rendered X11 screenshots and synthetic input; no physical keyboard/mouse/monitor/hearing acceptance',
            'Selected author previews are not a human full-story or duration playtest', 'No production model or ASR claim']}
    (evidence / 'result.json').write_text(json.dumps(report, indent=2) + '\n')


if __name__ == '__main__':
    main()
