"""End-to-end ``plan-started`` / ``plan-completed`` lifecycle tests.

Maps to ``specs/017-conversion-plan-agents/contracts/
planagents_cli.md`` § ``plan-started`` / ``plan-completed`` +
``planagents_schema.md`` write protocol (US1 AC3 / SC-003). T021.
Gated by ``@needs_bridge``.
"""

from __future__ import annotations

import json
from pathlib import Path

from codeconv.tools.discover.tombstone import read_tombstone

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


def _plan_row(repo_root: Path, path: str):
    from sqlalchemy import text

    with _engine(repo_root).connect() as conn:
        return conn.execute(
            text(
                "SELECT plan_started_at, plan_completed_at, "
                "sha256_of_dart_at_plan_start, plan_path, "
                "open_escalation_count FROM codeconv.dart_plans "
                "WHERE path = :p"
            ),
            {"p": path},
        ).first()


def _count(repo_root: Path) -> int:
    from sqlalchemy import text

    with _engine(repo_root).connect() as conn:
        return conn.execute(
            text("SELECT COUNT(*) FROM codeconv.dart_plans")
        ).scalar()


def _setup(discover_repo: Path):
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )


@needs_bridge
def test_plan_started_then_completed_happy_path(
    discover_repo: Path,
) -> None:
    _setup(discover_repo)
    p = run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart", "--json"
    )
    assert p.returncode == 0, p.stderr
    s = json.loads(_extract_json(p.stdout))
    assert s["action"] == "started"
    row = _plan_row(discover_repo, "lib/a.dart")
    assert row is not None and row[0] is not None and row[1] is None

    p = run_codeconv(
        discover_repo, "planagents", "plan-completed", "lib/a.dart",
        "--plan-path", ".codeconv/conversion-plans/lib/a.dart.md",
        "--escalations", "2", "--json",
    )
    assert p.returncode == 0, p.stderr
    s = json.loads(_extract_json(p.stdout))
    assert s["action"] == "completed"
    assert s["conversion_blocked"] is True  # escalations > 0 (FR-017)
    row = _plan_row(discover_repo, "lib/a.dart")
    assert row[1] is not None  # completed_at
    assert row[3] == ".codeconv/conversion-plans/lib/a.dart.md"
    assert row[4] == 2  # open_escalation_count


@needs_bridge
def test_plan_started_idempotent_no_dup_row(discover_repo: Path) -> None:
    """FR-014: re-plan-started warns, creates no duplicate row."""
    _setup(discover_repo)
    run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart",
        "--no-tombstone-update",
    )
    before = _count(discover_repo)
    p = run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart", "--json"
    )
    assert p.returncode == 0
    s = json.loads(_extract_json(p.stdout))
    assert s["action"] == "noop"
    assert "already started" in s["warning"]
    assert _count(discover_repo) == before


@needs_bridge
def test_plan_completed_before_started_exits_2(
    discover_repo: Path,
) -> None:
    """No auto-create: plan-completed on a never-started file ⇒ exit 2."""
    _setup(discover_repo)
    proc = run_codeconv(
        discover_repo, "planagents", "plan-completed", "lib/a.dart"
    )
    assert proc.returncode == 2, proc.stdout + proc.stderr
    assert "plan-started" in (proc.stdout + proc.stderr).lower()


@needs_bridge
def test_plan_completed_idempotent_warns(discover_repo: Path) -> None:
    _setup(discover_repo)
    run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart",
        "--no-tombstone-update",
    )
    run_codeconv(
        discover_repo, "planagents", "plan-completed", "lib/a.dart",
        "--no-tombstone-update",
    )
    p = run_codeconv(
        discover_repo, "planagents", "plan-completed", "lib/a.dart", "--json"
    )
    assert p.returncode == 0
    assert "already completed" in json.loads(_extract_json(p.stdout))["warning"]


@needs_bridge
def test_plan_started_unknown_path_exits_2(discover_repo: Path) -> None:
    _setup(discover_repo)
    proc = run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/nope.dart"
    )
    assert proc.returncode == 2, proc.stdout + proc.stderr


@needs_bridge
def test_tombstone_keys_round_trip(discover_repo: Path) -> None:
    """The four plan-state keys are stamped + null-vs-missing correct."""
    _setup(discover_repo)
    # Before any plan: no plan keys on the tombstone.
    fm0 = read_tombstone(
        discover_repo / ".codeconv" / "tombstones" / "lib" / "a.dart.md"
    )
    for k in (
        "plan_started_at",
        "plan_completed_at",
        "plan_path",
        "open_escalation_count",
    ):
        assert k not in fm0

    run_codeconv(discover_repo, "planagents", "plan-started", "lib/a.dart")
    fm1 = read_tombstone(
        discover_repo / ".codeconv" / "tombstones" / "lib" / "a.dart.md"
    )
    assert fm1.get("plan_started_at")  # present + non-null
    assert "plan_completed_at" not in fm1  # not written by plan-started

    run_codeconv(
        discover_repo, "planagents", "plan-completed", "lib/a.dart",
        "--plan-path", ".codeconv/conversion-plans/lib/a.dart.md",
        "--escalations", "1",
    )
    fm2 = read_tombstone(
        discover_repo / ".codeconv" / "tombstones" / "lib" / "a.dart.md"
    )
    assert fm2.get("plan_started_at")  # survived the rewrite
    assert fm2.get("plan_completed_at")  # present + non-null
    assert fm2.get("plan_path") == ".codeconv/conversion-plans/lib/a.dart.md"
    assert fm2.get("open_escalation_count") == 1
    # Feature-015 keys (if any) must not be clobbered — order preserved
    # because _FIELD_ORDER is append-only.
