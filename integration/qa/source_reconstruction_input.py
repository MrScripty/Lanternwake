"""Normal Godot source comparison, specific retries and real v1 slot recovery."""
import argparse
from pathlib import Path
import time

from normal_player import digest
from pumas_unavailable_input import SettledPlayer


def tabs(game, count, backwards=False):
    if backwards:
        game.key('Shift_L', True)
    for _ in range(count):
        game.key('Tab')
    if backwards:
        game.key('Shift_L', False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--mode', choices=['interaction', 'headcount', 'legacy-after', 'legacy-completed'], required=True)
    args = parser.parse_args()
    with SettledPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.result['mode'] = args.mode
        game.click('Settings'); game.wait_visible('Reading settings')
        game.click('Toggle instant text')
        game.click('Settings'); game.wait_visible('Reading settings')
        game.click('Larger reading text')
        game.click('Settings'); game.wait_visible('Reading settings')
        game.click('Larger reading text'); game.key('Escape')
        original = game.snapshot()
        slots = game.slots()
        game.check(original['version'] == 1, 'real frozen v1 seed was used')
        game.click('Load'); game.wait_visible('Load current manual save')
        game.click('Load current manual save')
        game.check(game.slots() == slots, 'normal v1 Load does not rewrite source or backup bytes')
        game.click('Save')
        migrated = game.snapshot()
        gate = 'ch4_s2_reconstruction_evidence'
        game.check(migrated['version'] == 2 and migrated['beatId'] == original['beatId'] and
                   migrated['history'] == original['history'], 'explicit Save upgrades version while preserving position and exact history')
        expected = set(original['solvedActivities'])
        if args.mode != 'interaction':
            expected.add(gate)
        game.check(set(migrated['solvedActivities']) == expected, 'migration carries only the already-passed new gate')
        if args.mode in ['interaction', 'headcount']:
            activity_id = gate if args.mode == 'interaction' else 'ch4_s3a_evidence'
            if args.mode == 'interaction':
                game.click('Continue')
                unsolved = game.snapshot('autosave.json')
            else:
                unsolved = migrated
            game.check(unsolved['beatId'] == activity_id and activity_id not in unsolved['solvedActivities'], 'current authored comparison remains unanswered')
            game.click('Save')
            baseline = game.slots()
            activity = next(b['activity'] for b in game.beats if b['id'] == activity_id)
            prompt = 'Which correction is supported' if args.mode == 'interaction' else 'Two similar names appear'
            game.click('Examine evidence'); game.wait_visible(prompt)
            game.root_capture('question-150')
            # Opening focus is Review known evidence (after the three authored options).
            focused = 3
            for wrong in range(3):
                if wrong == activity['correctIndex']:
                    continue
                tabs(game, abs(focused - wrong), wrong < focused); game.key('Return')
                game.wait_visible('Check the source')
                game.wait_visible(' '.join(activity['optionFeedback'][wrong].split()[:6]))
                game.root_capture(f'wrong-{wrong}-feedback-150')
                game.check(game.slots() == baseline, 'wrong inference changes no saved state: ' + str(wrong))
                game.key('Escape'); game.wait_visible(prompt)
                focused = wrong
            correct = activity['correctIndex']
            tabs(game, abs(focused - correct), correct < focused); game.key('Return'); time.sleep(.5)
            solved = game.snapshot('autosave.json')
            game.check(solved['beatId'] == activity_id and solved['history'] == unsolved['history'] and
                       set(solved['solvedActivities']) == expected | {activity_id}, 'correct account solves only this gate without advancing')
            game.click('Continue')
            payoff = game.snapshot('autosave.json')
            successor = 'ch4_s2_reconstruction_014' if args.mode == 'interaction' else 'ch4_s4_b001'
            game.check(payoff['beatId'] == successor, 'checked account rejoins existing authored successor')
            game.native_capture('checked-correction-payoff')
            game.click('Save'); game.click('Load'); game.wait_visible('Load current manual save')
            game.click('Load current manual save')
            game.check(game.snapshot() == payoff, 'normal Save/Load preserves solved comparison and authored payoff')
        elif args.mode == 'legacy-completed':
            game.click('Continue'); game.wait_visible('You have reached the end of Lanternwake')
            game.root_capture('legacy-completed-watch')
            game.key('Escape')
        else:
            game.native_capture('legacy-after-insertion')
            game.click('Continue')
            game.check(game.snapshot('autosave.json')['beatId'] == 'ch4_s2_b007', 'legacy passed insertion continues through original successor')
        game.quit()
    print('PASS normal Godot source reconstruction and v1 recovery:', args.mode)


if __name__ == '__main__':
    main()
