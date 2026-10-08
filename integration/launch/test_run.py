"""Launcher admission and failure custody; no tools, network or assets downloaded."""
import os
import re
import contextlib
import errno
import io
from pathlib import Path
import sys
import tempfile
import time
import unittest
from unittest.mock import patch
import zipfile

PROJECT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(PROJECT / 'scripts'))
import run as launch


class SourceLauncherTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='lanternwake-launch-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / 'path with spaces'
        self.root.mkdir()
        self.godot = self.root / 'godot-console.exe'
        self.dotnet = self.root / 'dotnet'
        self.feed = self.root / 'GodotSharp/Tools/nupkgs'
        self.feed.mkdir(parents=True)
        for name in ['Godot.NET.Sdk', 'Godot.SourceGenerators', 'GodotSharp', 'GodotSharpEditor']:
            (self.feed / f'{name}.4.6.3.nupkg').touch()

    def output(self, version='4.6.3.stable.mono.official.test', sdks='8.0.425 [/installed/sdk]'):
        return lambda command: version if command[-1] == '--version' else sdks

    def test_explicit_non_mono_and_wrong_version_rejected(self):
        with patch.object(launch, 'executable', return_value=self.godot):
            for version in ['4.6.3.stable.official.test', '4.5.2.stable.mono.official.test']:
                with self.subTest(version=version), patch.object(launch, 'probe', side_effect=self.output(version)):
                    with self.assertRaisesRegex(launch.LaunchError, 'requires Godot .NET 4.6.3'):
                        launch.prerequisites(str(self.godot))

    def test_runtime_without_sdk_is_actionable(self):
        with patch.object(launch, 'executable', return_value=self.godot), patch.object(launch, 'probe', side_effect=self.output(sdks='')):
            with self.assertRaisesRegex(launch.LaunchError, '.NET 8 SDK is missing'):
                launch.prerequisites(str(self.godot))

    def test_bundled_feed_required_before_audio_mutation(self):
        (self.feed / 'GodotSharpEditor.4.6.3.nupkg').unlink()
        with patch.object(launch, 'executable', return_value=self.godot), patch.object(launch, 'probe', side_effect=self.output()):
            with self.assertRaisesRegex(launch.LaunchError, 'complete official .NET distribution'):
                launch.prerequisites(str(self.godot))

    def test_paths_with_spaces_and_pinned_feed(self):
        with patch.object(launch, 'executable', side_effect=[self.godot, self.dotnet]), patch.object(launch, 'probe', side_effect=self.output()):
            self.assertEqual(launch.prerequisites(str(self.godot)), (self.godot, self.dotnet, self.feed))

    def test_missing_executable_guidance(self):
        with self.assertRaisesRegex(launch.LaunchError, 'Install Godot'):
            launch.executable(str(self.root / 'missing-godot'), 'Install Godot')

    def test_prerequisite_failure_never_prepares_or_starts_game(self):
        with patch.object(launch, 'prerequisites', side_effect=launch.LaunchError('Missing installed SDK')), patch.object(launch, 'prepare') as prepare, patch.object(launch.subprocess, 'call') as play, contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()) as error:
            self.assertEqual(launch.main([]), 1)
            self.assertIn('Missing installed SDK', error.getvalue())
            prepare.assert_not_called()
            play.assert_not_called()

    def test_failed_preparation_never_starts_game(self):
        with patch.object(launch, 'prerequisites', return_value=(self.godot, self.dotnet, self.feed)), patch.object(launch, 'prepare', side_effect=launch.LaunchError('Import failed')), patch.object(launch.subprocess, 'call') as play, contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(launch.main([]), 1)
            play.assert_not_called()

    def test_setup_only_does_not_start_game(self):
        with patch.object(launch, 'prerequisites', return_value=(self.godot, self.dotnet, self.feed)), patch.object(launch, 'prepare', return_value={}), patch.object(launch.subprocess, 'call') as play, contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(launch.main(['--setup-only']), 0)
            play.assert_not_called()
            self.assertIn('with --editor', output.getvalue())
            self.assertIn('only to processes started by this launcher', output.getvalue())

    def test_editor_inherits_prepared_environment(self):
        environment = {'DOTNET_ROOT': '/selected/sdk', 'PATH': '/selected/sdk', 'NUGET_PACKAGES': '/project/cache', 'RestoreConfigFile': '/project/config'}
        with patch.object(launch, 'prerequisites', return_value=(self.godot, self.dotnet, self.feed)), patch.object(launch, 'prepare', return_value=environment), patch.object(launch.subprocess, 'call', return_value=0) as editor, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(launch.main(['--editor', '--', '--headless', '--build-solutions', '--quit']), 0)
            editor.assert_called_once_with([str(self.godot), '--path', str(launch.PROJECT), '--editor', '--headless', '--build-solutions', '--quit'], cwd=launch.PROJECT, env=environment)

    def test_offline_configuration_survives_preparation(self):
        with patch.object(launch, 'PROJECT', self.root), patch.object(launch, 'cache_bundled_sdk', side_effect=lambda _, cache: cache.mkdir(parents=True)), patch.object(launch, 'ensure_assets'), patch.object(launch, 'run_step'):
            environment = launch.prepare(self.godot, self.dotnet, self.feed)
        config = Path(environment['RestoreConfigFile'])
        self.assertTrue(config.is_file())
        self.assertIn(str(self.feed), config.read_text())
        self.assertNotIn('nuget.org', config.read_text())
        self.assertEqual(environment['DOTNET_ROOT'], str(self.dotnet.parent))
        self.assertEqual(environment['NUGET_PACKAGES'], str(self.root / '.godot/source-launch/nuget'))

    def test_editor_and_setup_only_are_mutually_exclusive(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as error:
            launch.main(['--editor', '--setup-only'])
        self.assertEqual(error.exception.code, 2)

    def test_setup_timeout_must_be_positive(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as error:
            launch.main(['--setup-timeout', '0'])
        self.assertEqual(error.exception.code, 2)

    def test_selected_timeout_only_applies_to_preparation(self):
        with patch.object(launch, 'prerequisites', return_value=(self.godot, self.dotnet, self.feed)), patch.object(launch, 'prepare', return_value={}) as prepare, patch.object(launch.subprocess, 'call', return_value=0) as play, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(launch.main(['--setup-timeout', '900']), 0)
            prepare.assert_called_once_with(self.godot, self.dotnet, self.feed, 900)
            self.assertNotIn('timeout', play.call_args.kwargs)

    def test_silent_setup_timeout_reaps_child(self):
        processes = []
        original = launch.subprocess.Popen

        def start(*args, **kwargs):
            process = original(*args, **kwargs)
            processes.append(process)
            return process

        began = time.monotonic()
        with patch.object(launch.subprocess, 'Popen', side_effect=start), contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaisesRegex(launch.LaunchError, 'increase --setup-timeout'):
                launch.run_step('Stalled setup fixture', [sys.executable, '-c', 'import time; time.sleep(60)'], timeout=.1)
        self.assertLess(time.monotonic() - began, 5)
        self.assertIsNotNone(processes[0].poll())

    def test_setup_exit_is_bounded_after_output_closes(self):
        processes = []
        original = launch.subprocess.Popen

        def start(*args, **kwargs):
            process = original(*args, **kwargs)
            processes.append(process)
            return process

        with patch.object(launch.subprocess, 'Popen', side_effect=start), contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaisesRegex(launch.LaunchError, 'timed out'):
                launch.run_step('Closed-output setup fixture', [sys.executable, '-c', 'import os,time; os.close(1); os.close(2); time.sleep(60)'], timeout=.1)
        self.assertIsNotNone(processes[0].poll())

    @unittest.skipUnless(sys.platform.startswith('linux'), 'Linux process-group fixture')
    def test_timeout_stops_descendant_that_keeps_output_open(self):
        pid_file = self.root / 'owned-child.pid'
        command = 'import subprocess,sys; from pathlib import Path; child=subprocess.Popen([sys.executable,"-c","import time; time.sleep(60)"]); Path(sys.argv[1]).write_text(str(child.pid))'
        with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(launch.LaunchError, 'timed out'):
            launch.run_step('Orphan-output setup fixture', [sys.executable, '-c', command, str(pid_file)], timeout=.5)
        pid = int(pid_file.read_text())
        self.assert_process_stopped(pid)

    def assert_process_stopped(self, pid):
        status = Path(f'/proc/{pid}/stat')
        deadline = time.monotonic() + 2
        while True:
            try:
                observed = status.read_text()
            except (FileNotFoundError, ProcessLookupError):
                return
            if re.match(r'^\d+ \(.+\) Z ', observed):
                return
            if time.monotonic() >= deadline:
                self.assertRegex(observed, r'^\d+ \(.+\) Z ')
            # Signal delivery to a descendant may settle after its parent exits.
            time.sleep(.005)

    def test_stopped_process_can_disappear_during_stat_read(self):
        for error in [FileNotFoundError(errno.ENOENT, 'No such file'),
                      ProcessLookupError(errno.ESRCH, 'No such process')]:
            with self.subTest(error=type(error).__name__), patch.object(Path, 'open') as open_stat:
                read = open_stat.return_value.__enter__.return_value.read
                read.side_effect = error
                self.assert_process_stopped(123)
                read.assert_called_once_with()

    def test_stopped_process_requires_zombie_if_stat_remains(self):
        for state in ['Z', 'R', 'S', 'D', 'T']:
            with self.subTest(state=state), patch.object(Path, 'read_text', return_value=f'123 (child) {state} 1'):
                if state == 'Z':
                    self.assert_process_stopped(123)
                else:
                    with self.assertRaises(self.failureException):
                        self.assert_process_stopped(123)

    def test_stopped_process_preserves_unrelated_stat_errors(self):
        with patch.object(Path, 'read_text', side_effect=PermissionError(errno.EACCES, 'Permission denied')):
            with self.assertRaises(PermissionError):
                self.assert_process_stopped(123)

    def test_setup_interrupt_reaps_child(self):
        processes = []
        original = launch.subprocess.Popen

        def start(*args, **kwargs):
            process = original(*args, **kwargs)
            processes.append(process)
            return process

        with patch.object(launch.subprocess, 'Popen', side_effect=start), patch.object(launch.queue.Queue, 'get', side_effect=KeyboardInterrupt), contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaises(KeyboardInterrupt):
                launch.run_step('Interrupted setup fixture', [sys.executable, '-c', 'import time; time.sleep(60)'])
        self.assertIsNotNone(processes[0].poll())

    def test_modified_audio_recovery_names_supported_setup_command(self):
        audio = self.root / 'Assets/Audio'
        audio.mkdir(parents=True)
        (audio / 'manifest.json').write_bytes((PROJECT / 'Assets/Audio/manifest.json').read_bytes())
        custom = audio / 'harbor.wav'
        custom.write_bytes(b'custom bytes to preserve')
        with self.assertRaises(ValueError) as error:
            launch.ensure_assets(audio)
        self.assertIn('scripts/setup_audio.py --regenerate', str(error.exception))
        self.assertIn('Preserve custom audio', str(error.exception))
        self.assertNotIn('run.py --regenerate', str(error.exception))
        self.assertEqual(custom.read_bytes(), b'custom bytes to preserve')

    def test_engine_and_game_arguments_and_exit_code_preserved(self):
        environment = {'fixture': 'owned'}
        with patch.object(launch, 'prerequisites', return_value=(self.godot, self.dotnet, self.feed)), patch.object(launch, 'prepare', return_value=environment), patch.object(launch.subprocess, 'call', return_value=7) as play, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(launch.main(['--', '--headless', '--', '--audio-smoke']), 7)
            play.assert_called_once_with([str(self.godot), '--path', str(launch.PROJECT), '--headless', '--', '--audio-smoke'], cwd=launch.PROJECT, env=environment)

    def test_native_import_errors_fail_even_with_zero_exit_code(self):
        child = unittest.mock.MagicMock()
        child.__enter__.return_value = child
        child.stdout = io.StringIO('ERROR: Missing imported resource\n')
        child.wait.return_value = 0
        with patch.object(launch.subprocess, 'Popen', return_value=child), contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaisesRegex(launch.LaunchError, 'game was not launched'):
                launch.run_step('Import', ['godot'], reject_engine_errors=True)

    def test_nonzero_build_exit_stops_preparation(self):
        child = unittest.mock.MagicMock()
        child.__enter__.return_value = child
        child.stdout = io.StringIO('Build FAILED\n')
        child.wait.return_value = 1
        with patch.object(launch.subprocess, 'Popen', return_value=child), contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaisesRegex(launch.LaunchError, 'exit 1'):
                launch.run_step('Build', ['dotnet'])

    def test_sdk_seed_uses_installed_bytes_and_is_idempotent(self):
        package = self.feed / 'Godot.NET.Sdk.4.6.3.nupkg'
        with zipfile.ZipFile(package, 'w') as archive:
            archive.writestr('Sdk/Sdk.props', '<Project/>')
            archive.writestr('Sdk/Sdk.targets', '<Project/>')
        cache = self.root / '.godot/source-launch/nuget'
        launch.cache_bundled_sdk(self.feed, cache)
        sdk = cache / 'godot.net.sdk/4.6.3'
        before = {p.relative_to(sdk): (p.read_bytes(), p.stat().st_mtime_ns) for p in sdk.rglob('*') if p.is_file()}
        self.assertEqual((sdk / 'godot.net.sdk.4.6.3.nupkg').read_bytes(), package.read_bytes())
        launch.cache_bundled_sdk(self.feed, cache)
        self.assertEqual(before, {p.relative_to(sdk): (p.read_bytes(), p.stat().st_mtime_ns) for p in sdk.rglob('*') if p.is_file()})

    def test_incomplete_sdk_cannot_publish_cache_completion(self):
        with zipfile.ZipFile(self.feed / 'Godot.NET.Sdk.4.6.3.nupkg', 'w') as archive:
            archive.writestr('wrong-file', 'not an SDK')
        cache = self.root / '.godot/source-launch/nuget'
        with self.assertRaisesRegex(launch.LaunchError, 'incomplete'):
            launch.cache_bundled_sdk(self.feed, cache)
        self.assertFalse(list(cache.rglob('.nupkg.metadata')))


class PumasSelectionTests(unittest.TestCase):
    def test_explicit_paths_are_process_local_and_never_start_pumas(self):
        with tempfile.TemporaryDirectory(prefix='owned-pumas-launch-') as temporary:
            root = Path(temporary); observer = root / 'owned observer'; observer.touch()
            with patch.object(launch, 'prerequisites', return_value=(Path('/owned/godot'), Path('/owned/dotnet'), root)), patch.object(launch, 'prepare', return_value={}), patch.object(launch.subprocess, 'call', return_value=0) as game, contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(launch.main(['--pumas-library', str(root), '--pumas-observer', str(observer)]), 0)
                self.assertEqual(game.call_count, 1)
                self.assertEqual(game.call_args.args[0][0], '/owned/godot')
                env = game.call_args.kwargs['env']
                self.assertEqual(env['LANTERNWAKE_PUMAS_LIBRARY_ROOT'], str(root.resolve()))
                self.assertEqual(env['LANTERNWAKE_PUMAS_OBSERVER'], str(observer.resolve()))
                self.assertEqual(env['LANTERNWAKE_PUMAS_SELECTION_OVERRIDE'], '1')

    def test_partial_selection_refuses_before_preparation(self):
        with tempfile.TemporaryDirectory(prefix='owned-pumas-launch-') as temporary, patch.dict(os.environ, {}, clear=True):
            with patch.object(launch, 'prerequisites', return_value=(Path('/owned/godot'), Path('/owned/dotnet'), Path(temporary))), patch.object(launch, 'prepare') as prepare, patch.object(launch.subprocess, 'call') as game, contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(launch.main(['--pumas-library', temporary]), 1)
                prepare.assert_not_called(); game.assert_not_called()

    def test_missing_library_is_not_created_and_never_prepares_or_starts(self):
        with tempfile.TemporaryDirectory(prefix='owned-pumas-launch-') as temporary:
            root = Path(temporary); missing = root / 'missing'
            with patch.object(launch, 'prerequisites', return_value=(root/'godot', root/'dotnet', root)), patch.object(launch, 'prepare') as prepare, patch.object(launch.subprocess, 'call') as game, contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(launch.main(['--pumas-library', str(missing)]), 1)
                self.assertFalse(missing.exists()); prepare.assert_not_called(); game.assert_not_called()


if __name__ == '__main__':
    unittest.main()
