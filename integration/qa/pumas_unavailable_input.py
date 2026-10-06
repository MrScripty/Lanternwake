"""Normal Godot input against a real empty Pumas; authored fallback is required."""
import argparse
import json
from pathlib import Path
import time
import urllib.request

from normal_player import NormalPlayer, digest


class SettledPlayer(NormalPlayer):
    def click(self, caption):
        deadline = time.monotonic() + 8
        while True:
            try:
                super().click(caption)
                time.sleep(.5)
                return
            except RuntimeError as error:
                if 'OCR control' not in str(error) or time.monotonic() >= deadline:
                    raise
                time.sleep(.2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--pumas-url', required=True)
    args = parser.parse_args()
    from urllib.parse import urlparse
    address = urlparse(args.pumas_url)
    if address.scheme != 'http' or address.hostname != '127.0.0.1' or address.path != '/' or address.username:
        raise SystemExit('Owned IPv4 loopback Pumas URL required.')
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    request = urllib.request.Request(args.pumas_url + 'rpc', data=json.dumps(
        {'jsonrpc': '2.0', 'id': 'lanternwake-status', 'method': 'get_serving_status', 'params': {}}).encode(),
        headers={'Content-Type': 'application/json'})
    with opener.open(request, timeout=10) as response:
        status = json.load(response)
    if status['result']['snapshot']['served_models'] != []:
        raise SystemExit('This regression requires the explicitly empty Pumas fixture.')
    game = SettledPlayer(args.display_state, args.fixture, args.output, args.seed)
    game.env.update(LANTERNWAKE_PUMAS_URL=args.pumas_url, LANTERNWAKE_PUMAS_MODEL='lanternwake-uninstalled')
    game.result['limits'] = ['Synthetic X11 input and software rendering; no physical input/hearing acceptance.',
                             'Real empty Pumas; no loaded provider, generated dialogue or model-quality claim.']
    with game:
        game.result['controllerSha256'] = digest(__file__)
        game.result['realPumasStatus'] = status
        game.click('Settings'); game.wait_visible('Reading settings')
        game.click('Toggle instant text'); game.key('Escape')
        game.click('Load'); game.wait_visible('Load current manual save')
        game.click('Load current manual save')
        current = game.snapshot()
        beat = next(b for b in game.beats if b['id'] == current['beatId'])
        game.check(beat['conversation'] is not None, 'loaded authored conversation is available')
        game.click('Stay and talk'); game.wait_visible('A moment between the lines')
        game.type_text('hello.'); game.key('Return')
        game.wait_visible('model_unavailable')
        after = game.snapshot('autosave.json')
        game.check(after['beatId'] == current['beatId'] and after['solvedActivities'] == current['solvedActivities'],
                   'real unavailable Pumas does not advance or change solved activities')
        game.check(after['history'][:-2] == current['history'], 'prior canonical history remains exact')
        game.check(after['history'][-2]['text'] == 'hello.' and
                   after['history'][-1]['text'] == beat['conversation']['fallback'] and
                   not after['history'][-1]['generated'], 'one exact authored fallback follows the actual typed turn')
        game.native_capture('real-pumas-unavailable')
        game.key('Escape'); game.wait_visible('A moment between the lines', False)
        game.click('Save')
        game.check(game.snapshot() == after, 'Return and Save preserve fallback result without canonical advance')
        game.quit()
    print('PASS normal Godot real-empty-Pumas fallback label, exact reply, Return and canonical preservation.')


if __name__ == '__main__':
    main()
