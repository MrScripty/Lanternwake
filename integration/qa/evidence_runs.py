"""Allocate fresh retained evidence; failed and historical runs are never replaced."""
from pathlib import Path
import tempfile


def new_evidence_run(base: Path, kind: str) -> Path:
    base.mkdir(parents=True, exist_ok=True)
    run = Path(tempfile.mkdtemp(prefix=kind + '-', dir=base))
    (run / 'owned-evidence-run').touch(exist_ok=False)
    print('Evidence run:', run, flush=True)
    return run
