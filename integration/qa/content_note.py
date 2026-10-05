#!/usr/bin/env python3
"""Title/settings content note in private normal, fresh-resume and author-preview processes."""
import hashlib
import json
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
    chat = next(b["id"] for c in story["chapters"] for s in c["scenes"] for b in s["beats"] if b.get("conversation"))
    evidence = project / "artifacts" / "content-note"
    evidence.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="lanternwake-content-note-") as temporary:
        root = Path(temporary)
        (root / "owned-fixture").touch()
        environment = os.environ.copy()
        for key, child in [("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
            environment[key] = str(root / child)
        environment["LANTERNWAKE_PUMAS_MODEL"] = ""
        environment["LANTERNWAKE_CONTENT_NOTE_FIXTURE"] = str(root)
        for mode in ("complete", "resume", "preview"):
            environment["LANTERNWAKE_CONTENT_NOTE_MODE"] = mode
            args = [executable, "--headless", "--path", str(project), "res://qualification/content-note.tscn"]
            if mode == "preview":
                args += ["--", "--author-preview-beat", chat]
            before = {p.relative_to(root): hashlib.sha256(p.read_bytes()).hexdigest() for p in (root / "data").rglob("*.json")}
            log_path = evidence / (mode + ".log")
            with log_path.open("wb") as log:
                result = subprocess.run(args, cwd=project, env=environment, stdout=log, stderr=subprocess.STDOUT, timeout=120)
            output = log_path.read_text(errors="replace")
            if result.returncode or "\nERROR:" in "\n" + output or "LANTERNWAKE_CONTENT_NOTE_OK" not in output:
                raise RuntimeError(f"Native {mode} failed; inspect {log_path}")
            if mode in ("resume", "preview"):
                after = {p.relative_to(root): hashlib.sha256(p.read_bytes()).hexdigest() for p in (root / "data").rglob("*.json")}
                if before != after:
                    raise RuntimeError(f"{mode} changed player save bytes.")
            print(next(line for line in output.splitlines() if line.startswith("LANTERNWAKE_CONTENT_NOTE_OK")), flush=True)
    print("PASS native content-note controls/copy/keyboard scrolling and save isolation. Headless; no physical input, human appearance, accessibility-tool, provider or ASR claim.")


if __name__ == "__main__":
    main()
