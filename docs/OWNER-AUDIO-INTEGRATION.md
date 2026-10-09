# Service and audio ownership

Model setup borrows the explicitly selected Pumas library's authenticated owner.
Lanternwake owns its HTTP clients and finite read-only observer process; closing
or quitting the game leaves the borrowed service running. See
[Pumas owner reuse](PUMAS-OWNER-REUSE.md) for authentication and acquisition limits.

Native authored audio has a separate lifetime. Music playback retains its
AudioStreamGenerator through a native metadata reference until the mixer releases
the playback. Pending retirement awaits the surviving SceneTree, so removing the
director does not strand its frame awaiter. Normal game Quit disables further
input, settles owned operations and retires audio before ending the tree. The
production audio retirement deadline remains two seconds.

An immediate direct SceneTree.Quit with active playback can stop the driver before
AudioServer's final playback deletion. This engine-boundary diagnostic can emit
ObjectDB exit warnings and is excluded from clean qualification. Normal Quit and
node teardown with a continuing tree must drain cleanly. See
[generator retirement](AUDIO-GENERATOR-LIFETIME.md) for the native ownership
explanation and regression commands.

The combined verification suite exercises selected-owner fixtures, native Main
startup, missing streams, location transitions, graceful Quit, director removal,
pending retirement and muted author previews. Reproduce after normal setup/build:

```sh
python3 integration/qa/pumas_owner.py
python3 integration/qa/runtime_lifecycle.py
python3 integration/qa/generator_lifetime.py
```

These checks use owned CLI/HTTP and headless audio fixtures. They establish
consumer and native lifecycle behavior, not installed producer interoperability,
model serving readiness, physical microphone operation or audible device output.
