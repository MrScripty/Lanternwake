"""Ordinary Godot staged descent, external frame captures and keyboard/save invariants."""
import argparse
from pathlib import Path
import time

from normal_player import digest, normalized
from pumas_unavailable_input import SettledPlayer
from bell_restore_observation import observe_player, verify_restore


def capture_beat(game, name, beat_id):
    beat = next(b for b in game.beats if b['id'] == beat_id)
    path = game.root_capture(name)
    visible = normalized(' '.join(word['text'] for group in game.words(path) for word in group))
    game.check(normalized(' '.join(beat['text'].split()[:6])) in visible,
               'captured actual rendered passage: ' + name + ' / ' + beat_id)
    game.result.setdefault('capturedBeats', {})[name] = beat_id


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--reduced', action='store_true')
    args = parser.parse_args()
    with observe_player(SettledPlayer(args.display_state, args.fixture, args.output, args.seed)) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.result['reducedMotion'] = args.reduced
        game.click('Settings'); game.click('Toggle instant text')
        if args.reduced:
            for _ in range(2):
                game.click('Settings'); game.click('Larger reading text'); game.key('Escape')
            game.click('Settings'); game.click('Toggle reduced motion')
        original = game.snapshot(); slots = game.slots()
        game.click('Load'); game.click('Load current manual save')
        game.check(game.slots() == slots and original['beatId'] == 'ch5_s3_b003', 'frozen before-descent Load preserves exact slots')
        capture_beat(game, 'suspended', 'ch5_s3_b003')
        game.key('Return')
        time.sleep(.4)
        capture_beat(game, 'descent-early', 'ch5_s3_b004')
        first = game.snapshot('autosave.json'); saved = game.slots()
        game.check(first['beatId'] == 'ch5_s3_b004' and first['solvedActivities'] == original['solvedActivities'] and
                   first['history'][:-1] == original['history'], 'one keyboard press reaches only existing descent beat')
        time.sleep(1.8); capture_beat(game, 'descent-settled', 'ch5_s3_b004')
        game.check(game.slots() == saved, 'cosmetic travel changes no saved progress')
        verify_restore(game, 'ch5_s3_b004', 'ch5_s3_b005')
        capture_beat(game, 'descent-loaded', 'ch5_s3_b004')
        game.key('Return'); time.sleep(2); capture_beat(game, 'flow-settled', 'ch5_s3_b005')
        game.check(game.snapshot('autosave.json')['beatId'] == 'ch5_s3_b005', 'Continue remains available through flow phase')
        game.key('Return'); time.sleep(.5); capture_beat(game, 'seated', 'ch5_s3_b006')
        seated = game.snapshot('autosave.json')
        game.check(seated['beatId'] == 'ch5_s3_b006' and seated['solvedActivities'] == original['solvedActivities'], 'same arrival cue beat reached without a new gate')
        verify_restore(game, 'ch5_s3_b006', 'ch5_s3_b007')
        capture_beat(game, 'seated-loaded', 'ch5_s3_b006')
        game.key('Return'); time.sleep(.5); capture_beat(game, 'wide-after-release', 'ch5_s3_b007')
        game.check(game.snapshot('autosave.json')['beatId'] == 'ch5_s3_b007', 'next original beat restores wide stage without accidental advance')
        game.quit()
    print('PASS normal guided bell presentation; reduced motion:', args.reduced)


if __name__ == '__main__': main()
