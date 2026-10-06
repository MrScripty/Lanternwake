"""Inspect the source-comparison passage through normal Godot controls and saves."""
import argparse
from pathlib import Path
import time

from normal_player import digest, normalized
from pumas_unavailable_input import SettledPlayer
from main_passage_keyboard import difference, prose_mask


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    with SettledPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.click('Settings'); game.wait_visible('Reading settings')
        game.click('Toggle instant text')
        game.click('Settings'); game.wait_visible('Reading settings')
        game.click('Larger reading text'); game.key('Escape')
        game.click('Load'); game.wait_visible('Load current manual save'); game.click('Load current manual save')
        original = game.snapshot()
        game.check(original['beatId'] == 'ch4_s2_b006', 'existing pre-insert ID remains loadable')
        additions = [b for b in game.beats if b['id'].startswith('ch4_s2_reconstruction_')]
        for index, beat in enumerate(additions):
            game.click('Continue')
            current = game.snapshot('autosave.json')
            game.check(current['beatId'] == beat['id'] and current['history'][-1]['text'] == beat['text'],
                       'normal Continue records exact authored reconstruction beat: ' + beat['id'])
            game.check(current['solvedActivities'] == original['solvedActivities'], 'new source comparison changes no evidence gates')
            if index == 0:
                for size in [125, 150]:
                    if size == 150:
                        game.click('Settings'); game.wait_visible('Reading settings')
                        game.click('Larger reading text'); game.key('Escape')
                    game.click('Save')
                    before = game.slots()
                    opening = prose_mask(game.root_capture(f'reconstruction-{size}-opening'))
                    tail = normalized(' '.join(beat['text'].split()[-8:]))
                    if tail not in game.visible():
                        for attempt in range(12):
                            game.key('Tab'); game.key('End'); time.sleep(.2)
                            ending = prose_mask(game.root_capture(f'reconstruction-{size}-tab-{attempt}'))
                            if difference(opening, ending) > 1000:
                                break
                        else:
                            raise RuntimeError('Tab/End did not reach the overflowing source-comparison passage.')
                        game.check(tail in game.visible(), f'{size}% native keyboard reader shows exact passage ending')
                        game.key('Return'); game.key('space')
                        game.check(game.slots() == before, f'{size}% reader accept keys preserve every saved byte')
                    else:
                        game.check(normalized(' '.join(beat['text'].split()[:8])) in game.visible(),
                                   f'{size}% full passage fits the visible reader without scrolling')
                    game.native_capture(f'reconstruction-{size}-ending')
            if index in [7, 13]:
                game.native_capture('reconstruction-' + beat['id'])
        game.click('Continue')
        game.check(game.snapshot('autosave.json')['beatId'] == 'ch4_s2_b007', 'new passage rejoins exact existing authored successor')
        game.quit()
    print('PASS normal reconstruction: 14 exact beats, stable gates/IDs, 125/150% readability, overflow keyboard scrolling and existing successor.')


if __name__ == '__main__':
    main()
