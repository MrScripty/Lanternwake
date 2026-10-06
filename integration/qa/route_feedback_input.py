"""Ordinary Godot route inference, specific retries and legacy/current slot preservation."""
import argparse
from pathlib import Path
import time

from normal_player import digest
from pumas_unavailable_input import SettledPlayer
from source_reconstruction_input import tabs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--percent', type=int, choices=[125, 150], required=True)
    args = parser.parse_args()
    with SettledPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.result['percent'] = args.percent
        game.click('Settings'); game.click('Toggle instant text')
        for _ in range((args.percent - 100) // 25):
            game.click('Settings'); game.click('Larger reading text'); game.key('Escape')
        original = game.snapshot(); slots = game.slots()
        gate = 'ch2_s3a_evidence'
        game.check(original['beatId'] == gate and original['version'] in [1, 2] and gate not in original['solvedActivities'], 'real frozen unanswered route fixture used')
        game.click('Load'); game.click('Load current manual save')
        game.check(game.slots() == slots, 'legacy/current Load preserves every slot byte')
        activity = next(b['activity'] for b in game.beats if b['id'] == gate)
        game.click('Examine evidence'); game.wait_visible('What does the route reconstruction')
        game.root_capture('question')
        focused = 3  # Review known evidence owns initial focus.
        for wrong, cue in [(0, 'but we'), (2, 'visitor map shows')]:
            tabs(game, abs(focused - wrong), wrong < focused); game.key('Return')
            game.wait_visible('Check the source'); game.wait_visible(cue)
            game.root_capture('wrong-' + str(wrong))
            game.check(game.slots() == slots, 'specific wrong inference changes no saved state: ' + str(wrong))
            game.key('Escape'); game.wait_visible('What does the route reconstruction')
            focused = wrong
        correct = activity['correctIndex']
        tabs(game, abs(focused - correct), correct < focused); game.key('Return'); time.sleep(.5)
        solved = game.snapshot('autosave.json')
        game.check(solved['version'] == 2 and solved['beatId'] == gate and solved['history'] == original['history'] and
                   set(solved['solvedActivities']) == set(original['solvedActivities']) | {gate}, 'correct answer solves only old gate, preserving history and position')
        game.click('Save'); game.click('Load'); game.click('Load current manual save')
        game.check(game.snapshot() == solved, 'Save/Load preserves solved route and exact retained record')
        game.click('Continue')
        game.check(game.snapshot('autosave.json')['beatId'] == 'ch2_s4_b001', 'supported account rejoins original successor')
        game.root_capture('successor')
        game.quit()
    print('PASS normal route reconstruction feedback, size', args.percent, 'save version', original['version'])


if __name__ == '__main__':
    main()
