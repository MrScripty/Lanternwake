"""Reach and answer the first required evidence question through normal desktop input."""
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
        index = next(i for i, beat in enumerate(game.beats) if beat['id'] == game.result['seed']['beatId'])
        question = next(i for i, beat in enumerate(game.beats) if beat.get('activity'))
        game.check(index < question, 'verified owned normal-session seed precedes first required question')
        game.click('Load'); game.click('Load current manual save')
        time.sleep(len(game.beats[index]['text']) / 55 + .3)
        for index in range(index + 1, question + 1):
            game.click('Continue')
            if game.snapshot('autosave.json')['beatId'] != game.beats[index]['id']:
                game.click('Continue')
            data = game.snapshot('autosave.json')
            game.check(data['beatId'] == game.beats[index]['id'], 'external Continue reaches ' + game.beats[index]['id'])
            canonical = [line for line in data['history'] if not line.get('conversationCharacterId')]
            game.check([line['beatId'] for line in canonical] == [beat['id'] for beat in game.beats[:index + 1]], 'exact ordered reached story IDs')
            game.check([line['text'] for line in canonical] == [beat['text'] for beat in game.beats[:index + 1]], 'exact reached authored prose')
        time.sleep(len(game.beats[question]['text']) / 55 + .3)
        game.click('Save')
        pending = game.snapshot()
        game.check(not pending['solvedActivities'], 'first question is genuinely unanswered in manual snapshot')
        stable = game.slots()
        game.click('Settings'); game.click('Larger reading text'); game.click('Larger reading text')
        game.wait_visible('150'); game.root_capture('reading-150')
        game.key('Escape'); game.wait_visible('Reading settings', False)
        game.check(game.slots() == stable, '150% session setting preserves unanswered slots')
        game.key('Return'); game.wait_visible('Compare the evidence')
        game.root_capture('question-default-review')
        for cycle in range(3):
            # Default focus must review evidence instead of choosing an answer.
            game.key('Return'); game.wait_visible('Your catalogue')
            game.root_capture(f'catalogue-{cycle}')
            game.check(game.slots() == stable, 'default keyboard review preserves unanswered story/save bytes')
            if cycle == 0:
                game.key('Return')  # Real focused Back to question action.
            else:
                game.key('Escape')
            game.wait_visible('Compare the evidence')
            game.check(game.slots() == stable, 'keyboard review return keeps first question unanswered')
        # Review focus -> next Tab is Read the record. Escape restores that origin.
        game.key('Tab'); game.key('Return'); game.wait_visible('The record')
        game.root_capture('record-review')
        game.check(game.slots() == stable, 'record review preserves reached history and every saved slot')
        game.key('Escape'); game.wait_visible('Compare the evidence')
        game.key('Escape'); game.wait_visible('Compare the evidence', False)
        game.check(game.slots() == stable, 'closing unanswered question preserves original snapshots')
        activity = game.beats[question]['activity']

        def choose(option, label):
            game.key('Return'); game.wait_visible('Compare the evidence')
            # Initial focus is Review known evidence, after the three options.
            game.key('Shift_L', True)
            try:
                for step in range(len(activity['options']) - option):
                    game.key('Tab')
            finally:
                game.key('Shift_L', False)
            game.root_capture(label + '-focused')
            game.key('Return'); game.wait_visible('Compare the evidence', False)

        wrong = next(i for i in range(len(activity['options'])) if i != activity['correctIndex'])
        choose(wrong, 'wrong-answer')
        game.wait_visible('That does not fit the evidence yet')
        game.root_capture('wrong-retry-feedback')
        game.check(game.slots() == stable, 'wrong answer keeps the exact unanswered manual/autosave bytes')
        choose(activity['correctIndex'], 'correct-answer')
        game.wait_visible(activity['explanation'])
        game.root_capture('authored-correct-explanation')
        solved = game.snapshot('autosave.json')
        game.check(solved['beatId'] == pending['beatId'] and solved['history'] == pending['history'], 'correct answer solves without advancing or rewriting reached prose')
        game.check(solved['solvedActivities'] == [pending['beatId']], 'only the authored first question is solved in normal autosave')
        game.check(game.snapshot() == pending, 'correct answer preserves the explicitly unanswered manual save')
        game.key('Return'); time.sleep(.25)
        game.check(game.snapshot('autosave.json')['beatId'] == game.beats[question + 1]['id'], 'correct answer enables exactly the next canonical beat')
        game.root_capture('next-authored-scene')
        game.result['question'] = {'index': question, 'beatId': pending['beatId'], 'activity': activity,
                                   'readingPercent': 150, 'reviewCycles': 3, 'wrongOption': wrong,
                                   'nextBeat': game.beats[question + 1]['id']}
        game.quit()
    print('PASS first required question at 150% through normal desktop review, record, cancel, wrong retry and authored correct progression.')


if __name__ == '__main__':
    main()
