"""Synthetic events confined to the runner's authenticated X11 display; no hardware claim."""
import ctypes
import json
import os
from pathlib import Path
import sys
import time

fixture = Path(os.environ.get('LANTERNWAKE_GRAPHICAL_FIXTURE', ''))
if not fixture.is_absolute() or not (fixture / 'owned-fixture').is_file():
    raise SystemExit('Owned graphical fixture required.')
contract = json.loads((fixture / 'display-contract.json').read_text())
if os.environ.get('DISPLAY') != contract['display'] or os.environ.get('XAUTHORITY') != contract['authority'] or not contract['authenticated'] or contract['tcp'] != 'disabled':
    raise SystemExit('Expected the explicitly owned authenticated display.')
x11 = ctypes.CDLL('libX11.so.6')
xtest = ctypes.CDLL('libXtst.so.6')
x11.XOpenDisplay.argtypes = [ctypes.c_char_p]
x11.XOpenDisplay.restype = ctypes.c_void_p
x11.XCloseDisplay.argtypes = [ctypes.c_void_p]
x11.XSync.argtypes = [ctypes.c_void_p, ctypes.c_int]
xtest.XTestFakeMotionEvent.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_ulong]
xtest.XTestFakeButtonEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_int, ctypes.c_ulong]
xtest.XTestFakeKeyEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_int, ctypes.c_ulong]
x11.XStringToKeysym.argtypes = [ctypes.c_char_p]
x11.XStringToKeysym.restype = ctypes.c_ulong
x11.XKeysymToKeycode.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
x11.XKeysymToKeycode.restype = ctypes.c_uint
display = x11.XOpenDisplay(None)
if not display:
    raise SystemExit('Owned authenticated display unavailable.')
try:
    if len(sys.argv) == 4 and sys.argv[1] == 'click':
        x, y = round(float(sys.argv[2])), round(float(sys.argv[3]))
        width, height = map(int, contract['screen'].split('x'))
        if not (0 <= x < width and 0 <= y < height):
            raise SystemExit('Input lies outside the owned screen.')
        assert xtest.XTestFakeMotionEvent(display, -1, x, y, 0)
        x11.XSync(display, 0)
        assert xtest.XTestFakeButtonEvent(display, 1, 1, 0)
        x11.XSync(display, 0)
        time.sleep(0.04)
        assert xtest.XTestFakeButtonEvent(display, 1, 0, 0)
    elif len(sys.argv) == 3 and sys.argv[1] == 'key':
        if sys.argv[2] not in ['Tab', 'Return', 'Escape', 'space', 'Down', 'Up', 'Right', 'Left', 'h', 'e']:
            raise SystemExit('Key is outside the verification set.')
        code = x11.XKeysymToKeycode(display, x11.XStringToKeysym(sys.argv[2].encode()))
        assert code and xtest.XTestFakeKeyEvent(display, code, 1, 0)
        x11.XSync(display, 0)
        time.sleep(0.04)
        assert xtest.XTestFakeKeyEvent(display, code, 0, 0)
    else:
        raise SystemExit('Expected click X Y or key NAME.')
    x11.XSync(display, 0)
finally:
    x11.XCloseDisplay(display)
