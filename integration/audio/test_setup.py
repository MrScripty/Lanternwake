"""Setup custody/idempotency; fixtures are the real generated assets from normal setup."""
import contextlib
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

PROJECT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(PROJECT / 'scripts'))
from setup_audio import ensure_assets


class AudioSetup(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = PROJECT / 'Assets' / 'Audio'
        self.manifest = json.loads((self.source / 'manifest.json').read_text())
        shutil.copyfile(self.source / 'manifest.json', self.root / 'manifest.json')
        self.calls = 0

    def render(self, destination):
        self.calls += 1
        for name in self.manifest:
            shutil.copyfile(self.source / (name + '.wav'), destination / (name + '.wav'))
        return self.manifest

    def setup(self, **kwargs):
        with contextlib.redirect_stdout(io.StringIO()):
            ensure_assets(self.root, renderer=kwargs.pop('renderer', self.render), **kwargs)

    def test_missing_assets_and_idempotent_repeat(self):
        self.setup()
        before = {p.name: (p.read_bytes(), p.stat().st_mtime_ns) for p in self.root.glob('*.wav')}
        self.assertEqual(len(before), 7)
        self.setup()
        self.assertEqual(self.calls, 1)
        self.assertEqual(before, {p.name: (p.read_bytes(), p.stat().st_mtime_ns) for p in self.root.glob('*.wav')})
        (self.root / 'harbor.wav').unlink()
        self.setup()
        self.assertEqual(self.calls, 2)
        self.assertEqual(before['archive.wav'], ((self.root / 'archive.wav').read_bytes(), (self.root / 'archive.wav').stat().st_mtime_ns))

    def test_custom_audio_requires_explicit_regeneration(self):
        path = self.root / 'harbor.wav'
        path.write_bytes(b'custom audio to preserve')
        with self.assertRaisesRegex(ValueError, 'was modified'):
            self.setup()
        self.assertEqual(self.calls, 0)
        self.assertEqual(path.read_bytes(), b'custom audio to preserve')
        self.setup(regenerate=True)
        self.assertEqual(path.read_bytes(), (self.source / 'harbor.wav').read_bytes())

    def test_failed_candidate_does_not_replace_existing(self):
        path = self.root / 'harbor.wav'
        path.write_bytes(b'preserve during failed generation')
        def fail(destination):
            self.render(destination)
            raise OSError('fixture generation failure')
        with self.assertRaisesRegex(OSError, 'fixture generation failure'):
            self.setup(regenerate=True, renderer=fail)
        self.assertEqual(path.read_bytes(), b'preserve during failed generation')
        self.assertFalse(list(self.root.glob('.audio-build-*')))
        self.assertEqual(len(list(self.root.glob('*.wav'))), 1)

    def test_unverified_candidate_is_not_published(self):
        def corrupt(destination):
            result = self.render(destination)
            (destination / 'archive.wav').write_bytes(b'wrong')
            return result
        with self.assertRaisesRegex(ValueError, 'bytes failed'):
            self.setup(renderer=corrupt)
        self.assertFalse(list(self.root.glob('*.wav')))
        self.assertFalse(list(self.root.glob('.audio-build-*')))


if __name__ == '__main__':
    unittest.main()
