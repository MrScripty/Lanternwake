"""Checks authored bytes and elementary engineering bounds, not listening quality."""
import hashlib
import json
from pathlib import Path
import struct
import unittest
import wave

ROOT = Path(__file__).resolve().parents[2] / 'Assets' / 'Audio'


class AudioAssets(unittest.TestCase):
    def test_manifest_and_pcm(self):
        manifest = json.loads((ROOT / 'manifest.json').read_text())
        self.assertEqual(set(manifest), {'harbor', 'keeper_house', 'archive', 'lantern_room', 'tide_cave', 'watch_theme', 'bell_release'})
        for name, expected in manifest.items():
            with self.subTest(asset=name):
                path = ROOT / (name + '.wav')
                self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), expected['sha256'])
                with wave.open(str(path)) as source:
                    self.assertEqual((source.getnchannels(), source.getsampwidth(), source.getframerate()), (1, 2, 24000))
                    self.assertEqual(source.getnframes(), expected['frames'])
                    samples = struct.unpack('<' + 'h' * source.getnframes(), source.readframes(source.getnframes()))
                self.assertGreater(max(abs(s) for s in samples), 0)
                self.assertLess(max(abs(s) for s in samples), 8192)
                if expected['loop']:
                    self.assertEqual(len(samples), 384000)
                    self.assertLess(abs(samples[-1] - samples[0]), 128)
                else:
                    self.assertEqual(samples[0], 0)
                    self.assertEqual(samples[-1], 0)


if __name__ == '__main__':
    unittest.main()
