"""T019 — escalate-don't-guess + report counts match state (FR-007/FR-009).

@needs_bridge. When the codegen sub-agent cannot faithfully derive C#, it
writes a structured escalation artifact under
``.codeconv/conversion-code/<rel>.dart.md`` instead of a guessed ``.cs``.
``ingest`` then returns ``escalated`` and records
``open_escalation_count``; ``aggregate-escalations`` produces a report
whose total equals ``Σ dart_codegen.open_escalation_count > 0``.
"""

from __future__ import annotations

import json
from pathlib import Path

from codeconv.tools.codegen.buildgate import BuildResult
from codeconv.tools.codegen.escalations import escalation_artifact_path
from codeconv.tools.codegen.workflow import run_codegen_ingest

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


_PASS = lambda _proj: BuildResult(status="pass")  # noqa: E731

_ESCALATION = """\
# Codegen escalations for lib/a.dart

### E1: Dart isolate has no decided C# equivalent
- **Kind**: undecidable
- **File(s)**: lib/a.dart
- **Detail**: the source spawns an isolate; the plan does not decide the
  concurrency model for C#.
- **Needs**: human decision on the concurrency model.
- **Status**: open
"""


def _setup(discover_repo: Path):
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0


def _write_escalation(repo_root: Path, rel: str, text: str) -> Path:
    p = escalation_artifact_path(repo_root, rel)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8")
    return p


@needs_bridge
def test_open_escalation_blocks_and_records(discover_repo: Path) -> None:
    _setup(discover_repo)
    _write_escalation(discover_repo, "lib/a.dart", _ESCALATION)
    res = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    assert res["outcome"] == "escalated"
    assert res["open_escalation_count"] == 1
    assert res["conversion_blocked"] is True

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(discover_repo, ready_timeout=30.0))
    with eng.connect() as conn:
        oec, completed = conn.execute(
            text(
                "SELECT open_escalation_count, codegen_completed_at "
                "FROM codeconv.dart_codegen WHERE path = 'lib/a.dart'"
            )
        ).first()
    assert oec == 1
    assert completed is None  # blocked, not built


@needs_bridge
def test_aggregate_report_count_matches_db(discover_repo: Path) -> None:
    _setup(discover_repo)
    _write_escalation(discover_repo, "lib/a.dart", _ESCALATION)
    run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    proc = run_codeconv(discover_repo, "codegen", "aggregate-escalations", "--json")
    assert proc.returncode == 0, proc.stdout + proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["open_escalations_total"] == 1

    # Report file written and reconciles with the DB sum.
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(discover_repo, ready_timeout=30.0))
    with eng.connect() as conn:
        db_total = conn.execute(
            text(
                "SELECT COALESCE(SUM(open_escalation_count),0) "
                "FROM codeconv.dart_codegen WHERE open_escalation_count > 0"
            )
        ).scalar()
    assert int(db_total) == summary["open_escalations_total"]
    report = discover_repo / ".codeconv" / "conversion-code" / "_escalations-report.md"
    assert report.is_file()
    assert "Total open escalations: 1" in report.read_text(encoding="utf-8")
