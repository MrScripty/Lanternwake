#!/usr/bin/env python3
"""Full native story with every authored optional suggestion; empty-model fallback only."""
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
        raise SystemExit("Official GODOT_MONO and POSIX process groups required.")
    story = json.loads((root / "Content/story.json").read_text())
    chapters = [[b for scene in c["scenes"] for b in scene["beats"]] for c in story["chapters"]]
    before = fingerprint(root)
    evidence = root / "artifacts" / "optional-conversations"
    evidence.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    results = []
    with tempfile.TemporaryDirectory(prefix="lanternwake-optional-conversations-") as temporary:
        owned = Path(temporary)
        (owned / "owned-fixture").touch()
        environment = os.environ.copy()
        for key, child in [("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
            environment[key] = str(owned / child)
        environment["LANTERNWAKE_PUMAS_MODEL"] = ""
        environment.pop("LANTERNWAKE_PUMAS_URL", None)
        environment["LANTERNWAKE_QUALIFICATION_ROOT"] = str(owned)
        for chapter in range(len(chapters) + 1):
            environment["LANTERNWAKE_QUALIFICATION_CHAPTER"] = str(chapter)
            path = evidence / f"chapter-{chapter + 1}.log"
            with path.open("wb") as log:
                process = subprocess.Popen([executable, "--headless", "--path", str(root),
                                            "res://qualification/optional-conversations.tscn"], cwd=root, env=environment,
                                           stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
                try:
                    status = process.wait(timeout=300)
                finally:
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    process.wait(timeout=10)
            lines = path.read_text(errors="replace").splitlines()
            prefix = "LANTERNWAKE_OPTIONAL_CONVERSATION_OK "
            markers = [line[len(prefix):] for line in lines if line.startswith(prefix)]
            if status != 0 or any(line.startswith("ERROR:") for line in lines) or len(markers) != 1:
                raise RuntimeError(f"Optional chapter {chapter + 1} failed, exit={status}; inspect {path}")
            result = json.loads(markers[0])
            beats = chapters[chapter] if chapter < len(chapters) else []
            chats = [b for b in beats if b.get("conversation")]
            choices = [f"{b['id']}:{index}" for b in chats for index in range(len(b["conversation"]["suggestions"]))]
            if (result["chapter"] != chapter or result["visited"] != [b["id"] for b in beats]
                    or result["conversations"] != [b["id"] for b in chats] or result["choices"] != choices
                    or result["submissions"] != sum(2 * len(b["conversation"]["suggestions"]) + 4 for b in chats)):
                raise RuntimeError(f"Canonical optional coverage mismatch: {chapter + 1}")
            if fingerprint(root) != before:
                raise RuntimeError("Optional workflow changed tracked source.")
            results.append(result)
            print(f"PASS fresh process {chapter + 1}: {len(result['visited'])} beats, {len(chats)} chats, "
                  f"{len(choices)} choices, {result['submissions']} authored fallback replies, history={result['history']}; Quit exit=0", flush=True)
    summary = {"beats": sum(len(r["visited"]) for r in results),
               "conversationPoints": sum(len(r["conversations"]) for r in results),
               "authoredChoices": sum(len(r["choices"]) for r in results),
               "submissions": sum(r["submissions"] for r in results), "loads": sum(r["loads"] for r in results),
               "cancels": sum(r["cancels"] for r in results), "emptyNoops": sum(r["noops"] for r in results),
               "budgetTrims": sum(r["budgetTrims"] for r in results), "checks": sum(r["checks"] for r in results),
               "nativeProcesses": len(results), "completedHistory": results[-1]["history"],
               "seconds": round(time.monotonic() - started, 2),
               "storySha256": hashlib.sha256((root / "Content/story.json").read_bytes()).hexdigest()}
    if not results[-1]["terminal"] or summary["budgetTrims"] == 0:
        raise RuntimeError("Terminal/serialized Unicode budget coverage missing.")
    (evidence / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    print("PASS native authored optional conversations " + json.dumps(summary), flush=True)
    print("No live provider, microphone or audio. Headless callbacks are not human accessibility or reading-duration acceptance.")


if __name__ == "__main__":
    main()
