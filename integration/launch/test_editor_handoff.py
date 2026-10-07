"""Fail-closed classification of the recorded native-editor shutdown diagnostic."""
import contextlib
import io
from pathlib import Path
import subprocess
import unittest

import editor_handoff as handoff


class EditorHandoffDiagnosticTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        # Recorded stdout from run 37700304364, job 113061894984. Only GitHub
        # timestamps, ANSI colors and trailing whitespace were removed; see the note.
        cls.recorded = (Path(__file__).parent / 'fixtures/godot463_editor_shutdown.txt').read_text()
        cls.diagnostic = '\n'.join(handoff.ANDROID_SHUTDOWN) + '\n'

    def validate(self, output=None, returncode=0, requests=()):
        return handoff.validate_handoff_output(self.recorded if output is None else output, returncode, requests)

    def rejected(self, output=None, **kwargs):
        with self.assertRaises(RuntimeError):
            self.validate(output, **kwargs)

    def test_recorded_successful_rebuild_with_exact_shutdown_signature_is_classified(self):
        self.assertTrue(self.validate())

    def test_clean_handoff_needs_no_exception(self):
        self.assertFalse(self.validate(self.recorded.replace(self.diagnostic, '')))

    def test_original_colored_output_is_retained_and_ci_warning_is_explicit(self):
        colored = self.recorded.replace(handoff.BUILD_DONE, '\x1b[92m[ DONE ]\x1b[39m \x1b[1mdotnet_build_project\x1b[22m')
        result = subprocess.CompletedProcess([], 0, colored)
        with contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertTrue(handoff.report_handoff_result(result, []))
        self.assertTrue(output.getvalue().startswith(colored))
        self.assertEqual(output.getvalue().count('::warning title='), 1)
        self.assertIn('not a warning-free engine shutdown', output.getvalue())

    def test_clean_output_does_not_emit_known_diagnostic_warning(self):
        clean = self.recorded.replace(self.diagnostic, '')
        with contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertFalse(handoff.report_handoff_result(subprocess.CompletedProcess([], 0, clean), []))
        self.assertEqual(output.getvalue(), clean)

    def test_failed_process_and_network_attempt_never_qualify(self):
        for code in [-9, 1, 130]:
            with self.subTest(code=code):
                self.rejected(returncode=code)
        self.rejected(requests=['CONNECT'])

    def test_full_changed_dll_success_marker_is_required_once_at_end(self):
        for replacement in ['', 'LANTERNWAKE_EDITOR_HANDOFF_OK', handoff.HANDOFF_OK + '\n' + handoff.HANDOFF_OK]:
            with self.subTest(replacement=replacement):
                self.rejected(self.recorded.replace(handoff.HANDOFF_OK, replacement))
        self.rejected(self.recorded + 'unexpected trailing output\n')

    def test_exact_official_engine_identity_is_required_in_final_editor_stage(self):
        for wrong in ['4.6.2', '4.7', '4.6.3.stable.mono.custom.7d41c59c4',
                      '4.6.3.stable.mono.official.otherhash']:
            with self.subTest(version=wrong):
                prefix, editor = self.recorded.split(handoff.EDITOR_START)
                editor = editor.replace(handoff.PINNED_ENGINE, 'Godot Engine v' + wrong + ' - https://godotengine.org')
                self.rejected(prefix + handoff.EDITOR_START + editor)

    def test_import_header_cannot_qualify_an_unidentified_or_nested_editor(self):
        prefix, editor = self.recorded.split(handoff.EDITOR_START)
        self.rejected(prefix + handoff.EDITOR_START + editor.replace(handoff.PINNED_ENGINE, ''))
        self.rejected(prefix + handoff.EDITOR_START + editor.replace(handoff.PINNED_ENGINE, handoff.PINNED_ENGINE + '\n' + handoff.PINNED_ENGINE))
        self.rejected(self.recorded.replace(handoff.EDITOR_START, ''))
        self.rejected(handoff.EDITOR_START + '\n' + self.recorded)

    def test_native_build_must_finish_once_before_shutdown_diagnostic(self):
        self.rejected(self.recorded.replace(handoff.BUILD_DONE, ''))
        self.rejected(self.recorded.replace(handoff.BUILD_DONE, handoff.BUILD_DONE + '\n' + handoff.BUILD_DONE))
        without = self.recorded.replace(self.diagnostic, '')
        self.rejected(without.replace(handoff.BUILD_DONE, self.diagnostic + handoff.BUILD_DONE))

    def test_other_settings_source_locations_and_messages_remain_fatal(self):
        for original, changed in [('android_sdk_path', 'java_sdk_path'),
                                  ('editor_settings.cpp:1531', 'editor_settings.cpp:1532'),
                                  ('_EDITOR_GET', 'other_function'),
                                  ('not instantiated yet', 'not found'),
                                  ('   at: ', 'at: ')]:
            with self.subTest(changed=changed):
                self.rejected(self.recorded.replace(original, changed))
        self.rejected(self.recorded.replace(handoff.ANDROID_SHUTDOWN[1] + '\n', ''))

    def test_repeated_signature_or_diagnostic_from_preparation_remains_fatal(self):
        self.rejected(self.recorded.replace(self.diagnostic, self.diagnostic * 2))
        self.rejected(self.diagnostic + self.recorded)
        without = self.recorded.replace(self.diagnostic, '')
        self.rejected(without.replace(handoff.EDITOR_START, self.diagnostic + handoff.EDITOR_START))

    def test_every_other_error_warning_and_build_failure_remains_fatal(self):
        for diagnostic in ['ERROR: Missing resource', 'SCRIPT ERROR: Parse failed',
                           'WARNING: Orphan node', 'error CS0001: compiler error',
                           'warning CS0001: compiler warning', 'Build FAILED', 'Failed to build']:
            with self.subTest(diagnostic=diagnostic):
                self.rejected(diagnostic + '\n' + self.recorded)
                self.rejected(self.recorded.replace(self.diagnostic, diagnostic + '\n' + self.diagnostic))
                self.rejected(self.recorded.replace(handoff.HANDOFF_OK, diagnostic + '\n' + handoff.HANDOFF_OK))


if __name__ == '__main__':
    unittest.main()
