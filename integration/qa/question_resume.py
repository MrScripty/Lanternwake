"""Fresh normal player resumes an actually saved unanswered evidence question."""
import argparse
from pathlib import Path
import time

from normal_player import NormalPlayer, digest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    with NormalPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        pending = game.snapshot()
        index = next(i for i, beat in enumerate(game.beats) if beat['id'] == pending['beatId'])
        activity = game.beats[index]['activity']
        game.check(not pending['solvedActivities'], 'actual preceding player saved the first question unanswered')
        stable = game.slots()
        game.click('Load'); game.root_capture('explicit-pending-load-choice')
        game.click('Load current manual save'); time.sleep(len(game.beats[index]['text']) / 55 + .3)
        game.check(game.slots() == stable, 'fresh normal Load preserves the saved unanswered bytes')
        game.click('Settings'); game.wait_visible('100'); game.root_capture('fresh-reading-default-100')
        game.key('Escape'); game.wait_visible('Reading settings', False)
        game.key('Return'); game.wait_visible('Compare the evidence')
        game.root_capture('resumed-unanswered-question')
        game.key('Return'); game.wait_visible('Your catalogue')
        game.check(game.slots() == stable, 'fresh resumed default activation reviews instead of answering')
        game.key('Escape'); game.wait_visible('Compare the evidence')
        game.check(game.slots() == stable, 'fresh resumed review restores same unanswered question')
        game.key('Shift_L', True)
        try:
            for step in range(len(activity['options']) - activity['correctIndex']):
                game.key('Tab')
        finally:
            game.key('Shift_L', False)
        game.root_capture('resumed-correct-choice-focused')
        game.key('Return'); game.wait_visible('Compare the evidence', False)
        game.wait_visible(activity['explanation']); game.root_capture('resumed-authored-explanation')
        solved = game.snapshot('autosave.json')
        game.check(solved['beatId'] == pending['beatId'] and solved['history'] == pending['history'], 'fresh correct answer preserves reached beat and exact history')
        game.check(solved['solvedActivities'] == [pending['beatId']], 'fresh normal autosave solves only the selected question')
        game.check(game.snapshot() == pending, 'fresh solved autosave leaves pending manual save unchanged')
        game.key('Return'); time.sleep(.25)
        game.check(game.snapshot('autosave.json')['beatId'] == game.beats[index + 1]['id'], 'fresh correct answer enables only next canonical beat')
        game.root_capture('fresh-next-beat')
        game.result['resumedQuestion'] = pending['beatId']
        game.quit()
    print('PASS fresh normal unanswered-question Load, 100% session default, keyboard review and correct progression.')


if __name__ == '__main__':
    main()
