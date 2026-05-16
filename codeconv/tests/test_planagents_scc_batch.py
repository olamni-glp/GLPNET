"""US3 — SCC coordinated-batch planning, against the scc_fixture.

Fixture: ``specs/017-conversion-plan-agents/scripts/scc_fixture/`` —
A->B->C->A (3-cycle) plus D->A. Maps to spec US3 AC1/AC2/AC3, the edge
case "SCC member subset already planned", and SC-006. T030. Gated by
``@needs_bridge``.
"""

from __future__ import annotations

import json
import shutil
from pathlib import Path

from codeconv.tools.planagents.artefact import validate

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json

_FIXTURE = (
    Path(__file__).resolve().parents[2]
    / "specs" / "017-conversion-plan-agents" / "scripts" / "scc_fixture"
)
_A, _B, _C, _D = "lib/A.dart", "lib/B.dart", "lib/C.dart", "lib/D.dart"

_ART = """---
path: {rel}
cycle_group_id: {cgid}
scc_siblings: {sib}
generated_at: 2026-05-16T00:00:00Z
source_sha256: stub
schema_version: 1
---

# Conversion Plan: {rel}

## 1. Source Analysis
Stub analysis of {rel}.

## 2. Dart → C#/.NET Conversion Plan
1:1 class port — derived from the referenced convention.

## 3. Decomposed Task Units
- T1: port. DoD: compiles.

## 4. Research Findings
none required

## 5. Consistency Pass
Consistent.

## 6. Escalations
None.

## 7. Cycle Siblings
{sib_notes}
"""


def _setup(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    shutil.copytree(_FIXTURE, sub)
    assert run_codeconv(repo_root, "migrate", timeout=180.0).returncode == 0
    assert run_codeconv(
        repo_root, "discover", "run", "--root", str(sub), "--json"
    ).returncode == 0
    assert run_codeconv(repo_root, "depgraph", "compute").returncode == 0
    return sub


def _next(repo_root: Path) -> dict:
    proc = run_codeconv(repo_root, "planagents", "next", "--json")
    assert proc.returncode == 0, proc.stdout + proc.stderr
    return json.loads(_extract_json(proc.stdout))


def _plan(repo_root: Path, row: dict) -> None:
    assert run_codeconv(
        repo_root, "planagents", "plan-started", row["path"]
    ).returncode == 0
    art = repo_root / Path(row["artefact"])
    art.parent.mkdir(parents=True, exist_ok=True)
    sib = row["scc_siblings"]
    notes = "\n".join(f"- {s}: shared-cycle co-dependency." for s in sib) or "- none"
    art.write_text(
        _ART.format(
            rel=row["path"], cgid=row["cycle_group_id"], sib=sib,
            sib_notes=notes,
        ),
        encoding="utf-8",
    )
    assert validate(art).ok, validate(art).errors
    assert run_codeconv(
        repo_root, "planagents", "plan-completed", row["path"],
        "--plan-path", row["artefact"],
    ).returncode == 0


@needs_bridge
def test_scc_emitted_as_one_batch_with_siblings(discover_repo: Path) -> None:
    """US3 AC1: A,B,C in ONE batch, same cycle_group_id, each lists the
    other two as scc_siblings; D NOT in the batch."""
    _setup(discover_repo)
    payload = _next(discover_repo)
    by_path = {r["path"]: r for r in payload["batch"]}
    assert {_A, _B, _C}.issubset(by_path.keys())
    assert _D not in by_path  # D not plan-ready until A/B/C planned
    cgids = {by_path[p]["cycle_group_id"] for p in (_A, _B, _C)}
    assert len(cgids) == 1, f"A/B/C must share one cycle_group_id; {cgids}"
    assert sorted(by_path[_A]["scc_siblings"]) == [_B, _C]
    assert sorted(by_path[_B]["scc_siblings"]) == [_A, _C]
    assert sorted(by_path[_C]["scc_siblings"]) == [_A, _B]


@needs_bridge
def test_each_member_artefact_has_section7(discover_repo: Path) -> None:
    _setup(discover_repo)
    payload = _next(discover_repo)
    for row in payload["batch"]:
        if row["path"] in (_A, _B, _C):
            _plan(discover_repo, row)
    root = discover_repo / ".codeconv" / "conversion-plans" / "lib"
    for name in ("A.dart.md", "B.dart.md", "C.dart.md"):
        txt = (root / name).read_text(encoding="utf-8")
        assert "## 7. Cycle Siblings" in txt
        assert validate(root / name).ok


@needs_bridge
def test_d_blocked_until_all_three_completed(discover_repo: Path) -> None:
    """US3 AC2/AC3 + SC-006: D not plan-ready until EVERY member done."""
    _setup(discover_repo)
    payload = _next(discover_repo)
    rows = {r["path"]: r for r in payload["batch"]}
    # Complete only A and B.
    _plan(discover_repo, rows[_A])
    _plan(discover_repo, rows[_B])
    nxt = _next(discover_repo)
    paths = [r["path"] for r in nxt["batch"]]
    assert _D not in paths, "D must stay blocked while C is unplanned"
    assert paths == [_C], f"only C (the un-started member) resumable; {paths}"
    # Complete C ⇒ D becomes plan-ready.
    c_row = next(r for r in nxt["batch"] if r["path"] == _C)
    _plan(discover_repo, c_row)
    final = _next(discover_repo)
    assert [r["path"] for r in final["batch"]] == [_D]


@needs_bridge
def test_partial_batch_resume_does_not_respawn_done_members(
    discover_repo: Path,
) -> None:
    """Edge 'SCC member subset already planned': A done, B in progress,
    C un-started ⇒ a re-run re-selects only C; A/B not re-emitted; D
    blocked; C resumable."""
    _setup(discover_repo)
    payload = _next(discover_repo)
    rows = {r["path"]: r for r in payload["batch"]}
    _plan(discover_repo, rows[_A])  # A completed
    assert run_codeconv(  # B started, NOT completed
        discover_repo, "planagents", "plan-started", _B
    ).returncode == 0
    nxt = _next(discover_repo)
    paths = [r["path"] for r in nxt["batch"]]
    assert paths == [_C], f"only un-started C resumable; got {paths}"
    assert _D not in paths
