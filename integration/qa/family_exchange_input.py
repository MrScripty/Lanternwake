"""Normal Godot keyboard intentions and real v2 save recovery, observed externally."""
import argparse
from pathlib import Path
import time

from normal_player import digest
from pumas_unavailable_input import SettledPlayer


def read_response(game, choice, option):
    # Short authored phrases avoid OCR dropping the isolated "I" in choice 1.
    game.wait_visible(['permission sheet', 'appreciate that', 'The channel log'][choice])
    if choice:
        # The response opens on Back to story; reverse Tab reaches its prose scrollbar.
        game.key('Shift_L', True); game.key('Tab'); game.key('Shift_L', False)
        game.key('End')
    # The longer choice 2 wraps "lives" beside the scrollbar; match its clear final phrase.
    game.wait_visible(' '.join(option['reply'].split()[-(3 if choice == 2 else 5):]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--choice', type=int, choices=range(3), required=True)
    args = parser.parse_args()
    with SettledPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.result['choice'] = args.choice
        game.click('Settings'); game.click('Toggle instant text')
        for _ in range(2):
            game.click('Settings'); game.click('Larger reading text'); game.key('Escape')
        original = game.snapshot(); slots = game.slots()
        game.check(original['version'] == 2 and original['beatId'] == 'ch4_s1a_b035', 'frozen v2 target used')
        game.click('Load'); game.click('Load current manual save')
        game.check(game.slots() == slots, 'legacy Load preserves source save bytes')
        exchange = next(b['exchange'] for b in game.beats if b['id'] == original['beatId'])
        game.click('Speak with'); game.wait_visible('The private folder is closed')
        game.root_capture('intentions-150')
        game.key('Escape')
        game.check(game.slots() == slots, 'Escape leaves exchange unspoken and saves unchanged')
        game.click('Speak with'); game.wait_visible('The private folder is closed')
        for _ in range(args.choice):
            game.key('Tab')
        game.root_capture('focused-intention-150')
        game.key('Return')
        option = exchange['options'][args.choice]
        read_response(game, args.choice, option)
        game.root_capture('authored-consequence-150')
        chosen = game.snapshot('autosave.json')
        game.check(chosen['beatId'] == original['beatId'] and chosen['solvedActivities'] == original['solvedActivities'] and
                   chosen['history'][:-2] == original['history'], 'selection changes no progress, gate or prior history')
        pair = chosen['history'][-2:]
        game.check([p['text'] for p in pair] == [option['label'], option['reply']] and
                   [p['speaker'] for p in pair] == ['you', 'tomas'] and all(not p['generated'] and
                   p.get('sceneId') is None and p.get('conversationCharacterId') is None for p in pair), 'exact authored pair retained without model scope')
        game.key('Escape'); game.click('Speak with'); read_response(game, args.choice, option)
        game.key('Escape'); game.click('Save')
        game.check(game.snapshot() == chosen, 'read-only reopening and Save preserve exact chosen record')
        game.click('Load'); game.click('Load current manual save'); game.click('Speak with')
        read_response(game, args.choice, option)
        game.root_capture('restored-consequence-150')
        game.key('Escape'); game.click('Continue')
        game.check(game.snapshot('autosave.json')['beatId'] == 'ch4_s1a_b036', 'each intention rejoins unchanged successor')
        game.quit()
    print('PASS normal Godot authored family boundary, choice', args.choice)


if __name__ == '__main__':
    main()
