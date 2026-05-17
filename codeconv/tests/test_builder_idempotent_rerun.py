"""T025 [US1] — resumed run state == uninterrupted run (SC-002).

Two consecutive uninterrupted ``builder run``s reach the SAME terminal
state under the SAME deterministic outer id (the second is a pure
idempotent replay — DBOS returns the checkpointed result without
re-executing steps). This is the SC-002 invariant.
"""

from __future__ import annotations

from pathlib import Path

from .conftest import needs_bridge
from ._builder_e2e_helpers import (
    builder_run,
    last_json,
    migrate_discover_depgraph,
    mk_nfile_subtree,
)


@needs_bridge
def test_rerun_is_idempotent_same_state(discover_repo: Path) -> None:
    sub = mk_nfile_subtree(discover_repo, n=20)
    migrate_discover_depgraph(discover_repo, sub)

    p1 = builder_run(discover_repo, timeout=600.0)
    assert p1.returncode == 0, p1.stderr
    r1 = last_json(p1)
    assert r1["outcome"] == "completed", r1

    p2 = builder_run(discover_repo, timeout=300.0)
    assert p2.returncode == 0, p2.stderr
    r2 = last_json(p2)

    # SC-002: identical terminal outcome, identical run identity, same
    # unit count — the resumed/replayed run is indistinguishable from
    # the uninterrupted one.
    assert r2["outcome"] == r1["outcome"] == "completed"
    assert r2["outer_workflow_id"] == r1["outer_workflow_id"]
    assert r2["units"] == r1["units"]


@needs_bridge
def test_restart_run_is_explicit_new_epoch(discover_repo: Path) -> None:
    """R13: ``--restart-run`` is the ONLY way a new run epoch is minted
    (explicit, non-default); a plain re-run never does."""
    sub = mk_nfile_subtree(discover_repo, n=20)
    migrate_discover_depgraph(discover_repo, sub)

    r1 = last_json(builder_run(discover_repo, timeout=600.0))
    r_plain = last_json(builder_run(discover_repo, timeout=300.0))
    assert r_plain["outer_workflow_id"] == r1["outer_workflow_id"]

    r_restart = last_json(
        builder_run(discover_repo, "--restart-run", timeout=600.0)
    )
    assert r_restart["outer_workflow_id"] != r1["outer_workflow_id"], (
        "--restart-run must mint a NEW epoch (R13)"
    )
    assert r_restart["restart"] is True
