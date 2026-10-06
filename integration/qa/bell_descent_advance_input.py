"""Normal keyboard Continue can interrupt guided travel without waiting or replay."""
import argparse
from pathlib import Path
import time

from normal_player import digest
from pumas_unavailable_input import SettledPlayer
from bell_descent_input import capture_beat


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    with SettledPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.click('Settings'); game.click('Toggle instant text')
        game.wait_visible('Instant text enabled')
        game.click('Load'); game.click('Load current manual save')
        before = game.snapshot()
        game.check(before['beatId'] == 'ch5_s3_b003', 'owned frozen countdown fixture loaded')
        reached = []
        for beat_id in ['ch5_s3_b004', 'ch5_s3_b005', 'ch5_s3_b006']:
            game.key('Return')
            deadline = time.monotonic() + 2
            while not (game.saves / 'autosave.json').exists() or game.snapshot('autosave.json')['beatId'] != beat_id:
                if time.monotonic() >= deadline:
                    raise RuntimeError('One keyboard Continue did not reach ' + beat_id)
                time.sleep(.02)
            reached.append(time.monotonic())
            current = game.snapshot('autosave.json')
            game.check(current['solvedActivities'] == before['solvedActivities'], 'rapid Continue preserves required gates: ' + beat_id)
            game.root_capture('rapid-' + beat_id)
            time.sleep(.15)  # Distinct input edges after the new camera frame, below the travel interval.
        elapsed = reached[-1] - reached[0]
        game.result['advanceElapsedSeconds'] = elapsed
        game.check(elapsed < 1.8, 'both next beats reached within one phase travel interval; no animation wait')
        seated = game.snapshot('autosave.json'); slots = game.slots()
        time.sleep(2)
        game.check(game.snapshot('autosave.json') == seated and game.slots() == slots,
                   'retired travel does not automatically advance or rewrite slots')
        capture_beat(game, 'rapid-seated', 'ch5_s3_b006')
        game.click('Save'); game.click('Load'); game.click('Load current manual save')
        game.check(game.snapshot() == seated, 'rapidly seated state survives exact Save/Load')
        game.quit()
    print('PASS normal rapid keyboard descent; no animation wait or accidental advance.')


if __name__ == '__main__':
    main()
