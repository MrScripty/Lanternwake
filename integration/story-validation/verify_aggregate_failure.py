#!/usr/bin/env python3
"""Check aggregate failure propagation with real validation and disposable input."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    root = Path(__file__).resolve().parents[2]
    dotnet = shutil.which("dotnet")
    if dotnet is None or not os.environ.get("GODOT_MONO"):
        raise SystemExit("Set PATH to .NET and GODOT_MONO to the provisioned .NET Godot.")
    core = ["run", "--project", "tests/Lanternwake.Tests.csproj", "--", "Content/story.json"]
    validation = ["run", "--project", "integration/story-validation/StoryValidation.csproj", "--", "Content/story.json"]
    with tempfile.TemporaryDirectory(prefix="lanternwake-aggregate-probe-") as temporary:
        directory = Path(temporary)
        fixture = json.loads((root / "Content/story.json").read_text(encoding="utf-8"))
        fixture["chapters"][0]["title"] = None
        (directory / "invalid-story.json").write_text(json.dumps(fixture), encoding="utf-8")
        wrapper = directory / "dotnet"
        wrapper.write_text('''#!/usr/bin/env python3
import json, os, subprocess, sys
from pathlib import Path
directory = Path(os.environ["LANTERNWAKE_VERIFY_PROBE"])
arguments = sys.argv[1:]
with (directory / "calls.jsonl").open("a", encoding="utf-8") as log:
    log.write(json.dumps(arguments) + "\\n")
if arguments == ["run", "--project", "tests/Lanternwake.Tests.csproj", "--", "Content/story.json"]:
    # Only this earlier gate is simulated, so the invalid fixture reaches validation.
    sys.exit(0)
if arguments == ["run", "--project", "integration/story-validation/StoryValidation.csproj", "--", "Content/story.json"]:
    arguments[-1] = str(directory / "invalid-story.json")
    result = subprocess.run([os.environ["LANTERNWAKE_VERIFY_DOTNET"], *arguments])
    code = result.returncode if result.returncode >= 0 else 128 - result.returncode
    (directory / "validator-status.txt").write_text(str(code), encoding="utf-8")
    sys.exit(code)
sys.exit("Unexpected downstream dotnet call: " + repr(arguments))
''', encoding="utf-8")
        wrapper.chmod(0o700)
        environment = os.environ.copy()
        environment["PATH"] = str(directory) + os.pathsep + environment["PATH"]
        environment["LANTERNWAKE_VERIFY_PROBE"] = str(directory)
        environment["LANTERNWAKE_VERIFY_DOTNET"] = dotnet
        result = subprocess.run(["bash", "scripts/verify.sh"], cwd=root, env=environment,
                                text=True, capture_output=True, timeout=120)
        calls = [json.loads(line) for line in (directory / "calls.jsonl").read_text().splitlines()]
        status_path = directory / "validator-status.txt"
        if not status_path.exists():
            raise SystemExit("Aggregate did not execute the real validation harness: " + result.stderr)
        validator_status = int(status_path.read_text())
        if validator_status == 0 or result.returncode != validator_status or calls != [core, validation]:
            raise SystemExit(f"Failure propagation mismatch: aggregate={result.returncode}, validator={validator_status}, calls={calls}")
        if "title requires nonblank text" not in result.stdout + result.stderr:
            raise SystemExit("Actual invalid-story diagnostic was not observed.")
        print(f"PASS aggregate invoked real story validation and propagated status {validator_status}; no downstream dotnet checks ran.")
        print("Earlier core gate simulated only for this isolated failure probe; canonical input unchanged.")


if __name__ == "__main__":
    main()
