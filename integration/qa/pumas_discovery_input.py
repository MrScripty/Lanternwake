"""Normal Godot setup UI with an explicitly controlled Pumas RPC fixture; no HF access."""
import argparse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import threading
import time
import subprocess
from PIL import Image

from normal_player import NormalPlayer, digest, normalized


class SetupPlayer(NormalPlayer):
    def visible(self):
        visible = super().visible()
        # The full-page OCR sometimes drops the first letter of the embedded
        # window title. Read its inspected normal-session region separately.
        title = self.output / 'setup-title-ocr.png'
        Image.open(self.output / 'current.png').crop((296, 153, 1143, 185)).save(title)
        text = subprocess.check_output(['tesseract', str(title), 'stdout', '--psm', '7'],
                                       stderr=subprocess.DEVNULL, text=True, timeout=15)
        return visible + ' ' + normalized(text)

    def words(self, path=None):
        # Keep OCR bounding boxes while allowing repo/quant punctuation to form
        # multiple normalized words within a single OCR token.
        return [[dict(row, text=word) for row in group for word in normalized(row['text']).split()]
                for group in super().words(path)]

    def click(self, caption):
        deadline = time.monotonic() + 2
        while True:
            try:
                super().click(caption)
                time.sleep(.5)
                return
            except RuntimeError as error:
                if 'OCR control' not in str(error) or time.monotonic() >= deadline:
                    raise
                time.sleep(.2)


class Fixture:
    def __init__(self):
        root = Path(__file__).resolve().parents[1] / 'pumas/DiscoveryTests/Fixtures'
        self.responses = {name: json.loads((root / (name + '.json')).read_text()) for name in ['search', 'details', 'started']}
        self.requests = []
        self.mode = 'accepted'
        self.hold_search = False
        self.entered, self.released = threading.Event(), threading.Event()
        fixture = self

        class Handler(BaseHTTPRequestHandler):
            def do_POST(self):
                if self.path != '/rpc':
                    raise RuntimeError('Only controlled /rpc is permitted')
                body = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
                fixture.requests.append(body)
                method = body['method']
                if method == 'search_hf_models':
                    fixture.entered.set()
                    if fixture.hold_search:
                        fixture.released.wait(15)
                    result = fixture.responses['search']
                elif method == 'get_hf_download_details':
                    result = fixture.responses['details']
                elif method == 'start_model_download_from_hf':
                    result = {'success': False, 'error': 'fixture acquisition rejected'} if fixture.mode == 'rejected' else fixture.responses['started']
                else:
                    raise RuntimeError('Unexpected method: ' + method)
                raw = json.dumps({'jsonrpc': '2.0', 'id': body['id'], 'result': result}).encode()
                self.send_response(200)
                self.send_header('Content-Type', 'application/json')
                self.send_header('Content-Length', str(len(raw)))
                self.end_headers()
                try:
                    self.wfile.write(raw)
                except (BrokenPipeError, ConnectionResetError):
                    pass

            def log_message(self, *_):
                pass

        self.server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.url = 'http://127.0.0.1:%d/' % self.server.server_port

    def close(self):
        self.released.set()
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(5)


def reach(game, caption):
    for _ in range(18):
        try:
            NormalPlayer.click(game, caption)
            time.sleep(.5)
            return
        except RuntimeError as error:
            if 'OCR control' not in str(error):
                raise
            game.key('Tab')
    raise RuntimeError('Control did not become visible: ' + caption)


def open_setup(game):
    game.click('Settings')
    reach(game, 'Local conversation setup')
    game.wait_visible('Model name or repository')


def inspect(game):
    game.type_text('Dialogue')
    game.key('Return')
    game.wait_visible('Pumas search results')
    game.click('Inspect Fixture')
    game.wait_visible('Immutable revision')


