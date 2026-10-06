"""Normal native input observes pose changes and exact Save/Load/recovery."""
import argparse
from pathlib import Path
import time

from normal_player import digest
from pumas_unavailable_input import SettledPlayer
from bell_descent_input import capture_beat
from bell_restore_observation import observe_player, runtime_state, verify_restore


def capture_settled(game, label, beat_id):
    beat = next(b for b in game.beats if b['id'] == beat_id)
    game.wait_visible(' '.join(beat['text'].split()[:6]))
    capture_beat(game, label, beat_id)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--reduced', action='store_true')
    args = parser.parse_args()
    with observe_player(SettledPlayer(args.display_state, args.fixture, args.output, args.seed)) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.result['reducedMotion'] = args.reduced
        game.click('Settings'); game.click('Toggle instant text'); game.wait_visible('Instant text enabled')
        if args.reduced:
            for _ in range(2): game.click('Settings'); game.click('Larger reading text'); game.key('Escape')
            game.click('Settings'); game.click('Toggle reduced motion')
        seed = game.snapshot(); slots = game.slots()
        game.click('Load'); game.click('Load current manual save')
        beat = seed['beatId']; before = runtime_state(game, beat)
        game.check(game.slots() == slots, 'initial Load preserves owned fixture slots')
        capture_settled(game, 'loaded-working', beat)
        catalogue = beat == 'ch5_s5_b002'
        game.check(beat == 'ch1_s2_b008' or catalogue, 'explicit authored performance fixture selected')
        next_beat = 'ch5_s5_b003' if catalogue else 'ch1_s2_b009'
        following = 'ch5_s5_b004' if catalogue else 'ch1_s2_b010'
        game.key('Return'); runtime_state(game, next_beat)
        capture_settled(game, 'next-pose', next_beat)
        live = runtime_state(game, next_beat); saved = game.slots(); time.sleep(1)
        game.check(runtime_state(game, next_beat) == live and game.slots() == saved, 'holding a posed passage cannot advance or rewrite story')
        verify_restore(game, next_beat, following)
        capture_settled(game, 'loaded-next-pose', next_beat)
        game.click('Load'); game.click('Recover previous manual save')
        game.check(runtime_state(game, beat) == before, 'previous manual recovery restores full earlier live state')
        capture_settled(game, 'recovered-working', beat)
        if not catalogue:
            game.key('Return'); runtime_state(game, next_beat)
            game.key('Return'); runtime_state(game, following)
            capture_settled(game, 'nessa-speaking-ada-listening', following)
        game.quit()
    print('PASS normal authored performance and exact restored state; reduced:', args.reduced)


if __name__ == '__main__': main()
