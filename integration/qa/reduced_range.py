#!/usr/bin/env python3
"""Native Main sound controls and deterministic mixer capture in a private profile."""
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    with tempfile.TemporaryDirectory(prefix="lanternwake-reduced-range-") as temporary:
        root = Path(temporary)
        (root / "owned-fixture").touch()
        environment = os.environ.copy()
        for key, child in [("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
            environment[key] = str(root / child)
        environment.update(LANTERNWAKE_RANGE_FIXTURE=str(root), LANTERNWAKE_PUMAS_MODEL="",
                           DBUS_SESSION_BUS_ADDRESS="unix:path=" + str(root / "no-keyring"))
        environment.pop("OPENROUTER_API_KEY", None)
        for mode, marker in [("toggle", "LANTERNWAKE_REDUCED_RANGE_OK"), ("fresh", "LANTERNWAKE_REDUCED_RANGE_FRESH_OK")]:
            environment["LANTERNWAKE_RANGE_MODE"] = mode
            before = {p.relative_to(root): p.read_bytes() for p in (root / "data").rglob("*.json")}
            result = subprocess.run([environment["GODOT_MONO"], "--headless", "--path", str(project),
                                     "res://qualification/reduced-range.tscn"], cwd=project, env=environment,
                                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT, encoding="utf-8", timeout=60)
            print(result.stdout, end="", flush=True)
            if result.returncode or any(m in result.stdout for m in ("ERROR:", "SCRIPT ERROR:", "WARNING:")) or marker not in result.stdout:
                raise RuntimeError(f"Native reduced-range {mode} failed.")
            after = {p.relative_to(root): p.read_bytes() for p in (root / "data").rglob("*.json")}
            if before != after:
                raise RuntimeError(f"Native {mode} sound settings changed saves.")
    print("PASS native Main sound controls, keyboard, session retention, fresh default, measured PCM and unchanged saves. Headless; no physical sound, human preference, provider or ASR claim.")


if __name__ == "__main__":
    main()