def request(game, group=False):
    game.click('Review Two GGUF shards' if group else 'Review Q4_K_M')
    game.wait_visible('Review Pumas download request')
    game.root_capture('group-review' if group else 'quant-review')
    game.click('Request download through Pumas')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--percent', choices=[100, 150], type=int, required=True)
    args = parser.parse_args()
    fixture = Fixture()
    game = SetupPlayer(args.display_state, args.fixture, args.output)
    game.env['LANTERNWAKE_PUMAS_URL'] = fixture.url
    game.result['controllerSha256'] = digest(__file__)
    game.result['limits'] = ['Controlled source-derived Pumas RPC fixture; no live Pumas acquisition/inference, HF request, downloaded model/runtime, physical input or human-duration acceptance.']
    try:
        with game:
            game.click('Arrive on the island')
            game.key('Return')
            game.click('Save')
            before = game.slots()
            if args.percent == 150:
                for _ in range(2):
                    game.click('Settings'); game.click('Larger reading text'); game.key('Escape')
            open_setup(game)
            game.check(not fixture.requests, 'opening setup never contacts Pumas')
            game.root_capture('setup-' + str(args.percent))
            inspect(game)
            game.check([r['method'] for r in fixture.requests] == ['search_hf_models', 'get_hf_download_details'], 'search and inspection send exactly the two metadata RPCs')
            game.root_capture('options-' + str(args.percent))
            game.click('Review Two GGUF shards' if args.percent == 150 else 'Review Q4_K_M')
            game.wait_visible('Review Pumas download request')
            game.root_capture('review-' + str(args.percent))
            game.check(len(fixture.requests) == 2, 'reviewing selection does not request acquisition')
            game.click('Request download through Pumas')
            game.wait_visible('Pumas accepted the request')
            game.wait_visible('fixture-download-001')
            game.root_capture('accepted-' + str(args.percent))
            game.check(len(fixture.requests) == 3, 'explicit button sends exactly one acquisition request')
            parameters = fixture.requests[-1]['params']
            game.check(parameters['repo_id'] == 'Fixture/Dialogue-GGUF' and parameters['family'] == 'Fixture' and
                       parameters['official_name'] == 'Dialogue-GGUF' and 'revision' not in parameters, 'Pumas identifiers preserved without invented immutable revision')
            game.check(parameters['filenames'] == (['model-1.gguf', 'model-2.gguf'] if args.percent == 150 else None) and
                       parameters['quant'] == (None if args.percent == 150 else 'Q4_K_M'), 'selected quant or exact grouped filenames forwarded')
            game.key('Escape')
            game.check(game.slots() == before, 'setup acceptance changes no player save bytes')

            fixture.mode = 'rejected'
            open_setup(game); inspect(game); request(game)
            game.wait_visible('Pumas setup unavailable')
            game.wait_visible('fixture acquisition rejected')
            game.root_capture('rejected-' + str(args.percent))
            game.check(len(fixture.requests) == 6, 'rejected acquisition is reported once without replay')
            game.key('Escape')

            fixture.hold_search = True
            fixture.entered.clear()
            open_setup(game)
            game.type_text('Dialogue'); game.key('Return')
            game.check(fixture.entered.wait(3), 'controlled pending lookup observed')
            game.key('Escape')
            fixture.released.set()
            time.sleep(.8)
            game.check('pumas search results' not in game.visible(), 'late lookup cannot reopen a dismissed setup window')
            game.click('Save')
            game.check(game.slots() == before, 'accepted, rejected and cancelled setup paths leave story, transcript and save slots unchanged')
            game.check(len(fixture.requests) == 7, 'cancelled lookup does not replay or acquire')
            game.quit()
    finally:
        (args.output / 'rpc-requests.json').write_text(json.dumps(fixture.requests, indent=2) + '\n')
        fixture.close()
    print('PASS normal Godot Pumas setup at', args.percent, 'percent,', len(game.result['checks']), 'checks')


if __name__ == '__main__':
    main()
