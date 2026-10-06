#!/usr/bin/env python3
"""Owned late snapshots and actual Load/Save controls across three native restarts per fault."""
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import tempfile
import time

from non_audio_flows import fingerprint


def archive_fingerprint(owned):
    return {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in (owned / "archive").glob("*.json")}


def main():
    root = Path(__file__).resolve().parents[2]
    executable = os.environ.get("GODOT_MONO")
    if not executable or os.name != "posix":
        raise SystemExit("Set GODOT_MONO to the official .NET engine; POSIX process groups are required.")
    before = fingerprint(root)
    evidence = root / "artifacts" / "late-recovery"
    evidence.mkdir(parents=True, exist_ok=True)
    scenarios = [f"{slot}-{damage}" for slot in ["manual", "auto"]
                 for damage in ["missing", "corrupt", "truncated", "both-invalid", "read-error"]]
    scenarios += ["manual-captured", "all-invalid"]
    results = []
    started = time.monotonic()
    for scenario in scenarios:
        with tempfile.TemporaryDirectory(prefix="lanternwake-late-recovery-") as temporary:
            owned = Path(temporary)
            (owned / "owned-fixture").touch()
            environment = os.environ.copy()
            for key, child in [("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
                environment[key] = str(owned / child)
            environment["LANTERNWAKE_PUMAS_MODEL"] = ""
            environment["LANTERNWAKE_QUALIFICATION_ROOT"] = str(owned)
            environment["LANTERNWAKE_RECOVERY_CASE"] = scenario
            archived = None
            for phase in ["prepare", "recover", "verify"]:
                environment["LANTERNWAKE_RECOVERY_PHASE"] = phase
                path = evidence / f"{scenario}-{phase}.log"
                with path.open("wb") as log:
                    process = subprocess.Popen([executable, "--headless", "--path", str(root),
                                                "res://qualification/late-recovery.tscn"], cwd=root, env=environment,
                                               stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
                    try:
                        status = process.wait(timeout=120)
                    finally:
                        try:
                            os.killpg(process.pid, signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                        process.wait(timeout=10)
                output = path.read_text(errors="replace")
                prefix = "LANTERNWAKE_LATE_RECOVERY_OK "
                markers = [line[len(prefix):] for line in output.splitlines() if line.startswith(prefix)]
                if status != 0 or any(line.startswith("ERROR:") for line in output.splitlines()) or len(markers) != 1:
                    raise RuntimeError(f"{scenario}/{phase} failed, exit={status}; inspect {path}")
                result = json.loads(markers[0])
                if result["scenario"] != scenario or result["phase"] != phase:
                    raise RuntimeError("Fault phase identity mismatch.")
                if fingerprint(root) != before:
                    raise RuntimeError(f"Native workflow changed tracked source: {scenario}/{phase}")
                current_archive = archive_fingerprint(owned)
                if archived is not None and current_archive != archived:
                    raise RuntimeError("Intact checkpoint archive was altered by recovery.")
                archived = current_archive
                results.append(result)
                print(f"PASS {scenario}/{phase}: {result['loads']} real loads, {result['cancels']} cancels, "
                      f"history={result['history']}, real Quit exit=0", flush=True)
    summary = {"scenarios": len(scenarios), "nativeProcesses": len(results),
               "checks": sum(r["checks"] for r in results), "loads": sum(r["loads"] for r in results),
               "cancels": sum(r["cancels"] for r in results), "saveErrors": sum(r["saveErrors"] for r in results),
               "capturedSelections": sum(r["capturedSelections"] for r in results),
               "seconds": round(time.monotonic() - started, 2),
               "storySha256": hashlib.sha256((root / "Content/story.json").read_bytes()).hexdigest()}
    if summary["saveErrors"] != 2 or summary["capturedSelections"] != 1:
        raise RuntimeError("Required read-error/captured-selection coverage missing.")
    (evidence / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    print("PASS late native recovery " + json.dumps(summary), flush=True)
    print("Owned synthetic corruption and headless controls only; no human accessibility, physical input, audio or inference claim.")


if __name__ == "__main__":
    main()
