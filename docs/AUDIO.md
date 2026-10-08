# Audio integration and authoring

## Adaptive Saltmere score

The former `watch_theme` tone loop is no longer assigned to the game or included in executable exports. Nineteen original
MIDI tracks now use soft acoustic piano, cello, solo violin, nylon guitar,
quiet accordion reeds, and pizzicato strings. This small ensemble follows the
story's coastal domestic detail, investigation, physical storm, grief, and
ordinary aftermath. It avoids a heroic shanty or horror soundtrack. These are
first compositions for listening review, not a claim of final mastering.

| Suite | Musical role |
| --- | --- |
| Saltmere | Arrival, practical kindness, household inventory, memory |
| Undertow | Inquiry, the receiving stylus, uncertainty, historical responsibility |
| Night Ledger | Checked readiness, storm operations, controlled release |
| Open Horizon | Repairs, living obligations, voluntary departure |

The four approved mood suites have four separately synthesized stems: **Ground** (cello and restrained
reeds), **Theme** (piano and guitar), **Bowed** (violin), and **Motion** (pizzicato
and sparse piano). All share the same 32-bar 6/8 grid, quarter-note tempo 72,
D-minor/F-major harmonic sequence, and exact 80-second length. Different melodies,
voicings, phrasing and densities distinguish the arrangements. Suite crossfades
remain harmonically compatible at any point in this shared grid.

Five additional **environment tracks** give each place an identity:

| Environment | Theme | Character of the arrangement |
| --- | --- | --- |
| Harbor | Ropes and Returning Boats | Accordion lead, plucked bass and offbeat guitar; a rolling 6/8 tune |
| Keeper house | A Room Still Used | Solo piano, high sustained melody and open left-hand intervals; spacious half-time feel |
| Archive | Source, Copy, Interpretation | Dry nylon guitar questions and short plucked replies; three straight beats against the compound meter |
| Lantern room | The Receiving Stylus | High and low pizzicato mechanisms; two-note upper pattern against three lower steps |
| Tide cave | Beneath the Postcard | Long cello and violin ribbons, slow expression swells and no rhythmic pulse |

Five **character tracks** have recognizable recurring motifs: Ada's rising
piano question (*A Hand of Her Own*), Nessa's practical reed phrase (*A Place on
the List*), Tomas's falling guitar response (*Listening for the Ordinary*),
Sera's measured pizzicato/piano figure (*A Useful Error*), and Ivo's incomplete
low-cello memory (*Affection and the Missing Margin*). Ivo's motif is underscore
for his recorded presence and responsibility; it does not add a living or
supernatural character or an actual voice recording.

Five **journey tracks** develop a shared D–E–F question through the chapters:

| Chapter | Development of the shared theme |
| --- | --- |
| I — The Inventory | Tentative piano question, ending without a firm answer |
| II — The Wrong Channel | Separated guitar fragments and more space between notes |
| III — What the Water Kept | A complete longer phrase with a bowed answering line |
| IV — The Night Ledger | Guitar, cello and restrained reeds share the phrase |
| V — An Open Horizon | Longer piano resolution and open F-major colour |

These are separate compositions, not pitch-shifted copies or the same track at
different volumes. The recurring opening pitches keep the journey recognizable.
Each place has its own lead instrument, melodic register, articulation and
perceived pulse. Their phrases unfold across the loop rather than sharing one
arpeggio template. Character motifs and the chapter theme recur over those
local arrangements; the mood beds support them. The common tempo and harmonic
clock allow smooth transitions without requiring identical musical phrasing. A
return to the same room and person retains their identity while the chapter
layer changes the meaning around them. No cue depends on the live calendar or
on guessed keywords in dialogue.

`Scenes/Audio/AudioDirector.tscn` owns one Music player with a 32 kHz
`AudioStreamGenerator`. The game bundles MeltySynth as a managed dependency and
loads the nineteen MIDI files and one shared acoustic SoundFont at startup.
All 31 logical score voices advance on the same sample clock, including
inaudible voices, so activating a character or environment preserves phrase
position and release tails. No music WAVs are loaded or required.

