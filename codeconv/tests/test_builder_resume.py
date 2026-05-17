"""T024 [US1] — crash mid-step → recovery skips completed steps (FR-003).

Kill ``builder run`` mid-flight (subprocess timeout = SIGKILL-equiv),
then re-run the SAME command: it must recover the SAME deterministic
outer workflow (R9), complete, and NOT re-process from scratch — DBOS
replay skips checkpointed steps so the resumed run reaches the same
terminal state (FR-003/FR-004).
"""

from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv
from ._builder_e2e_helpers import (
    builder_run,
    last_json,
    migrate_discover_depgraph,
    mk_nfile_subtree,
)


@needs_bridge
def test_kill_midrun_then_resume_completes(discover_repo: Path) -> None:
    sub = mk_nfile_subtree(discover_repo, n=22)
    migrate_discover_depgraph(discover_repo, sub)

    # Kill mid-run: a deliberately tight timeout interrupts the durable
    # walk partway (TimeoutExpired ⇒ the process was killed mid-step).
    killed = False
    try:
        run_codeconv(discover_repo, "builder", "run", "--json", timeout=4.0)
    except subprocess.TimeoutExpired:
        killed = True
    assert killed, (
        "expected the 22-file run to still be in-flight at 4 s so we "
        "exercise mid-step recovery; it finished too fast to test resume"
    )

    # Re-run the SAME command: DBOS recovers the same deterministic
    # outer id and drives to completion (skipping checkpointed steps).
    p = builder_run(discover_repo, timeout=600.0)
    assert p.returncode == 0, f"resume failed: {p.stderr}"
    r = last_json(p)
    assert r["outcome"] == "completed", r
    assert r["units"] >= 20, r

    # A third run is a clean idempotent no-op on the same id.
    p3 = builder_run(discover_repo, timeout=300.0)
    assert p3.returncode == 0, p3.stderr
    r3 = last_json(p3)
    assert r3["outcome"] == "completed"
    assert r3["outer_workflow_id"] == r["outer_workflow_id"], (
        "resume must reuse the deterministic outer id across kills"
    )
