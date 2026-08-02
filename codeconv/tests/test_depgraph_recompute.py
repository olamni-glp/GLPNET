"""Tests for ``codeconv depgraph mark-and-recompute`` (feature 062, US1 / T009).

Two layers:

* **Pure** (no bridge): :func:`codeconv.tools.depgraph.subgraph.dirty_set`
  reverse-reachability — marked ∪ transitive dependents.
* **End-to-end** (``@needs_bridge``): on the A→B→C chain fixture, marking a
  node recomputes only its dirty subgraph and leaves every unmarked row
  byte-identical; unknown paths recompute nothing and exit 1 (spec Edge Cases).

Maps to ``specs/062-.../contracts/depgraph-cli.md`` § mark-and-recompute,
spec FR-001.
"""

from __future__ import annotations

import json
from pathlib import Path

from codeconv.tools.depgraph.subgraph import dirty_set

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree

# A→B→C chain: b imports a, c imports b. Edge (u, v) = u depends on v.
CHAIN_EDGES = [("lib/b.dart", "lib/a.dart"), ("lib/c.dart", "lib/b.dart")]


# ---------------------------------------------------------------------------
# Pure dirty-set logic (bridge-free)
# ---------------------------------------------------------------------------


def test_dirty_set_marks_all_transitive_dependents() -> None:
    # a is imported (transitively) by b and c → marking a dirties everything.
    assert dirty_set(["lib/a.dart"], CHAIN_EDGES) == {
        "lib/a.dart",
        "lib/b.dart",
        "lib/c.dart",
    }


def test_dirty_set_leaf_marks_only_itself() -> None:
    # Nothing depends on c → marking c dirties only c.
    assert dirty_set(["lib/c.dart"], CHAIN_EDGES) == {"lib/c.dart"}


def test_dirty_set_middle_marks_self_and_upstream_dependents() -> None:
    assert dirty_set(["lib/b.dart"], CHAIN_EDGES) == {"lib/b.dart", "lib/c.dart"}


def test_dirty_set_empty_marks_is_empty() -> None:
    assert dirty_set([], CHAIN_EDGES) == set()


def test_dirty_set_handles_cycles_without_looping() -> None:
    # Two-node cycle a↔b plus c depends on b.
    edges = [("a", "b"), ("b", "a"), ("c", "b")]
    assert dirty_set(["a"], edges) == {"a", "b", "c"}


# ---------------------------------------------------------------------------
# End-to-end (bridge-gated)
# ---------------------------------------------------------------------------


def _computed_at(repo_root: Path, path: str):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(repo_root, ready_timeout=30.0)
    engine = build_engine(endpoint)
    with engine.connect() as conn:
        return conn.execute(
            text("SELECT computed_at FROM codeconv.dart_depgraph WHERE path = :p"),
            {"p": path},
        ).scalar()


def _summary(proc) -> dict:
    return json.loads(_extract_json(proc.stdout))


@needs_bridge
def test_mark_and_recompute_touches_only_marked_subgraph(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )

    a_before = _computed_at(discover_repo, "lib/a.dart")
    b_before = _computed_at(discover_repo, "lib/b.dart")
    c_before = _computed_at(discover_repo, "lib/c.dart")

    # Mark the leaf c: nothing depends on it, so only c is dirty.
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-and-recompute", "--mark", "lib/c.dart",
        "--json",
    )
    assert proc.returncode == 0, proc.stderr
    s = _summary(proc)
    assert s["nodes_recomputed"] == 1
    assert s["nodes_preserved"] == 2
    assert s["recomputed_paths"] == ["lib/c.dart"]

    # Unmarked rows are byte-identical (computed_at untouched); c advanced.
    assert _computed_at(discover_repo, "lib/a.dart") == a_before
    assert _computed_at(discover_repo, "lib/b.dart") == b_before
    assert _computed_at(discover_repo, "lib/c.dart") >= c_before


@needs_bridge
def test_mark_and_recompute_marks_transitive_dependents(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0

    # Mark the base a: b and c depend on it (transitively) → all three dirty.
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-and-recompute", "--mark", "lib/a.dart",
        "--json",
    )
    assert proc.returncode == 0, proc.stderr
    s = _summary(proc)
    assert s["nodes_recomputed"] == 3
    assert s["nodes_preserved"] == 0
    assert s["recomputed_paths"] == ["lib/a.dart", "lib/b.dart", "lib/c.dart"]


@needs_bridge
def test_mark_and_recompute_unknown_path_recomputes_nothing_exit_1(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0

    proc = run_codeconv(
        discover_repo, "depgraph", "mark-and-recompute", "--mark", "lib/nope.dart",
        "--json",
    )
    assert proc.returncode == 1, (
        f"unknown path must exit 1; got {proc.returncode} "
        f"{proc.stdout}{proc.stderr}"
    )
    s = _summary(proc)
    assert s["nodes_recomputed"] == 0
    assert "lib/nope.dart" in s["unknown_paths"]


@needs_bridge
def test_mark_and_recompute_dry_run_writes_nothing(discover_repo: Path) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    c_before = _computed_at(discover_repo, "lib/c.dart")
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-and-recompute", "--mark", "lib/c.dart",
        "--dry-run", "--json",
    )
    assert proc.returncode == 0, proc.stderr
    assert _summary(proc)["nodes_recomputed"] == 1
    # dry-run: c's row untouched.
    assert _computed_at(discover_repo, "lib/c.dart") == c_before
