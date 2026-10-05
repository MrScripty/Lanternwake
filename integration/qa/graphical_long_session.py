#!/usr/bin/env python3
"""Full normal-mode save/recovery watch in six rendered native processes, using existing fixture."""
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
    args = parser.parse_args()
    display = json.loads(args.display_state.read_text())
    authority = Path(display['authority'])
    if not display['authenticated'] or display['tcp'] != 'disabled' or authority.stat().st_uid != os.getuid() or authority.stat().st_mode & 0o077:
        raise SystemExit('Owned authenticated display required.')
    os.kill(display['pid'], 0)
    executable = os.environ.get('GODOT_MONO')
    if not executable:
        raise SystemExit('Set GODOT_MONO to the official .NET engine.')
    project = Path(__file__).resolve().parents[2]
    story = json.loads((project / 'Content/story.json').read_text())
    expected = [[b['id'] for s in c['scenes'] for b in s['beats']] for c in story['chapters']]
    before = fingerprint(project)
    evidence = project / 'artifacts' / 'graphical-long-session'
    evidence.mkdir(parents=True, exist_ok=True)
    results = []
    with tempfile.TemporaryDirectory(prefix='lanternwake-rendered-watch-') as temporary:
        fixture = Path(temporary)
        (fixture / 'owned-fixture').touch()
        env = os.environ.copy()
        env.update(DISPLAY=display['display'], XAUTHORITY=str(authority), LIBGL_ALWAYS_SOFTWARE='1',
            LANTERNWAKE_PUMAS_MODEL='', LANTERNWAKE_QUALIFICATION_ROOT=str(fixture))
        for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
            env[key] = str(fixture / child)
        for chapter in range(len(expected) + 1):
            env['LANTERNWAKE_QUALIFICATION_CHAPTER'] = str(chapter)
            path = evidence / f'chapter-{chapter + 1}.log'
            command = [executable, '--path', str(project), '--audio-driver', 'Dummy', '--disable-vsync',
                '--max-fps', '30', 'res://qualification/long-session.tscn']
            with path.open('wb') as log:
                process = subprocess.run(command, env=env, cwd=project, stdout=log, stderr=subprocess.STDOUT, timeout=300)
            text = path.read_text(errors='replace')
            markers = [json.loads(line.removeprefix('LANTERNWAKE_LONG_SESSION_CHAPTER_OK ')) for line in text.splitlines() if line.startswith('LANTERNWAKE_LONG_SESSION_CHAPTER_OK ')]
            unexpected = [line for line in text.splitlines() if line.startswith('WARNING:') and 'Could not set V-Sync mode' not in line]
            if process.returncode or 'ERROR:' in text or unexpected or len(markers) != 1 or 'llvmpipe' not in text:
                raise RuntimeError(f'Rendered chapter {chapter + 1} failed; inspect {path}')
            result = markers[0]
            if result['chapter'] != chapter or result['visited'] != (expected[chapter] if chapter < len(expected) else []) or fingerprint(project) != before:
                raise RuntimeError(f'Exact-source/canonical sequence mismatch for chapter {chapter + 1}.')
            results.append(result)
            print(f'PASS rendered chapter {chapter + 1}: {len(result["visited"])} beats, {result["resumes"]} resumes, {result["wrongAnswers"]} retries; production Quit exit 0', flush=True)
    summary = {'beats': sum(len(r['visited']) for r in results), 'scenes': len(set(s for r in results for s in r['scenes'])),
        'checks': sum(r['checks'] for r in results), 'resumes': sum(r['resumes'] for r in results),
        'wrongAnswers': sum(r['wrongAnswers'] for r in results), 'recoveries': sum(r['recoveries'] for r in results),
        'rollbackRecoveries': sum(r['rollbackRecoveries'] for r in results), 'processes': len(results),
        'terminal': results[-1]['terminal'], 'renderer': 'OpenGL Mesa llvmpipe', 'audioDriver': 'Dummy',
        'limits': 'Native production button callbacks under real software rendering; no physical input/hearing, human duration, production model or ASR acceptance.'}
    activities = sum(b.get('activity') is not None for c in story['chapters'] for s in c['scenes'] for b in s['beats'])
    if summary['beats'] != sum(map(len, expected)) or summary['wrongAnswers'] != activities or summary['recoveries'] != activities or not summary['terminal']:
        raise RuntimeError('Incomplete rendered full-story/activity/recovery/terminal coverage.')
    (evidence / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    print('PASS rendered full watch ' + json.dumps(summary), flush=True)


if __name__ == '__main__':
    main()
