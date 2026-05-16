"""Structural validation of conversion-plan artefacts (SC-004).

Maps to ``specs/017-conversion-plan-agents/contracts/
conversion_plan_artefact_format.md`` § "Structural validation". NO
``@needs_bridge`` — ``artefact.validate`` is pure. T022.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.planagents.artefact import (
    artefact_path,
    artefact_rel_path,
    count_open_escalations,
    escalations_report_path,
    iter_open_escalations,
    validate,
)


_VALID_SINGLETON = """---
path: lib/a.dart
cycle_group_id: 3
scc_siblings: []
generated_at: 2026-05-16T12:00:00Z
source_sha256: abc123
schema_version: 1
---

# Conversion Plan: lib/a.dart

## 1. Source Analysis
Public class A; no async; no codegen.

## 2. Dart → C#/.NET Conversion Plan
`class A {}` → `public class A {}` (1:1 — referenced convention).

## 3. Decomposed Task Units
- T1: port `class A` skeleton. DoD: compiles.

## 4. Research Findings
none required

## 5. Consistency Pass
§2 vs §3 consistent; no gaps.

## 6. Escalations
None.
"""

_VALID_SCC = """---
path: lib/x.dart
cycle_group_id: 9
scc_siblings: [lib/y.dart]
generated_at: 2026-05-16T12:00:00Z
source_sha256: def456
schema_version: 1
---

# Conversion Plan: lib/x.dart

## 1. Source Analysis
X imports Y; cyclic.

## 2. Dart → C#/.NET Conversion Plan
Map X with Y co-dependency.

## 3. Decomposed Task Units
- T1: port X. DoD: compiles with Y stub.

## 4. Research Findings
none required

## 5. Consistency Pass
Consistent with sibling.

## 6. Escalations
### E1: ambiguous mixin mapping
- **File(s)**: lib/x.dart, lib/y.dart
- **Observed**: X uses a mixin with no pre-specified C#/.NET mapping.
- **Why not pre-specified+incremental**: language-semantics judgement; not verbatim-derivable.
- **Decision required**: choose interface-default vs base-class mapping.
- **Status**: open

## 7. Cycle Siblings
- lib/y.dart: shares cycle 9; the mixin decision in E1 is co-dependent.
"""


def _write(tmp_path: Path, text: str, rel: str = "lib/a.dart") -> Path:
    p = tmp_path / ".codeconv" / "conversion-plans" / (rel + ".md")
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8")
    return p


def test_valid_singleton_artefact_accepted(tmp_path: Path) -> None:
    p = _write(tmp_path, _VALID_SINGLETON)
    r = validate(p)
    assert r.ok, r.errors


def test_valid_scc_artefact_with_section7_accepted(tmp_path: Path) -> None:
    p = _write(tmp_path, _VALID_SCC, rel="lib/x.dart")
    r = validate(p)
    assert r.ok, r.errors


def test_missing_section_rejected(tmp_path: Path) -> None:
    bad = _VALID_SINGLETON.replace(
        "## 3. Decomposed Task Units\n- T1: port `class A` skeleton. DoD: compiles.\n\n",
        "",
    )
    p = _write(tmp_path, bad)
    r = validate(p)
    assert not r.ok
    assert any("Decomposed Task Units" in e for e in r.errors)


def test_out_of_order_sections_rejected(tmp_path: Path) -> None:
    # Swap §2 and §1 headings.
    bad = _VALID_SINGLETON.replace("## 1. Source Analysis", "## 1. SRC_TMP")
    bad = bad.replace(
        "## 2. Dart → C#/.NET Conversion Plan", "## 1. Source Analysis"
    )
    bad = bad.replace("## 1. SRC_TMP", "## 2. Dart → C#/.NET Conversion Plan")
    p = _write(tmp_path, bad)
    r = validate(p)
    assert not r.ok


def test_section7_without_siblings_rejected(tmp_path: Path) -> None:
    bad = _VALID_SINGLETON.rstrip() + "\n\n## 7. Cycle Siblings\n- none\n"
    p = _write(tmp_path, bad)
    r = validate(p)
    assert not r.ok
    assert any("section 7" in e for e in r.errors)


def test_scc_without_section7_rejected(tmp_path: Path) -> None:
    bad = _VALID_SCC.split("## 7. Cycle Siblings")[0].rstrip() + "\n"
    p = _write(tmp_path, bad, rel="lib/x.dart")
    r = validate(p)
    assert not r.ok
    assert any("section 7" in e for e in r.errors)


def test_malformed_escalation_missing_bullet_rejected(tmp_path: Path) -> None:
    bad = _VALID_SCC.replace(
        "- **Decision required**: choose interface-default vs base-class mapping.\n",
        "",
    )
    p = _write(tmp_path, bad, rel="lib/x.dart")
    r = validate(p)
    assert not r.ok
    assert any("Decision required" in e for e in r.errors)


def test_missing_frontmatter_key_rejected(tmp_path: Path) -> None:
    bad = _VALID_SINGLETON.replace("source_sha256: abc123\n", "")
    p = _write(tmp_path, bad)
    r = validate(p)
    assert not r.ok
    assert any("source_sha256" in e for e in r.errors)


def test_count_open_escalations(tmp_path: Path) -> None:
    assert count_open_escalations(_write(tmp_path, _VALID_SINGLETON)) == 0
    assert (
        count_open_escalations(_write(tmp_path, _VALID_SCC, rel="lib/x.dart"))
        == 1
    )


def test_iter_open_escalations_parses_fields(tmp_path: Path) -> None:
    p = _write(tmp_path, _VALID_SCC, rel="lib/x.dart")
    es = iter_open_escalations(p)
    assert len(es) == 1
    e = es[0]
    assert e["e_number"] == 1
    assert "mixin" in e["title"]
    assert "lib/x.dart" in e["files"]
    assert "language-semantics" in e["why"]
    assert "interface-default" in e["decision"]


def test_path_helpers() -> None:
    assert artefact_rel_path("lib/a.dart") == (
        ".codeconv/conversion-plans/lib/a.dart.md"
    )
    assert artefact_rel_path("lib/a.dart.md") == (
        ".codeconv/conversion-plans/lib/a.dart.md"
    )
    rp = Path("/repo")
    assert artefact_path(rp, "lib/a.dart").as_posix().endswith(
        ".codeconv/conversion-plans/lib/a.dart.md"
    )
    assert escalations_report_path(rp).name == "_escalations-report.md"
