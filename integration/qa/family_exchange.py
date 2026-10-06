#!/usr/bin/env python3
"""Native authored intentions, paused reading and legacy save compatibility."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    project = Path(__file__).resolve().parents[2]
    executable = os.environ["GODOT_MONO"]
    evidence = project / "artifacts/family-exchange"
    evidence.mkdir(parents=True, exist_ok=True)
    for mode in ("player", "preview", "preview-mode-mismatch"):
        with tempfile.TemporaryDirectory(prefix="lanternwake-family-exchange-") as temporary:
            root = Path(temporary)
            (root / "owned-fixture").touch()
            environment = os.environ.copy()
            for key, child in [("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
                environment[key] = str(root / child)
            expected = "player" if mode == "player" else "preview"
            environment.update(LANTERNWAKE_PUMAS_MODEL="", LANTERNWAKE_FAMILY_EXCHANGE_FIXTURE=str(root),
                               LANTERNWAKE_FAMILY_EXCHANGE_EXPECTED_MODE=expected)
            saves = root / "data/godot/app_userdata/Lanternwake"
            saves.mkdir(parents=True)
            shutil.copy2(project / "tests/Fixtures/family-exchange/target.json", saves / "save.json")
            def slots():
                return {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
                        for p in (root / "data").rglob("*.json")}
            before = slots()
            args = [executable, "--headless", "--path", str(project), "res://qualification/family-exchange.tscn"]
            if mode == "preview":
                args += ["--", "--author-preview-beat", "ch4_s1a_b035"]
            log_path = evidence / (mode + ".log")
            with log_path.open("wb") as log:
                result = subprocess.run(args, cwd=project, env=environment, stdout=log, stderr=subprocess.STDOUT, timeout=120)
            output = log_path.read_text(errors="replace")
            if mode == "preview-mode-mismatch":
                # Intentionally omit preview args while the native fixture expects preview.
                if (result.returncode == 0 or "Family exchange launch mode does not match the expected test mode." not in output
                        or "LANTERNWAKE_FAMILY_EXCHANGE_OK" in output or slots() != before):
                    raise RuntimeError(f"Mismatched preview mode was accepted or wrote slots; inspect {log_path}")
                print("PASS mismatched preview launch rejected before player slot writes.", flush=True)
                continue
            if result.returncode or "\nERROR:" in "\n" + output or "LANTERNWAKE_FAMILY_EXCHANGE_OK" not in output:
                raise RuntimeError(f"Native {mode} failed; inspect {log_path}")
            markers = [line for line in output.splitlines() if line.startswith("LANTERNWAKE_FAMILY_EXCHANGE_OK ")]
            if len(markers) != 1 or json.loads(markers[0].split(" ", 1)[1]).get("preview") is not (mode == "preview"):
                raise RuntimeError(f"Wrong native launch mode marker for {mode}; inspect {log_path}")
            if mode == "preview" and slots() != before:
                raise RuntimeError("Author preview changed owned player slot bytes.")
            print(markers[0], flush=True)
    print("PASS native authored exchange. Owned headless input; no live-provider or human-duration claim.")


if __name__ == "__main__":
    main()