A dedicated worker owns synthesis and fills a bounded 4,096-frame PCM queue.
The main thread feeds Godot's generator in 512-frame batches; all Godot API calls
stay on that thread. The native output buffer is 0.25 seconds, with up to another
0.128 seconds queued by the worker. Gain changes fade from the current mix over
**Adaptive score → Music Fade Seconds**, and the renderer interpolates gains
within each audio block. Short stereo reflection buffers preserve room sound.
Playback keeps advancing through story changes and menus without restarting.
Music still reaches the Music bus for EQ, sidechain compression and voice
activity ducking. Normal startup has no full-track render or audio cache.

Playback acquisition first checks that the player has a native playback and
retains only a non-null handle. Missing streams therefore cannot put null into
per-frame retirement or tree-exit disposal. This does not replace setup: absent
location assets still report the Inspector assignment failure, with fresh-checkout
setup commands for Linux and Windows. The
native `qualification/audio-lifecycle.tscn` regression covers missing music and
effects, naturally finished effects, live loop retention, interrupted transitions,
repeat shutdown and direct tree exit. `scripts/verify.sh` runs it after the audio smoke.

An AudioEffectCapture on the real Master mixer observes nonzero bounded PCM.
This is native Godot mixing evidence, not proof of physical speaker output,
subjective sound quality, production voice recognition, model quality, or
five-hour gameplay duration. If a backend produces no capture frames, the
smoke reports PCM unavailable rather than fabricating a pass.

The decoded instrument bank is shared by every voice. Memory holds the bank,
active synth voices and small output/reflection buffers, rather than hundreds
of megabytes of full-length PCM. There is no native 32-stream slot limit now;
the practical limit is synthesis cost, which must be measured when expanding
the catalog. Shutdown stops and joins the worker before retiring the native
playback handle; callbacks after stopping cannot restart it.

`Assets/Music/SaltmereScore.tres` is the editable spotting sheet. Its Inspector
shows **Title Mix** and **Spots**. Each spot has a Scene Id, an optional Start At
Beat Id, a reusable Mix resource, **Character Focus**, and a reason for the musical choice. An empty
beat ID sets that scene's opening mix. A beat-specific mix remains active until
the next spot in actual story order. Loading, rewinding, and author preview
reconstruct the same mix from the reached beats. The score currently covers all
41 story scenes with 109 spots; newly unspotted scenes use the title mix and no character focus. The environment comes from the story's
actual location and the journey comes from its chapter; neither needs a new
spot for every repeated room. Character Focus is authored according to the
scene's subject, independently of the current speaker. **Inherit** retains the
scene's current focus; **None** removes the character motif; Ada/Nessa/Tomas/Sera/
Ivo selects that character. Beat focus changes persist and reconstruct on load,
rewind, and preview, just like mood changes.

Mix resources in `Assets/Music/Cues` expose the mood suite and its four stem levels from
zero to one, plus **Story layers → Environment / Character / Journey** gains.
**Mood Under Environment** reduces the shared mood bed while the local track
plays (default 0.30); **Environment** defaults to 0.85 so each room has an audible
identity throughout the loop, including arbitrary entry phases. **Theme Under
Character** additionally reduces the mood melody while a character motif leads.
**Silence Story Layers** reserves space across all three new roles. Edit a shared mix to change every spot that uses it, or duplicate a
mix for a local exception. `source_space.tres` deliberately silences the
underscore during selected ordinary recordings, the public statement, and the
song extracts; location ambience remains. This reserves space for future source
audio rather than substituting the underscore for the story's choir recording.
No voice, choir performance, or microphone integration is added here.

## Compose, render, and audition

The nineteen multitrack MIDI files in `Assets/Music/Source` are the authoritative
musical inputs. They can be opened in a MIDI editor or DAW. Keep their tempo,
length, harmonic grid, and channel grouping compatible when editing:

| Stem | Zero-based MIDI channels | MIDI-editor channels | Instruments |
| --- | --- | --- | --- |
| Ground | 0, 1 | 1, 2 | GM cello 42, accordion 21 |
| Theme | 2, 3 | 3, 4 | GM piano 0, nylon guitar 24 |
| Bowed | 4 | 5 | GM violin 40 |
| Motion | 5, 6 | 6, 7 | GM pizzicato strings 45, piano 0 |

