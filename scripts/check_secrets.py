#!/usr/bin/env python3
"""Reject OpenRouter credentials in tracked or otherwise commit-ready files.

Only filenames are reported. Ignored runtime/toolchain files are not scanned.
This complements OS credential storage; it is not a general secret detector.
"""
from pathlib import Path
import re
import subprocess
import sys

KEY = re.compile(rb"sk-or-v1-[A-Za-z0-9_-]{24,}")


def contains_key(path: Path) -> bool:
    with path.open("rb") as source:
        tail = b""
        while chunk := source.read(65536):
            data = tail + chunk
            if KEY.search(data):
                return True
            tail = data[-128:]
    return False


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    names = subprocess.check_output(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=root
    ).decode("utf-8").split("\0")
    found = sorted({name for name in names if name and (root / name).is_file()
                    and not (root / name).is_symlink() and contains_key(root / name)})
    if found:
        print("OpenRouter credential detected. Remove it from these files before committing:", file=sys.stderr)
        for name in found:
            print(name, file=sys.stderr)
        return 1
    print("PASS repository OpenRouter credential check.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
