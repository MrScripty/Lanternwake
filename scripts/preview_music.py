#!/usr/bin/env python3
"""Make music-only previews of every mood, place, character, and chapter track."""
from array import array
import argparse
import json
from pathlib import Path
import re
import sys
import wave

PROJECT = Path(__file__).resolve().parents[1]
ROOT = PROJECT / 'Assets/Music'
CACHE = PROJECT / '.toolchain/music-render-cache'
OUTPUT = PROJECT / '.toolchain/music-previews'
RATE = 32000
PAIRS = [('saltmere', 'arrival'), ('undertow', 'inquiry'), ('night_ledger', 'storm'), ('open_horizon', 'horizon')]


def write(path, pcm):
    if sys.byteorder != 'little': pcm.byteswap()
    with wave.open(str(path), 'wb') as output:
        output.setparams((2, 2, RATE, 0, 'NONE', 'not compressed'))
        output.writeframes(pcm.tobytes())


def comparison(path, excerpts):
    reel = array('h')
    for result, start, length in excerpts:
        excerpt = result[round(start * RATE) * 2:round((start + length) * RATE) * 2]
        for i in range(len(excerpt) // 2):
            fade = min(1, i / (RATE * .4), (len(excerpt) // 2 - 1 - i) / (RATE * .4))
            excerpt[2 * i] = round(excerpt[2 * i] * fade)
            excerpt[2 * i + 1] = round(excerpt[2 * i + 1] * fade)
        reel.extend(excerpt); reel.extend(array('h', [0]) * RATE * 2)
    write(path, reel)
    print(path)


def game_mix_previews(library, log):
    """Use native scene-render targets instead of duplicating the mix resolver."""
    mixes = {}
    for line in log.read_text().splitlines():
        if line.startswith('LANTERNWAKE_MUSIC_PREVIEW '):
            mix = json.loads(line.removeprefix('LANTERNWAKE_MUSIC_PREVIEW '))
            mixes[mix['location']] = mix
    places = [layer['id'] for layer in library if layer['role'] == 'environment']
    if set(mixes) != set(places):
        raise ValueError('Run the native audio smoke first; its log must contain all five scene mixes.')
    names = {stem['name'] for layer in library for stem in layer['stems']}
    results = {}
    for location in places:
        gains = mixes[location]['stems']
        if set(gains) != names or any(not 0 <= gain <= 1 for gain in gains.values()):
            raise ValueError('Preview log must identify the current catalog and bounded gains.')
        streams, levels = [], []
        for name, gain in gains.items():
            if gain == 0: continue
            with wave.open(str(CACHE / (name + '.wav')), 'rb') as source:
                pcm = array('h', source.readframes(RATE * 24))
            if sys.byteorder != 'little': pcm.byteswap()
            streams.append(pcm); levels.append(gain)
        results[location] = array('f', (sum(sample * gain for sample, gain in zip(samples, levels)) for samples in zip(*streams)))
    # One listening gain for the entire reel preserves the relative scene balance.
    peak = max(max(abs(min(pcm)), abs(max(pcm))) for pcm in results.values())
    rendered = {}
    for location, pcm in results.items():
        rendered[location] = array('h', (round(sample * .7 * 32767 / peak) for sample in pcm))
        write(OUTPUT / (location + '-scene-mix.wav'), rendered[location])
    comparison(OUTPUT / 'places-scene-mixes.wav', [(rendered[location], 0, 18) for location in places])
    (OUTPUT / 'scene-mixes.txt').write_text(
        'Native scene-target music arrangements; one common listening gain.\n'
        'Includes mood, place, character and chapter layers. Ambience and bus EQ/ducking are omitted.\n'
        'Reel order:\n' + '\n'.join(f"{place}: {mixes[place]['scene_id']} / {mixes[place]['mix_title']}" for place in places) + '\n')


def main(mix_log=None):
    OUTPUT.mkdir(parents=True, exist_ok=True)
    library = json.loads((ROOT / 'catalog.json').read_text())['layers']
    if mix_log is not None:
        game_mix_previews(library, mix_log)
        return
    results = {}
    for layer in library:
        if layer['role'] == 'mood':
            profile = dict(PAIRS)[layer['id']]
            text = (ROOT / 'Cues' / (profile + '.tres')).read_text()
            levels = [float(re.search(r'^' + name + r' = ([0-9.]+)', text, re.M)[1]) for name in ('Ground', 'Theme', 'Bowed', 'Motion')]
        else: levels = [1] * len(layer['stems'])
        streams = []
        for stem in layer['stems']:
            with wave.open(str(CACHE / (stem['name'] + '.wav')), 'rb') as source:
                pcm = array('h', source.readframes(source.getnframes()))
            if sys.byteorder != 'little': pcm.byteswap()
            streams.append(pcm)
        mixed = array('f', (sum(s * gain for s, gain in zip(samples, levels)) for samples in zip(*streams)))
        peak = max(abs(min(mixed)), abs(max(mixed)))
        result = array('h', (round(sample * .7 * 32767 / peak) for sample in mixed))
        results[(layer['role'], layer['id'])] = result
        write(OUTPUT / (layer['id'] + '.wav'), result)
    for role, filename, start, length in [('mood','saltmere-comparison',20,20), ('environment','places-comparison',0,9),
                                          ('character','characters-comparison',4.5,9), ('journey','journey-comparison',9.5,12)]:
        comparison(OUTPUT / (filename + '.wav'), [(results[(role,l['id'])],start,length) for l in library if l['role']==role])
    (OUTPUT / 'README.txt').write_text('Music-only normalized previews.\n\n' + '\n'.join(f"{l['id']}.wav — {l['role']}: {l['title']}" for l in library) +
        '\n\nComparison reels follow this catalog order. The journey reel plays Chapters I–V in sequence.\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mix-log', type=Path, help='Render scene arrangements from a native --audio-smoke log, with one shared listening gain.')
    main(parser.parse_args().mix_log)
