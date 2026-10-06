"""Observe production setup lifecycle UI against controlled Pumas RPC snapshots only."""
import argparse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import threading
import time

from normal_player import digest, normalized
from pumas_discovery_input import SetupPlayer, open_setup, reach, inspect, request

ACTIVE = 'fixture-download-001'
QUEUED = 'fixture-download-002'
FAILED = 'fixture-download-003'


class ActivityFixture:
    def __init__(self):
        root = Path(__file__).resolve().parents[1] / 'pumas/DiscoveryTests/Fixtures'
        self.responses = {name: json.loads((root / (name + '.json')).read_text()) for name in ['search', 'details', 'started', 'progress']}
        template = dict(self.responses['progress'])
        template.pop('success')
        queued = dict(template, downloadId=QUEUED, repoId='Fixture/Queued-GGUF', modelName='Queued-GGUF',
                      status='queued', progress=None, downloadedBytes=None, totalBytes=None, libraryModelId=None, selectedArtifactId=None)
        failed = dict(template, downloadId=FAILED, repoId='Fixture/Failed-GGUF', modelName='Failed-GGUF',
                      status='error', error='The model download did not complete successfully.')
        self.active_template = template
        self.downloads = {QUEUED: queued, FAILED: failed}
        self.requests = []
        self.hold_method = None
        self.missing_id = None
        self.cancel_mode = 'accepted'
        self.entered, self.released = threading.Event(), threading.Event()
        fixture = self

        class Handler(BaseHTTPRequestHandler):
            def do_POST(self):
                if self.path != '/rpc':
                    raise RuntimeError('Fixture only permits /rpc')
                body = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
                fixture.requests.append(body)
                method, parameters = body['method'], body['params']
                if method == 'search_hf_models':
                    result = fixture.responses['search']
                elif method == 'get_hf_download_details':
                    result = fixture.responses['details']
                elif method == 'start_model_download_from_hf':
                    fixture.downloads[ACTIVE] = dict(fixture.active_template)
                    result = fixture.responses['started']
                elif method == 'list_model_downloads':
                    result = {'success': True, 'downloads': list(fixture.downloads.values())}
                elif method == 'get_model_download_status':
                    selected = parameters['download_id']
                    result = {'success': False, 'error': 'Download not found'} if selected == fixture.missing_id or selected not in fixture.downloads else dict(fixture.downloads[selected], success=True)
                elif method == 'cancel_model_download':
                    selected = parameters['download_id']
                    if fixture.cancel_mode == 'rejected':
                        result = {'success': False, 'error': 'A required operation is currently unavailable.'}
                    else:
                        fixture.downloads[selected]['status'] = 'cancelling'
                        result = {'success': True}
                else:
                    raise RuntimeError('Unexpected fixture method: ' + method)
                # Freeze this reply before holding it, just as a late server snapshot.
                raw = json.dumps({'jsonrpc': '2.0', 'id': body['id'], 'result': result}).encode()
                if method == fixture.hold_method:
                    fixture.entered.set()
                    fixture.released.wait(30)
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

    def methods(self, name):
        return [body for body in self.requests if body['method'] == name]

    def hold(self, method):
        self.entered.clear(); self.released.clear(); self.hold_method = method

    def release(self):
        self.hold_method = None; self.released.set()

    def close(self):
        self.release(); self.server.shutdown(); self.server.server_close(); self.thread.join(5)


def activity(game):
    open_setup(game)
    reach(game, 'View Pumas download activity')
    game.wait_visible('Pumas download activity')


def select(game, download_id):
    reach(game, 'Inspect ' + download_id)
    game.wait_visible('Pumas download status')


