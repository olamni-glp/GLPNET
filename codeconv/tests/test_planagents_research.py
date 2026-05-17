"""US5 — separate research sub-agent delegation (auditable).

The research sub-agent is spawned by the SKILL.md orchestration loop
(a Claude Code harness capability, not the Python CLI — R1/R5). Per
plan §Testing the *deterministic, testable* surface is the artefact
§4 contract + the research-failure-as-escalation behaviour. This test
drives a mocked planning agent emitting the three §4 variants from
``agent_orchestration.md`` § "Research sub-agent prompt contract":

- (a) no-research file ⇒ §4 == "none required" (US5 AC1);
- (b) research-needed ⇒ §4 carries findings + provenance + the
  VERBATIM external request, and the plan body cites them (US5 AC2);
- (c) research-fail ⇒ an open `### E… research unavailable`
  escalation, the file is `planned` (not stalled), no guessed
  mapping (Clarification Q6 / R10 edge case).

T039. Gated by ``@needs_bridge``.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.planagents.artefact import (
    count_open_escalations,
    validate,
)

from .conftest import needs_bridge, run_codeconv
from .test_depgraph_compute import _migrate_and_discover


_NO_RESEARCH = """---
path: lib/nr.dart
cycle_group_id: {cg}
scc_siblings: []
generated_at: 2026-05-16T00:00:00Z
source_sha256: s
schema_version: 1
---

# Conversion Plan: lib/nr.dart

## 1. Source Analysis
Self-contained; no external library.

## 2. Dart → C#/.NET Conversion Plan
1:1 port (referenced convention).

## 3. Decomposed Task Units
- T1: port. DoD: compiles.

## 4. Research Findings
none required

## 5. Consistency Pass
Consistent.

## 6. Escalations
None.
"""

_WITH_RESEARCH = """---
path: lib/wr.dart
cycle_group_id: {cg}
scc_siblings: []
generated_at: 2026-05-16T00:00:00Z
source_sha256: s
schema_version: 1
---

# Conversion Plan: lib/wr.dart

## 1. Source Analysis
Uses external Dart library `pkg:foo` behaviour Z.

## 2. Dart → C#/.NET Conversion Plan
Map Z to the .NET equivalent per the research finding in §4 (cited:
RF-1) — NOT re-derived inline.

## 3. Decomposed Task Units
- T1: port using RF-1's mapping. DoD: compiles.

## 4. Research Findings
RF-1 (provided by the SEPARATE research sub-agent):
- **Finding**: `pkg:foo` behaviour Z maps to .NET `System.Bar`.
- **Provenance**: https://example.invalid/foo-docs "Foo docs §Z"
- **Verbatim external request(s)**:
  > WebSearch: "Dart pkg:foo behaviour Z idiomatic C# .NET equivalent"

## 5. Consistency Pass
§2 cites RF-1; consistent (no inline research — FR-009 honoured).

## 6. Escalations
None.
"""

_RESEARCH_FAILED = """---
path: lib/rf.dart
cycle_group_id: {cg}
scc_siblings: []
generated_at: 2026-05-16T00:00:00Z
source_sha256: s
schema_version: 1
---

# Conversion Plan: lib/rf.dart

## 1. Source Analysis
Needs external info on `pkg:bar` behaviour W.

## 2. Dart → C#/.NET Conversion Plan
The W-dependent mapping is DEFERRED to E1 (NOT guessed — Clarification
Q6); the rest of the surface is mapped best-effort.

## 3. Decomposed Task Units
- T1: port the non-W surface. DoD: compiles minus W.

## 4. Research Findings
Research requested for `pkg:bar` behaviour W; the separate research
sub-agent returned nothing usable (timeout). Recorded as E1.

## 5. Consistency Pass
W mapping unresolved ⇒ ESCALATED → §6 (best-effort completion;
no silent guess — R10).

## 6. Escalations
### E1: research unavailable for pkg:bar behaviour W
- **File(s)**: lib/rf.dart
- **Observed**: research unavailable for pkg:bar behaviour W (research
  sub-agent timed out / returned nothing usable).
