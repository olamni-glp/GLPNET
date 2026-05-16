"""US4 — escalation of non-incremental gaps + aggregated report.

Maps to spec US4 AC1/AC2/AC3, SC-005, FR-016/FR-017. T035. Gated by
``@needs_bridge``. The mocked planning agent emits (a) a
pre-specified-incremental FIXED gap (no escalation) and (b) a
non-incremental ESCALATION; the test asserts the escalate-don't-guess
discipline + the aggregated report + conversion-gating.
"""

from __future__ import annotations

import json
from pathlib import Path

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover


# (a) file with a pre-specified+incremental fixed gap ⇒ NO escalation.
_ART_FIXED = """---
path: lib/fixed.dart
cycle_group_id: {cg}
scc_siblings: []
generated_at: 2026-05-16T00:00:00Z
source_sha256: s1
schema_version: 1
---

# Conversion Plan: lib/fixed.dart

## 1. Source Analysis
`class Fixed {{}}` — trivial.

## 2. Dart → C#/.NET Conversion Plan
`class Fixed` → `public class Fixed` (1:1).

## 3. Decomposed Task Units
- T1: port. DoD: compiles.

## 4. Research Findings
none required

## 5. Consistency Pass
Gap: Dart `class` vs C# class visibility — fixed (pre-specified,
incremental) — derived from the referenced Dart→C#/.NET convention
(public top-level class). No escalation.

## 6. Escalations
None.
"""

# (b) file using an unmapped Dart construct ⇒ open escalation, no guess.
_ART_ESCALATED = """---
path: lib/escal.dart
cycle_group_id: {cg}
scc_siblings: []
generated_at: 2026-05-16T00:00:00Z
source_sha256: s2
schema_version: 1
---

# Conversion Plan: lib/escal.dart

## 1. Source Analysis
Uses a Dart construct with no pre-specified C#/.NET mapping.

## 2. Dart → C#/.NET Conversion Plan
Public surface mapped; the unmapped construct is DEFERRED to E1 (not
guessed).

## 3. Decomposed Task Units
- T1: port the mapped surface. DoD: compiles minus the deferred bit.

## 4. Research Findings
none required

## 5. Consistency Pass
Gap: no pre-specified C#/.NET mapping for the construct ⇒ ESCALATED →
see §6 (NOT guessed — DISCIPLINE §1.2/§1.10).

## 6. Escalations
### E1: no pre-specified mapping for Dart construct X
- **File(s)**: lib/escal.dart
- **Observed**: source uses construct X with no written C#/.NET mapping.
- **Why not pre-specified+incremental**: language-semantics judgement;
  not verbatim-derivable from spec / a referenced 012/015 contract /
  a written project convention.
- **Decision required**: choose the C#/.NET equivalent for construct X.
- **Status**: open
"""


def _mk(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "fixed.dart").write_text(
        "/// Fixed.\nclass Fixed {}\n", encoding="utf-8"
    )
    (sub / "lib" / "escal.dart").write_text(
        "/// Escal.\nclass Escal {}\n", encoding="utf-8"
    )
    return sub


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


def _setup(discover_repo: Path):
    sub = _mk(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )


def _cg(repo_root: Path, path: str) -> int:
    from sqlalchemy import text

    with _engine(repo_root).connect() as conn:
        return conn.execute(
            text(
                "SELECT cycle_group_id FROM codeconv.dart_depgraph "
                "WHERE path = :p"
            ),
            {"p": path},
        ).scalar()


def _plan(repo_root: Path, path: str, art_text: str, escalations: int):
    assert run_codeconv(
        repo_root, "planagents", "plan-started", path
    ).returncode == 0
    art = (
        repo_root / ".codeconv" / "conversion-plans"
        / (path + ".md")
    )
    art.parent.mkdir(parents=True, exist_ok=True)
    art.write_text(
        art_text.format(cg=_cg(repo_root, path)), encoding="utf-8"
    )
    assert run_codeconv(
        repo_root, "planagents", "plan-completed", path,
        "--plan-path", f".codeconv/conversion-plans/{path}.md",
        "--escalations", str(escalations),
    ).returncode == 0


