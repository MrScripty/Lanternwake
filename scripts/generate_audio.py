#!/usr/bin/env python3
"""Original, reproducible low-level ambience. No samples or network dependencies.

These are new production placeholders, not recovered stems from the delivered sampler.
All loops use integer cycles in a 16-second period; their seam is one sample step.
"""
import hashlib
import json
import math
from pathlib import Path
import random
import struct
import wave

ROOT = Path(__file__).resolve().parents[1] / 'Assets' / 'Audio'
RATE = 24000
SECONDS = 16


def loop(seed, resonances, gain):
    rng = random.Random(seed)
    # A deterministic, periodic bank creates a soft broad noise bed without a splice.
    waves = [(rng.randrange(180, 11000), rng.random() * math.tau, 0.0007) for _ in range(40)]
    waves += [(round(hz * SECONDS), 0, level) for hz, level in resonances]
    frames = []
    for i in range(RATE * SECONDS):
        phase = math.tau * i / (RATE * SECONDS)
        breath = 0.78 + 0.22 * math.cos(phase * 2)
        value = gain * breath * sum(math.sin(phase * cycle + offset) * level for cycle, offset, level in waves)
        frames.append(round(value * 32767))
    return frames


def bell():
    frames = []
    for i in range(RATE * 5):
        t = i / RATE
        attack = min(1, t / 0.08)
        value = attack * math.exp(-t * 1.3) * sum(level * math.sin(math.tau * hz * t) for hz, level in [(110, .07), (221, .035), (329, .018), (461, .008)])
        value *= min(1, (5 - t) / .25)
        frames.append(round(value * 32767))
    return frames


def generate(destination=ROOT):
    destination.mkdir(parents=True, exist_ok=True)
    sources = {
        'harbor': loop(110, [(73, .025), (146, .01)], 1),
        'keeper_house': loop(220, [(60, .009), (120, .005)], .6),
        'archive': loop(330, [(50, .014), (100, .006)], .5),
        'lantern_room': loop(440, [(82.5, .019), (165, .009)], .8),
        'tide_cave': loop(550, [(55, .025), (110, .01)], 1),
        'watch_theme': loop(660, [(130.8125, .025), (196, .012), (261.625, .008)], .65),
        'bell_release': bell(),
    }
    manifest = {}
    for name, samples in sources.items():
        path = destination / (name + '.wav')
        with wave.open(str(path), 'wb') as output:
            output.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
            output.writeframes(struct.pack('<' + 'h' * len(samples), *samples))
        manifest[name] = {'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'frames': len(samples), 'sample_rate': RATE, 'peak': max(abs(s) for s in samples) / 32768, 'loop': name != 'bell_release', 'seam_step': abs(samples[-1] - samples[0]) / 32768}
    (destination / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    return manifest


if __name__ == '__main__':
    generate()
