"""Read the first authored passage at enlarged sizes through normal X11 keys.

No injected script, preview, altered save or engine callback is used. Tab must
reach the production scrollbar; rendered prose and saved state are the observers.
"""
import argparse
from pathlib import Path
import time

from PIL import Image

from normal_player import NormalPlayer, digest, normalized


def prose_mask(path):
    image = Image.open(path).convert('RGB').crop((140, 640, 1285, 765))
    # The panel is translucent; tolerate at most 100 mask pixels of moving
    # rain behind it when comparing unchanged prose, far below reflow changes.
    return bytes(min(pixel) >= 140 for pixel in image.get_flattened_data())


def difference(left, right):
    return sum(a != b for a, b in zip(left, right))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    with NormalPlayer(args.display_state, args.fixture, args.output) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.click('Arrive on the island')
        first = game.beats[0]
        time.sleep(len(first['text']) / 55 + 1)
        game.wait_visible(' '.join(first['text'].split()[-6:]))
        game.click('Save')
        original = game.snapshot()
        game.check(original['beatId'] == first['id'], 'ordinary arrival and Save reach the first authored passage')
        for percent in [125, 150]:
            game.click('Settings'); game.wait_visible('Reading settings')
            game.click('Larger reading text')
            game.wait_visible(str(percent)); game.key('Escape')
            game.wait_visible('Reading settings', False)
            stable = game.slots()
            top = prose_mask(game.root_capture(f'passage-{percent}-top'))
            # Only Tab changes focus. A PageDown-induced prose change proves
            # that the scrollbar is reachable without clicking the scrollbar.
            attempts = []
            for step in range(12):
                game.key('Tab'); game.key('Page_Down')
                bottom_path = game.root_capture(f'passage-{percent}-tab-{step}')
                changed = difference(top, prose_mask(bottom_path))
                attempts.append(changed)
                game.check(game.slots() == stable, f'{percent}% Tab/PageDown preserves every saved byte, attempt {step}')
                if changed > 1000:
                    break
            else:
                raise RuntimeError(f'{percent}% Tab cannot reach a scrolling main passage')
            bottom = prose_mask(bottom_path)
            game.result.setdefault('scrollObservations', []).append({'percent': percent, 'tabAttempts': step + 1,
                'proseRegion': [140, 640, 1285, 765], 'differingPixels': attempts})
            tail = normalized(' '.join(first['text'].split()[-10:]))
            game.check(tail in game.visible(), f'{percent}% keyboard scroll exposes the exact authored passage ending')
            for key in ['Return', 'space']:
                game.key(key)
                game.check(game.slots() == stable, f'{percent}% scrollbar {key} does not reveal or advance/save')
                game.check(difference(bottom, prose_mask(game.root_capture(f'passage-{percent}-{key}'))) <= 100,
                           f'{percent}% scrollbar {key} preserves rendered passage')
            game.key('Home')
            game.check(difference(top, prose_mask(game.root_capture(f'passage-{percent}-home'))) <= 100,
                       f'{percent}% Home restores exact opening prose')
            game.key('End')
            game.check(difference(bottom, prose_mask(game.root_capture(f'passage-{percent}-end'))) <= 100,
                       f'{percent}% End restores exact ending prose')
            game.key('Up'); game.key('Down'); game.key('Page_Up')
            game.check(difference(top, prose_mask(game.root_capture(f'passage-{percent}-pageup'))) <= 100,
                       f'{percent}% arrows and PageUp remain in the native reader')
            game.check(game.slots() == stable, f'{percent}% all reading keys preserve saved state')
            game.click('Save')
            game.check(game.snapshot() == original, f'{percent}% saved beat, history, facts, items and activities remain exact')
        # Save leaves focus on its button. Reacquire the reader through Tab
        # before checking that the next Tab reaches Continue.
        for step in range(12):
            game.key('Tab'); game.key('Page_Down')
            if difference(top, prose_mask(game.root_capture(f'return-reader-{step}'))) > 1000:
                break
        else:
            raise RuntimeError('Tab did not return to the main reader')
        game.key('Tab')
        game.key('Return'); time.sleep(.3)
        game.check(game.snapshot('autosave.json')['beatId'] == game.beats[1]['id'],
                   'Tab leaves the scrollbar for Continue; Return advances exactly once')
        game.quit()
    print('PASS normal first passage: 125/150% Tab scrolling, full ending, no accidental advance and one intended Continue.')


if __name__ == '__main__':
    main()
