#!/usr/bin/env python3
"""Normal project sessions driven only by owned-display X11 input and screen OCR.

No preview, smoke entry, alternate scene, reflection, injected script or accelerated
engine clock is used. The controller observes screenshots and committed save files.
It deliberately kills one owned game after completed saves, then tests restart,
draft cancellation and explicit recovery from a deliberately damaged owned slot.
"""
import argparse
import csv
import ctypes
import hashlib
import io
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--display-state', required=True, type=Path)
    parser.add_argument('--fixture', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--arrival-only', action='store_true', help='Focused first-passage keyboard regression only.')
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[2]
    fixture, output = args.fixture.resolve(), args.output.resolve()
    fixture.mkdir(parents=True, exist_ok=False)
    output.mkdir(parents=True, exist_ok=False)
    (fixture / 'owned-fixture').touch()
    contract = json.loads(args.display_state.read_text())
    authority = Path(contract['authority'])
    if not contract['authenticated'] or contract['tcp'] != 'disabled' or authority.stat().st_uid != os.getuid() or authority.stat().st_mode & 0o077:
        raise RuntimeError('Private owned authenticated display required.')
    os.kill(contract['pid'], 0)
    environment = os.environ.copy()
    environment.update(DISPLAY=contract['display'], XAUTHORITY=str(authority), LIBGL_ALWAYS_SOFTWARE='1', LANTERNWAKE_PUMAS_MODEL='')
    os.environ['XAUTHORITY'] = str(authority)
    for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
        environment[key] = str(fixture / child)
    saves = fixture / 'data/godot/app_userdata/Lanternwake'
    engine = os.environ['GODOT_MONO']
    command = [engine, '--path', str(repository), '--audio-driver', 'Dummy', '--disable-vsync', '--max-fps', '30']
    story = json.loads((repository / 'Content/story.json').read_text())
    beats = [b for c in story['chapters'] for s in c['scenes'] for b in s['beats']]
    conversation = next(i for i, b in enumerate(beats) if b.get('conversation'))
    before = {p: hashlib.sha256((repository / p).read_bytes()).hexdigest() for p in subprocess.check_output(['git', 'ls-files', '-z'], cwd=repository).decode().split('\0') if p}
    result = {'baseCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repository, text=True).strip(),
              'baseTree': subprocess.check_output(['git', 'rev-parse', 'HEAD^{tree}'], cwd=repository, text=True).strip(),
              'workingSourceSha256': before,
              'controllerSha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              'command': command, 'checks': [], 'processes': [], 'images': {}, 'mode': 'normal player; real X11 synthetic input',
              'limits': ['Software-rendered screen inspection and synthetic X11 input; no physical device acceptance.',
                         'SIGKILL occurs after completed saves, not during an atomic-write transaction.',
                         'Empty model uses authored fallback; no real in-flight inference/cancellation, microphone, hearing or human duration claim.']}
    process = None
    x11, xtest = ctypes.CDLL('libX11.so.6'), ctypes.CDLL('libXtst.so.6')
    x11.XOpenDisplay.argtypes, x11.XOpenDisplay.restype = [ctypes.c_char_p], ctypes.c_void_p
    x11.XCloseDisplay.argtypes = [ctypes.c_void_p]
    x11.XSync.argtypes = [ctypes.c_void_p, ctypes.c_int]
    x11.XSetInputFocus.argtypes = [ctypes.c_void_p, ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
    x11.XStringToKeysym.argtypes, x11.XStringToKeysym.restype = [ctypes.c_char_p], ctypes.c_ulong
    x11.XKeysymToKeycode.argtypes, x11.XKeysymToKeycode.restype = [ctypes.c_void_p, ctypes.c_ulong], ctypes.c_uint
    xtest.XTestFakeKeyEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_int, ctypes.c_ulong]
    xtest.XTestFakeMotionEvent.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_ulong]
    xtest.XTestFakeButtonEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_int, ctypes.c_ulong]
    display = x11.XOpenDisplay(contract['display'].encode())
    if not display:
        raise RuntimeError('Owned display unavailable.')
    hud_positions = {}
    # Actual inspected root-screen HUD coordinates for the authored 1280x800
    # client at +80+50 on this 1440x900 display. Aliased glyphs occasionally make
    # OCR miss a static caption; save/beat checks still validate each action.
    fixed_hud = {'Arrive on the island': (1212, 790), 'Continue': (1250, 790),
                 'Save': (1148, 101), 'Load': (1210, 101), 'Settings': (1285, 101),
                 'Stay and talk': (1108, 790),
                 beats[conversation]['conversation']['suggestions'][0]: (720, 280),
                 'Say this': (425, 525), 'Return to story': (534, 525)}
    result['inputEvents'] = []

    def check(value, claim):
        if not value:
            raise RuntimeError(claim)
        result['checks'].append(claim)

    def screen(name=None):
        path = output / ((name or 'current') + '.png')
        subprocess.run(['import', '-window', 'root', str(path)], env=environment, check=True, timeout=10)
        if name:
            result['images'][path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
        text = subprocess.check_output(['tesseract', str(path), 'stdout', 'tsv'], stderr=subprocess.DEVNULL, text=True, timeout=15)
        groups = {}
        for row in csv.DictReader(io.StringIO(text), delimiter='\t'):
            if row['text'].strip() and float(row['conf']) >= 20:
                groups.setdefault((row['block_num'], row['par_num'], row['line_num']), []).append(row)
        return list(groups.values())

    def normalized(text):
        return re.sub(r'[^a-z0-9]+', ' ', re.sub("['’]", '', text.lower())).strip()

    def visible_text(name=None):
        return normalized(' '.join(row['text'] for group in screen(name) for row in group))

    def wait_visible(caption, present, claim, name=None):
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            if (caption in visible_text(name)) == present:
                check(True, claim)
                return
            time.sleep(.1)
        check(False, claim)

    def click_text(text):
        wanted = normalized(text).split()
        matches = []
        for group in screen():
            words = [normalized(row['text']) for row in group]
            for start in range(len(words) - len(wanted) + 1):
                if words[start:start + len(wanted)] == wanted:
                    rows = group[start:start + len(wanted)]
                    left = min(int(r['left']) for r in rows); right = max(int(r['left']) + int(r['width']) for r in rows)
                    top = min(int(r['top']) for r in rows); bottom = max(int(r['top']) + int(r['height']) for r in rows)
                    matches.append(((left + right) // 2, (top + bottom) // 2))
        if text in fixed_hud:
            # The status hint also contains "continue" after returning from
            # conversation. Only the inspected control region is actionable.
            expected_x, expected_y = fixed_hud[text]
            matches = [(x, y) for x, y in matches if abs(x - expected_x) <= 90 and abs(y - expected_y) <= 30]
            if not matches:
                matches = [hud_positions.get(text, fixed_hud[text])]
        if len(matches) != 1:
            raise RuntimeError(f'Expected one visible OCR control {text!r}, found {matches}; inspect {output / "current.png"}')
        if text in ['Save', 'Load', 'Settings']:
            hud_positions[text] = matches[0]
        x, y = matches[0]
        result['inputEvents'].append({'mouse': text, 'x': x, 'y': y})
        assert xtest.XTestFakeMotionEvent(display, -1, x, y, 0)
        x11.XSync(display, 0); time.sleep(.04)
        assert xtest.XTestFakeButtonEvent(display, 1, 1, 0)
        x11.XSync(display, 0); time.sleep(.04)
        assert xtest.XTestFakeButtonEvent(display, 1, 0, 0)
        x11.XSync(display, 0); time.sleep(.25)

    def key(name, pressed=None):
        result['inputEvents'].append({'key': name, 'edge': pressed})
        code = x11.XKeysymToKeycode(display, x11.XStringToKeysym(name.encode()))
        if not code:
            raise RuntimeError('Key unavailable: ' + name)
        assert xtest.XTestFakeKeyEvent(display, code, 1 if pressed is None else int(pressed), 0)
        x11.XSync(display, 0)
        if pressed is None:
            time.sleep(.04)
            assert xtest.XTestFakeKeyEvent(display, code, 0, 0)
            x11.XSync(display, 0); time.sleep(.15)

    def type_text(text):
        key('Control_L', True); key('a'); key('Control_L', False); key('BackSpace')
        for char in text:
            key({' ': 'space', '.': 'period'}.get(char, char))

    def files():
        return {p.name: p.read_bytes() for p in saves.glob('*.json')}

    def snapshot(name='save.json'):
        return json.loads((saves / name).read_text())

    def save_at(index):
        click_text('Save')
        data = snapshot()
        check(data['beatId'] == beats[index]['id'], 'normal Save records exact beat ' + beats[index]['id'])
        # Optional dialogue retains its originating beatId too. Its explicit
        # conversation scope distinguishes it from canonical reached prose.
        authored = [h for h in data['history'] if not h.get('conversationCharacterId')]
        check([h['beatId'] for h in authored] == [b['id'] for b in beats[:index + 1]], 'canonical reached history remains ordered without duplicates')
        check([h['text'] for h in authored] == [b['text'] for b in beats[:index + 1]], 'saved reached prose matches exact authored text')
        return data

    def move_to(index):
        # A mouse click may first reveal the current passage. Observe committed
        # autosave identity rather than assuming a fixed number of key presses.
        click_text('Continue')
        if not (saves / 'autosave.json').exists() or snapshot('autosave.json')['beatId'] != beats[index]['id']:
            click_text('Continue')
        check(snapshot('autosave.json')['beatId'] == beats[index]['id'], 'visible Continue reaches next canonical beat without skipping')

    def start(label):
        nonlocal process
        with (output / (label + '.log')).open('wb') as log:
            process = subprocess.Popen(command, env=environment, cwd=repository, stdout=log, stderr=subprocess.STDOUT)
        result['processes'].append({'name': label, 'pid': process.pid})
        deadline = time.monotonic() + 45
        while time.monotonic() < deadline and process.poll() is None:
            visible = ' '.join(row['text'] for group in screen() for row in group)
            if 'arrive on the island' in normalized(visible):
                break
            time.sleep(.25)
        else:
            raise RuntimeError(label + ' title did not become visibly ready within 45 seconds')
        check(process.poll() is None, label + ' normal project remains running')
        windows = subprocess.check_output(['xwininfo', '-root', '-tree'], env=environment, text=True)
        owned = [line.split()[0] for line in windows.splitlines() if '("Godot_Engine" "Lanternwake")' in line]
        check(len(owned) == 1, 'owned display contains exactly this normal game window')
        check('1280x800+80+50' in next(line for line in windows.splitlines() if '("Godot_Engine" "Lanternwake")' in line), 'inspected fixed HUD geometry matches owned window')
        # There is no window manager on the dummy display. Focus only the window
        # from this sole owned client, equivalent to selecting its visible window.
        x11.XSetInputFocus(display, int(owned[0], 16), 2, 0); x11.XSync(display, 0)
        screen(label + '-title')

    def stop(label, interrupted=False):
        if interrupted:
            os.kill(process.pid, signal.SIGKILL)
        else:
            click_text('Settings')
            for attempt in range(12):
                try:
                    click_text('Quit game')
                    break
                except RuntimeError as error:
                    if 'visible OCR control' not in str(error): raise
                    key('Tab')
            else:
                raise RuntimeError('Native Settings Quit did not become visible through Tab navigation')
        code = process.wait(timeout=20)
        check(code == (-signal.SIGKILL if interrupted else 0), label + ' expected owned interruption or production Quit status')
        text = (output / (label + '.log')).read_text()
        unexpected = [line for line in text.splitlines() if line.startswith('WARNING:') and 'Could not set V-Sync mode' not in line]
        check('ERROR:' not in text and not unexpected, label + ' no unexpected native errors/warnings')
        result['processes'][-1].update(exitCode=code, intentionalInterruption=interrupted)

    try:
        start('first')
        click_text('Arrive on the island')
        visible = ' '.join(row['text'] for group in screen('arrival-typewriter') for row in group)
        check('the ferry' in normalized(visible), 'actual Arrival click starts the visible authored typewriter passage')
        key('space'); screen('arrival-revealed'); save_at(0)
        if args.arrival_only:
            stop('first')
            result['passed'] = True
            print('PASS focused X11 Space reveals the first normal passage without advancing.', flush=True)
            return
        # Closing a real modal restores focus to Continue. Enter must advance once.
        key('h')
        wait_visible('the record', True, 'native History visibly opens from the focused advance button')
        key('Escape')
        wait_visible('the record', False, 'native History visibly closes before focused Enter')
        key('Return'); save_at(1)
        check(snapshot('autosave.json')['beatId'] == beats[1]['id'], 'focused X11 Enter advances exactly one beat')
        for index in range(2, conversation + 1):
            move_to(index); save_at(index)
        stable = files()
        screen('normal-conversation-beat')
        click_text('Stay and talk')
        click_text(beats[conversation]['conversation']['suggestions'][0])
        # Production suggestion selection focuses the real editable LineEdit.
        typed = 'i want to catalogue this carefully.'
        type_text(typed); screen('edited-normal-input')
        click_text('Say this'); time.sleep(.5); screen('normal-authored-fallback')
        click_text('Return to story'); saved = save_at(conversation)
        check(any(h['text'] == typed for h in saved['history']), 'actually typed editable text survives in normal saved history')
        check(any(h['text'] == beats[conversation]['conversation']['fallback'] and not h['generated'] for h in saved['history']), 'normal saved history labels exact authored fallback without generated claim')
        screen('returned-normal-passage')
        stable = files(); click_text('Stay and talk')
        draft = 'this unsent draft must not become history.'
        type_text(draft); screen('unsent-draft-before-interruption')
        check(files() == stable, 'active unsent draft does not mutate normal save slots')
        stop('first', interrupted=True)
        check(files() == stable, 'committed manual/autosave/previous bytes survive owned SIGKILL')
        start('restart')
        click_text('Load'); screen('normal-restart-load-choices')
        click_text('Load current manual save'); time.sleep(len(beats[conversation]['text']) / 55 + .1)
        check(files() == stable, 'fresh-process explicit Load leaves every slot byte unchanged')
        screen('normal-restarted-passage')
        for attempt in range(4):
            for shortcut, caption in [('h', 'the record'), ('e', 'your catalogue')]:
                key(shortcut)
                wait_visible(caption, True, 'repeated native keyboard modal visibly opens: ' + caption)
                key('Escape')
                wait_visible(caption, False, 'repeated native keyboard modal visibly closes: ' + caption)
        check(files() == stable, 'eight repeated native keyboard modal open/close cycles preserve saves')
        click_text('Stay and talk')
        wait_visible('a moment between the lines', True, 'native conversation visibly opens after restart')
        key('Escape')
        wait_visible('a moment between the lines', False, 'one focused X11 Escape visibly closes the conversation panel', 'escape-returned-normal-passage')
        check(files() == stable, 'reopened conversation and Escape cancel without persisted draft')
        move_to(conversation + 1); save_at(conversation + 1)
        screen('normal-next-beat-before-recovery')
        previous = (saves / 'save.previous.json').read_bytes()
        check(json.loads(previous)['beatId'] == beats[conversation]['id'], 'previous good manual is exact earlier conversation snapshot')
        stop('restart')
        # Synthetic corruption is limited to this runner's owned current manual slot.
        (saves / 'save.json').write_text('{ deliberately damaged owned qualification slot')
        damaged = files()
        start('recovery')
        click_text('Load'); screen('damaged-current-visible-recovery')
        click_text('Recover previous manual save'); time.sleep(len(beats[conversation]['text']) / 55 + .1)
        check(files() == damaged, 'explicit previous-good recovery does not rewrite damaged current or other slot bytes')
        screen('visibly-recovered-normal-passage')
        saved = save_at(conversation)
        check((saves / 'save.previous.json').read_bytes() == previous, 'saving after recovery retains last compatible previous bytes')
        check(not any(h['text'] == draft for h in saved['history']), 'interrupted unsent draft is absent after restart and explicit recovery')
        stop('recovery')
        check(before == {p: hashlib.sha256((repository / p).read_bytes()).hexdigest() for p in before}, 'all tracked source bytes preserved')
        result['passed'] = True
        print('PASS normal visible X11 input, editable fallback, committed-save interruption, fresh restart and explicit damaged-primary recovery.', flush=True)
    except Exception as error:
        result['failure'] = str(error)
        raise
    finally:
        if process is not None and process.poll() is None:
            process.kill(); process.wait(timeout=10)
        x11.XCloseDisplay(display)
        result['saveSha256'] = {name: hashlib.sha256(data).hexdigest() for name, data in files().items()}
        (output / 'receipt.json').write_text(json.dumps(result, indent=2) + '\n')


if __name__ == '__main__':
    main()
