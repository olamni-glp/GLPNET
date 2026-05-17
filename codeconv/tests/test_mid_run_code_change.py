"""T057 [US1] — E4 remedy: mid-run code-change semantics (R13).

DBOS replays a recovered workflow against the CURRENT code: completed
steps are NOT re-run; remaining steps run new code. The contract is
*deterministic and documented* — the builder records ``code_version``
(git HEAD at launch) in ``builder_runs`` so a behaviour-changing edit is
visible in trace, and the operator opts into ``--restart-run`` (explicit,
non-default) rather than an implicit restart.
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


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


@needs_bridge
def test_code_version_recorded_and_restart_is_opt_in(
    discover_repo: Path,
) -> None:
    sub = mk_nfile_subtree(discover_repo, n=20)
    migrate_discover_depgraph(discover_repo, sub)

    r1 = last_json(builder_run(discover_repo, timeout=600.0))
    assert r1["outcome"] == "completed", r1

    from sqlalchemy import text

    with _engine(discover_repo).connect() as conn:
        row = conn.execute(
            text(
                "SELECT code_version, outer_workflow_id "
                "FROM codeconv.builder_runs "
                "WHERE outer_workflow_id = :o"
            ),
            {"o": r1["outer_workflow_id"]},
        ).first()
    assert row is not None, "builder_runs row missing"
    # code_version is the git HEAD at launch (R13 visibility). It may be
    # None only outside a git repo; this repo IS git ⇒ a 40-char sha.
    assert row[0] and len(row[0]) >= 7, (
        f"code_version not recorded for R13 mid-run-change visibility: {row[0]!r}"
    )

    # A plain re-run RECOVERS the same run against current code
    # (completed steps not re-run — deterministic, not a fresh restart).
    r_plain = last_json(builder_run(discover_repo, timeout=300.0))
    assert r_plain["outer_workflow_id"] == r1["outer_workflow_id"]
    assert r_plain["restart"] is False

    # --restart-run is the EXPLICIT, non-default opt-in for a fresh epoch.
    r_restart = last_json(
        builder_run(discover_repo, "--restart-run", timeout=600.0)
    )
    assert r_restart["restart"] is True
    assert r_restart["outer_workflow_id"] != r1["outer_workflow_id"]
