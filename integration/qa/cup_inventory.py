"""Observe the authored cup insert through ordinary normal-player controls."""
import argparse
from pathlib import Path
import time

from PIL import Image

from normal_player import NormalPlayer, digest


def cup_pixels(path):
    image = Image.open(path).convert('RGB').crop((560, 135, 745, 405))
    return bytes(b > r + 35 and b > g + 5 and b > 120 for r, g, b in image.get_flattened_data())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--fresh', action='store_true')
    args = parser.parse_args()
    with NormalPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        time.sleep(.5)
        game.click('Load'); game.wait_visible('Load or recover a watch')
        game.click('Load current manual save')
        current = game.snapshot()['beatId']

        def advance(expected):
            time.sleep(.5)
            game.click('Continue')
            if game.snapshot('autosave.json')['beatId'] == current:
                game.click('Continue')
            game.check(game.snapshot('autosave.json')['beatId'] == expected,
                       'ordinary Continue reaches ' + expected)
            time.sleep(.5)

        if not args.fresh:
            index = next(i for i, beat in enumerate(game.beats) if beat['id'] == current)
            target = next(i for i, beat in enumerate(game.beats) if beat['id'] == 'ch1_s2_b007')
            game.check(current == 'ch1_s1a_evidence', 'actual prior player seed resumes first unanswered question')
            beat = game.beats[index]
            time.sleep(len(beat['text']) / 55 + .5)
            game.key('Return'); game.wait_visible('Compare the evidence')
            game.key('Shift_L', True)
            try:
                for step in range(len(beat['activity']['options']) - beat['activity']['correctIndex']):
                    game.key('Tab')
            finally:
                game.key('Shift_L', False)
            game.key('Return'); game.wait_visible('Compare the evidence', False)
            solved = game.snapshot('autosave.json')
            game.check(solved['solvedActivities'] == [current], 'normal keyboard answer solves only reached question')
            while index < target:
                advance(game.beats[index + 1]['id'])
                index += 1
                current = game.beats[index]['id']
            game.click('Save')
            wide_before = cup_pixels(game.native_capture('wide-before'))
            advance('ch1_s2_b008'); current = 'ch1_s2_b008'
            time.sleep(len(game.beats[target + 1]['text']) / 55 + .5)
            game.click('Save')
        else:
            game.check(current == 'ch1_s2_b008', 'fresh process loads actual preceding inventory manual save')
            stable = game.slots()
            time.sleep(8)
            game.check(game.slots() == stable, 'fresh normal Load preserves saved bytes')

        initial = cup_pixels(game.native_capture('inventory-100'))
        game.check(sum(initial) > 6000, 'actual normal pixels show enlarged blue cup body above prose')
        if not args.fresh:
            game.check(sum(initial) > sum(wide_before) * 5, 'inventory materially enlarges the cup over preceding wide view')
            stable = game.slots()
            for key, title in [('h', 'The record'), ('e', 'Your catalogue')]:
                game.key(key); game.wait_visible(title); game.root_capture('inventory-' + key)
                game.key('Escape'); game.wait_visible(title, False)
                game.check(game.slots() == stable, 'ordinary reader preserves every inventory save byte: ' + title)
            game.click('Settings'); game.click('Larger reading text'); game.click('Larger reading text')
            game.wait_visible('150'); game.key('Escape'); game.wait_visible('Reading settings', False)
            game.check(cup_pixels(game.native_capture('inventory-150')) == initial,
                       '150 percent prose retains exact blue cup pixels above panel')
            game.click('Settings'); game.click('Toggle reduced motion')
            game.wait_visible('Reduced motion enabled')
            reduced = cup_pixels(game.native_capture('inventory-reduced-motion'))
            time.sleep(1)
            game.check(reduced == initial and cup_pixels(game.native_capture('inventory-reduced-motion-repeat')) == initial,
                       'reduced motion retains the same static inventory composition')
            game.check(game.slots() == stable, 'reading size and reduced motion preserve all save bytes')

        advance('ch1_s2_b009'); current = 'ch1_s2_b009'
        game.check(sum(cup_pixels(game.native_capture('wide-after'))) * 5 < sum(initial),
                   'following beat restores wide view')
        if not args.fresh:
            stable = game.slots()
            game.click('Load'); game.click('Load current manual save'); time.sleep(.5)
            game.check(cup_pixels(game.native_capture('inventory-current-load')) == initial,
                       'current manual Load restores inventory composition')
            game.check(game.slots() == stable, 'current inventory Load preserves every save byte')
            game.click('Load'); game.click('Recover previous manual save'); time.sleep(.5)
            game.check(sum(cup_pixels(game.native_capture('wide-previous-recovery'))) * 5 < sum(initial),
                       'previous manual recovery restores preceding wide view')
            game.check(game.slots() == stable, 'previous recovery preserves every save byte')
            current = 'ch1_s2_b007'
            advance('ch1_s2_b008'); current = 'ch1_s2_b008'
            game.check(cup_pixels(game.native_capture('inventory-repeat')) == initial,
                       'repeated ordinary progression restores inventory composition')
        slots = game.output / 'save-slots'
        slots.mkdir()
        for name, data in game.slots().items():
            (slots / name).write_bytes(data)
        game.result['route'] = 'fresh inventory load' if args.fresh else 'first question, wide/insert/wide, readers/settings/load/recovery/repeat'
        game.quit()
    print('PASS actual normal-player cup inventory staging and production Quit.')


if __name__ == '__main__':
    main()
