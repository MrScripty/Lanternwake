"""Read normal record/catalogue windows with keyboard, including question review."""
import argparse
from pathlib import Path
import subprocess
import time

from PIL import Image

from normal_player import NormalPlayer, digest, normalized


def text_mask(path):
    # Observed prose area shared by the ordinary and question-review windows.
    image = Image.open(path).convert('RGB').crop((324, 211, 1106, 600))
    return bytes(min(pixel) >= 140 for pixel in image.get_flattened_data())


def difference(left, right):
    return sum(a != b for a, b in zip(left, right))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--question', default='ch1_s3_evidence')
    args = parser.parse_args()
    with NormalPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        time.sleep(.5)
        game.click('Load'); game.wait_visible('Load or recover a watch')
        game.click('Load current manual save')
        original = game.snapshot()
        index = next(i for i, beat in enumerate(game.beats) if beat['id'] == original['beatId'])
        target = next(i for i, beat in enumerate(game.beats) if beat['id'] == args.question)
        game.check(index <= target and game.beats[target].get('activity'), 'owned seed precedes the selected authored question')
        time.sleep(len(game.beats[index]['text']) / 55 + .5)
        # Build sufficient genuine reached evidence for a long catalogue. This is
        # ordinary keyboard progression, with reveal/advance, never a forged save.
        while index < target:
            time.sleep(.5)  # Let newly instantiated locations settle before input.
            beat = game.beats[index]
            current = game.snapshot('autosave.json') if (game.saves / 'autosave.json').exists() else original
            if beat.get('activity') and beat['id'] not in current['solvedActivities']:
                time.sleep(len(beat['text']) / 55 + .5)
                game.key('Return'); game.wait_visible('Compare the evidence')
                game.key('Shift_L', True)
                try:
                    for step in range(len(beat['activity']['options']) - beat['activity']['correctIndex']):
                        game.key('Tab')
                finally:
                    game.key('Shift_L', False)
                game.key('Return'); game.wait_visible('Compare the evidence', False)
                game.check(beat['id'] in game.snapshot('autosave.json')['solvedActivities'],
                           'normal keyboard answer solves reached prerequisite ' + beat['id'])
            game.key('Return')
            if game.snapshot('autosave.json')['beatId'] == beat['id']:
                game.key('Return')
            index += 1
            game.check(game.snapshot('autosave.json')['beatId'] == game.beats[index]['id'],
                       'actual keyboard progression reaches ' + game.beats[index]['id'])
        if original['beatId'] != args.question:
            game.click('Save')
        pending = game.snapshot()
        game.check(pending['beatId'] == args.question and args.question not in pending['solvedActivities'],
                   'selected question is genuinely reached and saved unanswered')
        time.sleep(len(game.beats[index]['text']) / 55 + .5)
        game.result['preparation'] = {'fromBeat': original['beatId'], 'question': args.question,
                                     'actualTransitions': target - next(i for i, b in enumerate(game.beats) if b['id'] == original['beatId'])}
        known = {fact for beat in game.beats[:target + 1] for fact in beat.get('unlockFacts', [])}
        last_fact = [fact['text'] for fact in game.story['facts'] if fact['id'] in known][-1]
        stable = game.slots()

        def wait_reader_title(title, present=True):
            # A question action also says "Read the record". Observe the actual
            # window title so returning to that question cannot look still open.
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline:
                root = game.root_capture('current')
                crop = game.output / 'reader-title.png'
                Image.open(root).crop((296, 153, 1143, 185)).save(crop)
                game.result['images'][crop.name] = digest(crop)
                text = subprocess.check_output(['tesseract', str(crop), 'stdout', '--psm', '7'],
                                               stderr=subprocess.DEVNULL, text=True, timeout=10)
                if (normalized(title) in normalized(text)) == present:
                    game.check(True, ('reader title: ' if present else 'reader closed: ') + title)
                    return
                time.sleep(.1)
            raise RuntimeError('Reader title did not settle: ' + title)

        def read(label, title, record):
            wait_reader_title(title)
            initial = text_mask(game.root_capture(label + '-top'))
            # Initial question-review focus is Back to question; ordinary readers
            # have no forced focus. Find the scrolling control through real Tab.
            for attempt in range(5):
                game.key('Tab')
                focused = game.root_capture(label + '-focus-' + str(attempt))
                focus = Image.open(focused).convert('RGB').crop((1107, 205, 1117, 600))
                # The scaled one-pixel border is antialiased in root captures.
                if sum(r > 140 and g > 100 and b > 60 and r - g > 15 and g - b > 20
                       for r, g, b in focus.get_flattened_data()) > 20:
                    break
            else:
                raise RuntimeError('Tab cannot reach visibly focused prose: ' + label)
            game.check(True, 'Tab reaches prose with a visible gold focus border: ' + label)
            game.key('Down')
            game.check(difference(initial, text_mask(game.root_capture(label + '-down'))) > 10,
                       'Down scrolls the focused prose: ' + label)
            game.key('Up')
            game.check(difference(initial, text_mask(game.root_capture(label + '-up'))) == 0,
                       'Up returns the exact opening prose pixels: ' + label)
            game.key('Next')
            game.check(difference(initial, text_mask(game.root_capture(label + '-page-down'))) > 10,
                       'Page Down reads beyond the opening viewport: ' + label)
            game.key('Prior')
            game.check(difference(initial, text_mask(game.root_capture(label + '-page-up'))) == 0,
                       'Page Up returns the opening viewport: ' + label)
            game.key('End')
            end = game.root_capture(label + '-end')
            prose = game.output / (label + '-end-prose.png')
            Image.open(end).crop((324, 211, 1106, 652)).save(prose)
            game.result['images'][prose.name] = digest(prose)
            observed = normalized(' '.join(word['text'] for group in game.words(prose) for word in group))
            ending = pending['history'][-1]['text'] if record else ' '.join(last_fact.split()[-7:])
            game.check(normalized(ending) in observed, 'End shows the exact reached final prose in its own reader: ' + label)
            game.check(difference(initial, text_mask(game.root_capture(label + '-end-observed'))) > 10,
                       'End reaches later prose: ' + label)
            game.key('Home')
            game.check(difference(initial, text_mask(game.root_capture(label + '-home'))) == 0,
                       'Home returns the opening viewport: ' + label)
            game.check(game.slots() == stable, 'keyboard reading preserves every save byte: ' + label)
            # Tab exits the scrollbar to the first ordinary action/Close.
            game.key('Tab'); game.key('Return')
            wait_reader_title(title, False)
            game.check(game.slots() == stable, 'keyboard close/return preserves unanswered slots: ' + label)

        for percent in [100, 150]:
            if percent == 150:
                game.click('Settings'); game.click('Larger reading text'); game.click('Larger reading text')
                game.wait_visible('150'); game.key('Escape')
                game.wait_visible('Reading settings', False)
            game.key('h'); read(f'record-{percent}', 'The record', True)
            game.key('e'); read(f'catalogue-{percent}', 'Your catalogue', False)
            for record in [False, True]:
                game.key('Return'); game.wait_visible('Compare the evidence')
                if record:
                    game.key('Tab')
                game.key('Return')
                read(f'question-{"record" if record else "catalogue"}-{percent}',
                     'The record' if record else 'Your catalogue', record)
                game.wait_visible('Compare the evidence')
                # The original review action must regain focus, never an answer.
                game.key('Return')
                title = 'The record' if record else 'Your catalogue'
                wait_reader_title(title)
                game.key('Escape'); game.wait_visible('Compare the evidence')
                game.key('Escape'); game.wait_visible('Compare the evidence', False)
                game.check(game.slots() == stable, 'repeated review/return remains unanswered at ' + str(percent))
        game.check(game.snapshot() == pending, 'keyboard reading retains exact original pending manual save')
        game.quit()
    print('PASS normal record/catalogue and question review at 100/150%: Tab, arrows, Page Up/Down, Home/End and keyboard return.')


if __name__ == '__main__':
    main()
