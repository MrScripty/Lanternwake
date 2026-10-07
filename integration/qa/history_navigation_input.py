"""Normal native player, external keyboard input and observed History pixels/files."""
import argparse
from pathlib import Path
import shutil
import subprocess
import time

from PIL import Image
from normal_player import NormalPlayer, digest, normalized


class HistoryPlayer(NormalPlayer):
    def wait_visible(self, caption, present=True):
        if caption != 'Load or recover a watch':
            return super().wait_visible(caption, present)
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            image = self.root_capture('current')
            title = self.output / 'load-title.png'
            Image.open(image).crop((296, 153, 1143, 185)).save(title)
            self.result['images'][title.name] = digest(title)
            text = subprocess.check_output(['tesseract', str(title), 'stdout', '--psm', '7'],
                                           stderr=subprocess.DEVNULL, text=True, timeout=15)
            if (normalized(caption) in normalized(text)) == present:
                self.check(True, ('visible: ' if present else 'closed: ') + caption)
                return
            time.sleep(.2)
        self.check(False, 'Load window title did not settle')

    # Allow native frames between synthetic edges on the software renderer.
    def key(self, name, pressed=None):
        if pressed is not None:
            super().key(name, pressed)
            time.sleep(.15)
            return
        super().key(name, True); time.sleep(.2)
        super().key(name, False); time.sleep(.3)

    def click_at(self, x, y, label):
        self.result['events'].append({'mouse': label, 'x': x, 'y': y, 'holdSeconds': .2})
        assert self.xtest.XTestFakeMotionEvent(self.display, -1, x, y, 0)
        self.sync(); time.sleep(.2)
        assert self.xtest.XTestFakeButtonEvent(self.display, 1, 1, 0)
        self.sync(); time.sleep(.2)
        assert self.xtest.XTestFakeButtonEvent(self.display, 1, 0, 0)
        self.sync(); time.sleep(.5)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed', 'later-seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    with HistoryPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        # Both slots came from the native progression fixture; neither is fabricated.
        shutil.copy2(args.later_seed, game.saves / 'autosave.json')
        stable = game.slots()
        early, later = game.snapshot(), game.snapshot('autosave.json')

        def load(automatic):
            game.click('Load'); game.wait_visible('Load or recover a watch')
            time.sleep(.8)
            game.click('Load current ' + ('autosave' if automatic else 'manual save'))
            game.wait_visible('Load or recover a watch', False)
            time.sleep(1)

        def reader_text(label):
            path = game.root_capture(label)
            crop = game.output / (label + '-reader.png')
            # Crop the native record body, excluding the main story passage.
            Image.open(path).crop((320, 325, 1120, 650)).save(crop)
            game.result['images'][crop.name] = digest(crop)
            return normalized(' '.join(word['text'] for group in game.words(crop) for word in group))

        def wait_reader_text(label, expected):
            deadline = time.monotonic() + 12
            attempt = 0
            while time.monotonic() < deadline:
                if normalized(expected) in reader_text(label + '-settling-' + str(attempt)):
                    return True
                attempt += 1
                time.sleep(.3)
            return False

        load(True); game.key('h'); game.wait_visible('The record')
        game.key('Return')
        time.sleep(.4)
        last = ' '.join(later['history'][-1]['text'].split()[:8])
        game.check(wait_reader_text('later-latest', last), 'latest shows the actual later-slot reached entry in the record')
        game.key('Escape'); game.wait_visible('The record', False); time.sleep(.5); load(False)
        for percent in [100, 150]:
            if percent == 150:
                game.click('Settings'); game.click('Larger reading text'); game.click('Larger reading text')
                game.wait_visible('150'); game.key('Escape')
            for repeat in range(2):
                prefix = f'history-{percent}-{repeat}'
                game.key('h'); game.wait_visible('The record')
                # Opening focus is latest; Shift-Tab reaches the actual editable search.
                game.key('Shift_L', True); game.key('Tab'); game.key('Shift_L', False)
                game.type_text('zzqqxx'); game.wait_visible('No reached entries match your search')
                game.root_capture(prefix + '-no-results')
                game.type_text('daylight'); time.sleep(.3)
                game.check('in daylight' in reader_text(prefix + '-search'), 'typing filters to actually restored reached prose')
                game.key('Tab'); game.key('Return'); time.sleep(.4)
                last = ' '.join(early['history'][-1]['text'].split()[:8])
                game.check(wait_reader_text(prefix + '-latest', last), 'keyboard latest clears search and shows exact earlier-restored final entry')
                # Reading focus returns to chapter, whose popup contains only reached titles.
                game.key('Shift_L', True); game.key('Tab'); game.key('Shift_L', False)
                game.key('Return'); time.sleep(.3)
                popup = game.output / (prefix + '-popup.png')
                Image.open(game.root_capture(prefix + '-chapters')).crop((324, 300, 1118, 550)).save(popup)
                game.result['images'][popup.name] = digest(popup)
                visible = normalized(subprocess.check_output(['tesseract', str(popup), 'stdout', '--psm', '6'],
                                     stderr=subprocess.DEVNULL, text=True, timeout=15))
                game.check('the inventory' in visible and 'the wrong channel' in visible, 'authoritative reached chapters appear in native selector')
                game.check('what the water kept' not in visible and 'the night ledger' not in visible and 'an open horizon' not in visible, 'selector after earlier Load excludes later chapters')
                game.key('Home'); game.key('Down'); game.key('Return'); time.sleep(.3)
                game.check('the ferry leaves you' in reader_text(prefix + '-first-chapter'), 'keyboard chapter jump reaches first recorded entry')
                game.key('Escape'); game.wait_visible('The record', False)
                game.check(game.slots() == stable, 'search/jumps/repeated close preserve all manual and automatic bytes')
        game.check(game.snapshot() == early and game.snapshot('autosave.json') == later, 'normal fresh process retains both exact reached snapshots')
        game.quit()
    print('PASS native History keyboard/latest/search/reached chapters/no results/reopen at 100/150%; actual saved earlier slot after later restore.')


if __name__ == '__main__':
    main()
