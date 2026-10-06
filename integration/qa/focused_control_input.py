"""Normal-player accept keys must activate their focused control without advancing."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import time

from PIL import Image

from normal_player import NormalPlayer, digest


FOCUS_REGIONS = {'Save': (1119, 125, 1179, 130), 'History': (1038, 125, 1116, 130),
                 'Stay and talk': (1079, 807, 1196, 813)}


def gold_pixels(path, region, native=False):
    left, top, right, bottom = region
    if native:
        left -= 80; right -= 80; top -= 50; bottom -= 50
    image = Image.open(path).convert('RGB').crop((left, top, right, bottom))
    return sum(r > 180 and 130 < g < 210 and 80 < b < 150 for r, g, b in image.get_flattened_data())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    with NormalPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.click('Load'); game.click('Load current manual save')
        index = next(i for i, beat in enumerate(game.beats) if beat['id'] == game.result['seed']['beatId'])
        time.sleep(len(game.beats[index]['text']) / 55 + .5)
        current = game.snapshot()
        game.check(current['beatId'] == 'ch1_s1_b021', 'fully revealed normal passage has optional Talk and exact saved origin')
        windows = subprocess.check_output(['xwininfo', '-root', '-tree'], env=game.env, text=True)
        window = next(line.split()[0] for line in windows.splitlines() if '("Godot_Engine" "Lanternwake")' in line)
        with (game.output / 'keyboard-events.log').open('wb') as event_log:
            observer = subprocess.Popen(['stdbuf', '-oL', 'xev', '-id', window, '-event', 'keyboard'], env=game.env,
                                        stdout=event_log, stderr=subprocess.STDOUT)
        try:
            for target in ['Save', 'History', 'Stay and talk']:
                for key in ['Return', 'space']:
                    for hold in [False, True]:
                        label = f'{target.lower().replace(" ", "-")}-{key}-{int(hold)}'
                        for attempt in range(14):
                            image = game.root_capture(label + '-focus-current')
                            if gold_pixels(image, FOCUS_REGIONS[target]) > 30:
                                break
                            game.key('Tab')
                        else:
                            raise RuntimeError('Tab did not visibly focus ' + target)
                        native = game.native_capture(label + '-focused')
                        game.check(gold_pixels(native, FOCUS_REGIONS[target], native=True) > 30,
                                   'native gold underline confirms Tab focus: ' + target)
                        before = game.slots()
                        stamp = (game.saves / 'save.json').stat().st_mtime_ns
                        game.key(key, True)
                        try:
                            time.sleep(1.2 if hold else .15)
                            game.root_capture(label + '-pressed')
                            game.check(game.slots() == before, target + ' key press/held repeats preserve every save before release')
                            if target == 'Save':
                                game.check((game.saves / 'save.json').stat().st_mtime_ns == stamp,
                                           'focused Save does not rewrite its slot on press or held repeats before release')
                        finally:
                            game.key(key, False); time.sleep(.25)
                        if target == 'Save':
                            game.check(game.snapshot() == current, 'keyboard Save preserves exact beat and history')
                            game.check((game.saves / 'save.json').stat().st_mtime_ns > stamp, 'keyboard Save actually writes the intended manual slot')
                            game.check('autosave.json' not in game.slots(), 'keyboard Save does not create an advance autosave')
                        else:
                            caption = 'The record' if target == 'History' else 'A moment between the lines'
                            game.wait_visible(caption)
                            game.check(game.slots() == before, 'keyboard ' + target + ' opens without save or progress changes')
                            game.root_capture(label + '-opened')
                            game.key('Escape'); game.wait_visible(caption, False)
                            game.check(game.slots() == before, 'closing keyboard ' + target + ' preserves every save byte')
            # Actual text-field Space and Enter retain typing/submission behavior.
            game.click('Stay and talk'); game.wait_visible('A moment between the lines')
            text = 'a careful inventory.'
            game.type_text(text)
            game.root_capture('text-space-preserved')
            game.key('Return')
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline and not (game.saves / 'autosave.json').exists():
                time.sleep(.1)
            data = game.snapshot('autosave.json')
            game.check(data['beatId'] == current['beatId'], 'text-field Enter submits without canonical advance')
            game.check(len(data['history']) == len(current['history']) + 2, 'text-field Enter records one optional reply only')
            game.check(data['history'][-2]['text'] == text, 'typed spaces survive real text-field input')
            game.check(data['history'][-1]['text'] == game.beats[index]['conversation']['fallback'] and not data['history'][-1]['generated'], 'submitted reply is exact labelled authored fallback')
            game.key('Escape'); game.wait_visible('A moment between the lines', False)
            # The preserved focused Continue path still advances once.
            game.key('Return'); time.sleep(.25)
            game.check(game.snapshot('autosave.json')['beatId'] == game.beats[index + 1]['id'], 'focused Continue still advances exactly once')
        finally:
            observer.terminate(); observer.wait(timeout=10)
        events = (game.output / 'keyboard-events.log').read_text()
        game.result['rawX11KeyPressEvents'] = len(re.findall(r'^KeyPress event', events, re.M))
        game.result['rawX11KeyReleaseEvents'] = len(re.findall(r'^KeyRelease event', events, re.M))
        game.result['limits'].append('Held keys exercise X11 server autorepeat; no gamepad bindings or acceptance is claimed.')
        game.quit()
    print('PASS actual Tab-focused Save/History/Talk Return/Space press, release and held repeats; text submission and Continue.')


if __name__ == '__main__':
    main()
