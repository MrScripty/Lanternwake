#!/usr/bin/env python3
"""Native author-preview captures of the six directed performance scenes."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile

from non_audio_flows import fingerprint
from evidence_runs import new_evidence_run


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--display-state', type=Path, required=True)
    args = parser.parse_args()
    display = json.loads(args.display_state.read_text()); authority = Path(display['authority'])
    if not display['authenticated'] or display['tcp'] != 'disabled' or authority.stat().st_uid != os.getuid() or authority.stat().st_mode & 0o077:
        raise SystemExit('Owned private authenticated display required.')
    os.kill(display['pid'], 0)
    project = Path(__file__).resolve().parents[2]
    output = new_evidence_run(project / 'artifacts/character-performance/runs', 'rendered')
    before = fingerprint(project); results = []
    cases = [('house-speaking', 'ch1_s2_b009'), ('house-listening', 'ch1_s2_b010'),
        ('cave-measurement', 'ch3_s1_b014'), ('archive-work', 'ch3_s2_b001'),
        ('recorded-testimony', 'ch4_s1_b006'), ('release-working', 'ch5_s3_b003'),
        ('release-resting', 'ch5_s3_b004'), ('catalogue-working', 'ch5_s5_b002'),
        ('catalogue-resting', 'ch5_s5_b003')]
    for label, beat in cases:
        destination = output / label; destination.mkdir(exist_ok=False)
        with tempfile.TemporaryDirectory(prefix='lanternwake-performance-render-') as temporary:
            fixture = Path(temporary); (fixture / 'owned-fixture').touch()
            (fixture / 'display-contract.json').write_text(json.dumps(display))
            env = os.environ.copy()
            env.update(DISPLAY=display['display'], XAUTHORITY=str(authority), LIBGL_ALWAYS_SOFTWARE='1',
                LANTERNWAKE_PUMAS_MODEL='', LANTERNWAKE_GRAPHICAL_FIXTURE=str(fixture), LANTERNWAKE_GRAPHICAL_OUTPUT=str(destination))
            for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]: env[key] = str(fixture / child)
            with (destination / 'native.log').open('wb') as log:
                run = subprocess.run([env['GODOT_MONO'], '--path', str(project), '--audio-driver', 'Dummy',
                    '--disable-vsync', '--max-fps', '30', '--script', 'res://integration/qa/graphical_probe.gd',
                    '--', '--author-preview-beat', beat], cwd=project, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=120)
            text = (destination / 'native.log').read_text()
            unexpected = [line for line in text.splitlines() if line.startswith('WARNING:') and 'Could not set V-Sync mode' not in line]
            if run.returncode or 'ERROR:' in text or unexpected or 'LANTERNWAKE_GRAPHICAL_PROBE_OK' not in text or 'llvmpipe' not in text:
                raise RuntimeError('Native render failed: ' + label)
            if list((fixture / 'data').rglob('*.json')) or fingerprint(project) != before:
                raise RuntimeError('Preview changed player slots or source: ' + label)
        images = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in destination.glob('*.png')}
        if set(images) != {beat + '.png'}:
            raise RuntimeError('Unexpected or missing rendered images: ' + label + ': ' + str(sorted(images)))
        results.append(dict(case=label, beat=beat, exitCode=run.returncode, images=images))
        print('PASS rendered performance ' + label, flush=True)
    (output / 'receipt.json').write_text(json.dumps(dict(passed=True, cases=results,
        sourceFingerprint=before, nativeDllSha256=hashlib.sha256((project / '.godot/mono/temp/bin/Debug/Lanternwake.dll').read_bytes()).hexdigest(),
        limits='Native author preview with synthetic X11 and software renderer; no human art/hearing/duration acceptance.'), indent=2) + '\n')


if __name__ == '__main__': main()
