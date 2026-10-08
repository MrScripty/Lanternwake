#!/usr/bin/env python3
"""Original Saltmere score, editable as standard MIDI. No borrowed tunes.

The four suites and fifteen story layers share a 32-bar, 6/8, D-minor/F-major grid.
Quarter note = 72; exact loop = 80 seconds. Channels use zero-based GM programs.
"""
from pathlib import Path
import argparse
import math
import struct

ROOT = Path(__file__).resolve().parents[1] / 'Assets' / 'Music' / 'Source'
PPQ = 480
BARS = 32
END = BARS * 3 * PPQ
# Four-bar phrases; common bass and upper chord tones make suite fades consonant.
CHORDS = [(38, [57, 60, 64, 65]), (34, [57, 60, 62, 65]),
          (41, [57, 60, 65, 67]), (36, [55, 60, 62, 64])]
SUITES = ['saltmere', 'undertow', 'night_ledger', 'open_horizon']
STEMS = ['ground', 'theme', 'bowed', 'motion']


def vlq(value):
    result = [value & 127]
    while value >> 7:
        value >>= 7
        result.insert(0, 128 | (value & 127))
    return bytes(result)


def meta(kind, payload):
    return bytes([255, kind]) + vlq(len(payload)) + payload


def track(events):
    events = sorted(events, key=lambda x: (x[0], x[1]))
    data, previous = bytearray(), 0
    for tick, _, event in events:
        data.extend(vlq(tick - previous)); data.extend(event); previous = tick
    data.extend(vlq(END - previous)); data.extend(b'\xff\x2f\0')
    return b'MTrk' + struct.pack('>I', len(data)) + data


def write_midi(path, tracks):
    conductor = track([(0, -10, meta(3, b'Saltmere / original Lanternwake score')),
        (0, -9, meta(81, (833333).to_bytes(3, 'big'))),
        (0, -8, meta(88, bytes([6, 3, 36, 8]))), (0, -7, meta(89, bytes([255, 1])))])
    path.write_bytes(b'MThd' + struct.pack('>IHHH', 6, 1, 1 + len(tracks), PPQ) + conductor + b''.join(track(t) for t in tracks))


def instrument(events, channel, program, pan, volume=90):
    events.extend([(0, -5, bytes([192 + channel, program])),
                   (0, -4, bytes([176 + channel, 10, pan])),
                   (0, -3, bytes([176 + channel, 7, volume]))])


def note(events, channel, start, duration, pitch, velocity):
    # A restrained deterministic performance, never random at setup time.
    on = round(start * PPQ); off = min(END - 24, round((start + duration) * PPQ))
    if off <= on: return
    events.extend([(on, 1, bytes([144 + channel, pitch, velocity])),
                   (off, 0, bytes([128 + channel, pitch, 0]))])


