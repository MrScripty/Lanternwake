"""Real Main, external input: first-route retries, focus and exact live/slot state."""
import argparse
from pathlib import Path
import time

from normal_player import digest
from pumas_unavailable_input import SettledPlayer
from source_reconstruction_input import tabs
from bell_restore_observation import observe_player, runtime_state


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--percent', type=int, choices=[100, 125, 150], required=True)
    args = parser.parse_args()
    player = observe_player(SettledPlayer(args.display_state, args.fixture, args.output, args.seed))
    # Populate all four ordinary slots before launch. Input never edits slots.
    for name in ['save.previous.json', 'autosave.json', 'autosave.previous.json']:
        (player.saves / name).write_bytes(args.seed.read_bytes())
    with player as game:
        game.result['controllerSha256'] = digest(__file__)
        game.result['percent'] = args.percent
        gate = 'ch1_s1a_evidence'
        game.click('Settings'); game.click('Toggle instant text')
        for _ in range((args.percent - 100) // 25):
            game.click('Settings'); game.click('Larger reading text'); game.key('Escape')
        slots = game.slots(); game.click('Load'); game.click('Load current manual save')
        original = runtime_state(game, gate)
        game.check(not original['canAdvance'] and gate not in original['snapshot']['solvedActivities'],
                   'actual loaded first-route question is unanswered')
        game.check(game.slots() == slots, 'Load preserves all four seeded save/backup byte sequences')
        activity = next(b['activity'] for b in game.beats if b['id'] == gate)
        prompt = 'Which route is the agreed bad-weather route'
        game.click('Examine evidence'); game.wait_visible(prompt); game.root_capture('question')
        focused = 3  # Existing Review known evidence initial focus.
        for wrong, cue in [(0, 'maintenance access'), (2, 'distance')]:
            tabs(game, abs(focused - wrong), wrong < focused); game.key('Return')
            game.wait_visible('Check the source'); game.wait_visible(cue)
            game.wait_visible(' '.join(activity['optionFeedback'][wrong].split()[-6:]))
            game.root_capture('wrong-' + str(wrong))
            game.check(runtime_state(game, gate) == original and game.slots() == slots,
                       'wrong answer preserves full live state and all slot bytes: ' + str(wrong))
            game.click('Back to question'); game.wait_visible(prompt)
            game.root_capture('back-' + str(wrong))
            # Immediate Enter must reactivate the same wrong option: observe
            # actual originating keyboard focus instead of inferring it from Tab counts.
            game.key('Return'); game.wait_visible('Check the source'); game.wait_visible(cue)
            game.check(runtime_state(game, gate) == original and game.slots() == slots,
                       'Back restores originating focus; repeated wrong answer remains inert: ' + str(wrong))
            game.key('Escape'); game.wait_visible(prompt)
            focused = wrong
        tabs(game, abs(focused - activity['correctIndex']), activity['correctIndex'] < focused)
        game.key('Return'); time.sleep(.5)
        solved = runtime_state(game, gate)
        game.check(solved['canAdvance'] and
                   set(solved['snapshot']['solvedActivities']) == set(original['snapshot']['solvedActivities']) | {gate},
                   'only the explicit correct answer unlocks Continue at the same beat')
        for name in ['history', 'beatId']:
            game.check(solved['snapshot'][name] == original['snapshot'][name], 'correct answer preserves ' + name)
        for name in ['facts', 'inventory', 'activeStageCues']:
            game.check(solved[name] == original[name], 'correct answer preserves ' + name)
        game.check((game.saves / 'save.json').read_bytes() == slots['save.json'] and
                   (game.saves / 'save.previous.json').read_bytes() == slots['save.previous.json'],
                   'correct answer leaves both explicit manual checkpoints untouched')
        game.wait_visible('Continue'); game.root_capture('solved')
        game.click('Save'); game.click('Load'); game.click('Load current manual save')
        game.check(runtime_state(game, gate) == solved, 'actual Save/Load restores solved full live state without duplicating the record')
        game.click('Continue'); successor = runtime_state(game, 'ch1_s2_b001')
        game.check(successor['snapshot']['beatId'] == 'ch1_s2_b001', 'explicit Continue reaches the original inventory scene')
        game.root_capture('successor')
        game.result['liveStates'] = dict(unanswered=original, solved=solved, successor=successor)
        game.quit()
    print('PASS normal first-route feedback, exact state/save preservation and keyboard focus at', args.percent)


if __name__ == '__main__':
    main()
