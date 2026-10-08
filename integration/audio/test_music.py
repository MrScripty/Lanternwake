"""Bundled MIDI/source custody and WAV-free music packaging."""
import contextlib
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

PROJECT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(PROJECT / 'scripts'))
import setup_music
from setup_music import ROOT, VENDOR, catalog, digest, ensure_music, inputs


class MusicAssets(unittest.TestCase):
    def test_runtime_sources_and_packaging(self):
        ensure_music()
        self.assertEqual(inputs(), json.loads((ROOT / 'manifest.json').read_text())['inputs'])
        self.assertFalse(list(ROOT.glob('*.wav')), 'music WAVs are absent from game resources')
        self.assertFalse(list(ROOT.glob('*.wav.import')))
        library = catalog()['layers']
        self.assertEqual({role: sum(layer['role'] == role for layer in library) for role in ('mood', 'environment', 'character', 'journey')},
                         {'mood': 4, 'environment': 5, 'character': 5, 'journey': 5})
        self.assertEqual(len({layer['midi'] for layer in library}), 19)
        for layer in library:
            self.assertTrue((ROOT / 'Source' / layer['midi']).read_bytes().startswith(b'MThd'))
        vendor = json.loads((VENDOR / 'UPSTREAM.json').read_text())
        for name, sha in vendor['files'].items(): self.assertEqual(digest(VENDOR / Path(name).name), sha, name)
        self.assertIn('Permission is hereby granted', (VENDOR / 'LICENSE.txt').read_text())
        self.assertIn('ProjectReference Include="ThirdParty/MeltySynth/MeltySynth.csproj"', (PROJECT / 'Lanternwake.csproj').read_text())
        scene = (PROJECT / 'Scenes/Audio/AudioDirector.tscn').read_text()
        self.assertIn('AudioStreamGenerator', scene)
        self.assertNotIn('res://Assets/Music/', scene.replace('res://Assets/Music/SaltmereScore.tres', ''))
        preset = (PROJECT / 'export_presets.cfg').read_text()
        self.assertIn('Assets/Music/Source/*.mid', preset)
        self.assertIn('Assets/Music/Source/*.sf2', preset)
        self.assertIn('Assets/Music/*.wav', preset)
        self.assertFalse((ROOT / 'Source/.gdignore').exists(), 'raw MIDI/bank must be visible to export filters')


class MusicSetup(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.project = Path(self.temporary.name)
        self.root = self.project / 'Assets/Music'
        shutil.copytree(ROOT, self.root)
        self.addCleanup(patch.stopall)
        patch.object(setup_music, 'PROJECT', self.project).start()
        patch.object(setup_music, 'ROOT', self.root).start()

    def setup(self):
        with contextlib.redirect_stdout(io.StringIO()): ensure_music()

    def test_setup_is_read_only_and_never_renders_music(self):
        before = {str(p.relative_to(self.root)): (digest(p), p.stat().st_mtime_ns) for p in self.root.rglob('*') if p.is_file()}
        with patch.object(setup_music, 'render_cache', side_effect=AssertionError('default setup must not render')):
            self.setup(); self.setup()
        self.assertEqual(before, {str(p.relative_to(self.root)): (digest(p), p.stat().st_mtime_ns) for p in self.root.rglob('*') if p.is_file()})
        self.assertFalse(list(self.root.rglob('*.wav')))

    def test_edited_midi_requires_deliberate_provenance_update(self):
        midi = self.root / 'Source/harbor_score.mid'
        original = midi.read_bytes()
        midi.write_bytes(original + b'edited')
        with self.assertRaisesRegex(ValueError, 'Music source changed'): self.setup()
        self.assertEqual(midi.read_bytes(), original + b'edited')

    def test_missing_midi_is_not_replaced_or_fabricated(self):
        midi = self.root / 'Source/harbor_score.mid'
        midi.unlink()
        with self.assertRaises(FileNotFoundError): self.setup()
        self.assertFalse(midi.exists())