Programs in this table are zero-based. The trimmed bank supplies these six presets. Each environment, character, and
journey MIDI uses channels 0–6 as one independently mixable voice. The catalog
maps MIDI files and channel groups to ordered mix voices; it no longer maps to
native WAV slots. Other instruments need a deliberately updated bank and MIDI
program assignments.

Run `bash scripts/setup.sh` before opening a fresh checkout in Godot. On Windows
without Bash, run `py -3 scripts/setup_audio.py` and `py -3 scripts/setup_music.py`.
Default setup uses Python's standard library to generate the existing ambience
and effects and verify bundled MIDI/bank/synth provenance. It does not invoke a
music renderer. The game build uses .NET 8, and its MeltySynth project reference
is compiled and published with the game, without a separate synth installation.

The authoritative MIDI files are editable in a DAW. After deliberate edits run:

```sh
python3 scripts/setup_music.py --author
```

This updates the source provenance manifest; the next game launch synthesizes
the edited MIDI directly. `scripts/compose_music.py` overwrites all nineteen
source MIDIs, `--expand` overwrites only the fifteen place/person/chapter files,
and `--places-only` overwrites just the five location arrangements. Preserve DAW
edits before recomposing.

Developer listening WAVs are optional and stay outside game assets:

```sh
python3 scripts/setup_music.py --render --regenerate
python3 scripts/preview_music.py
```

This explicitly renders into `.toolchain/music-render-cache` and writes
normalized listening files into `.toolchain/music-previews`. Normal game setup,
playback and export never depend on either cache. `--regenerate` deliberately
replaces stale audition renders; preserve custom cache files first. The optional
offline renderer uses scalar synthesis for repeatable developer comparisons.

For complete scene arrangements, retain a native `--audio-smoke` log and use
`python3 scripts/preview_music.py --mix-log <log-path>` after rendering the cache.
The reel includes all musical roles at the actual scene-target gains, with one
shared listening gain; it omits ambience and bus EQ/ducking. Hear the game for
the final routed mix.

## Packaging

`export_presets.cfg` supplies a Linux Desktop preset. Its include filters retain
the raw `.mid` and `.sf2` inputs, catalog, source manifest, character/story JSON
and both instrument/synth licences. MIDI/bank inputs must not be hidden by a
`.gdignore`. Music WAVs and developer tools/caches are excluded. The MeltySynth
assembly is a normal project dependency and is published with the .NET game.
The music source payload is roughly 5 MB, replacing about 317 MB of rendered
music. Ambience and bell effects remain normal imported audio assets.

A full desktop export needs the matching official Godot .NET templates and the
.NET runtime packages for the chosen platform. These are build prerequisites;
players receive the exported runtime, synth and music inputs together. Keep
the executable, `.pck` and `data_Lanternwake_linuxbsd_x86_64` folder together
when distributing the Linux export. The generated build stays in the ignored
`.toolchain/export` folder.
Preserve the asset include/exclude rules when adding other desktop presets. Source scans
alone are not proof of packaging: inspect the actual exported archive and run
the exported game's audio smoke after changing those rules.

## EQ and future character voices

Open Godot's **Audio** panel at the bottom of the editor. The editable effects
and starting gains live in `default_bus_layout.tres`:

- **Music → Music / leave room for speech**: ten-band EQ, initially a modest
  dip around 1–4 kHz and reduced extreme lows/highs.
- **Music → Music / Dialogue sidechain**: compressor triggered by audio on the
  Dialogue bus. Adjust its threshold, ratio, attack, and release here.
- **Dialogue → Dialogue / low cut**, then **Dialogue / presence**: starting
  voice treatment, adjustable once actual character voices exist.

`AudioDirector` also exposes **Speech clarity** in its Inspector: enable
speech ducking, music dip, additional 2/4 kHz presence dip, attack, and release.
Its default activity envelope lowers music by 3 dB and adds a 3 dB presence
pocket while a voice plays; the sidechain provides additional level-dependent
reduction. The player's Music slider stays unchanged. Muted Dialogue stops the
activity envelope from lowering music, and the mix recovers smoothly afterward.
Disable both the activity envelope and the Music sidechain effect if neither
form of voice ducking is wanted.

