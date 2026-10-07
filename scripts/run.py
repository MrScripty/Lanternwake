#!/usr/bin/env python3
"""Prepare and run Lanternwake from source using installed tools, without downloads."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

from setup_audio import ensure_assets

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


def run_step(label, command, *, environment=None, reject_engine_errors=False):
    print(label, flush=True)
    failed_output = False
    try:
        with subprocess.Popen(command, cwd=PROJECT, env=environment, encoding='utf-8', errors='replace', stdout=subprocess.PIPE,
                              stderr=subprocess.STDOUT) as child:
            for line in child.stdout:
                print(line, end='', flush=True)
                if reject_engine_errors and re.search(r'\b(?:ERROR:|SCRIPT ERROR:)|Failed to build', line):
                    failed_output = True
            code = child.wait()
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


def prepare(godot, dotnet, feed):
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
    # Use only packages already shipped with Godot. Neither remote feeds nor
    # automatic SDK/workload installation belong to a source game launcher.
    with tempfile.TemporaryDirectory(prefix='lanternwake-nuget-') as temporary:
        config = Path(temporary) / 'NuGet.Config'
        root = ET.Element('configuration')
        sources = ET.SubElement(root, 'packageSources')
        ET.SubElement(sources, 'clear')
        ET.SubElement(sources, 'add', key='installed-godot', value=str(feed))
        ET.ElementTree(root).write(config, encoding='utf-8', xml_declaration=True)
        run_step('Restoring C# packages from installed Godot (offline)…',
                 [str(dotnet), 'restore', 'Lanternwake.csproj', '--configfile', str(config)], environment=environment)
        run_step('Building Lanternwake…',
                 [str(dotnet), 'build', 'Lanternwake.csproj', '--configuration', 'Debug', '--no-restore'], environment=environment)
    run_step('Importing game assets…', [str(godot), '--headless', '--editor', '--path', str(PROJECT), '--import'],
             environment=environment, reject_engine_errors=True)
    return environment


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__, epilog='Pass Godot arguments after --, for example: -- --headless --quit-after 120. No export templates are needed.')
    parser.add_argument('--godot', help='Path to installed Godot .NET 4.6.3 executable (default: GODOT_MONO or PATH)')
    parser.add_argument('--dotnet', default='dotnet', help='Path to installed dotnet executable (default: PATH)')
    parser.add_argument('--setup-only', action='store_true', help='Prepare the project without starting the game; then open project.godot in the .NET editor')
    parser.add_argument('godot_arguments', nargs=argparse.REMAINDER, help='Optional Godot arguments after --')
    options = parser.parse_args(arguments)
    if options.setup_only and options.godot_arguments:
        parser.error('--setup-only cannot be combined with game arguments')
    try:
        print('Checking installed Godot .NET and .NET 8 SDK…', flush=True)
        godot, dotnet, feed = prerequisites(options.godot, options.dotnet)
        environment = prepare(godot, dotnet, feed)
        if options.setup_only:
            print('Ready. Open project.godot in Godot .NET, or rerun this command without --setup-only to play.')
            return 0
        extra = options.godot_arguments
        if extra[:1] == ['--']:
            extra = extra[1:]
        print('Starting Lanternwake from source…', flush=True)
        return subprocess.call([str(godot), '--path', str(PROJECT), *extra], cwd=PROJECT, env=environment)
    except (LaunchError, OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print(f'Lanternwake launch failed: {error}', file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        return 130


if __name__ == '__main__':
    raise SystemExit(main())
