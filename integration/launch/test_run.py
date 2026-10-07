"""Launcher admission and failure custody; no tools, network or assets downloaded."""
import contextlib
import io
from pathlib import Path
import sys
import tempfile
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
        with patch.object(launch, 'prerequisites', return_value=(self.godot, self.dotnet, self.feed)), patch.object(launch, 'prepare', return_value={}), patch.object(launch.subprocess, 'call') as play, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(launch.main(['--setup-only']), 0)
            play.assert_not_called()

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


if __name__ == '__main__':
    unittest.main()
