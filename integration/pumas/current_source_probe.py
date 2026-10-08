"""Probe a locally built Pumas95 with a fresh empty library; never claim inference."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import time
import urllib.request

PIN = '95a0baad2d0aea4650fc36ad4afd969ac9391bf5'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--binary', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--display-state', type=Path)
    parser.add_argument('--seed', type=Path)
    args = parser.parse_args()
    source, binary, output = args.source.resolve(), args.binary.resolve(), args.output.resolve()
    repo = Path(__file__).resolve().parents[2]
    actual = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=source, text=True).strip()
    if actual != PIN:
        raise SystemExit('Pumas source does not match the approved pin.')
    subprocess.run(['git', 'diff', '--exit-code', 'HEAD', '--'], cwd=source, check=True, stdout=subprocess.DEVNULL)
    output.mkdir(parents=True, exist_ok=False)
    launcher = output / 'launcher'
    launcher.mkdir()
    environment = os.environ.copy()
    for variable, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
        environment[variable] = str(output / child)
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    captures = {}
    process = None
    try:
        with (output / 'pumas.log').open('wb') as log:
            process = subprocess.Popen([str(binary), '--host', '127.0.0.1', '--port', '0',
                                        '--launcher-root', str(launcher)], cwd=source, env=environment,
                                       stdout=log, stderr=subprocess.STDOUT)
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline and process.poll() is None:
            match = re.search(r'^RPC_PORT=(\d+)$', (output / 'pumas.log').read_text(), re.M)
            if match:
                break
            time.sleep(.1)
        else:
            raise RuntimeError('Pumas did not publish readiness; inspect pumas.log.')
        url = f'http://127.0.0.1:{match[1]}/'

        def request(name, payload=None):
            body = None if payload is None else json.dumps(payload).encode()
            req = urllib.request.Request(url + ('health' if payload is None else 'rpc'), data=body,
                                         headers={'Content-Type': 'application/json'})
            with opener.open(req, timeout=10) as response:
                raw = response.read(262145)
                if len(raw) > 262144:
                    raise RuntimeError('Probe response exceeded its bound.')
                value = json.loads(raw)
                if payload is not None and (value.get('jsonrpc') != '2.0' or value.get('id') != payload['id']
                                             or value.get('result', {}).get('success') is not True):
                    raise RuntimeError('Pumas RPC probe did not return its matching successful envelope.')
                captures[name] = {'httpStatus': response.status, 'body': value}
                (output / (name + '.json')).write_bytes(raw)
                return value

        request('health')
        status = request('serving', {'jsonrpc': '2.0', 'id': 'lanternwake-status',
                                    'method': 'get_serving_status', 'params': {}})
        snapshot = status['result']['snapshot']
        if status['result']['success'] is not True or snapshot['schema_version'] != 1 or snapshot['served_models'] != []:
            raise RuntimeError('Expected source-owned schema1 empty serving snapshot.')
        for method, params in [('get_models', {}), ('get_runtime_profiles_snapshot', {}),
                               ('get_installed_versions', {'app_id': 'llama-cpp'}),
                               ('get_active_version', {'app_id': 'llama-cpp'})]:
            request(method, {'jsonrpc': '2.0', 'id': method, 'method': method, 'params': params})
        if (captures['get_models']['body']['result']['models'] != {} or
                captures['get_installed_versions']['body']['result']['versions'] != [] or
                captures['get_active_version']['body']['result']['version'] != ''):
            raise RuntimeError('The owned fixture unexpectedly contains models or a managed llama runtime.')
        environment.update(LANTERNWAKE_PUMAS_URL=url, LANTERNWAKE_PUMAS_MODEL='lanternwake-uninstalled')
        command = ['dotnet', 'run', '--no-build', '--project', 'integration/pumas/LiveSmoke/LiveSmoke.csproj',
                   '--', 'Content/story.json']
        result = subprocess.run(command, cwd=repo, env=environment, capture_output=True, text=True, timeout=60)
        (output / 'client.log').write_text(result.stdout + result.stderr)
        reply = json.loads(result.stdout.strip())
        if result.returncode != 1 or reply['success'] or reply['errorCode'] != 'model_unavailable' or not reply['canonicalUnchanged']:
            raise RuntimeError('Empty real Pumas did not produce the expected production-client outcome.')
        if args.display_state is not None:
            if args.seed is None:
                raise RuntimeError('Normal UI qualification also needs an owned authored-conversation seed.')
            subprocess.run(['python3', 'integration/qa/pumas_unavailable_input.py',
                            '--display-state', str(args.display_state.resolve()), '--seed', str(args.seed.resolve()),
                            '--fixture', str(output / 'game-fixture'), '--output', str(output / 'normal-ui'),
                            '--pumas-url', url], cwd=repo, env=environment, check=True, timeout=180)
        receipt = {'pumasSource': PIN, 'pumasBinarySha256': sha(binary), 'captures': captures, 'client': reply,
                   'realEmptyService': True, 'liveInference': False,
                   'limits': ['Fresh empty library; loaded-model projection is tested separately with simulated fixtures.',
                              'No model, managed llama runtime, inference quality or in-flight native-provider acceptance.']}
        (output / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    finally:
        if process is not None and process.poll() is None:
            process.terminate()
            process.wait(timeout=20)
    if process.returncode != 0:
        raise RuntimeError('Owned Pumas did not terminate cleanly.')
    receipt['pumasExitCode'] = process.returncode
    (output / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print('PASS Pumas95 real empty serving contract and authored-context client fallback; no inference claim.')


if __name__ == '__main__':
    main()