def compose(destination=ROOT):
    destination.mkdir(parents=True, exist_ok=True)
    for suite_index, suite in enumerate(SUITES):
        parts = {stem: [] for stem in STEMS}
        ground, theme, bowed, motion = [parts[x] for x in STEMS]
        instrument(ground, 0, 42, 56, 76)  # cello, below the conversational band
        instrument(ground, 1, 21, 72, 43)  # quiet reeds, breathed rather than held forever
        instrument(theme, 2, 0, 51, 91)   # soft acoustic piano
        instrument(theme, 3, 24, 78, 75)  # fingerpicked nylon guitar
        instrument(bowed, 4, 40, 84, 48)  # solo violin, a score voice, not a full orchestra
        instrument(motion, 5, 45, 43, 70) # pizzicato
        instrument(motion, 6, 0, 82, 60)
        for bar in range(BARS):
            start = bar * 3
            root, chord = CHORDS[(bar // 2) % 4]
            phrase, local = bar // 8, bar % 8
            if bar % 2 == 0:
                note(ground, 0, start + .015, 5.75, root, [49, 44, 53, 42][suite_index])
                if suite_index in (0, 3) or phrase % 2 == 1:
                    for j, p in enumerate(chord[::2]):
                        note(ground, 1, start + .08 + j * .012, 4.6, p - 12, 34 + (bar % 4) * 2)
            # Named original melodic gestures, with rests and eight-bar phrase variations.
            if suite_index == 0:
                melody = [(0, 69, 1.0), (1.5, 67, .8), (3, 65, 1.5), (6, 64, .9), (7.5, 62, 1.8), (12, 65, 1.2), (15, 67, 2.0), (19.5, 64, 1.4)]
            elif suite_index == 1:
                melody = [(0, 62, .7), (1.5, 64, .7), (4.5, 65, 1.6), (9, 67, 1.1), (12, 64, 1.0), (16.5, 62, 2.2), (21, 60, 1.4)]
            elif suite_index == 2:
                melody = [(0, 62, .9), (3, 69, .7), (7.5, 67, .9), (12, 65, 1.6), (15, 64, .9), (18, 62, 2.4)]
            else:
                melody = [(0, 65, 1.3), (3, 69, 1.2), (4.5, 67, .7), (6, 65, 2), (12, 72, 1.6), (15, 69, 1.1), (18, 67, 1), (21, 65, 2.2)]
            for offset, pitch, duration in melody:
                if local * 3 <= offset < (local + 1) * 3:
                    velocity = [52, 47, 50, 55][suite_index] + (phrase % 2) * 4
                    note(theme, 2, phrase * 24 + offset + .025, duration, pitch + (12 if phrase == 2 and suite_index == 3 else 0), velocity)
            # Arpeggios stay in the same chords across suites. Leave the melody room.
            if bar % 2 == 1 or suite_index == 3:
                pattern = [0, 2, 1] if phrase % 2 == 0 else [1, 0, 2]
                for j, n in enumerate(pattern):
                    note(theme, 3, start + .04 + j, .78, chord[n], 41 + (j % 2) * 5)
            if bar % 2 == 0 and (phrase != 0 or suite_index in (1, 2)):
                top = chord[2] + 12 if suite_index != 2 else chord[1] + 12
                note(bowed, 4, start + .11, 5.1, top, 34 + suite_index * 3)
                if suite_index == 2:
                    note(bowed, 4, start + 3.15, 2.0, chord[3] + 12, 32)
            # Quiet two-in-three mechanics for inquiry; denser pizzicato only during storm.
            steps = [0, 1.5] if suite_index != 2 else [0, .5, 1.5, 2]
            for j, offset in enumerate(steps):
                note(motion, 5, start + offset + .008, .38, root + 12 if j % 2 == 0 else chord[0], 38 + suite_index * 3 + (bar % 2) * 4)
            if suite_index == 1 and local in (2, 6):
                note(motion, 6, start + 1.5, .42, chord[2] + 12, 31)
        write_midi(destination / f'{suite}_score.mid', list(parts.values()))
    print('Composed four original multitrack MIDI scores; each has four renderable stems.')


def compose_places(destination=ROOT):
    """Five different musical languages on the common transition/harmony clock."""
    destination.mkdir(parents=True, exist_ok=True)
    # Channel 0 carries a different lead instrument in every location. Other
    # voices reinforce that place's feel rather than repeating a common arpeggio.
    palettes = {
        'harbor': [(21, 55, 76), (45, 74, 69), (24, 82, 45)],
        'keeper_house': [(0, 54, 84), (0, 72, 72)],
        'archive': [(24, 55, 88), (45, 79, 48)],
        'lantern_room': [(45, 48, 85), (45, 84, 67)],
        'tide_cave': [(42, 54, 78), (40, 81, 62)],
    }
    harbor_phrases = [
        [(0, 69, .9), (1, 72, .4), (1.5, 74, 1.1), (3, 72, .8), (4, 69, .4), (4.5, 65, 1.1)],
        [(0, 65, .9), (1, 69, .4), (1.5, 72, 1.1), (3, 69, .9), (4, 65, .4), (4.5, 62, 1.1)],
        [(0, 72, .9), (1, 69, .4), (1.5, 67, 1.1), (3, 69, .8), (4, 72, .4), (4.5, 77, 1.1)],
        [(0, 67, .9), (1, 64, .4), (1.5, 62, 1.1), (3, 64, .8), (4, 67, .4), (4.5, 72, 1.1)],
    ]
    house_phrases = [
        [(0, 77, 2.2), (3, 76, 2.1), (6, 72, 2.8), (9, 69, 2.7)],
        [(0, 69, 2.2), (3, 72, 2.1), (6, 77, 2.8), (9, 76, 2.7)],
    ]
    for name, palette in palettes.items():
        events = []
        for channel, (program, pan, volume) in enumerate(palette):
            instrument(events, channel, program, pan, volume)
        for bar in range(BARS):
            root, chord = CHORDS[(bar // 2) % 4]
            start = bar * 3
            if name == 'harbor':
                # Dotted-quarter rolling pulse; a reed tune, plucked bass, offbeat strum.
                if bar % 2 == 0:
                    for offset, pitch, length in harbor_phrases[(bar // 2) % 4]:
                        note(events, 0, start + offset + .02, length, pitch, 47 + (bar // 8) % 2 * 3)
                for offset, pitch in [(0, root + 12), (1.5, root + 19)]:
                    note(events, 1, start + offset, .75, pitch, 52 if offset == 0 else 46)
                for offset in [.5, 2]:
                    for pitch in chord[::2]: note(events, 2, start + offset + .035, .6, pitch, 32)
            elif name == 'keeper_house':
                # Slow, high piano melody and widely spaced left-hand intervals.
                # Sustaining notes overlap the next attack; no guitar or ticking pulse.
                if bar % 4 == 0:
                    for offset, pitch, length in house_phrases[(bar // 4) % 2]:
                        note(events, 0, start + offset + .065, length, pitch, 43 + (bar // 8) % 2 * 3)
                note(events, 1, start + .08, 3.7, root + 12, 45)
                note(events, 1, start + .14, 3.5, chord[2], 37)
                if bar % 2:
                    note(events, 0, start + 1.7, 1.6, chord[0] + 12, 33)
            elif name == 'archive':
                # Three straight beats against the maritime compound meter;
                # clipped repeated guitar questions, with a short plucked response.
                cell = [[69, 69, 64], [64, 62, 64], [72, 72, 69], [67, 64, 67]][(bar // 2) % 4]
                for n, pitch in enumerate(cell):
                    note(events, 0, start + n + .025, .7 if n < 2 else .95, pitch, 55 if n == 0 else 49)
                for offset, pitch in [(0, root + 12), (1, chord[0]), (2, chord[2])]:
                    note(events, 1, start + offset + .075, .8, pitch, 36)
                if bar % 4 == 3:
                    note(events, 0, start + 2.5, .35, chord[0] + 12, 42)
            elif name == 'lantern_room':
                # Register-separated clockwork: a two-note upper pattern against
                # a three-step lower mechanism. No chordal piano wash.
                for n, offset in enumerate([0, .5, 1, 1.5, 2, 2.5]):
                    pitch = (chord[2] if n % 2 == 0 else chord[0]) + 12
                    note(events, 0, start + offset + .008, .42, pitch, 54 if n % 3 == 0 else 44)
                for n, offset in enumerate([0, 1, 2]):
                    note(events, 1, start + offset + .025, .9, root + [12, 19, 24][n], 49 if n == 0 else 40)
            elif name == 'tide_cave':
                # Long bowed ribbons and slow contrary motion, without a pulse.
                if bar % 2 == 0:
                    note(events, 0, start + .04, 5.9, root + 12, 53)
                    note(events, 1, start + .12, 5.85, chord[2] + 12, 40)
                    if bar % 8 in (0, 4):
                        note(events, 0, start + 2.4, 3.3, chord[0] - 12, 32)
                # A very slow breath changes expression, not musical tempo.
                for step in range(6):
                    expression = round(95 + 10 * math.sin((start + step * .5) * math.pi / 12))
                    for channel in (0, 1): events.append((round((start + step * .5) * PPQ), -2, bytes([176 + channel, 11, expression])))
        write_midi(destination / f'{name}_score.mid', [events])
    print('Composed five place identities: rolling reeds, spacious piano, dry guitar, clockwork pizzicato, and bowed tide.')


def compose_layers(destination=ROOT, places_only=False):
    """Recurring people and chapter developments support distinct place tracks."""
    compose_places(destination)

    if places_only:
        print('Composed five continuous place arrangements; other source MIDIs preserved.')
        return

    people = {
        'ada': (0, 24, [62, 64, 65, 69, 67, 64, 62], [0, .5, 1.5, 2.25, 3.25, 4.5, 5.25]),
        'nessa': (21, 42, [65, 69, 67, 62, 65, 62], [0, .75, 1.5, 3, 4.5, 5.25]),
        'tomas': (24, 0, [69, 67, 64, 62, 60, 62], [0, 1, 1.75, 3, 4.25, 5.25]),
        'sera': (45, 0, [62, 69, 64, 65, 64, 62], [0, .5, 1.5, 2, 3.5, 5]),
        'ivo': (42, 0, [50, 53, 52, 48, 57, 52], [0, 1, 1.75, 3, 4, 5.25]),
    }
    for index, (name, (lead, support, melody, offsets)) in enumerate(people.items()):
        events = []
        instrument(events, 0, lead, 59, 86 if name != 'ivo' else 63)
        instrument(events, 1, support, 78, 53)
        for phrase in range(4):
            start = phrase * 24 + 6  # people answer the place, rather than compete with it
            for n, (pitch, offset) in enumerate(zip(melody, offsets)):
                length = .55 if name == 'sera' else 1.0 if name == 'ivo' else .8
                note(events, 0, start + offset + .03, length, pitch, 48 + n % 2 * 4 + phrase % 2 * 3)
            # A quieter answer later in the phrase makes each cue an actual track.
            for n, pitch in enumerate(melody[:3]):
                note(events, 1, phrase * 24 + 21 + n * .75, .65, pitch - (12 if name != 'ivo' else 0), 30 + n * 2)
        write_midi(destination / f'{name}_score.mid', [events])

    # A recognisable question becomes fragmented, understood, shared, then freely completed.
    # The first three pitches (D E F) survive every chapter; it is one developing theme.
    journeys = [
        ('inventory', 0, [(0, 62, .7), (.75, 64, .7), (1.5, 65, 1), (3, 69, 1.4), (5.25, 64, 1.5)]),
        ('wrong_channel', 24, [(0, 62, .45), (.75, 64, .45), (2.25, 65, .8), (3.75, 67, .7), (6, 64, 1.25)]),
        ('water_kept', 0, [(0, 62, .65), (.75, 64, .65), (1.5, 65, .9), (3, 69, 1), (4.5, 67, .7), (6, 64, .7), (7.5, 62, 1)]),
        ('shared_ledger', 24, [(0, 62, .55), (.75, 64, .55), (1.5, 65, .9), (3, 69, .8), (4.5, 67, .65), (6, 65, .65), (7.5, 62, 1.15)]),
        ('chosen_horizon', 0, [(0, 62, .7), (.75, 64, .7), (1.5, 65, 1.25), (3, 69, 1.2), (4.5, 67, .8), (6, 65, 1), (7.5, 62, 1.3)]),
    ]
    for chapter, (name, program, melody) in enumerate(journeys):
        events = []
        instrument(events, 0, program, 53, 79)
        instrument(events, 1, 40 if chapter == 2 else 42, 78, 44)
        instrument(events, 2, 21 if chapter == 3 else 24, 69, 46)
        for phrase in range(4):
            start = phrase * 24 + 12
            for n, (offset, pitch, length) in enumerate(melody):
                note(events, 0, start + offset + .04, length, pitch, 40 + chapter * 2 + n % 3 * 2)
                if chapter == 3 and n in (0, 3, 6):
                    note(events, 2, start + offset + .09, length * 1.1, pitch - 12, 29)
            if chapter >= 2:
                note(events, 1, start + .12, 5.7, 53 if chapter != 3 else 50, 31 + chapter)
            if chapter == 4:
                # Open F-major colour after the shared D-minor question: room beyond the task.
                note(events, 2, start + 6.12, 2.7, 57, 32)
                note(events, 2, start + 6.14, 2.7, 60, 30)
        write_midi(destination / f'{name}_score.mid', [events])
    print('Composed five place themes, five character motifs, and five chapter developments.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--expand', action='store_true', help='Compose the fifteen new layers while preserving the approved four suites.')
    parser.add_argument('--places-only', action='store_true', help='Recompose only the five place arrangements, preserving all other source MIDIs.')
    options = parser.parse_args()
    if not options.expand and not options.places_only: compose()
    compose_layers(places_only=options.places_only)
