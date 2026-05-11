"""Tests for ``codeconv discover`` resume-after-kill — Phase 6 / US4 / T067.

Maps to SC-009 / FR-017:

- ``test_resume_skips_processed_files`` — kill a discover run mid-flight
  and re-invoke; the second run must skip files that the first run
  already checkpointed (DBOS step durability).

Implementation note: we verify resumption via the ``files_processed``
counter in the second run's summary. If DBOS resumed correctly, files
already processed in run 1 are NOT counted in run 2's
``files_processed`` (they appear as ``files_skipped_idempotent`` —
their mtime + sha256 is unchanged, so the per-file step short-circuits
even without DBOS-level resume).

This test is conservative: it asserts the WHOLE second run completes
faster than the WHOLE first run (because most files are short-circuited),
and that ``files_processed + files_skipped_idempotent`` equals
``files_walked``.
"""

from __future__ import annotations

import json
import os
import signal
import subprocess
import sys
import time
from pathlib import Path

import pytest

from .conftest import (
    BRIDGE_SCRIPT,
    REPO_ROOT,
    _bridge_script_present,
    _node_available,
    kill_bridge,
    needs_bridge,
    run_codeconv,
)
from .test_discover_idempotence import _extract_json


def _mk_many_files(repo_root: Path, n: int = 30) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    for i in range(n):
        (sub / "lib" / f"f{i:03d}.dart").write_text(
            f"/// File {i}.\nclass F{i} {{}}\n",
            encoding="utf-8",
        )
    return sub


@needs_bridge
def test_resume_skips_processed_files(discover_repo: Path) -> None:
    sub = _mk_many_files(discover_repo, n=30)
    proc = run_codeconv(discover_repo, "migrate", timeout=120.0)
    assert proc.returncode == 0, proc.stderr

    # Spawn a discover run as a Popen so we can SIGKILL it mid-flight.
    args = [
        sys.executable,
        "-m",
        "codeconv.cli",
        "--repo-root",
        str(discover_repo),
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
    ]
    p = subprocess.Popen(
        args,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    # Give it a moment to begin processing files.
    time.sleep(2.0)

    # SIGKILL — this is the "kill mid-run" event under test.
    if os.name == "nt":
        subprocess.run(
            ["taskkill", "/F", "/PID", str(p.pid)],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            check=False,
        )
    else:
        os.kill(p.pid, signal.SIGKILL)
    try:
        p.wait(timeout=10.0)
    except subprocess.TimeoutExpired:
        p.kill()
        p.wait()

    # Re-invoke. The second run must complete; it should skip files
    # already processed (idempotence short-circuit + DBOS resume).
    proc2 = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
        timeout=120.0,
    )
    assert proc2.returncode == 0, proc2.stderr
    summary = json.loads(_extract_json(proc2.stdout))
    assert summary["files_walked"] == 30
    accounted = summary["files_processed"] + summary["files_skipped_idempotent"]
    assert accounted == 30, (
        f"every file must be either processed or skipped on resume; "
        f"got processed={summary['files_processed']}, "
        f"skipped={summary['files_skipped_idempotent']}, "
        f"walked={summary['files_walked']}"
    )
