#!/usr/bin/env python3
"""Owned Linux process interruption and repeated non-audio Godot workflows."""
import hashlib
import os
from pathlib import Path
import signal
import subprocess
import tempfile
import time


def fingerprint(root):
    paths = subprocess.check_output(["git", "ls-files", "-z"], cwd=root).split(b"\0")
    return {os.fsdecode(p): hashlib.sha256((root / os.fsdecode(p)).read_bytes()).hexdigest()
            for p in paths if p}


def process_identity(pid):
    try:
        fields = Path(f"/proc/{pid}/stat").read_text().rsplit(")", 1)[1].split()
        return fields[19], fields[0]  # Linux start time and state; guard against PID reuse.
    except FileNotFoundError:
        return None


def owned_children(parent):
    # Some cloud procfs mounts omit task/*/children. Use process parent metadata
    # instead, and read arguments only for children of our own Editor process.
    for path in Path("/proc").glob("[0-9]*/stat"):
        try:
            fields = path.read_text().rsplit(")", 1)[1].split()
            if int(fields[1]) == parent:
                yield int(path.parent.name)
        except (FileNotFoundError, ProcessLookupError):
            continue


def main():
    root = Path(__file__).resolve().parents[2]
    executable = os.environ.get("GODOT_MONO")
    if not executable or not Path("/proc/self/stat").exists():
        raise SystemExit("This owned-process probe requires Linux and GODOT_MONO.")
    evidence = root / "artifacts" / "non-audio-qa"
    evidence.mkdir(parents=True, exist_ok=True)
    before = fingerprint(root)
    editor_args = [executable, "--headless", "--editor", "--path", str(root), "--", "--editor-roundtrip"]
    ui_args = [executable, "--headless", "--path", str(root), "--", "--ui-smoke"]
    with tempfile.TemporaryDirectory(prefix="lanternwake-non-audio-qa-") as temporary:
        environment = os.environ.copy()
        for key, child in [("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
            environment[key] = str(Path(temporary) / child)
        environment["LANTERNWAKE_PUMAS_MODEL"] = ""
        with (evidence / "interrupted-editor.log").open("wb") as log:
            editor = subprocess.Popen(editor_args, cwd=root, env=environment, stdout=log,
                                      stderr=subprocess.STDOUT, start_new_session=True)
            observed = {}
            try:
                deadline = time.monotonic() + 30
                while editor.poll() is None and time.monotonic() < deadline:
                    for pid in owned_children(editor.pid):
                        try:
                            arguments = Path(f"/proc/{pid}/cmdline").read_bytes().split(b"\0")
                        except FileNotFoundError:
                            continue
                        identity = process_identity(pid)
                        if b"--author-preview-smoke" in arguments and identity is not None and identity[1] != "Z":
                            observed[pid] = identity[0]
                    if observed:
                        break
                    time.sleep(.01)
                if not observed:
                    raise RuntimeError(f"Could not observe an active owned selected-beat child before interruption; Editor status={editor.poll()}, log={evidence / 'interrupted-editor.log'}")
                # Deliberate hard interruption of this task's process group. This
                # checks restart/source isolation, not application graceful cleanup.
                os.killpg(editor.pid, signal.SIGKILL)
                editor.wait(timeout=10)
                deadline = time.monotonic() + 5
                while time.monotonic() < deadline:
                    active = [pid for pid, birth in observed.items()
                              if (identity := process_identity(pid)) is not None
                              and identity[0] == birth and identity[1] != "Z"]
                    if not active:
                        break
                    time.sleep(.01)
                if active:
                    raise RuntimeError(f"Owned child execution survived deliberate group interruption: {active}")
                print(f"PASS deliberate Editor/preview process-group interruption; observed children={list(observed)}, parent status={editor.returncode}")
            finally:
                try:
                    os.killpg(editor.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                editor.wait(timeout=10)
        if fingerprint(root) != before:
            raise RuntimeError("Interrupted workflow changed tracked source or canonical content.")
        editor_markers = ["LANTERNWAKE_STORY_DOCK_COLD_SELECTION_OK", "LANTERNWAKE_CHARACTER_AUTHORING_OK",
                          "LANTERNWAKE_STORY_DOCK_LAYOUT_OK", "LANTERNWAKE_STORY_DOCK_OK",
                          "LANTERNWAKE_PLAYTEST_LAUNCH_OK", "LANTERNWAKE_EDITOR_ROUNDTRIP_OK"]
        ui_markers = ["LANTERNWAKE_READING_SIZE_OK", "LANTERNWAKE_READING_CHAT_OK",
                      "LANTERNWAKE_RECOVERY_UI_OK", "LANTERNWAKE_UI_SMOKE_OK"]
        for name, arguments, markers in [("editor-1", editor_args, editor_markers),
                                         ("ui-1", ui_args, ui_markers), ("ui-2", ui_args, ui_markers),
                                         ("editor-2", editor_args, editor_markers), ("ui-3", ui_args, ui_markers)]:
            with (evidence / f"{name}.log").open("wb") as log:
                process = subprocess.Popen(arguments, cwd=root, env=environment, stdout=log,
                                           stderr=subprocess.STDOUT, start_new_session=True)
                try:
                    code = process.wait(timeout=120)
                finally:
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    process.wait(timeout=10)
            output = (evidence / f"{name}.log").read_text(errors="replace")
            if code != 0 or "\nERROR:" in "\n" + output or any(marker not in output for marker in markers):
                raise RuntimeError(f"Repeated {name} failed; inspect {evidence / (name + '.log')}")
            if fingerprint(root) != before:
                raise RuntimeError(f"Repeated {name} changed tracked source or canonical content.")
            print(f"PASS repeated {name}; exit=0; {len(markers)} markers; tracked source unchanged")
    print("PASS five clean repeated exits after interruption. Headless only; external hard interruption is not graceful application teardown evidence.")


if __name__ == "__main__":
    main()
