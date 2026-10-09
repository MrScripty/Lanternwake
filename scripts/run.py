#!/usr/bin/env python3
"""Prepare and run Lanternwake from source using installed tools, without downloads."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import queue
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import threading
import time
import xml.etree.ElementTree as ET
import zipfile

from setup_audio import ensure_assets
from setup_music import ensure_music

PROJECT = Path(__file__).resolve().parents[1]
GODOT_VERSION = '4.6.3'


class LaunchError(RuntimeError):
    pass


def executable(value, guidance):
    resolved = shutil.which(os.path.expanduser(value))
    if not resolved:
        raise LaunchError(f"Cannot find '{value}'. {guidance}")
    return Path(resolved).resolve()


def probe(command):
    try:
        result = subprocess.run(command, cwd=PROJECT, encoding='utf-8', errors='replace', stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=30)
    except (OSError, subprocess.TimeoutExpired) as error:
        raise LaunchError(f"Cannot run {command[0]}: {error}") from error
    if result.returncode:
        raise LaunchError(f"Prerequisite check failed for {command[0]}:\n{result.stdout.strip()}")
    return result.stdout.strip()


def prerequisites(godot_value=None, dotnet_value='dotnet'):
    guidance = 'Install the official Godot .NET 4.6.3 distribution, then pass --godot its executable or set GODOT_MONO. On Windows use the *_console.exe executable.'
    godot_value = godot_value or os.environ.get('GODOT_MONO')
    candidates = [godot_value] if godot_value else ['godot-mono', 'godot4-mono', 'godot', 'godot4']
    failures = []
    godot = None
    for candidate in candidates:
        try:
            found = executable(candidate, guidance)
            version = probe([str(found), '--version'])
            if not version.startswith(GODOT_VERSION + '.stable.mono.'):
                raise LaunchError(f"{found} reports '{version}'; this project requires Godot .NET {GODOT_VERSION}. {guidance}")
            godot = found
            break
        except LaunchError as error:
            failures.append(str(error))
    if godot is None:
        raise LaunchError('\n'.join(failures))

    dotnet = executable(dotnet_value, 'Install the .NET 8 SDK (a runtime alone cannot build C#), add dotnet to PATH, or pass --dotnet its executable.')
    if not re.search(r'^8\.\d+\.\d+\s+\[', probe([str(dotnet), '--list-sdks']), re.MULTILINE):
        raise LaunchError('The .NET 8 SDK is missing. Install it from dotnet.microsoft.com/download/dotnet/8.0; a runtime alone cannot build this project.')

    # Windows/Linux distributions keep GodotSharp next to the executable;
    # the macOS app bundle keeps it in Contents/Resources.
    feeds = [godot.parent / 'GodotSharp/Tools/nupkgs', godot.parent.parent / 'Resources/GodotSharp/Tools/nupkgs']
    packages = ['Godot.NET.Sdk', 'Godot.SourceGenerators', 'GodotSharp', 'GodotSharpEditor']
    feed = next((path for path in feeds if all((path / f'{name}.{GODOT_VERSION}.nupkg').is_file() for name in packages)), None)
    if feed is None:
        raise LaunchError('The installed Godot .NET package feed is missing. Extract the complete official .NET distribution and keep its GodotSharp folder with the executable. No packages will be downloaded.')
    return godot, dotnet, feed


def stop_setup_process(child):
    # Preparation owns this process tree. Stop descendants as well, including
    # children that could otherwise keep the output pipe open after a timeout.
    if os.name == 'posix':
        try:
            os.killpg(child.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    elif os.name == 'nt':
        taskkill = Path(os.environ.get('SystemRoot', r'C:\Windows')) / 'System32/taskkill.exe'
        try:
            subprocess.run([str(taskkill), '/PID', str(child.pid), '/T', '/F'],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=5)
        except (OSError, subprocess.TimeoutExpired):
            pass
    if child.poll() is None:
        child.kill()
    child.wait(timeout=5)


def run_step(label, command, *, environment=None, reject_engine_errors=False, timeout=300):
    print(label, flush=True)
    failed_output = False
    try:
        with subprocess.Popen(command, cwd=PROJECT, env=environment, encoding='utf-8', errors='replace', stdout=subprocess.PIPE,
                              stderr=subprocess.STDOUT, start_new_session=os.name == 'posix') as child:
            output = queue.Queue()

            def read_output():
                try:
                    for line in child.stdout:
                        output.put(line)
                finally:
                    output.put(None)

            reader = threading.Thread(target=read_output, daemon=True)
            reader.start()
            deadline = time.monotonic() + timeout
            try:
                while True:
                    remaining = deadline - time.monotonic()
                    if remaining <= 0:
                        raise subprocess.TimeoutExpired(command, timeout)
                    line = output.get(timeout=remaining)
                    if line is None:
                        break
                    print(line, end='', flush=True)
                    if reject_engine_errors and re.search(r'\b(?:ERROR:|SCRIPT ERROR:)|Failed to build', line):
                        failed_output = True
                code = child.wait(timeout=max(0, deadline - time.monotonic()))
            except (queue.Empty, subprocess.TimeoutExpired) as error:
                stop_setup_process(child)
                raise LaunchError(f'{label} timed out after {timeout:g} seconds. Resolve the stalled tool or increase --setup-timeout, then rerun.') from error
            except KeyboardInterrupt:
                stop_setup_process(child)
                raise
            finally:
                reader.join(timeout=1)
    except OSError as error:
        raise LaunchError(f'{label} could not start: {error}') from error
    if code or failed_output:
        raise LaunchError(f'{label} failed (exit {code}). Resolve the errors above and rerun this command; the game was not launched.')


def cache_bundled_sdk(feed, packages):
    # MSBuild's NuGet SDK resolver runs before restore's --configfile is read.
    # Seed that one SDK from the installed distribution in a project-local
    # NuGet cache, so an empty user cache cannot trigger an online SDK lookup.
    name = f'godot.net.sdk.{GODOT_VERSION}.nupkg'
    destination = packages / 'godot.net.sdk' / GODOT_VERSION
    if (destination / '.nupkg.metadata').is_file():
        return
    source = feed / f'Godot.NET.Sdk.{GODOT_VERSION}.nupkg'
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.sdk-', dir=destination.parent) as temporary:
        staging = Path(temporary) / 'package'
        staging.mkdir()
        with zipfile.ZipFile(source) as archive:
            if not {'Sdk/Sdk.props', 'Sdk/Sdk.targets'}.issubset(archive.namelist()):
                raise LaunchError('The bundled Godot.NET.Sdk package is incomplete. Re-extract the official Godot .NET distribution.')
            archive.extractall(staging)
        shutil.copyfile(source, staging / name)
        digest = base64.b64encode(hashlib.sha512(source.read_bytes()).digest()).decode('ascii')
        (staging / (name + '.sha512')).write_text(digest, encoding='utf-8')
        (staging / '.nupkg.metadata').write_text(json.dumps({'version': 2, 'contentHash': digest, 'source': str(feed)}), encoding='utf-8')
        staging.rename(destination)


def prepare(godot, dotnet, feed, timeout=300):
    environment = os.environ.copy()
    # Godot locates hostfxr and invokes dotnet itself when opening its editor.
    # Honor an explicitly selected SDK even when it is outside the user's PATH.
    environment['DOTNET_ROOT'] = str(dotnet.parent)
    environment['PATH'] = str(dotnet.parent) + os.pathsep + environment.get('PATH', '')
    packages = PROJECT / '.godot/source-launch/nuget'
    environment['NUGET_PACKAGES'] = str(packages)
    environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE'] = 'true'
    cache_bundled_sdk(feed, packages)
    ensure_assets(PROJECT / 'Assets/Audio')
    ensure_music()
    # Use only packages already shipped with Godot. Neither remote feeds nor
    # automatic SDK/workload installation belong to a source game launcher.
    config = PROJECT / '.godot/source-launch/NuGet.Config'
    # Editor-initiated builds must retain the same local feed after preparation.
    # This MSBuild property is inherited by dotnet processes spawned by Godot.
    environment['RestoreConfigFile'] = str(config)
    with tempfile.TemporaryDirectory(prefix='.config-', dir=config.parent) as temporary:
        candidate = Path(temporary) / 'NuGet.Config'
        root = ET.Element('configuration')
        sources = ET.SubElement(root, 'packageSources')
        ET.SubElement(sources, 'clear')
        ET.SubElement(sources, 'add', key='installed-godot', value=str(feed))
        ET.ElementTree(root).write(candidate, encoding='utf-8', xml_declaration=True)
        os.replace(candidate, config)
    run_step('Restoring C# packages from installed Godot (offline)…',
             [str(dotnet), 'restore', 'Lanternwake.csproj', '--configfile', str(config)], environment=environment, timeout=timeout)
    run_step('Building Lanternwake…',
             [str(dotnet), 'build', 'Lanternwake.csproj', '--configuration', 'Debug', '--no-restore'], environment=environment, timeout=timeout)
    run_step('Importing game assets…', [str(godot), '--headless', '--editor', '--path', str(PROJECT), '--import'],
             environment=environment, reject_engine_errors=True, timeout=timeout)
    return environment


def setup_seconds(value):
    seconds = int(value)
    if seconds < 1:
        raise argparse.ArgumentTypeError('Use a positive number of seconds for setup stages.')
    return seconds


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__, epilog='Pass Godot arguments after --, for example: -- --headless --quit-after 120. No export templates are needed.')
    parser.add_argument('--godot', help='Path to installed Godot .NET 4.6.3 executable (default: GODOT_MONO or PATH)')
    parser.add_argument('--dotnet', default='dotnet', help='Path to installed dotnet executable (default: PATH)')
    parser.add_argument('--pumas-library', help='Explicit existing canonical Pumas library folder; used by model setup, never starts Pumas')
    parser.add_argument('--pumas-observer', help='Absolute installed pumas-rpc executable for authenticated read-only owner discovery')
    parser.add_argument('--setup-timeout', type=setup_seconds, default=300, metavar='SECONDS', help='Maximum seconds per restore/build/import process (default: 300)')
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--setup-only', action='store_true', help='Prepare assets/builds only; rerun with --editor to edit using the prepared environment')
    mode.add_argument('--editor', action='store_true', help='Prepare and open the .NET editor with the selected SDK and offline package configuration')
    parser.add_argument('godot_arguments', nargs=argparse.REMAINDER, help='Optional Godot arguments after --')
    options = parser.parse_args(arguments)
    if options.setup_only and options.godot_arguments:
        parser.error('--setup-only cannot be combined with game arguments')
    try:
        print('Checking installed Godot .NET and .NET 8 SDK…', flush=True)
        godot, dotnet, feed = prerequisites(options.godot, options.dotnet)
        selection = {}
        for value, variable, directory in [(options.pumas_library, 'LANTERNWAKE_PUMAS_LIBRARY_ROOT', True), (options.pumas_observer, 'LANTERNWAKE_PUMAS_OBSERVER', False)]:
            if value is not None:
                path = Path(os.path.expanduser(value))
                if not path.is_absolute() or not (path.is_dir() if directory else path.is_file()):
                    raise LaunchError('Select existing absolute Pumas library and observer paths. No Pumas service was started.')
                selection[variable] = str(path.resolve())
        if selection:
            root_value = selection.get('LANTERNWAKE_PUMAS_LIBRARY_ROOT', os.environ.get('LANTERNWAKE_PUMAS_LIBRARY_ROOT', ''))
            observer_value = selection.get('LANTERNWAKE_PUMAS_OBSERVER', os.environ.get('LANTERNWAKE_PUMAS_OBSERVER', ''))
            if not root_value or not observer_value:
                raise LaunchError('Select both --pumas-library and --pumas-observer, or provide the other path in the environment. No Pumas service was started.')
            for value, variable, directory in [(root_value, 'LANTERNWAKE_PUMAS_LIBRARY_ROOT', True), (observer_value, 'LANTERNWAKE_PUMAS_OBSERVER', False)]:
                path = Path(os.path.expanduser(value))
                if not path.is_absolute() or not (path.is_dir() if directory else path.is_file()):
                    raise LaunchError('Select existing absolute Pumas library and observer paths. No Pumas service was started.')
                selection[variable] = str(path.resolve())
            selection['LANTERNWAKE_PUMAS_SELECTION_OVERRIDE'] = '1'
        environment = prepare(godot, dotnet, feed, options.setup_timeout)
        environment.update(selection)
        if options.setup_only:
            print('Prepared. Rerun this command with --editor to edit, or without --setup-only to play. Tool and package settings apply only to processes started by this launcher.')
            return 0
        extra = options.godot_arguments
        if extra[:1] == ['--']:
            extra = extra[1:]
        print('Starting Godot .NET editor…' if options.editor else 'Starting Lanternwake from source…', flush=True)
        return subprocess.call([str(godot), '--path', str(PROJECT), *(['--editor'] if options.editor else []), *extra], cwd=PROJECT, env=environment)
    except (LaunchError, OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print(f'Lanternwake launch failed: {error}', file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        return 130


if __name__ == '__main__':
    raise SystemExit(main())
