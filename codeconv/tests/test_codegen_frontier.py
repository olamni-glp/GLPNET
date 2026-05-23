"""T017 — codegen frontier: topo/SCC order + dependency-before invariant.

@needs_bridge. Mirrors ``test_planagents_frontier`` for the codegen
stage: with the c→b→a chain, ``codegen next`` selects only the leaf until
its dependency is codegen-complete (``codegen_completed_at IS NOT NULL``).
Completion is simulated by a direct ``dart_codegen`` write (the CLI route
is ``ingest`` + a passing build; here we isolate the readiness oracle —
FR-002). SC-002-style: no cross-SCC dependent is ready before its dep is
complete.
"""

from __future__ import annotations

import json
from pathlib import Path

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def _ready(repo_root: Path) -> list[str]:
    proc = run_codeconv(repo_root, "codegen", "next", "--json")
    assert proc.returncode == 0, proc.stdout + proc.stderr
    return [r["path"] for r in json.loads(_extract_json(proc.stdout))["batch"]]


def _setup(discover_repo: Path):
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0


def _mark_codegen_complete(repo_root: Path, rel: str) -> None:
    """Directly mark a file codegen-complete (build-passing accept)."""
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))
    with eng.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_codegen "
                "(path, codegen_started_at, codegen_completed_at, "
                " sha256_of_dart_at_codegen_start, target_cs_path, "
                " build_status, open_escalation_count) "
                "VALUES (:p, NOW(), NOW(), 'x', :tp, 'pass', 0) "
                "ON CONFLICT (path) DO UPDATE SET "
                "  codegen_completed_at = NOW(), build_status = 'pass'"
            ),
            {"p": rel, "tp": f"out/csharp/{rel[:-5]}.cs"},
        )


@needs_bridge
def test_run1_selects_only_leaf(discover_repo: Path) -> None:
    """Empty dart_codegen ⇒ only the leaf (lib/a.dart) is codegen-ready."""
    _setup(discover_repo)
    assert _ready(discover_repo) == ["lib/a.dart"]


@needs_bridge
def test_in_progress_dep_keeps_dependent_blocked(discover_repo: Path) -> None:
    """A codegen row with NULL completed_at does NOT unblock downstream."""
    _setup(discover_repo)
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(discover_repo, ready_timeout=30.0))
    with eng.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_codegen "
                "(path, codegen_started_at, sha256_of_dart_at_codegen_start, "
                " build_status) VALUES ('lib/a.dart', NOW(), 'x', 'not_built')"
            )
        )
    assert _ready(discover_repo) == []  # A in progress; B not unblocked


@needs_bridge
def test_frontier_advances_leaf_then_dependent(discover_repo: Path) -> None:
    """Complete A ⇒ B ready (C not); complete B ⇒ C ready (FR-002)."""
    _setup(discover_repo)
    _mark_codegen_complete(discover_repo, "lib/a.dart")
    assert _ready(discover_repo) == ["lib/b.dart"]
    _mark_codegen_complete(discover_repo, "lib/b.dart")
    assert _ready(discover_repo) == ["lib/c.dart"]


@needs_bridge
def test_no_cross_scc_dependent_ready_before_dep_complete(
    discover_repo: Path,
) -> None:
    """SC-002-style: a SQL self-join proves no cross-SCC dependent is
    codegen-ready while its dependency is not codegen-complete."""
    _setup(discover_repo)
    _mark_codegen_complete(discover_repo, "lib/a.dart")
    # B is now ready; C must NOT be (depends on B, not complete).
    assert "lib/c.dart" not in _ready(discover_repo)
