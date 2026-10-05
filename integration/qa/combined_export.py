#!/usr/bin/env python3
"""Audit an official Linux data export of an immutable Git source snapshot.

Missing matching player templates are recorded as a blocker. This never downloads
templates and never promotes a data archive or managed publish to a player build.
The project-local restore feed allows only bundled official Godot packages and
the standard official NuGet endpoint. All output and userdata are task-owned.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tarfile
import xml.etree.ElementTree as ET
import zipfile


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ref', default='HEAD')
    parser.add_argument('--output-root', required=True, type=Path)
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[2]
    output = args.output_root.resolve()
    output.mkdir(parents=True, exist_ok=False)
    source = output / 'source'
    source.mkdir()
    engine = Path(os.environ['GODOT_MONO']).resolve()
    environment = os.environ.copy()
    environment['LANTERNWAKE_PUMAS_MODEL'] = ''
    for key, child in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
        environment[key] = str(output / child)
    version = subprocess.check_output([str(engine), '--version'], env=environment, text=True).strip()
    if not version.startswith('4.6.3.stable.mono.official.'):
        raise RuntimeError('Exact official Godot 4.6.3 .NET required.')
    commit, tree = subprocess.check_output(['git', 'show', '-s', '--format=%H %T', args.ref], cwd=repository, text=True).split()
    archive = output / 'source.tar'
    subprocess.run(['git', 'archive', '--format=tar', '--output', str(archive), commit], cwd=repository, check=True)
    with tarfile.open(archive) as snapshot:
        snapshot.extractall(source, filter='data')
    manifest = {str(p.relative_to(source)): digest(p) for p in source.rglob('*') if p.is_file()}
    report = {'sourceCommit': commit, 'sourceTree': tree, 'sourceArchiveSha256': digest(archive),
              'engine': version, 'engineSha256': digest(engine),
              'sdk': subprocess.check_output(['dotnet', '--version'], env=environment, text=True).strip(),
              'sourceFiles': manifest, 'steps': [], 'standaloneQualified': False,
              'limits': ['Data archive and managed publish are not a standalone player.',
                         'No clean-machine, physical-device, hearing, model, ASR or human duration acceptance.']}

    def save():
        (output / 'receipt.json').write_text(json.dumps(report, indent=2) + '\n')

    def run(label, command, *, clean=True, expected=0, cwd=source):
        log = output / (label + '.log')
        with log.open('wb') as stream:
            result = subprocess.run(command, cwd=cwd, env=environment, stdout=stream, stderr=subprocess.STDOUT, timeout=300)
        text = log.read_text(errors='replace')
        problems = re.findall(r'^.*(?:ERROR:|SCRIPT ERROR:|WARNING:|error [A-Z]+[0-9]+:|warning [A-Z]+[0-9]+:|Build FAILED|Failed to build).*$', text, re.M)
        report['steps'].append({'name': label, 'exitCode': result.returncode, 'logSha256': digest(log), 'diagnostics': problems})
        save()
        if result.returncode != expected or (clean and problems):
            raise RuntimeError(f'{label} failed qualification; inspect {log}')
        return text

    # This config exists only in the disposable snapshot, not user/global settings.
    configuration = ET.Element('configuration')
    feeds = ET.SubElement(configuration, 'packageSources')
    ET.SubElement(feeds, 'clear')
    ET.SubElement(feeds, 'add', key='official-godot', value=str(engine.parent / 'GodotSharp/Tools/nupkgs'))
    ET.SubElement(feeds, 'add', key='official-nuget', value='https://api.nuget.org/v3/index.json')
    ET.ElementTree(configuration).write(source / 'NuGet.Config', encoding='unicode')
    (source / 'export_presets.cfg').write_text('''[preset.0]
name="Linux diagnostic"
platform="Linux"
runnable=true
export_filter="all_resources"
include_filter="Content/*.json"
exclude_filter="docs/*,tests/*,integration/*,scripts/*,qualification/*,addons/*,artifacts/*"
export_path=""
[preset.0.options]
binary_format/architecture="x86_64"
dotnet/include_scripts_content=false
''')
    try:
        run('setup', ['bash', 'scripts/setup.sh'])
        run('restore', ['dotnet', 'restore', 'Lanternwake.sln', '--configfile', 'NuGet.Config'])
        for mode in ['Debug', 'ExportDebug', 'ExportRelease']:
            run('build-' + mode, ['dotnet', 'build', 'Lanternwake.sln', '-c', mode, '--no-restore', '--warnaserror'])
        run('import', [str(engine), '--headless', '--editor', '--path', str(source), '--import'])
        run('editor-build', [str(engine), '--headless', '--editor', '--path', str(source), '--build-solutions', '--quit'])
        run('runtime-restore', ['dotnet', 'restore', 'Lanternwake.sln', '--configfile', 'NuGet.Config', '-r', 'linux-x64', '-p:SelfContained=true'])
        data = output / 'lanternwake-data.zip'
        run('export-pack', [str(engine), '--headless', '--editor', '--path', str(source), '--export-pack', 'Linux diagnostic', str(data)])
        official_logs = output / 'data/godot/mono/build_logs'
        publish_logs = list(official_logs.glob('*_ExportRelease/msbuild_log.txt'))
        if len(publish_logs) != 1 or 'godot-publish-dotnet' not in publish_logs[0].read_text():
            raise RuntimeError('Official exporter did not retain a .NET publish log.')
        copied_logs = output / 'official-build-logs'
        copied_logs.mkdir()
        report['officialBuildLogs'] = {}
        for path in sorted(official_logs.glob('*/*')):
            if not path.is_file():
                continue
            if path.name == 'msbuild_issues.csv' and path.read_text().strip():
                raise RuntimeError(f'Official build/publish contains compiler diagnostics: {path}')
            name = path.parent.name + '-' + path.name
            shutil.copy2(path, copied_logs / name)
            report['officialBuildLogs'][name] = digest(path)
        with zipfile.ZipFile(data) as pack:
            if pack.testzip() is not None:
                raise RuntimeError('Archive CRC failure.')
            names = set(pack.namelist())
            if pack.read('Content/story.json') != (source / 'Content/story.json').read_bytes():
                raise RuntimeError('Canonical story bytes differ.')
            mapped = []
            for path in sorted((source / 'Scenes').rglob('*')):
                if path.suffix not in ['.tscn', '.tres']:
                    continue
                relative = str(path.relative_to(source))
                remap = relative + '.remap'
                if remap not in names:
                    raise RuntimeError(f'Missing scene/resource remap: {remap}')
                target = re.search(r'path="res://([^"]+)"', pack.read(remap).decode())
                if target is None or target[1] not in names or not pack.read(target[1]):
                    raise RuntimeError(f'Invalid remap: {remap}')
                mapped.append({'resource': relative, 'target': target[1]})
            audio = []
            for path in sorted((source / 'Assets/Audio').glob('*.wav')):
                relative = str(path.relative_to(source))
                metadata = relative + '.import'
                if metadata not in names:
                    raise RuntimeError(f'Missing audio import: {metadata}')
                target = re.search(r'path="res://([^"]+)"', pack.read(metadata).decode())
                if target is None or target[1] not in names or not pack.read(target[1]):
                    raise RuntimeError(f'Missing imported audio: {metadata}')
                audio.append({'asset': relative, 'generatedSha256': digest(path), 'imported': target[1]})
            if len(audio) != 7:
                raise RuntimeError('Expected all seven original generated audio samples.')
            if any(n.startswith(('docs/', 'tests/', 'integration/', 'qualification/', 'addons/')) for n in names):
                raise RuntimeError('Diagnostic export contains development-only resources.')
            report['dataArchive'] = {'sha256': digest(data), 'crcPassed': True, 'entries': len(names),
                                     'storySha256': digest(source / 'Content/story.json'), 'mappedResources': mapped, 'audio': audio}
        environment['LANTERNWAKE_AUDIT_PACK'] = str(data)
        header_text = run('archived-header', [str(engine), '--headless', '--path', str(source), '--script',
                                             str(repository / 'integration/qa/export_header_audit.gd')])
        marker = 'LANTERNWAKE_EXPORTED_HEADER_OK '
        records = [json.loads(line.removeprefix(marker)) for line in header_text.splitlines() if line.startswith(marker)]
        if len(records) != 1:
            raise RuntimeError('Archived binary header resource did not report success.')
        report['dataArchive']['headerResource'] = records[0]
        # The Editor removes its temporary publish directory after packing data.
        # Retain a separate explicit publish of the same immutable source for audit.
        publish = output / 'managed-publish'
        run('retained-publish', ['dotnet', 'publish', 'Lanternwake.csproj', '-c', 'ExportRelease',
                                '-r', 'linux-x64', '--self-contained', 'true', '--no-restore',
                                '--warnaserror', '-o', str(publish)])
        dll = publish / 'Lanternwake.dll'
        pdb = dll.with_suffix('.pdb')
        audit_project = repository / 'integration/qa/ExportAudit/ExportAudit.csproj'
        text = run('compiler-binding', ['dotnet', 'run', '--project', str(audit_project), '--', str(source), str(dll), str(pdb)], cwd=repository)
        binding = json.loads(text.splitlines()[-1])
        compiler_paths = {d['path'] for d in binding['documents']}
        expected_paths = {p for p in manifest if p.endswith('.cs') and not p.startswith(('tests/', 'integration/'))}
        if not expected_paths.issubset(compiler_paths):
            raise RuntimeError(f'Tracked source missing from compiler PDB: {expected_paths - compiler_paths}')
        report['managedPublish'] = {'dll': str(dll.relative_to(output)), 'dllSha256': digest(dll),
                                    'pdbSha256': digest(pdb), 'compilerBinding': binding,
                                    'files': {p.name: digest(p) for p in dll.parent.iterdir() if p.is_file()}}
        # Reuse approved locally installed exact templates if present. Never fetch a denied route.
        approved = Path(os.environ.get('XDG_DATA_HOME', str(Path.home() / '.local/share'))) / 'godot/export_templates/4.6.3.stable.mono'
        required = ['linux_debug.x86_64', 'linux_release.x86_64']
        report['templates'] = {'directory': str(approved), 'present': {n: (approved / n).is_file() for n in required}}
        if all((approved / n).is_file() for n in required):
            raise RuntimeError('Templates are present; standalone runtime qualification needs its own runner.')
        failure = run('standalone-attempt', [str(engine), '--headless', '--editor', '--path', str(source), '--export-release', 'Linux diagnostic', str(output / 'Lanternwake.x86_64')], clean=False, expected=1)
        if 'linux_release.x86_64' not in failure or 'template' not in failure.lower() or (output / 'Lanternwake.x86_64').exists():
            raise RuntimeError('Standalone failure did not establish the expected missing-template blocker.')
        report['standaloneBlocker'] = 'Matching official Godot 4.6.3 .NET Linux templates absent; no download retry or bypass.'
        for name, expected_hash in manifest.items():
            if digest(source / name) != expected_hash:
                raise RuntimeError(f'Export changed immutable source file: {name}')
        report['dataExportQualified'] = True
        save()
        print('PASS exact-source builds, official Editor/publish/data export, CRC/resources/audio and DLL/PDB/source bindings.')
        print('BLOCKED standalone player: matching official templates absent.')
    except Exception as error:
        report['failure'] = str(error)
        save()
        raise


if __name__ == '__main__':
    main()
