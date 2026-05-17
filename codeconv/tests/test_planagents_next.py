"""End-to-end tests for ``codeconv planagents next`` / ``status``.

Maps to ``specs/017-conversion-plan-agents/contracts/
planagents_cli.md`` § ``status`` / ``next`` (US1 AC2 / SC-002). T020.
Gated by ``@needs_bridge``.
"""

from __future__ import annotations

import json
from pathlib import Path

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def _next(repo_root: Path, *extra: str) -> dict:
    proc = run_codeconv(
        repo_root, "planagents", "next", "--json", *extra
    )
    assert proc.returncode == 0, proc.stdout + proc.stderr
    return json.loads(_extract_json(proc.stdout))


@needs_bridge
def test_depgraph_empty_exits_2(discover_repo: Path) -> None:
    """US1 AC2 / FR-018: empty dart_depgraph ⇒ exit 2, actionable."""
    proc = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr
    proc = run_codeconv(discover_repo, "planagents", "status", "--json")
    assert proc.returncode == 2, proc.stdout + proc.stderr
    assert "depgraph" in (proc.stdout + proc.stderr).lower()
    # next must also exit 2 (not a silent no-op).
    proc = run_codeconv(discover_repo, "planagents", "next", "--json")
    assert proc.returncode == 2, proc.stdout + proc.stderr


@needs_bridge
def test_leaves_are_exactly_plan_ready_on_empty_dart_plans(
    discover_repo: Path,
) -> None:
    """First wave = depgraph leaves. Chain c->b->a ⇒ only a plan-ready."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )
    payload = _next(discover_repo)
    paths = [r["path"] for r in payload["batch"]]
    assert paths == ["lib/a.dart"], paths
    assert payload["batch"][0]["scc_siblings"] == []
    assert payload["batch"][0]["cycle_group_id"] is not None
    assert payload["batch"][0]["artefact"] == (
        ".codeconv/conversion-plans/lib/a.dart.md"
    )


@needs_bridge
def test_status_counts_classify_all_non_orphaned(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )
    proc = run_codeconv(discover_repo, "planagents", "status", "--json")
    assert proc.returncode == 0, proc.stderr
    s = json.loads(_extract_json(proc.stdout))
    assert s["files_total"] == 3
    assert s["plan_ready"] == 1  # only a
    assert s["plan_pending"] == 2  # b, c
    assert s["plan_in_progress"] == 0
    assert s["planned"] == 0
    assert s["open_escalations_total"] == 0


@needs_bridge
def test_already_in_progress_excluded_from_next(
    discover_repo: Path,
) -> None:
    """FR-014: a plan-started (not completed) file is NOT re-emitted."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )
    assert run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart",
        "--no-tombstone-update",
    ).returncode == 0
    payload = _next(discover_repo)
    paths = [r["path"] for r in payload["batch"]]
    assert "lib/a.dart" not in paths  # in progress ⇒ not re-spawned
    assert paths == []  # b still pending (a not completed)


@needs_bridge
def test_limit_is_honoured(discover_repo: Path) -> None:
    sub = discover_repo / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    for i in range(10):
        (sub / "lib" / f"f{i}.dart").write_text(
            f"/// File {i}.\nclass F{i} {{}}\n", encoding="utf-8"
        )
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )
    payload = _next(discover_repo, "--limit", "4")
    assert len(payload["batch"]) == 4
    assert payload["remaining_ready"] == 6


@needs_bridge
def test_nothing_to_plan_exits_zero(discover_repo: Path) -> None:
    """FR-018: every file planned ⇒ exit 0 + 'nothing to plan'."""
    sub = discover_repo / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "solo.dart").write_text(
        "/// Solo.\nclass Solo {}\n", encoding="utf-8"
    )
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )
    assert run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/solo.dart",
        "--no-tombstone-update",
    ).returncode == 0
    assert run_codeconv(
        discover_repo, "planagents", "plan-completed", "lib/solo.dart",
        "--no-tombstone-update",
    ).returncode == 0
    proc = run_codeconv(discover_repo, "planagents", "next", "--json")
    assert proc.returncode == 0, proc.stdout + proc.stderr
    payload = json.loads(_extract_json(proc.stdout))
    assert payload["batch"] == []
    assert payload.get("message") == "nothing to plan"
