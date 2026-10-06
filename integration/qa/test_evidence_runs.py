"""Runner-level stale-artifact regressions; controlled producers, not native qualification."""
import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import character_performance as native
import character_performance_render as rendered


class EvidenceRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='lanternwake-evidence-regression-')
        self.addCleanup(self.temporary.cleanup)
        self.project = Path(self.temporary.name)
        self.base = self.project / 'artifacts/character-performance'
        self.base.mkdir(parents=True)
        binary = self.project / '.godot/mono/temp/bin/Debug/Lanternwake.dll'
        binary.parent.mkdir(parents=True)
        binary.write_bytes(b'controlled regression binary')
        self.authority = self.project / 'Xauthority'
        self.authority.touch(mode=0o600)
        self.display = self.project / 'display.json'
        self.display.write_text(json.dumps(dict(authenticated=True, tcp='disabled',
            authority=str(self.authority), display=':199', pid=123)))

    def retained_files(self):
        return {str(p.relative_to(self.base)): p.read_bytes()
                for p in self.base.rglob('*') if p.is_file()}

    def assert_preserved(self, before):
        for path, contents in before.items():
            self.assertEqual((self.base / path).read_bytes(), contents, path)

    def native_run(self, fixtures, exit_code=0):
        def produce(command, **kwargs):
            root = Path(kwargs['env']['LANTERNWAKE_PERFORMANCE_FIXTURE']) / 'fixtures'
            root.mkdir()
            for name, contents in fixtures.items():
                (root / name).write_bytes(contents)
            kwargs['stdout'].write(b'LANTERNWAKE_CHARACTER_PERFORMANCE_OK {"assertions": 74}\n')
            return subprocess.CompletedProcess(command, exit_code)
        with patch.object(native, '__file__', str(self.project / 'integration/qa/character_performance.py')), \
                patch.dict(os.environ, GODOT_MONO='controlled-producer'), \
                patch.object(native.subprocess, 'run', side_effect=produce), \
                contextlib.redirect_stdout(io.StringIO()):
            native.main()

    def rendered_run(self, extra_image=False):
        def produce(command, **kwargs):
            destination = Path(kwargs['env']['LANTERNWAKE_GRAPHICAL_OUTPUT'])
            beat = command[-1]
            (destination / (beat + '.png')).write_bytes(('current-' + beat).encode())
            if extra_image:
                (destination / 'obsolete.png').write_bytes(b'unexpected image')
            kwargs['stdout'].write(b'Compatibility llvmpipe\nLANTERNWAKE_GRAPHICAL_PROBE_OK\n')
            return subprocess.CompletedProcess(command, 0)
        with patch.object(rendered, '__file__', str(self.project / 'integration/qa/character_performance_render.py')), \
                patch.dict(os.environ, GODOT_MONO='controlled-producer'), \
                patch.object(rendered.os, 'kill'), \
                patch.object(rendered, 'fingerprint', return_value={'source': 'unchanged'}), \
                patch.object(rendered.subprocess, 'run', side_effect=produce), \
                patch('sys.argv', ['character_performance_render.py', '--display-state', str(self.display)]), \
                contextlib.redirect_stdout(io.StringIO()):
            rendered.main()

    def runs(self, kind):
        return set((self.base / 'runs').glob(kind + '-*'))

    def test_native_repeat_preserves_history_and_excludes_removed_fixture(self):
        (self.base / 'fixtures').mkdir()
        (self.base / 'fixtures/obsolete.json').write_bytes(b'legacy')
        (self.base / 'receipt.json').write_bytes(b'legacy receipt')
        self.native_run({'current.json': b'first', 'removed.json': b'first-only'})
        first = self.runs('native')
        self.assertEqual(len(first), 1)
        before = self.retained_files()
        self.native_run({'current.json': b'second'})
        second, = self.runs('native') - first
        self.assert_preserved(before)
        self.assertEqual({p.name for p in (second / 'fixtures').iterdir()}, {'current.json'})
        receipt = json.loads((second / 'receipt.json').read_text())
        self.assertEqual(receipt['fixtureSha256'],
                         {'current.json': hashlib.sha256(b'second').hexdigest()})

    def test_native_failure_retains_log_without_reusing_success_receipt(self):
        self.native_run({'current.json': b'accepted'})
        first = self.runs('native')
        before = self.retained_files()
        with self.assertRaisesRegex(RuntimeError, 'Native character performance failed'):
            self.native_run({'current.json': b'failed'}, exit_code=1)
        failed, = self.runs('native') - first
        self.assert_preserved(before)
        self.assertTrue((failed / 'native.log').is_file())
        self.assertFalse((failed / 'receipt.json').exists())

    def test_render_repeat_does_not_admit_stale_case_images(self):
        legacy = self.base / 'rendered/house-speaking'
        legacy.mkdir(parents=True)
        (legacy / 'obsolete.png').write_bytes(b'legacy screenshot')
        self.rendered_run()
        first = self.runs('rendered')
        first_run, = first
        (first_run / 'house-speaking/obsolete.png').write_bytes(b'earlier-run screenshot')
        before = self.retained_files()
        self.rendered_run()
        second, = self.runs('rendered') - first
        self.assert_preserved(before)
        receipt = json.loads((second / 'receipt.json').read_text())
        self.assertEqual(len(receipt['cases']), 9)
        for case in receipt['cases']:
            expected = case['beat'] + '.png'
            self.assertEqual(set(case['images']), {expected})
            self.assertEqual({p.name for p in (second / case['case']).glob('*.png')}, {expected})
            self.assertEqual(case['images'][expected],
                             hashlib.sha256(('current-' + case['beat']).encode()).hexdigest())

    def test_render_unexpected_current_image_rejects_run_and_preserves_previous(self):
        self.rendered_run()
        first = self.runs('rendered')
        before = self.retained_files()
        with self.assertRaisesRegex(RuntimeError, 'Unexpected or missing rendered images'):
            self.rendered_run(extra_image=True)
        failed, = self.runs('rendered') - first
        self.assert_preserved(before)
        self.assertTrue((failed / 'house-speaking/obsolete.png').is_file())
        self.assertTrue((failed / 'house-speaking/native.log').is_file())
        self.assertFalse((failed / 'receipt.json').exists())


if __name__ == '__main__':
    unittest.main()
