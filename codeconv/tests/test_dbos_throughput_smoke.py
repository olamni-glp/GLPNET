"""T054 [US1] — DBOS sustained-throughput acceptance gate (R12, E1).

**US1 is not accepted until this passes.** The plan's designated top
human-gate item: drive ≥20 files through the durable pipeline with the
default serial worker on single-writer PGLite, assert no bridge-lock
starvation (the run completes, every unit done), every step
checkpointed (a second run is an idempotent no-op — deterministic
recovery), and ``builder status`` still answers < 5 s (SC-009).
"""

from __future__ import annotations

from pathlib import Path

from .conftest import needs_bridge
from ._builder_e2e_helpers import (
    builder_run,
    last_json,
    migrate_discover_depgraph,
    mk_nfile_subtree,
    timed_status,
)


@needs_bridge
def test_sustained_throughput_22_files(discover_repo: Path) -> None:
    sub = mk_nfile_subtree(discover_repo, n=22)
    migrate_discover_depgraph(discover_repo, sub)

    # Drive the whole pipeline (serial worker, R12 concurrency=1).
    p1 = builder_run(discover_repo, timeout=600.0)
    assert p1.returncode == 0, (
        f"sustained run failed (rc={p1.returncode}) — possible "
        f"bridge-lock starvation (R12)\nSTDERR:\n{p1.stderr}"
    )
    r1 = last_json(p1)
    assert r1["outcome"] == "completed", r1
    assert r1["units"] >= 20, f"expected ≥20 units, got {r1}"

    # SC-009: status reconciles in < 5 s on the warm bridge.
    snap, dt = timed_status(discover_repo)
    assert dt < 5.0, f"builder status took {dt:.2f}s (> 5 s budget, SC-009)"
    assert snap["counts"]["total"] >= 20, snap

    # Every step checkpointed ⇒ a second run RECOVERS deterministically
    # and is an idempotent no-op (no bridge starvation on replay either).
    p2 = builder_run(discover_repo, timeout=300.0)
    assert p2.returncode == 0, p2.stderr
    r2 = last_json(p2)
    assert r2["outcome"] == "completed", r2
    assert r2["outer_workflow_id"] == r1["outer_workflow_id"], (
        "resume must reuse the deterministic outer id, not mint a new "
        f"run: {r1['outer_workflow_id']} vs {r2['outer_workflow_id']}"
    )
