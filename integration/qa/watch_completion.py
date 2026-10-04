#!/usr/bin/env python3
"""Native completion/replay controls in owned normal-mode slots, plus fresh resume and preview."""
import json
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    executable = os.environ.get("GODOT_MONO")
    if not executable:
        raise SystemExit("Set GODOT_MONO to the official Godot .NET executable.")
    story = json.loads((project / "Content/story.json").read_text())
    final_beat = story["chapters"][-1]["scenes"][-1]["beats"][-1]["id"]
    evidence = project / "artifacts" / "watch-completion"
    evidence.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="lanternwake-completion-") as temporary:
        root = Path(temporary)
        (root / "owned-fixture").touch()
        environment = os.environ.copy()
        for key, child in [("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
            environment[key] = str(root / child)
        environment["LANTERNWAKE_PUMAS_MODEL"] = ""
        environment["LANTERNWAKE_COMPLETION_FIXTURE"] = str(root)
        for mode in ("complete", "resume", "preview"):
            environment["LANTERNWAKE_COMPLETION_MODE"] = mode
            args = [executable, "--headless", "--path", str(project), "res://qualification/watch-completion.tscn"]
            if mode == "preview":
                args += ["--", "--author-preview-beat", final_beat]
            before = {p.relative_to(root): hashlib.sha256(p.read_bytes()).hexdigest() for p in (root / "data").rglob("*.json")}
            with (evidence / (mode + ".log")).open("wb") as log:
                result = subprocess.run(args, cwd=project, env=environment, stdout=log, stderr=subprocess.STDOUT, timeout=300)
            output = (evidence / (mode + ".log")).read_text(errors="replace")
            if result.returncode or "\nERROR:" in "\n" + output or "LANTERNWAKE_WATCH_COMPLETION_OK" not in output:
                raise RuntimeError(f"Native {mode} failed; inspect {evidence / (mode + '.log')}")
            marker = next(line for line in output.splitlines() if line.startswith("LANTERNWAKE_WATCH_COMPLETION_OK"))
            if mode == "preview":
                after = {p.relative_to(root): hashlib.sha256(p.read_bytes()).hexdigest() for p in (root / "data").rglob("*.json")}
                if before != after:
                    raise RuntimeError("Author preview changed player save bytes.")
            print(marker, flush=True)
    print("PASS owned native completion, replay, fresh-process completed-save resume and author-preview isolation. Headless controls; no human appearance, hearing, model or microphone claim.")


if __name__ == "__main__":
    main()
