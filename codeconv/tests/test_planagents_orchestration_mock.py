"""Mocked-agent orchestration-loop harness (SC-001 / SC-003).

The skill's Agent-spawn is a Claude Code harness capability not
invokable from pytest; per plan §Testing the Python contract surface is
deterministic and fully testable with a STUB planning agent. This test
drives the exact loop the SKILL.md pseudocode prescribes
(``next`` → ``plan-started`` → stub writes a canned VALID artefact →
``plan-completed``) and asserts:

- exactly N artefacts + N completed ``dart_plans`` rows for N leaves;
- the loop never holds > 7 planning "agents" concurrently (the stub
  tracks a concurrency counter);
- an idempotent second run = zero new rows / artefacts and zero
  artefact-content diff except ``generated_at`` (SC-001 / SC-003).

T023. Gated by ``@needs_bridge``.
"""

from __future__ import annotations

import json
from pathlib import Path

from codeconv.tools.planagents.artefact import validate

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


_ARTEFACT_TMPL = """---
path: {rel}
cycle_group_id: {cgid}
scc_siblings: {sib}
generated_at: {gen}
source_sha256: {sha}
schema_version: 1
---

# Conversion Plan: {rel}

## 1. Source Analysis
Stub analysis of the real {rel} (canned for the orchestration harness).

## 2. Dart → C#/.NET Conversion Plan
1:1 class port — derived from the referenced Dart→C#/.NET convention.

## 3. Decomposed Task Units
- T1: port the public surface. DoD: compiles.

## 4. Research Findings
none required

## 5. Consistency Pass
§2 vs §3 consistent; no gaps.

## 6. Escalations
None.
"""


def _mk_two_leaves(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "leaf1.dart").write_text(
        "/// Leaf 1.\nclass Leaf1 {}\n", encoding="utf-8"
    )
    (sub / "lib" / "leaf2.dart").write_text(
        "/// Leaf 2.\nclass Leaf2 {}\n", encoding="utf-8"
    )
    return sub


class _ConcurrencyTracker:
    def __init__(self) -> None:
        self.live = 0
        self.peak = 0

    def __enter__(self):
        self.live += 1
        self.peak = max(self.peak, self.live)
        return self

    def __exit__(self, *exc):
        self.live -= 1


def _run_loop(repo_root: Path, tracker: _ConcurrencyTracker) -> int:
    """Drive the SKILL.md orchestration loop with a stub planning agent.

    Returns the number of tombstones planned this pass.
    """
    planned = 0
    while True:
        proc = run_codeconv(
            repo_root, "planagents", "next", "--limit", "7", "--json"
        )
        assert proc.returncode == 0, proc.stdout + proc.stderr
        payload = json.loads(_extract_json(proc.stdout))
        batch = payload["batch"]
        if not batch:
            break
        # ≤7 concurrent: process the batch, each "agent" entering the
        # tracker; the loop never exceeds 7 because next --limit 7 caps
        # singletons and the stub is synchronous (peak <= len(batch) <= 7).
        for row in batch:
            with tracker:
                assert run_codeconv(
                    repo_root,
                    "planagents",
                    "plan-started",
                    row["path"],
                ).returncode == 0
                # Stub planning agent: write a canned VALID artefact.
                art = repo_root / Path(row["artefact"])
                art.parent.mkdir(parents=True, exist_ok=True)
                art.write_text(
                    _ARTEFACT_TMPL.format(
                        rel=row["path"],
                        cgid=row["cycle_group_id"],
                        sib=row["scc_siblings"],
                        gen="2026-05-16T00:00:00Z",
                        sha="stubsha",
                    ),
                    encoding="utf-8",
                )
                assert validate(art).ok, validate(art).errors
                assert run_codeconv(
                    repo_root,
                    "planagents",
                    "plan-completed",
                    row["path"],
                    "--plan-path",
                    row["artefact"],
                    "--escalations",
                    "0",
                ).returncode == 0
                planned += 1
    return planned


@needs_bridge
def test_orchestration_plans_all_leaves_idempotently(
    discover_repo: Path,
) -> None:
    sub = _mk_two_leaves(discover_repo)
    proc = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr
    proc = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc.returncode == 0, proc.stderr
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )

    tracker = _ConcurrencyTracker()
    n = _run_loop(discover_repo, tracker)
    assert n == 2, "both leaves planned in the first pass"
    assert tracker.peak <= 7, f"concurrency cap exceeded: {tracker.peak}"

    # Exactly 2 artefacts, both structurally valid + checked-in path.
    root = discover_repo / ".codeconv" / "conversion-plans"
    arts = sorted(root.rglob("*.dart.md"))
    assert [a.name for a in arts] == ["leaf1.dart.md", "leaf2.dart.md"]
    for a in arts:
        assert validate(a).ok

    # Exactly 2 completed dart_plans rows.
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(
        acquire_or_discover(discover_repo, ready_timeout=30.0)
    )
    with eng.connect() as conn:
        rows = conn.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.dart_plans "
                "WHERE plan_completed_at IS NOT NULL"
            )
        ).scalar()
    assert rows == 2

    # Snapshot artefact bytes + row count, then idempotent second pass.
    before_digest = {a.name: a.read_bytes() for a in arts}
    with eng.connect() as conn:
        before_n = conn.execute(
            text("SELECT COUNT(*) FROM codeconv.dart_plans")
        ).scalar()

    tracker2 = _ConcurrencyTracker()
    n2 = _run_loop(discover_repo, tracker2)
    assert n2 == 0, "idempotent re-run plans zero files"

    with eng.connect() as conn:
        after_n = conn.execute(
            text("SELECT COUNT(*) FROM codeconv.dart_plans")
        ).scalar()
    assert after_n == before_n, "no duplicate dart_plans rows"
    # Artefacts untouched (the stub was never re-invoked — already
    # planned ⇒ next returns empty ⇒ loop writes nothing). SC-003.
    arts2 = sorted(root.rglob("*.dart.md"))
    assert {a.name: a.read_bytes() for a in arts2} == before_digest