@needs_bridge
def test_fixed_gap_no_escalation_escalated_gap_recorded(
    discover_repo: Path,
) -> None:
    """US4 AC1+AC2: fixed gap ⇒ count 0, no escalation; unmapped gap ⇒
    open E1, count 1, file still `planned`, no silent guess (SC-005)."""
    _setup(discover_repo)
    _plan(discover_repo, "lib/fixed.dart", _ART_FIXED, 0)
    _plan(discover_repo, "lib/escal.dart", _ART_ESCALATED, 1)

    from sqlalchemy import text

    with _engine(discover_repo).connect() as conn:
        rows = dict(
            conn.execute(
                text(
                    "SELECT path, open_escalation_count "
                    "FROM codeconv.dart_plans"
                )
            ).all()
        )
        # Both completed (planned for the frontier — FR-017).
        completed = conn.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.dart_plans "
                "WHERE plan_completed_at IS NOT NULL"
            )
        ).scalar()
        # Conversion-blocked query (FR-017 index): only escal.dart.
        blocked = [
            r[0]
            for r in conn.execute(
                text(
                    "SELECT path FROM codeconv.dart_plans "
                    "WHERE open_escalation_count > 0"
                )
            ).all()
        ]
    assert rows["lib/fixed.dart"] == 0
    assert rows["lib/escal.dart"] == 1
    assert completed == 2  # both planned (US4 AC3 — frontier advances)
    assert blocked == ["lib/escal.dart"]  # only the escalated one


@needs_bridge
def test_aggregate_report_contains_only_open_escalation(
    discover_repo: Path,
) -> None:
    """FR-016: report lists (b) and NOT (a); File(s)/Observed/Why/
    Decision verbatim + back-link; zero un-escalated gaps (SC-005)."""
    _setup(discover_repo)
    _plan(discover_repo, "lib/fixed.dart", _ART_FIXED, 0)
    _plan(discover_repo, "lib/escal.dart", _ART_ESCALATED, 1)

    proc = run_codeconv(
        discover_repo, "planagents", "aggregate-escalations", "--json"
    )
    assert proc.returncode == 0, proc.stderr
    s = json.loads(_extract_json(proc.stdout))
    assert s["open_escalations_total"] == 1

    report = (
        discover_repo / ".codeconv" / "conversion-plans"
        / "_escalations-report.md"
    ).read_text(encoding="utf-8")
    assert "lib/escal.dart" in report
    assert "construct X" in report
    assert "choose the C#/.NET equivalent" in report
    assert "lib/fixed.dart" not in report  # the fixed gap is NOT escalated
    # Back-link is to `<rel>.dart.md#e<n>` per
    # conversion_plan_artefact_format.md § "Aggregated escalations report".
    assert "lib/escal.dart.md#e1" in report  # back-link


@needs_bridge
def test_escalated_file_still_unblocks_downstream_planning(
    discover_repo: Path,
) -> None:
    """US4 AC3 / FR-017: a plan completed WITH open escalations still
    counts `planned` for the PLANNING frontier (downstream may plan)."""
    sub = discover_repo / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "escal.dart").write_text(
        "/// Escal.\nclass Escal {}\n", encoding="utf-8"
    )
    (sub / "lib" / "downstream.dart").write_text(
        "/// Down.\nimport 'escal.dart';\nclass Down {}\n", encoding="utf-8"
    )
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )
    _plan(discover_repo, "lib/escal.dart", _ART_ESCALATED, 1)
    proc = run_codeconv(discover_repo, "planagents", "next", "--json")
    assert proc.returncode == 0
    paths = [
        r["path"]
        for r in json.loads(_extract_json(proc.stdout))["batch"]
    ]
    assert "lib/downstream.dart" in paths, (
        "downstream must become plan-ready even though its dep has an "
        "open escalation (FR-017 — escalations gate conversion not "
        "planning)"
    )