def read_end(game, phrase):
    for _ in range(14):
        if normalized(phrase) in game.visible():
            game.check(True, 'keyboard-readable prose: ' + phrase)
            return
        game.key('Tab'); game.key('End')
    raise RuntimeError('Keyboard reader did not reach: ' + phrase)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--percent', choices=[100, 150], type=int, required=True)
    args = parser.parse_args()
    fixture = ActivityFixture()
    game = SetupPlayer(args.display_state, args.fixture, args.output)
    game.env['LANTERNWAKE_PUMAS_URL'] = fixture.url
    game.result['controllerSha256'] = digest(__file__)
    game.result['limits'] = ['Controlled Pumas source-derived lifecycle snapshots; no real acquisition, cleanup, immutable receipt, runtime, loaded model or inference.',
                             'Synthetic X11/software renderer; no physical-input, assistive-tool or human-duration acceptance.']
    try:
        with game:
            game.click('Arrive on the island'); game.key('Return'); game.click('Save')
            before = game.slots()
            if args.percent == 150:
                for _ in range(2):
                    game.click('Settings'); game.click('Larger reading text'); game.key('Escape')
            open_setup(game); inspect(game); request(game)
            game.wait_visible('Pumas accepted the request')
            reach(game, 'View this request')
            game.wait_visible('Status: Downloading')
            game.wait_visible('50%')
            game.root_capture('active-' + str(args.percent))
            fixture.downloads[ACTIVE].update(progress=1, downloadedBytes=1000)
            reach(game, 'Refresh this request'); game.wait_visible('Status: Downloading'); game.wait_visible('100%')
            game.check(len(fixture.methods('start_model_download_from_hf')) == 1, '100 percent does not infer readiness or reacquire')
            game.root_capture('hundred-percent-still-downloading')
            game.key('Escape')

            activity(game); select(game, ACTIVE)
            game.wait_visible('Status: Downloading')
            game.check(len(fixture.methods('start_model_download_from_hf')) == 1 and not fixture.methods('cancel_model_download'),
                       'close and reopen restores exact Pumas activity without acquisition or cancellation')
            fixture.hold('get_model_download_status')
            reach(game, 'Refresh this request')
            game.check(fixture.entered.wait(3), 'pending status reply observed')
            game.key('Escape'); fixture.release(); time.sleep(.8)
            game.check('pumas download status' not in game.visible(), 'late status cannot reopen a dismissed view')
            activity(game); select(game, ACTIVE); game.wait_visible('Status: Downloading')
            game.check(not fixture.methods('cancel_model_download'), 'closing status wait does not cancel a Pumas download')

            fixture.downloads[ACTIVE].update(status='completed')
            reach(game, 'Refresh this request'); game.wait_visible('Status: Completed')
            read_end(game, 'Pumas reports completion')
            game.root_capture('completed-' + str(args.percent))
            game.check(len(fixture.methods('start_model_download_from_hf')) == 1, 'completion does not load or request another model')
            reach(game, 'All Pumas download activity'); select(game, QUEUED)
            game.wait_visible('Status: Queued')
            reach(game, 'Cancel this download'); game.wait_visible('Confirm Pumas cancellation')
            game.root_capture('cancel-confirm-' + str(args.percent))
            game.check(not fixture.methods('cancel_model_download'), 'opening cancellation confirmation sends no command')
            game.key('Escape')
            activity(game); select(game, QUEUED); game.wait_visible('Status: Queued')
            game.check(not fixture.methods('cancel_model_download'), 'Escape from confirmation keeps Pumas transfer running')

            reach(game, 'Cancel this download'); reach(game, 'Send cancellation to Pumas')
            game.wait_visible('Pumas acknowledged cancellation')
            game.root_capture('cancel-acknowledged')
            cancellations = fixture.methods('cancel_model_download')
            game.check(len(cancellations) == 1 and cancellations[0]['params'] == {'download_id': QUEUED}, 'exact selected ID gets one explicit cancellation command')
            reach(game, 'Refresh this request'); game.wait_visible('Status: Cancelling')
            read_end(game, 'Pumas is cancelling')
            game.root_capture('cancelling')
            fixture.downloads[QUEUED].update(status='cancelled')
            reach(game, 'Refresh this request'); game.wait_visible('Status: Cancelled')
            read_end(game, 'Pumas reports terminal cancellation')
            game.root_capture('cancelled')
            game.check(len(fixture.methods('cancel_model_download')) == 1, 'terminal cancellation comes only from explicit backend status refresh')

            reach(game, 'All Pumas download activity'); select(game, FAILED)
            game.wait_visible('Status: Failed')
            read_end(game, 'Pumas reports failure')
            game.root_capture('failed-' + str(args.percent))
            fixture.cancel_mode = 'rejected'
            reach(game, 'Cancel this download'); reach(game, 'Send cancellation to Pumas')
            game.wait_visible('Pumas did not acknowledge cancellation')
            game.root_capture('cancel-rejected')
            game.check(len(fixture.methods('cancel_model_download')) == 2, 'rejected cancellation is displayed without replay or false terminal state')

            reach(game, 'All Pumas download activity')
            fixture.missing_id = ACTIVE
            select_caption = 'Inspect ' + ACTIVE
            reach(game, select_caption); game.wait_visible('Pumas activity unavailable'); game.wait_visible('Download not found')
            game.root_capture('missing-status')
            fixture.missing_id = None
            reach(game, 'Refresh activity'); select(game, FAILED)

            fixture.cancel_mode = 'accepted'; fixture.hold('cancel_model_download')
            reach(game, 'Cancel this download'); reach(game, 'Send cancellation to Pumas')
            game.check(fixture.entered.wait(3), 'cancellation accepted by controlled backend while response held')
            game.key('Escape'); fixture.release(); time.sleep(.8)
            game.check('pumas cancellation result' not in game.visible(), 'late cancellation acknowledgement cannot reopen setup')
            activity(game); select(game, FAILED); game.wait_visible('Status: Cancelling')
            game.check(len(fixture.methods('cancel_model_download')) == 3 and fixture.methods('cancel_model_download')[-1]['params'] == {'download_id': FAILED},
                       'reopening after unknown acknowledgement reads Pumas and does not replay cancellation')
            game.key('Escape')
            game.check(len(fixture.methods('start_model_download_from_hf')) == 1, 'entire lifecycle flow performs only the original explicit acquisition')
            game.check(game.slots() == before, 'all lifecycle screens and keyboard reading leave player slots untouched')
            game.click('Save')
            saved = game.slots()
            game.check(saved['save.json'] == before['save.json'] and saved['save.previous.json'] == before['save.json'],
                       'explicit Save preserves exact story and transcript state with the expected backup')
            game.quit()
    finally:
        (args.output / 'rpc-requests.json').write_text(json.dumps(fixture.requests, indent=2) + '\n')
        fixture.close()
    print('PASS normal Godot Pumas download activity at', args.percent, 'percent,', len(game.result['checks']), 'checks')


if __name__ == '__main__':
    main()
