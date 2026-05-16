"""US3 — SCC (cycle) handling, against the synthetic cycle fixture.

Fixture: ``specs/015-codeconv-depgraph/scripts/cycle_fixture/`` —
A->B->C->A (3-cycle) plus D->A. Maps to spec US3 acceptance scenarios
1-3 / ``data-model.md`` § 3. T027. Gated by ``@needs_bridge``.
"""

from __future__ import annotations

import json
import shutil
from pathlib import Path

from .conftest import needs_bridge, run_codeconv

_FIXTURE = (
    Path(__file__).resolve().parents[2]
    / "specs" / "015-codeconv-depgraph" / "scripts" / "cycle_fixture"
)


def _setup(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    shutil.copytree(_FIXTURE, sub)
    proc = run_codeconv(repo_root, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr
    proc = run_codeconv(
        repo_root, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc.returncode == 0, proc.stderr
    return sub


def _files(repo_root: Path) -> dict:
    payload = json.loads(
        (repo_root / ".codeconv" / "depgraph.json").read_text(
            encoding="utf-8"
        )
    )
    return {f["path"]: f for f in payload["files"]}, payload


_A, _B, _C, _D = (
    "lib/A.dart",
    "lib/B.dart",
    "lib/C.dart",
    "lib/D.dart",
)


@needs_bridge
def test_three_file_cycle_shares_one_cycle_group_id(
    discover_repo: Path,
) -> None:
    _setup(discover_repo)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    f, _ = _files(discover_repo)
    gid = {f[p]["cycle_group_id"] for p in (_A, _B, _C)}
    assert len(gid) == 1, f"A/B/C must share one cycle_group_id; got {gid}"


@needs_bridge
def test_three_file_cycle_shares_one_topo_level(
    discover_repo: Path,
) -> None:
    _setup(discover_repo)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    f, _ = _files(discover_repo)
    lvl = {f[p]["topo_level"] for p in (_A, _B, _C)}
    assert len(lvl) == 1, f"A/B/C must share one topo_level; got {lvl}"


@needs_bridge
def test_cycle_count_metric_equals_one_for_3cycle(
    discover_repo: Path,
) -> None:
    _setup(discover_repo)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    _, payload = _files(discover_repo)
    assert payload["metadata"]["cycle_count"] == 1


@needs_bridge
def test_d_above_cycle_at_higher_topo_level(discover_repo: Path) -> None:
    _setup(discover_repo)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    f, _ = _files(discover_repo)
    assert f[_D]["topo_level"] > f[_A]["topo_level"]
    assert f[_D]["cycle_group_id"] != f[_A]["cycle_group_id"]


@needs_bridge
def test_d_becomes_ready_when_all_cycle_members_converted(
    discover_repo: Path,
) -> None:
    _setup(discover_repo)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    f, _ = _files(discover_repo)
    assert f[_D]["status"] == "pending"  # cycle not converted yet
    for p in (_A, _B, _C):
        run_codeconv(discover_repo, "depgraph", "mark-started", p)
        run_codeconv(discover_repo, "depgraph", "mark-completed", p)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    f, _ = _files(discover_repo)
    assert f[_D]["status"] == "ready", (
        f"D must be ready once A/B/C are converted; got {f[_D]['status']}"
    )


@needs_bridge
def test_mark_started_on_one_cycle_member_keeps_others_ready(
    discover_repo: Path,
) -> None:
    """SCC members are ready as a batch; marking one in_progress must not
    flip the others to pending (per data-model § 3 batch semantics)."""
    _setup(discover_repo)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    f, _ = _files(discover_repo)
    # A/B/C have no SCC-external deps ⇒ ready as a batch.
    assert all(f[p]["status"] == "ready" for p in (_A, _B, _C)), {
        p: f[p]["status"] for p in (_A, _B, _C)
    }
    run_codeconv(discover_repo, "depgraph", "mark-started", _A)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    f, _ = _files(discover_repo)
    assert f[_A]["status"] == "in_progress"
    # B and C are not auto-pushed back to pending by A starting.
    assert f[_B]["status"] in ("ready", "in_progress")
    assert f[_C]["status"] in ("ready", "in_progress")
    assert f[_B]["status"] != "pending"
    assert f[_C]["status"] != "pending"
