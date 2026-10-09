"""Timeout diagnostics remain failures and preserve partial producer output."""
import contextlib
import io
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import missing_margin


class MissingMarginRunnerTests(unittest.TestCase):
    def test_real_inventory_timeout_stops_before_native_launch(self):
        real_output = subprocess.check_output
        def stalled_inventory(command, **options):
            self.assertEqual(command, ['git', 'ls-files', '-z'])
            self.assertEqual(options['timeout'], 30)
            return real_output([sys.executable, '-c', 'import time; time.sleep(60)'], timeout=0.1)
        with patch.object(missing_margin.subprocess, 'check_output', side_effect=stalled_inventory), \
                patch.object(missing_margin, 'run_native') as native:
            with self.assertRaisesRegex(RuntimeError, 'Missing Margin source inventory timed out'):
                missing_margin.main()
            native.assert_not_called()

    def test_failed_inventory_stops_before_native_launch(self):
        for failure in [subprocess.CalledProcessError(1, ['git']), FileNotFoundError('owned missing Git')]:
            with self.subTest(failure=failure), patch.object(missing_margin.subprocess, 'check_output', side_effect=failure), \
                    patch.object(missing_margin, 'run_native') as native:
                with self.assertRaisesRegex(RuntimeError, 'Missing Margin source inventory failed') as raised:
                    missing_margin.main()
                self.assertIs(raised.exception.__cause__, failure)
                native.assert_not_called()

    @unittest.skipUnless(os.name == 'posix', 'Non-UTF-8 filenames require a POSIX filesystem.')
    def test_non_utf8_spaced_inventory_and_deleted_source_remain_fail_closed(self):
        with tempfile.TemporaryDirectory(prefix='owned-margin-source-') as temporary:
            # Patch only the runner's project locator to an owned synthetic source tree.
            project = Path(temporary)
            source = project / os.fsdecode(b'owned spaced-\xff.cs'); source.write_bytes(b'owned source')
            fake_script = project / 'integration/qa/missing_margin.py'
            inventory = os.fsencode(source.name) + b'\0'
            result = subprocess.CompletedProcess(['owned-godot'], 0, stdout='LANTERNWAKE_MISSING_MARGIN_OK\n')
            with patch.object(missing_margin, '__file__', str(fake_script)), \
                    patch.object(missing_margin.subprocess, 'check_output', return_value=inventory) as files, \
                    patch.object(missing_margin, 'run_native', return_value=result) as native, \
                    patch.dict(os.environ, {'GODOT_MONO': 'owned-godot'}), contextlib.redirect_stdout(io.StringIO()):
                missing_margin.main()
                self.assertEqual(files.call_args.kwargs['timeout'], 30)
                self.assertEqual(native.call_args.args[0][0], 'owned-godot')
                def delete_during_native(*args, **kwargs):
                    source.unlink(); return result
                native.side_effect = delete_during_native
                with self.assertRaisesRegex(RuntimeError, 'Missing Margin changed tracked source'):
                    missing_margin.main()
                native.reset_mock()
                with self.assertRaisesRegex(RuntimeError, 'Missing Margin tracked source unavailable'):
                    missing_margin.main()
                native.assert_not_called()

    def test_timeout_preserves_byte_stdout_text_stderr_and_original_deadline(self):
        command = ['controlled-producer']
        timeout = subprocess.TimeoutExpired(command, 45, output=b'partial stdout\ninvalid utf8: \xff\n',
                                            stderr='partial stderr\n')
        output = io.StringIO()
        with patch.object(missing_margin.subprocess, 'run', side_effect=timeout) as run, \
                contextlib.redirect_stdout(output):
            with self.assertRaisesRegex(RuntimeError, 'timed out; inspect captured output') as raised:
                missing_margin.run_native(command, cwd='owned-project', env={'owned': 'fixture'})
        self.assertIs(raised.exception.__cause__, timeout)
        self.assertIn('partial stdout\ninvalid utf8: \ufffd\n', output.getvalue())
        self.assertIn('partial stderr\n', output.getvalue())
        self.assertIn('timed out after 45 seconds.', output.getvalue())
        run.assert_called_once_with(command, cwd='owned-project', env={'owned': 'fixture'}, text=True,
                                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=45)

    def test_timeout_with_success_marker_still_fails_and_missing_streams_are_safe(self):
        for captured in (None, 'LANTERNWAKE_MISSING_MARGIN_OK checks=44\n'):
            with self.subTest(output=captured), \
                    patch.object(missing_margin.subprocess, 'run',
                                 side_effect=subprocess.TimeoutExpired(['producer'], 45, output=captured)), \
                    contextlib.redirect_stdout(io.StringIO()) as output:
                with self.assertRaisesRegex(RuntimeError, 'timed out'):
                    missing_margin.run_native(['producer'], cwd='owned-project', env={})
                self.assertIn('timed out after 45 seconds.', output.getvalue())

    def test_completed_failure_is_returned_to_unchanged_game_assertions(self):
        failure = subprocess.CompletedProcess(['producer'], 1, stdout='ERROR: native failure\n')
        with patch.object(missing_margin.subprocess, 'run', return_value=failure):
            self.assertIs(missing_margin.run_native(['producer'], cwd='owned-project', env={}), failure)


if __name__ == '__main__':
    unittest.main()
