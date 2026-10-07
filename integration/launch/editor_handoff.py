#!/usr/bin/env python3
"""Native editor build in a copied checkout, empty profile/cache and blocked network."""
import argparse
import http.server
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import threading

PROJECT = Path(__file__).resolve().parents[2]


class BlockNetwork(http.server.BaseHTTPRequestHandler):
    def reject(self):
        self.server.requests.append(self.command)
        self.send_error(503, 'External network is disabled for this regression')

    do_GET = do_CONNECT = do_POST = reject

    def log_message(self, *_):
        pass


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', default=os.environ.get('GODOT_MONO'))
    parser.add_argument('--dotnet', default=shutil.which('dotnet'))
    options = parser.parse_args()
    if not options.godot or not options.dotnet:
        parser.error('Specify the installed Godot .NET and dotnet executables using --godot and --dotnet.')
    godot, dotnet = Path(options.godot).resolve(), Path(options.dotnet).resolve()
    with tempfile.TemporaryDirectory(prefix='lanternwake-editor-handoff-') as temporary:
        root = Path(temporary)
        project = root / 'checkout'
        # Copy current tracked source, including local changes under test, without
        # carrying imported resources, generated audio or a prior package cache.
        tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=PROJECT).decode('utf-8').split('\0')
        for relative in filter(None, tracked):
            source, target = PROJECT / relative, project / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
        assert not (project / '.godot').exists()
        assert not list((project / 'Assets/Audio').glob('*.wav'))

        # Force a source change after preparation so the real editor must rebuild
        # using the handed-off environment, rather than reuse the launcher DLL.
        probe = root / 'probe.py'
        probe.write_text('''import os, sys
from pathlib import Path
project = Path(sys.argv[1])
sys.path.insert(0, str(project / 'scripts'))
import run as launcher
prepare = launcher.prepare
binary = project / '.godot/mono/temp/bin/Debug/Lanternwake.dll'
before = None
def force_editor_build(*args):
    global before
    environment = prepare(*args)
    assert environment['DOTNET_ROOT'] == str(Path(sys.argv[3]).parent)
    assert Path(environment['RestoreConfigFile']).is_file()
    assert Path(environment['NUGET_PACKAGES']).is_relative_to(project)
    before = binary.stat().st_mtime_ns
    source = project / 'qualification/AudioLifecycleQualification.cs'
    source.write_text(source.read_text() + '\\n// Force native editor rebuild in owned test checkout.\\n')
    return environment
launcher.prepare = force_editor_build
code = launcher.main(['--godot', sys.argv[2], '--dotnet', sys.argv[3], '--editor', '--', '--headless', '--build-solutions', '--quit'])
if code:
    raise SystemExit(code)
assert binary.stat().st_mtime_ns > before, 'Native editor did not rebuild the changed source'
print('LANTERNWAKE_EDITOR_HANDOFF_OK editor rebuilt changed source with selected SDK and offline cache')
''', encoding='utf-8')
        environment = os.environ.copy()
        for key in list(environment):
            if key.upper().startswith(('DOTNET_', 'NUGET_', 'MSBUILD')) or key.lower().startswith('restore'):
                environment.pop(key)
        empty_path = root / 'empty-path'
        empty_path.mkdir()
        environment.update(PATH=str(empty_path), DOTNET_CLI_HOME=str(root / 'dotnet-profile'),
                           NUGET_PACKAGES=str(root / 'unused-empty-cache'),
                           DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1',
                           XDG_CONFIG_HOME=str(root / 'config'), XDG_DATA_HOME=str(root / 'data'),
                           XDG_CACHE_HOME=str(root / 'cache'), LANTERNWAKE_PUMAS_MODEL='')
        with http.server.ThreadingHTTPServer(('127.0.0.1', 0), BlockNetwork) as proxy:
            proxy.requests = []
            thread = threading.Thread(target=proxy.serve_forever, daemon=True)
            thread.start()
            url = f'http://127.0.0.1:{proxy.server_port}'
            environment.update(http_proxy=url, https_proxy=url, HTTP_PROXY=url, HTTPS_PROXY=url,
                               all_proxy=url, ALL_PROXY=url, no_proxy='', NO_PROXY='')
            try:
                result = subprocess.run([sys.executable, str(probe), str(project), str(godot), str(dotnet)],
                                        cwd=root, env=environment, encoding='utf-8', errors='replace',
                                        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=180)
            finally:
                proxy.shutdown()
                thread.join(timeout=5)
            output = result.stdout
            print(output, end='', flush=True)
            if result.returncode or re.search(r'ERROR:|SCRIPT ERROR:|WARNING:|Build FAILED|Failed to build', output):
                raise RuntimeError('Isolated native editor build failed; inspect the output above.')
            if 'LANTERNWAKE_EDITOR_HANDOFF_OK' not in output or proxy.requests:
                raise RuntimeError(f'Editor handoff did not complete offline; blocked network requests: {len(proxy.requests)}')
        print('PASS empty checkout/profile/cache, SDK initially outside PATH, real editor rebuild and zero external proxy requests. Linux qualification; no independent-editor or Windows/macOS claim.')


if __name__ == '__main__':
    main()