Future voice integration can call `AudioDirector.PlayDialogue(stream)` and
`StopDialogue()`. The authored Dialogue player routes to the Dialogue bus and
participates in native playback retirement. Moving to another beat or loading a
beat cancels stale speech. An external voice player must use **Dialogue** and
signal `SetDialogueActive(true/false)` for the activity/EQ envelope; the native
sidechain also responds directly to audio on that bus. This is an integration
hook, not a character-voice provider implementation. Voice intelligibility must
still be assessed with the eventual voices, their levels, and listening devices.

Settings → Sound settings provides Music, Location ambience, Effects, Character
voices, and master mute. Keyboard Tab chooses controls, arrow keys adjust
sliders, and Space toggles mute. Levels apply for the current process.

## Ambience and effects

The two native ambience players retain the five editable location loops and
short interruptible crossfades. Same-location beats retain the ambience
playhead. The single authored `bell_lowered` beat triggers the release effect;
loading or previewing that beat suppresses historic one-shots. `Assets/Audio`
still contains the original procedural ambience/effect source and manifest;
its unused old theme output is retained for provenance. Put custom loops outside
generated asset folders (for example `Assets/CustomAudio`) and assign them in
the AudioDirector Inspector. Runtime loop copies never mutate imported assets.

## Provenance and verification

The game vendors unmodified **MeltySynth** at commit
`4ba079c9ee1453b03be7e164d254f48426f0aac3`, under its **MIT licence**:
`ThirdParty/MeltySynth/LICENSE.txt` (also bundled as
`Assets/Music/MeltySynth-LICENSE.txt`). Upstream file hashes
are recorded beside it. [MeltySynth upstream](https://github.com/sinshu/meltysynth).

The **Saltmere Acoustic** bank is a renamed six-preset subset of S. Christian
Collins's **GeneralUser GS 2.0.3**, pinned at commit
`684543d5e5efaef08d02be50dcda8d552478fa60`. Its samples are unmodified; the SF2
index tables are rebuilt by `scripts/subset_soundfont.py`. The bank's separate
licence permits private/commercial music creation, use in software, modification,
and redistribution; it is **not MIT**. Preserve
`Assets/Music/Source/GeneralUser-GS-LICENSE.txt` and its upstream sample-origin
notes. Full-source and subset hashes are in `Saltmere-Acoustic.json`.
[GeneralUser GS upstream](https://github.com/mrbumpy409/GeneralUser-GS).
All nineteen melodies and arrangements are new project compositions, not extracted
from an existing track. The synth and SoundFont are bundled for gameplay; no external MIDI device,
synth installation or network connection is needed for music.

`bash scripts/verify.sh` verifies MIDI/bank/vendor custody, WAV-free resource
bindings, live bounded PCM, layer activation/silence, a full-score throughput
benchmark, producer/consumer shutdown, and the actual native generator output.
It exercises environment selection through real scene rendering, character
focus, chapter changes, source-space silence, fades/load reconstruction, voice
EQ/gain recovery, playback retirement and output underruns. Native scene-target
logs feed the shared live renderer to measure every location's three-second
place-to-mood RMS across the full 80-second loop, without WAV fixtures.
Editor roundtrip checks keep authoring silent and preserve the generator,
soundbank path and music controls. Export checks confirm all MIDI inputs and
licences are present and rendered music resources are absent.

Playback handles are retired only after native player/server ownership has
ended, using reference counts and a bounded two-second deadline. Engine changes
must revalidate this lifetime boundary. Tests establish engineering behavior;
physical speaker output, subjective musical quality, final mastering, and
intelligibility of future real voices require listening review.

Absent optional streams are skipped before playing, and only active, non-null
playback handles enter retirement ownership. The reported `f5d50d5` startup
failure came from an absent generated music WAV: an inactive player's null
playback was stored, then dereferenced by both processing and tree exit. The
ownership guard landed in `6a5c769` and is already present on main. Native
`audio-lifecycle.tscn` regression checks cover absent music/effects, natural
effect/dialogue completion, stream removal, interrupted ambience transitions,
repeatable shutdown, and tree exit with active or absent streams. Generate the
checkout's audio with `python3 scripts/run.py --setup-only` before normal play;
missing required ambience still produces an explicit setup diagnostic.
