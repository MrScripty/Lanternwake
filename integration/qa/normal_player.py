"""Owned normal Godot sessions; external X11 input and observed files/pixels only."""
import csv
import ctypes
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time

from PIL import Image


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def normalized(text):
    return re.sub(r'[^a-z0-9]+', ' ', re.sub("['’]", '', text.lower())).strip()


class NormalPlayer:
    def __init__(self, display_state, fixture, output, seed=None):
        self.repo = Path(__file__).resolve().parents[2]
        self.fixture, self.output = Path(fixture).resolve(), Path(output).resolve()
        self.fixture.mkdir(parents=True, exist_ok=False)
        self.output.mkdir(parents=True, exist_ok=False)
        (self.fixture / 'owned-fixture').touch()
        contract = json.loads(Path(display_state).read_text())
        authority = Path(contract['authority'])
        if (not contract['authenticated'] or contract['tcp'] != 'disabled'
                or authority.stat().st_uid != os.getuid() or authority.stat().st_mode & 0o077):
            raise RuntimeError('Owned private authenticated display required.')
        os.kill(contract['pid'], 0)
        self.env = os.environ.copy()
        self.env.update(DISPLAY=contract['display'], XAUTHORITY=str(authority),
                        LIBGL_ALWAYS_SOFTWARE='1', LANTERNWAKE_PUMAS_MODEL='')
        os.environ['XAUTHORITY'] = str(authority)
        for variable, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
            self.env[variable] = str(self.fixture / child)
        self.saves = self.fixture / 'data/godot/app_userdata/Lanternwake'
        self.story = json.loads((self.repo / 'Content/story.json').read_text())
        self.beats = [beat for chapter in self.story['chapters'] for scene in chapter['scenes'] for beat in scene['beats']]
        self.capture_backups = {}
        self.process = None
        tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=self.repo).decode().split('\0')
        self.before = {name: digest(self.repo / name) for name in tracked if name}
        self.command = [os.environ['GODOT_MONO'], '--path', str(self.repo), '--audio-driver', 'Dummy', '--disable-vsync', '--max-fps', '30']
        self.result = {'baseCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=self.repo, text=True).strip(),
                       'baseTree': subprocess.check_output(['git', 'rev-parse', 'HEAD^{tree}'], cwd=self.repo, text=True).strip(),
                       'sourceSha256': self.before, 'controllerLibrarySha256': digest(__file__),
                       'nativeDllSha256': digest(self.repo / '.godot/mono/temp/bin/Debug/Lanternwake.dll'),
                       'command': self.command, 'display': contract['display'], 'checks': [], 'events': [], 'images': {},
                       'passed': False, 'limits': ['Synthetic X11 input and software renderer; no physical device/hearing or human-duration acceptance.',
                                                  'Empty model selects authored fallback; no real-model or microphone claim.']}
        if seed:
            seed = Path(seed)
            data = json.loads(seed.read_text())
            if data['beatId'] not in {beat['id'] for beat in self.beats}:
                raise RuntimeError('Seed references unknown beat.')
            self.saves.mkdir(parents=True)
            shutil.copy2(seed, self.saves / 'save.json')
            self.result['seed'] = {'path': str(seed), 'sha256': digest(seed), 'beatId': data['beatId']}
        self.x11, self.xtest = ctypes.CDLL('libX11.so.6'), ctypes.CDLL('libXtst.so.6')
        self.x11.XOpenDisplay.argtypes, self.x11.XOpenDisplay.restype = [ctypes.c_char_p], ctypes.c_void_p
        self.x11.XCloseDisplay.argtypes = [ctypes.c_void_p]
        self.x11.XSync.argtypes = [ctypes.c_void_p, ctypes.c_int]
        self.x11.XSetInputFocus.argtypes = [ctypes.c_void_p, ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
        self.x11.XStringToKeysym.argtypes, self.x11.XStringToKeysym.restype = [ctypes.c_char_p], ctypes.c_ulong
        self.x11.XKeysymToKeycode.argtypes, self.x11.XKeysymToKeycode.restype = [ctypes.c_void_p, ctypes.c_ulong], ctypes.c_uint
        self.xtest.XTestFakeKeyEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_int, ctypes.c_ulong]
        self.xtest.XTestFakeMotionEvent.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_ulong]
        self.xtest.XTestFakeButtonEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_int, ctypes.c_ulong]
        self.display = self.x11.XOpenDisplay(contract['display'].encode())
        if not self.display:
            raise RuntimeError('Owned display unavailable.')
        self.hud = {'Continue': (1250, 790), 'Examine evidence': (1215, 790), 'Save': (1148, 101),
                    'Load': (1210, 101), 'Settings': (1285, 101), 'Stay and talk': (1137, 790),
                    'Say this': (425, 525), 'Return to story': (534, 525)}

    def check(self, value, claim):
        if not value:
            raise RuntimeError(claim)
        self.result['checks'].append(claim)

    def sync(self):
        self.x11.XSync(self.display, 0)

    def root_capture(self, name):
        path = self.output / (name + '.png')
        subprocess.run(['import', '-window', 'root', str(path)], env=self.env, check=True, timeout=10)
        self.result['images'][path.name] = digest(path)
        return path

    def words(self, path=None):
        path = path or self.root_capture('current')
        text = subprocess.check_output(['tesseract', str(path), 'stdout', 'tsv'], stderr=subprocess.DEVNULL, text=True, timeout=15)
        groups = {}
        for row in csv.DictReader(io.StringIO(text), delimiter='\t'):
            if row['text'].strip() and float(row['conf']) >= 20:
                groups.setdefault((row['block_num'], row['par_num'], row['line_num']), []).append(row)
        return list(groups.values())

    def visible(self):
        return normalized(' '.join(word['text'] for group in self.words() for word in group))

    def wait_visible(self, caption, present=True):
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            visible = self.visible()
            if caption in ['Compare the evidence', 'The record', 'Your catalogue', 'Reading settings']:
                # Full-page layout segmentation sometimes omits the legible
                # embedded-window title. Inspect its authored, observed region.
                title = self.output / 'title-ocr.png'
                Image.open(self.output / 'current.png').crop((296, 153, 1143, 185)).save(title)
                text = subprocess.check_output(['tesseract', str(title), 'stdout', '--psm', '7'],
                                               stderr=subprocess.DEVNULL, text=True, timeout=15)
                visible += ' ' + normalized(text)
            if (normalized(caption) in visible) == present:
                self.check(True, ('visible: ' if present else 'closed: ') + caption)
                return
            time.sleep(.1)
        self.check(False, 'Visible state did not settle: ' + caption)

    def click_at(self, x, y, label):
        self.result['events'].append({'mouse': label, 'x': x, 'y': y})
        assert self.xtest.XTestFakeMotionEvent(self.display, -1, x, y, 0)
        self.sync(); time.sleep(.04)
        assert self.xtest.XTestFakeButtonEvent(self.display, 1, 1, 0)
        self.sync(); time.sleep(.04)
        assert self.xtest.XTestFakeButtonEvent(self.display, 1, 0, 0)
        self.sync(); time.sleep(.25)

    def click(self, caption):
        wanted = normalized(caption).split()
        matches = []
        for group in self.words():
            words = [normalized(row['text']) for row in group]
            for start in range(len(words) - len(wanted) + 1):
                if words[start:start + len(wanted)] != wanted:
                    continue
                rows = group[start:start + len(wanted)]
                left = min(int(row['left']) for row in rows)
                right = max(int(row['left']) + int(row['width']) for row in rows)
                top = min(int(row['top']) for row in rows)
                bottom = max(int(row['top']) + int(row['height']) for row in rows)
                matches.append(((left + right) // 2, (top + bottom) // 2))
        if caption in self.hud:
            x, y = self.hud[caption]
            matches = [(a, b) for a, b in matches if abs(a - x) <= 90 and abs(b - y) <= 30]
            if not matches:
                matches = [(x, y)]
                self.result['events'].append({'inspectedCoordinateFallback': caption})
        if len(matches) != 1:
            raise RuntimeError(f'Expected one OCR control {caption!r}; found {matches}. Inspect current.png.')
        self.click_at(*matches[0], caption)

    def key(self, name, pressed=None):
        self.result['events'].append({'key': name, 'edge': pressed})
        code = self.x11.XKeysymToKeycode(self.display, self.x11.XStringToKeysym(name.encode()))
        if not code:
            raise RuntimeError('Unavailable key: ' + name)
        assert self.xtest.XTestFakeKeyEvent(self.display, code, 1 if pressed is None else int(pressed), 0)
        self.sync()
        if pressed is None:
            time.sleep(.04)
            assert self.xtest.XTestFakeKeyEvent(self.display, code, 0, 0)
            self.sync(); time.sleep(.15)

    def type_text(self, text):
        self.key('Control_L', True); self.key('a'); self.key('Control_L', False); self.key('BackSpace')
        for char in text:
            self.key({' ': 'space', '.': 'period'}.get(char, char))

    def slots(self):
        return {path.name: path.read_bytes() for path in self.saves.glob('*.json')}

    def snapshot(self, name='save.json'):
        return json.loads((self.saves / name).read_text())

    def native_capture(self, label):
        current = self.snapshot('autosave.json') if (self.saves / 'autosave.json').exists() else self.snapshot()
        scene = next(scene for chapter in self.story['chapters'] for scene in chapter['scenes']
                     if any(beat['id'] == current['beatId'] for beat in scene['beats']))
        source = self.repo / 'artifacts/captures' / (scene['id'] + '.png')
        if source not in self.capture_backups:
            self.capture_backups[source] = source.read_bytes() if source.exists() else None
        stamp = source.stat().st_mtime_ns if source.exists() else 0
        self.key('F12')
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            if source.exists() and source.stat().st_mtime_ns > stamp:
                path = self.output / (label + '-native.png')
                shutil.copy2(source, path)
                self.result['images'][path.name] = digest(path)
                return path
            time.sleep(.05)
        raise RuntimeError('Native Debug F12 capture did not arrive.')

    def __enter__(self):
        try:
            with (self.output / 'native.log').open('wb') as log:
                self.process = subprocess.Popen(self.command, env=self.env, cwd=self.repo, stdout=log, stderr=subprocess.STDOUT)
            self.result['pid'] = self.process.pid
            deadline = time.monotonic() + 45
            while time.monotonic() < deadline and self.process.poll() is None:
                if 'arrive on the island' in self.visible():
                    break
                time.sleep(.25)
            else:
                raise RuntimeError('Normal title not visibly ready within 45 seconds.')
            windows = subprocess.check_output(['xwininfo', '-root', '-tree'], env=self.env, text=True)
            owned = [line for line in windows.splitlines() if '("Godot_Engine" "Lanternwake")' in line]
            self.check(len(owned) == 1 and '1280x800+80+50' in owned[0], 'sole normal client has inspected geometry')
            self.x11.XSetInputFocus(self.display, int(owned[0].split()[0], 16), 2, 0); self.sync()
            self.root_capture('title')
            return self
        except BaseException as error:
            self.__exit__(type(error), error, error.__traceback__)
            raise

    def quit(self):
        self.click('Settings')
        for attempt in range(12):
            try:
                self.click('Quit game')
                break
            except RuntimeError as error:
                if 'OCR control' not in str(error):
                    raise
                self.key('Tab')
        else:
            raise RuntimeError('Quit did not become visible through Tab navigation.')
        code = self.process.wait(timeout=20)
        self.result['exitCode'] = code
        self.check(code == 0, 'production Settings Quit exits 0')
        log = (self.output / 'native.log').read_text()
        unexpected = [line for line in log.splitlines() if line.startswith('WARNING:') and 'Could not set V-Sync mode' not in line]
        self.check('ERROR:' not in log and not unexpected, 'native log has no unexpected error/warning')

    def __exit__(self, kind, error, traceback):
        if error:
            self.result['failure'] = str(error)
        if self.process is not None and self.process.poll() is None:
            self.process.kill(); self.process.wait(timeout=10)
        for path, original in self.capture_backups.items():
            if original is None:
                path.unlink(missing_ok=True)
            else:
                path.write_bytes(original)
        unchanged = all(digest(self.repo / name) == sha for name, sha in self.before.items())
        self.result['sourceUnchanged'] = unchanged
        self.result['savedSlotsSha256'] = {name: hashlib.sha256(data).hexdigest() for name, data in self.slots().items()}
        self.result['passed'] = error is None and unchanged and self.result.get('exitCode') == 0
        self.x11.XCloseDisplay(self.display)
        (self.output / 'receipt.json').write_text(json.dumps(self.result, indent=2) + '\n')
        if error is None and not self.result['passed']:
            raise RuntimeError('Session did not finish with clean Quit and preserved source.')
