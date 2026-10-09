"""Source inventory is bounded before any native qualification starts."""
import subprocess
import sys
import unittest
from unittest.mock import patch

import history_resume


class HistoryResumeRunnerTests(unittest.TestCase):
    def test_real_stalled_child_is_terminated_and_reported(self):
        real_output = subprocess.check_output

        def stalled_inventory(command, **options):
            self.assertEqual(command, ['git', 'ls-files', '-z'])
            self.assertEqual(options['timeout'], 30)
            # Use a short fixture deadline for a real owned child, not a 30-second test.
            return real_output([sys.executable, '-c', 'import time; time.sleep(60)'], timeout=0.1)

        with patch.object(history_resume.subprocess, 'check_output', side_effect=stalled_inventory):
            with self.assertRaisesRegex(RuntimeError, 'History source inventory timed out') as raised:
                history_resume.main()
        self.assertIsInstance(raised.exception.__cause__, subprocess.TimeoutExpired)

    def test_stalled_inventory_fails_before_native_launch(self):
        timeout = subprocess.TimeoutExpired(['git', 'ls-files', '-z'], 30)
        with patch.object(history_resume.subprocess, 'check_output', side_effect=timeout) as inventory, \
                patch.object(history_resume.subprocess, 'run') as native:
            with self.assertRaisesRegex(RuntimeError, 'History source inventory timed out') as raised:
                history_resume.main()
        self.assertIs(raised.exception.__cause__, timeout)
        self.assertEqual(inventory.call_args.kwargs['timeout'], 30)
        native.assert_not_called()

    def test_nul_inventory_parsing_and_native_deadline_preserved(self):
        result = subprocess.CompletedProcess(['owned-godot'], 0, stdout='LANTERNWAKE_HISTORY_RESUME_OK\n')
        with patch.object(history_resume.subprocess, 'check_output', return_value=b'README.md\0') as inventory, \
                patch.object(history_resume.subprocess, 'run', return_value=result) as native, \
                patch.dict(history_resume.os.environ, {'GODOT_MONO': 'owned-godot'}):
            history_resume.main()
        self.assertEqual(inventory.call_args.args[0], ['git', 'ls-files', '-z'])
        self.assertEqual(inventory.call_args.kwargs['timeout'], 30)
        self.assertEqual(native.call_args.kwargs['timeout'], 45)


if __name__ == '__main__':
    unittest.main()
