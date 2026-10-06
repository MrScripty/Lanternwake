#!/usr/bin/env python3
"""Owned native character performance, exact restore and authored-resource checks."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    evidence = project / 'artifacts/character-performance'
    evidence.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='lanternwake-performance-') as temporary:
        root = Path(temporary); (root / 'owned-fixture').touch()
        env = os.environ.copy()
        for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
            env[key] = str(root / child)
        env.update(LANTERNWAKE_PUMAS_MODEL='', LANTERNWAKE_PERFORMANCE_FIXTURE=str(root))
        log = evidence / 'native.log'
        with log.open('wb') as output:
            result = subprocess.run([env['GODOT_MONO'], '--headless', '--path', str(project),
                'res://qualification/character-performance.tscn'], cwd=project, env=env,
                stdout=output, stderr=subprocess.STDOUT, timeout=120)
        text = log.read_text(errors='replace')
        markers = [line for line in text.splitlines() if line.startswith('LANTERNWAKE_CHARACTER_PERFORMANCE_OK ')]
        if result.returncode or 'ERROR:' in text or 'WARNING:' in text or len(markers) != 1:
            raise RuntimeError(f'Native character performance failed; inspect {log}')
        receipt = json.loads(markers[0].split(' ', 1)[1])
        shutil.copytree(root / 'fixtures', evidence / 'fixtures', dirs_exist_ok=True)
        receipt.update(passed=True, nativeDllSha256=hashlib.sha256((project / '.godot/mono/temp/bin/Debug/Lanternwake.dll').read_bytes()).hexdigest(),
            fixtureOrigin='Actual canonical Core traversal; setup snapshots, not human playtime.',
            fixtureSha256={p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in (root / 'fixtures').glob('*.json')})
        (evidence / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
        print(markers[0]); print('PASS native performance; no human art, hearing or duration acceptance claim.')


if __name__ == '__main__': main()
