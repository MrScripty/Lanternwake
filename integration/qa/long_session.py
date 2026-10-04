#!/usr/bin/env python3
"""Six fresh native processes, actual normal slot policy in disposable XDG storage."""
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import tempfile
import time

from non_audio_flows import fingerprint


def main():
    root = Path(__file__).resolve().parents[2]
    executable = os.environ.get("GODOT_MONO")
    if not executable or os.name != "posix":
        raise SystemExit("Set GODOT_MONO to the official .NET engine; this runner requires POSIX process groups.")
    story = json.loads((root / "Content/story.json").read_text())
    expected = [[beat["id"] for scene in chapter["scenes"] for beat in scene["beats"]]
                for chapter in story["chapters"]]
    evidence = root / "artifacts" / "long-session"
    evidence.mkdir(parents=True, exist_ok=True)
    before = fingerprint(root)
    started = time.monotonic()
    results = []
    with tempfile.TemporaryDirectory(prefix="lanternwake-long-session-") as temporary:
        owned = Path(temporary)
        (owned / "owned-fixture").touch()
        environment = os.environ.copy()
        for key, child in [("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
            environment[key] = str(owned / child)
        environment["LANTERNWAKE_PUMAS_MODEL"] = ""
        environment["LANTERNWAKE_QUALIFICATION_ROOT"] = str(owned)
        for chapter in range(len(expected) + 1):
            environment["LANTERNWAKE_QUALIFICATION_CHAPTER"] = str(chapter)
            path = evidence / f"chapter-{chapter + 1}.log"
            with path.open("wb") as log:
                process = subprocess.Popen([executable, "--headless", "--path", str(root),
                                            "res://qualification/long-session.tscn"], cwd=root, env=environment,
                                           stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
                try:
                    status = process.wait(timeout=300)
                finally:
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    process.wait(timeout=10)
            output = path.read_text(errors="replace")
            prefix = "LANTERNWAKE_LONG_SESSION_CHAPTER_OK "
            markers = [line[len(prefix):] for line in output.splitlines() if line.startswith(prefix)]
            if status != 0 or any(line.startswith("ERROR:") for line in output.splitlines()) or len(markers) != 1:
                raise RuntimeError(f"Chapter {chapter + 1} failed, exit={status}; inspect {path}")
            result = json.loads(markers[0])
            if result["chapter"] != chapter or result["visited"] != (expected[chapter] if chapter < len(expected) else []):
                raise RuntimeError(f"Canonical chapter sequence mismatch: {chapter + 1}")
            if fingerprint(root) != before:
                raise RuntimeError(f"Native workflow changed tracked source: chapter {chapter + 1}")
            results.append(result)
            print(f"PASS fresh process {chapter + 1}: {len(result['visited'])} beats, {result['resumes']} resumes, "
                  f"{result['wrongAnswers']} wrong-answer retries, terminal={result['terminal']}; actual Quit exit=0", flush=True)
    summary = {"beats": sum(len(r["visited"]) for r in results),
               "scenes": len(set(s for r in results for s in r["scenes"])),
               "checks": sum(r["checks"] for r in results), "resumes": sum(r["resumes"] for r in results),
               "wrongAnswers": sum(r["wrongAnswers"] for r in results),
               "recoveries": sum(r["recoveries"] for r in results),
               "rollbackRecoveries": sum(r["rollbackRecoveries"] for r in results),
               "nativeProcesses": len(results), "seconds": round(time.monotonic() - started, 2),
               "storySha256": hashlib.sha256((root / "Content/story.json").read_bytes()).hexdigest()}
    activities = sum("activity" in beat and beat["activity"] is not None
                     for chapter in story["chapters"] for scene in chapter["scenes"] for beat in scene["beats"])
    if summary["wrongAnswers"] != activities or summary["recoveries"] != activities or not results[-1]["terminal"]:
        raise RuntimeError("Incomplete activity/recovery/terminal coverage.")
    (evidence / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    print("PASS native offline complete save/resume path " + json.dumps(summary), flush=True)
    print("Headless callbacks and accelerated reading only; not human-duration, physical input, inference or audio acceptance.")


if __name__ == "__main__":
    main()