- **Why not pre-specified+incremental**: unwritten mapping requiring
  external research; not verbatim-derivable.
- **Decision required**: supply the .NET equivalent for pkg:bar
  behaviour W (or approve a substitute).
- **Status**: open
"""


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


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


def _mk(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    for n in ("nr", "wr", "rf"):
        (sub / "lib" / f"{n}.dart").write_text(
            f"/// {n}.\nclass {n.upper()} {{}}\n", encoding="utf-8"
        )
    return sub


def _plan(repo_root: Path, path: str, art_text: str, escalations: int):
    assert run_codeconv(
        repo_root, "planagents", "plan-started", path
    ).returncode == 0
    art = repo_root / ".codeconv" / "conversion-plans" / (path + ".md")
    art.parent.mkdir(parents=True, exist_ok=True)
    art.write_text(
        art_text.format(cg=_cg(repo_root, path)), encoding="utf-8"
    )
    assert validate(art).ok, validate(art).errors
    assert run_codeconv(
        repo_root, "planagents", "plan-completed", path,
        "--plan-path", f".codeconv/conversion-plans/{path}.md",
        "--escalations", str(escalations),
    ).returncode == 0
    return art


def _setup(discover_repo: Path):
    sub = _mk(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )


@needs_bridge
def test_no_research_file_records_none_required(discover_repo: Path) -> None:
    """US5 AC1: no external research ⇒ §4 == 'none required'."""
    _setup(discover_repo)
    art = _plan(discover_repo, "lib/nr.dart", _NO_RESEARCH, 0)
    txt = art.read_text(encoding="utf-8")
    assert "## 4. Research Findings\nnone required" in txt
    assert count_open_escalations(art) == 0


@needs_bridge
def test_research_needed_embeds_findings_provenance_verbatim_request(
    discover_repo: Path,
) -> None:
    """US5 AC2: §4 has findings + provenance + the VERBATIM external
    request; the plan body cites them rather than re-deriving."""
    _setup(discover_repo)
    art = _plan(discover_repo, "lib/wr.dart", _WITH_RESEARCH, 0)
    txt = art.read_text(encoding="utf-8")
    assert "RF-1" in txt
    assert "Provenance" in txt and "example.invalid" in txt
    assert "Verbatim external request" in txt
    assert 'WebSearch: "Dart pkg:foo behaviour Z' in txt
    # §2 cites RF-1 (not re-derived inline).
    assert "per the research finding in §4 (cited:\nRF-1)" in txt or (
        "cited: RF-1" in txt.replace("\n", " ")
    )


@needs_bridge
def test_research_failure_is_open_escalation_not_stalled(
    discover_repo: Path,
) -> None:
    """Edge (Clarification Q6 / R10): research fail ⇒ open escalation,
    file `planned` (NOT stalled plan_in_progress), no guessed mapping."""
    _setup(discover_repo)
    art = _plan(discover_repo, "lib/rf.dart", _RESEARCH_FAILED, 1)
    assert count_open_escalations(art) == 1

    from sqlalchemy import text

    with _engine(discover_repo).connect() as conn:
        row = conn.execute(
            text(
                "SELECT plan_completed_at, open_escalation_count "
                "FROM codeconv.dart_plans WHERE path = 'lib/rf.dart'"
            )
        ).first()
    assert row[0] is not None, (
        "research-fail file must be `planned` (best-effort), NOT stalled "
        "plan_in_progress (Clarification Q6)"
    )
    assert row[1] == 1
    # The aggregated report surfaces it for the engineer.
    assert run_codeconv(
        discover_repo, "planagents", "aggregate-escalations"
    ).returncode == 0
    report = (
        discover_repo / ".codeconv" / "conversion-plans"
        / "_escalations-report.md"
    ).read_text(encoding="utf-8")
    assert "research unavailable for pkg:bar behaviour W" in report
