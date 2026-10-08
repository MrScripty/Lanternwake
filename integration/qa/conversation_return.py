#!/usr/bin/env python3
"""Native Return/cancel and retired-control regressions in owned save slots."""
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
    chat_beat = next(b["id"] for c in story["chapters"] for s in c["scenes"] for b in s["beats"] if b.get("conversation"))
    evidence = project / "artifacts" / "conversation-return"
    evidence.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="lanternwake-conversation-return-") as temporary:
        root = Path(temporary)
        (root / "owned-fixture").touch()
        environment = os.environ.copy()
        for key, child in [("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
            environment[key] = str(root / child)
        environment["LANTERNWAKE_PUMAS_MODEL"] = ""
        environment["LANTERNWAKE_CONVERSATION_RETURN_FIXTURE"] = str(root)
        for mode in ("complete", "closed", "preview"):
            environment["LANTERNWAKE_CONVERSATION_RETURN_MODE"] = mode
            args = [executable, "--headless", "--path", str(project), "res://qualification/conversation-return.tscn"]
            if mode == "preview":
                args += ["--", "--author-preview-beat", chat_beat]
            before = {p.relative_to(root): hashlib.sha256(p.read_bytes()).hexdigest() for p in (root / "data").rglob("*.json")}
            log_path = evidence / (mode + ".log")
            with log_path.open("wb") as log:
                result = subprocess.run(args, cwd=project, env=environment, stdout=log, stderr=subprocess.STDOUT, timeout=300)
            output = log_path.read_text(errors="replace")
            if result.returncode or "\nERROR:" in "\n" + output or "LANTERNWAKE_CONVERSATION_RETURN_OK" not in output:
                raise RuntimeError(f"Native {mode} failed; inspect {log_path}")
            if mode == "preview":
                after = {p.relative_to(root): hashlib.sha256(p.read_bytes()).hexdigest() for p in (root / "data").rglob("*.json")}
                if before != after:
                    raise RuntimeError("Author preview changed player save bytes.")
            print(next(line for line in output.splitlines() if line.startswith("LANTERNWAKE_CONVERSATION_RETURN_OK")), flush=True)
    print("PASS native conversation Return, cancellation and closed/retired controls. Headless offline fallback; no live provider, physical input, hearing or ASR claim.")


if __name__ == "__main__":
    main()
