#!/usr/bin/env python3
"""Canonical cup transitions, private fresh-save resumes and selected author previews."""
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def hashes(root):
    return {p.relative_to(root): hashlib.sha256(p.read_bytes()).hexdigest() for p in (root / "data").rglob("*.json")}


def main():
    project = Path(__file__).resolve().parents[2]
    executable = os.environ.get("GODOT_MONO")
    if not executable:
        raise SystemExit("Set GODOT_MONO to the official Godot .NET executable.")
    evidence = project / "artifacts" / "cup-states"
    evidence.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="lanternwake-cup-states-") as temporary:
        fixture = Path(temporary)

        def run(root, mode, label, beat=None):
            root.mkdir(exist_ok=True)
            (root / "owned-fixture").touch()
            env = os.environ.copy()
            for key, child in [("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
                env[key] = str(root / child)
            env.update(LANTERNWAKE_PUMAS_MODEL="", LANTERNWAKE_CUP_FIXTURE=str(root), LANTERNWAKE_CUP_MODE=mode)
            args = [executable, "--headless", "--path", str(project), "res://qualification/cup-states.tscn"]
            if beat:
                args += ["--", "--author-preview-beat", beat]
            before = hashes(root)
            log_path = evidence / (label + ".log")
            with log_path.open("wb") as log:
                result = subprocess.run(args, cwd=project, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=240)
            output = log_path.read_text(errors="replace")
            if result.returncode or "\nERROR:" in "\n" + output or "LANTERNWAKE_CUP_STATES_OK" not in output:
                raise RuntimeError(f"Native {label} failed; inspect {log_path}")
            if mode != "complete" and before != hashes(root):
                raise RuntimeError(f"{label} changed player save bytes.")
            print(next(line for line in output.splitlines() if line.startswith("LANTERNWAKE_CUP_STATES_OK")), flush=True)

        normal = fixture / "normal"
        run(normal, "complete", "complete")
        relative = Path((normal / "userdata-relative.txt").read_text())
        if relative.is_absolute() or ".." in relative.parts:
            raise RuntimeError("Native userdata escaped owned fixture.")
        for state, beat in [("prebreak", "ch2_s5_b011"), ("broken", "ch2_s5_b012"), ("boxed", "ch2_s5_b026"), ("mug", "ch3_s5_b001")]:
            root = fixture / ("resume-" + state)
            shutil.copytree(normal / "saved" / state, root / relative)
            run(root, "resume", "resume-" + state)
            preview = fixture / ("preview-" + state)
            shutil.copytree(normal / "saved" / state, preview / relative)
            run(preview, "preview", "preview-" + state, beat)
    print("PASS native cup boundaries, recovery/rollback, later visits, replay and four fresh-save/author-preview states. Headless scene visibility; no human art, hearing, provider or ASR acceptance.")


if __name__ == "__main__":
    main()
