"""Normal Godot authored response and real owned-filesystem autosave failure."""
import argparse
from pathlib import Path

from normal_player import digest
from pumas_unavailable_input import SettledPlayer
from family_exchange_input import read_response


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    with SettledPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.click('Settings'); game.click('Toggle instant text')
        game.click('Load'); game.click('Load current manual save')
        original = game.snapshot(); slots = game.slots()
        game.check(original['beatId'] == 'ch4_s1a_b035', 'owned legacy exchange fixture loaded')
        option = next(b['exchange']['options'][0] for b in game.beats if b['id'] == original['beatId'])
        blocker = game.saves / 'autosave.json'
        game.check(not blocker.exists(), 'fault target is absent in owned fixture')
        blocker.mkdir()  # Real write failure, without production hooks or permission changes.
        try:
            game.click('Speak with'); game.key('Return')
            read_response(game, 0, option)
            game.root_capture('response-after-failed-autosave')
            game.key('Escape'); game.wait_visible('Could not save')
            game.root_capture('warning-after-return')
            game.click('Speak with'); read_response(game, 0, option)
            game.key('Escape'); game.wait_visible('Could not save')
            game.root_capture('warning-after-reopening')
            game.check(game.snapshot() == original and not blocker.is_file(), 'failed autosave preserves original manual slot and publishes no autosave')
        finally:
            blocker.rmdir()
        game.check(game.slots() == slots, 'failure and read-only review leave every saved byte unchanged')
        game.click('Save'); game.wait_visible('Saved on this device')
        chosen = game.snapshot()
        game.check(chosen['beatId'] == original['beatId'] and chosen['solvedActivities'] == original['solvedActivities'] and
                   chosen['history'][:-2] == original['history'] and
                   [p['text'] for p in chosen['history'][-2:]] == [option['label'], option['reply']],
                   'successful manual retry saves exactly the retained authored pair')
        game.root_capture('successful-manual-retry')
        game.click('Speak with'); read_response(game, 0, option); game.key('Escape')
        game.wait_visible('Saved on this device')
        game.check(game.snapshot() == chosen, 'successful retry status and record survive normal Return')
        game.quit()
    print('PASS normal Godot real autosave failure, persistent warning, exact reading Return and successful retry.')


if __name__ == '__main__':
    main()
