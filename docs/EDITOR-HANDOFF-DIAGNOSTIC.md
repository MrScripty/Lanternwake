# Native editor handoff: tracked Godot 4.6.3 diagnostic

## What failed

[Main run 37700304364, job 113061894984](https://github.com/MrScripty/Lanternwake/actions/runs/37700304364/job/113061894984)
ran commit `1d653ba884a65569800b0b90e79cdfca2b0c66f1`. The isolated launcher prepared
the checkout offline, Godot completed its native .NET build, and the probe verified
that the changed source produced a newer `Lanternwake.dll`. The editor exited zero.
After native build completion, it nevertheless printed:

```text
ERROR: EditorSettings not instantiated yet when getting setting "export/android/android_sdk_path".
   at: _EDITOR_GET (editor/settings/editor_settings.cpp:1531)
```

The strict log scan rejected that output, so the later game checks were never run.
The preceding procfs-test fix is unrelated to this native engine diagnostic.
`integration/launch/fixtures/godot463_editor_shutdown.txt` preserves the probe's
stdout from this job, with GitHub timestamps and ANSI colors removed and trailing
whitespace trimmed.

## Engine evidence and limits

- [Godot PR 116515](https://github.com/godotengine/godot/pull/116515) documents this
  Android `EditorSettings` diagnostic and spurious CI failures. Its guard checks
  whether settings exist when the polling thread first enters.
- In the [4.6.3 Android exporter](https://github.com/godotengine/godot/blob/4.6.3-stable/platform/android/export/export_plugin.cpp),
  `_check_for_changes_poll_thread` calls `get_adb_path()` inside its loop, before
  checking whether a runnable Android preset exists. The getter reads
  `export/android/android_sdk_path` through `EDITOR_GET`. The exporter destructor
  requests the thread to stop and joins it.
- [EditorNode's destructor](https://github.com/godotengine/godot/blob/4.6.3-stable/editor/editor_node.cpp)
  destroys `EditorSettings`; the Android poll only checks for that singleton
  once at thread entry, not on later iterations. These lifecycle operations and
  the post-build log placement are consistent with a shutdown race. The exact
  thread/reference-destruction order in the failing process has not been traced
  with an instrumented engine, so this is an upstream-race diagnosis rather
  than proof of a particular interleaving.
- [Godot PR 116548](https://github.com/godotengine/godot/pull/116548) changes Android
  polling to run only with a runnable Android export preset. That follow-up was
  targeted at 4.7. We have not changed or qualified a new engine version here.

No supported 4.6.3 command-line or editor setting was found that stops this poll.
There is no Android export preset in this checkout; removing presets or changing
`ANDROID_HOME` still leaves the settings read. A shutdown sleep or extra frames
would only change timing. Disabling error printing would hide unrelated failures.

## Bounded qualification policy

Only `integration/launch/editor_handoff.py` classifies this diagnostic. It does
not repair Godot or claim warning-free engine shutdown. The original output is
always printed intact; a recognized occurrence also emits an explicit GitHub
Actions warning and points to this note.

The classification requires all of the following:

1. Probe exit zero, its exact full changed-DLL success marker once at the end,
   and zero requests to the blocking HTTP/HTTPS proxy.
2. The final editor stage identifies the exact pinned official binary as
   `4.6.3.stable.mono.official.7d41c59c4`. The workflow retains its official
   archive SHA-256 check and the launcher's version/feed checks.
3. Native `dotnet_build_project` completion occurs once in that stage before
   the diagnostic.
4. Exactly the two lines above occur immediately before the probe success
   marker, with no other error, warning, compiler diagnostic, or build failure
   anywhere in the captured preparation/editor output.

Changed engine hashes, source locations, messages, repeated occurrences,
preparation-stage errors, unsuccessful rebuilds, and network attempts fail
closed. The real editor rebuild, deliberately changed source, empty profile and
package cache, selected SDK, and blocked-network test remain in place.

This exception is intentionally unavailable to the production launcher, runtime
smoke tests, editor authoring tests, and the workflow's separate official Editor
solution-build check. An error in those checks still fails qualification.

## Verification and removal

Run the classifier and failure-custody regressions with:

```sh
python3 -m unittest discover -s integration/launch -v
python3 integration/launch/editor_handoff.py --godot /path/to/Godot.NET --dotnet /path/to/dotnet
```

The recorded CI fixture provides deterministic regression evidence. Negative
tests exercise each gate, additional errors/warnings, altered signatures,
duplicate diagnostics, and preserved raw output/CI warning reporting. A clean
native run does not prove that the intermittent engine race is gone.

Revisit and remove this classifier when changing the engine pin. Qualify the
upstream lifecycle fix with the same real changed-source rebuild and offline
handoff, then restore unconditional rejection of this diagnostic. Do not expand
the recognized version/signature merely to make a new failure green.
