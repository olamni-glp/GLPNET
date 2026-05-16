"""US2 — planning-frontier advance (chain A→B→C).

Maps to spec US2 (Independent Test + AC1/AC2/AC3) and SC-002. T025/T027.
Uses the existing ``_mk_chain_subtree`` (c→b→a; a is the leaf — exactly
the spec's "A no deps, B→A, C→B" with A=lib/a.dart). Gated by
``@needs_bridge``.
"""

from __future__ import annotations

import json
from pathlib import Path

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def _ready(repo_root: Path) -> list[str]:
    proc = run_codeconv(repo_root, "planagents", "next", "--json")
    assert proc.returncode == 0, proc.stdout + proc.stderr
    return [r["path"] for r in json.loads(_extract_json(proc.stdout))["batch"]]


def _setup(discover_repo: Path):
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )


@needs_bridge
def test_run1_selects_only_a(discover_repo: Path) -> None:
    """US2 AC1: empty dart_plans ⇒ only A plan-ready (B,C not)."""
    _setup(discover_repo)
    assert _ready(discover_repo) == ["lib/a.dart"]


@needs_bridge
def test_a_in_progress_keeps_b_blocked(discover_repo: Path) -> None:
    """US2 AC2: A plan-started (not completed) ⇒ B still NOT plan-ready."""
    _setup(discover_repo)
    assert run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart",
        "--no-tombstone-update",
    ).returncode == 0
    assert _ready(discover_repo) == []  # A in progress; B not unblocked


@needs_bridge
def test_frontier_advances_a_then_b_then_c(discover_repo: Path) -> None:
    """US2 AC3 + Independent Test: complete A ⇒ B ready (C not);
    complete B ⇒ C ready."""
    _setup(discover_repo)
    for f in ("lib/a.dart",):
        run_codeconv(
            discover_repo, "planagents", "plan-started", f,
            "--no-tombstone-update",
        )
        run_codeconv(
            discover_repo, "planagents", "plan-completed", f,
            "--no-tombstone-update",
        )
    assert _ready(discover_repo) == ["lib/b.dart"]  # B now ready, not C

    run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/b.dart",
        "--no-tombstone-update",
    )
    run_codeconv(
        discover_repo, "planagents", "plan-completed", "lib/b.dart",
        "--no-tombstone-update",
    )
    assert _ready(discover_repo) == ["lib/c.dart"]  # C now ready


@needs_bridge
def test_sc002_no_cross_scc_edge_planned_before_dep(
    discover_repo: Path,
) -> None:
    """SC-002: SQL self-join over dart_imports × dart_plans ×
    dart_depgraph proves no cross-SCC (A→B) had A planned before B
    plan_completed, after a full pass."""
    _setup(discover_repo)
    # Plan the whole chain in dependency order (a, then b, then c).
    for f in ("lib/a.dart", "lib/b.dart", "lib/c.dart"):
        run_codeconv(
            discover_repo, "planagents", "plan-started", f,
            "--no-tombstone-update",
        )
        run_codeconv(
            discover_repo, "planagents", "plan-completed", f,
            "--no-tombstone-update",
        )

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(
        acquire_or_discover(discover_repo, ready_timeout=30.0)
    )
    with eng.connect() as conn:
        # For every cross-SCC edge (A→B), A's plan_started_at must be
        # >= B's plan_completed_at (B was planned-completed first).
        bad = conn.execute(
            text(
                "SELECT COUNT(*) "
                "FROM codeconv.dart_imports i "
                "JOIN codeconv.dart_depgraph da ON da.path = i.from_path "
                "JOIN codeconv.dart_depgraph db ON db.path = i.to_path "
                "JOIN codeconv.dart_plans pa ON pa.path = i.from_path "
                "JOIN codeconv.dart_plans pb ON pb.path = i.to_path "
                "WHERE da.cycle_group_id <> db.cycle_group_id "
                "  AND ("
                "      pb.plan_completed_at IS NULL "
                "      OR pa.plan_started_at < pb.plan_completed_at"
                "  )"
            )
        ).scalar()
    assert bad == 0, f"SC-002 violation: {bad} cross-SCC edges out of order"
