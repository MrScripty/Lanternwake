#!/usr/bin/env python3
"""Verify bundled live MIDI inputs. Optional WAVs stay in the ignored developer cache."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile

PROJECT = Path(__file__).resolve().parents[1]
ROOT = PROJECT / 'Assets/Music'
VENDOR = PROJECT / 'ThirdParty/MeltySynth'
CACHE = PROJECT / '.toolchain/music-render-cache'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def catalog():
    data = json.loads((ROOT / 'catalog.json').read_text())
    names = [stem['name'] for layer in data['layers'] for stem in layer['stems']]
    if data['format'] != 2 or not names or len(names) != len(set(names)):
        raise ValueError('Music catalog must identify unique live voices.')
    for layer in data['layers']:
        if Path(layer['midi']).name != layer['midi'] or not layer['midi'].endswith('.mid'):
            raise ValueError('Music MIDI paths must be filenames within Source.')
        for stem in layer['stems']:
            if not stem['channels'] or any(not 0 <= channel <= 15 for channel in stem['channels']):
                raise ValueError('Invalid music MIDI channel group.')
    return data


def inputs():
    paths = [ROOT / 'Source' / layer['midi'] for layer in catalog()['layers']]
    paths += [ROOT / 'Source/Saltmere-Acoustic.sf2', ROOT / 'catalog.json']
    return {str(path.relative_to(PROJECT)): digest(path) for path in sorted(set(paths))}


def verify_vendor():
    upstream = json.loads((VENDOR / 'UPSTREAM.json').read_text())
    for name, sha in upstream['files'].items():
        if digest(VENDOR / Path(name).name) != sha:
            raise ValueError('Pinned synth source changed: ' + name)
    if (ROOT / 'MeltySynth-LICENSE.txt').read_bytes() != (VENDOR / 'LICENSE.txt').read_bytes():
        raise ValueError('Preserve the bundled synth licence in game assets.')
    font = json.loads((ROOT / 'Source/Saltmere-Acoustic.json').read_text())
    if digest(ROOT / 'Source/Saltmere-Acoustic.sf2') != font['subset_sha256']:
        raise ValueError('Instrument bank no longer matches its provenance.')


def ensure_music():
    verify_vendor()
    manifest = json.loads((ROOT / 'manifest.json').read_text())
    if manifest['format'] != 3 or manifest['inputs'] != inputs():
        raise ValueError('Music source changed. Run scripts/setup_music.py --author to record deliberate MIDI edits.')
    print(f'Music setup: verified {len(catalog()["layers"])} MIDI tracks and bundled synth/bank; no music WAVs needed.')


def render_cache(regenerate=False):
    """Developer audition only; never publishes WAVs into the game's resource tree."""
    CACHE.mkdir(parents=True, exist_ok=True)
    renderer_inputs = {str(path.relative_to(PROJECT)): digest(path) for path in
        [PROJECT / 'integration/music-renderer/Program.cs', PROJECT / 'integration/music-renderer/MusicRenderer.csproj', VENDOR / 'MeltySynth.csproj']}
    key = {'inputs': inputs(), 'renderer': renderer_inputs}
    manifest_path = CACHE / 'manifest.json'
    if not manifest_path.exists() and list(CACHE.glob('*.wav')) and not regenerate:
        raise ValueError('Audition cache has unrecorded WAVs; preserve them before --render --regenerate.')
    if manifest_path.exists() and not regenerate:
        existing = json.loads(manifest_path.read_text())
        if existing.get('source') == key and all((CACHE / (name + '.wav')).exists() and digest(CACHE / (name + '.wav')) == sha for name, sha in existing['stems'].items()):
            print('Music audition cache already matches.'); return
        raise ValueError('Audition cache changed or is stale; preserve custom files before --render --regenerate.')
    environment = dict(os.environ)
    environment.update(DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1', DOTNET_EnableHWIntrinsic='0', DOTNET_TieredCompilation='0')
    with tempfile.TemporaryDirectory(prefix='.render-', dir=CACHE) as directory:
        staging = Path(directory)
        subprocess.run([os.environ.get('DOTNET', 'dotnet'), 'run', '--project', str(PROJECT / 'integration/music-renderer/MusicRenderer.csproj'),
                        '--configuration', 'Release', '--', str(ROOT / 'Source/Saltmere-Acoustic.sf2'), str(ROOT / 'catalog.json'), str(staging)],
                       cwd=PROJECT, env=environment, check=True)
        names = {stem['name'] for layer in catalog()['layers'] for stem in layer['stems']}
        if {path.stem for path in staging.glob('*.wav')} != names: raise ValueError('Incomplete audition render.')
        hashes = {name: digest(staging / (name + '.wav')) for name in sorted(names)}
        for name in names: os.replace(staging / (name + '.wav'), CACHE / (name + '.wav'))
        manifest_path.write_text(json.dumps({'source': key, 'stems': hashes}, indent=2) + '\n')
    print('Rendered optional audition WAVs into .toolchain/music-render-cache only.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--author', action='store_true', help='Record deliberate MIDI/catalog source edits; no rendering.')
    parser.add_argument('--render', action='store_true', help='Render optional developer listening files outside game assets.')
    parser.add_argument('--regenerate', action='store_true')
    options = parser.parse_args()
    if options.author:
        verify_vendor()
        (ROOT / 'manifest.json').write_text(json.dumps({'format': 3, 'playback': 'bundled MeltySynth / live MIDI',
            'sample_rate': 32000, 'loop_frames': 2560000, 'inputs': inputs()}, indent=2) + '\n')
    ensure_music()
    if options.render: render_cache(options.regenerate)


if __name__ == '__main__':
    try: main()
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        raise SystemExit('Music setup failed: ' + str(error))
