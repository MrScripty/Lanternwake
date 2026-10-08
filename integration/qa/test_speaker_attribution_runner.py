"""Timeout diagnostics remain failures and preserve partial producer output."""
import contextlib
import io
import subprocess
import unittest
from unittest.mock import patch

import speaker_attribution


class SpeakerAttributionRunnerTests(unittest.TestCase):
    def test_timeout_preserves_byte_stdout_text_stderr_and_original_deadline(self):
        command = ['controlled-producer']
        timeout = subprocess.TimeoutExpired(command, 60, output=b'partial stdout\ninvalid utf8: \xff\n',
                                            stderr='partial stderr\n')
        output = io.StringIO()
        with patch.object(speaker_attribution.subprocess, 'run', side_effect=timeout) as run, \
                contextlib.redirect_stdout(output):
            with self.assertRaisesRegex(RuntimeError, 'timed out; inspect captured output') as raised:
                speaker_attribution.run_native(command, cwd='owned-project', env={'owned': 'fixture'})
        self.assertIs(raised.exception.__cause__, timeout)
        self.assertIn('partial stdout\ninvalid utf8: \ufffd\n', output.getvalue())
        self.assertIn('partial stderr\n', output.getvalue())
        self.assertIn('timed out after 60 seconds.', output.getvalue())
        run.assert_called_once_with(command, cwd='owned-project', env={'owned': 'fixture'}, text=True,
                                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=60)

    def test_timeout_with_success_marker_still_fails_and_missing_streams_are_safe(self):
        for captured in (None, 'LANTERNWAKE_SPEAKER_ATTRIBUTION_OK checks=187\n'):
            with self.subTest(output=captured), \
                    patch.object(speaker_attribution.subprocess, 'run',
                                 side_effect=subprocess.TimeoutExpired(['producer'], 60, output=captured)), \
                    contextlib.redirect_stdout(io.StringIO()) as output:
                with self.assertRaisesRegex(RuntimeError, 'timed out'):
                    speaker_attribution.run_native(['producer'], cwd='owned-project', env={})
                self.assertIn('timed out after 60 seconds.', output.getvalue())

    def test_completed_failure_is_returned_to_unchanged_game_assertions(self):
        failure = subprocess.CompletedProcess(['producer'], 1, stdout='ERROR: native failure\n')
        with patch.object(speaker_attribution.subprocess, 'run', return_value=failure):
            self.assertIs(speaker_attribution.run_native(['producer'], cwd='owned-project', env={}), failure)


if __name__ == '__main__':
    unittest.main()
