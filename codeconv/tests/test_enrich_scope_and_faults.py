"""Feature 035 / US3 (T018) — bounded, observable, fault-isolated runs.

- Acceptance 1: ``--path`` narrows candidates + counts (FR-012/013).
- Acceptance 2 / SC-007: a forced-raise ``infer_fn`` leaves that file's
  tombstone + ``dart_files`` row UNCHANGED and lists the failure, while
  other candidates still enrich (FR-010).
- Acceptance 3 / FR-011 / SC-001: the summary emits
  candidates/enriched/skipped/failed (+ low_confidence) with
  ``candidates == enriched + skipped + low_confidence + failed``, and a
  durable run-log file is written.
"""

from __future__ import annotations

import json
from pathlib import Path

from sqlalchemy import text

from .conftest import BRIDGE_SCRIPT, fake_infer_fn, needs_bridge, run_codeconv
from codeconv.tools.discover.tombstone import read_tombstone, tombstone_path
from codeconv.tools.enrich.seam import InferResult


def _mk_subtree(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib" / "keep").mkdir(parents=True)
    (sub / "lib" / "skip").mkdir(parents=True)
    for name in ("ok", "boom", "weak"):
        (sub / "lib" / "keep" / f"{name}.dart").write_text(
            f"class {name.capitalize()} {{\n  int v = 1;\n}}\n", encoding="utf-8"
        )
    (sub / "lib" / "skip" / "other.dart").write_text(
        "class Other {\n  int v = 1;\n}\n", encoding="utf-8"
    )
    return sub


def _selective_infer(req):
    if req.rel_path.endswith("boom.dart"):
        raise RuntimeError("forced seam failure")
    if req.rel_path.endswith("weak.dart"):
        return InferResult(purpose="", key_idea="", grounded=False, reason="trivial/generated")
    return fake_infer_fn(req)


@needs_bridge
def test_scope_faults_and_summary(discover_repo: Path) -> None:
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    ).returncode == 0

    from codeconv.tools.enrich.workflow import run_enrich

    summary = run_enrich(
        discover_repo,
        infer_fn=_selective_infer,
        paths=["lib/keep"],
        bridge_script=BRIDGE_SCRIPT,
    )

    # Acceptance 1: scope narrowed to lib/keep (3 candidates), lib/skip excluded.
    assert summary["candidates"] == 3, summary
    assert summary["enriched"] == 1
    assert summary["failed"] == 1
    assert summary["low_confidence"] == 1
    assert summary["skipped"] == 0
    # SC-001: every candidate accounted for, none silently blank.
    assert (
        summary["candidates"]
        == summary["enriched"] + summary["skipped"]
        + summary["low_confidence"] + summary["failed"]
    )
    # Acceptance 2: the failure is reported with its path.
    assert any(f["path"] == "lib/keep/boom.dart" for f in summary["failures"])

    troot = discover_repo / ".codeconv" / "tombstones"
    ok = read_tombstone(tombstone_path(troot, "lib/keep/ok.dart"))
    boom = read_tombstone(tombstone_path(troot, "lib/keep/boom.dart"))
    weak = read_tombstone(tombstone_path(troot, "lib/keep/weak.dart"))
    other = read_tombstone(tombstone_path(troot, "lib/skip/other.dart"))

    assert ok["purpose_source"] == "inferred" and ok["purpose"].strip() != ""
    # SC-007: failed + low-confidence tombstones UNCHANGED (still blank).
    assert boom["purpose"] == "" and boom["purpose_source"] != "inferred"
    assert weak["purpose"] == "" and weak["purpose_source"] != "inferred"
    # Out-of-scope file untouched.
    assert other["purpose"] == "" and other["purpose_source"] != "inferred"

    # DB: failed file's row unchanged (still blank/absent).
    from codeconv.db.engine import connect

    engine = connect(discover_repo)
    with engine.begin() as conn:
        row = conn.execute(
            text(
                "SELECT purpose, purpose_source FROM codeconv.dart_files "
                "WHERE path = :p"
            ),
            {"p": "lib/keep/boom.dart"},
        ).first()
    assert row is not None and row[0] == "" and row[1] != "inferred"

    # Acceptance 3 / FR-011: a durable run log was written + is valid JSON.
    assert summary["run_log"]
    log_path = discover_repo / summary["run_log"]
    assert log_path.is_file()
    payload = json.loads(log_path.read_text(encoding="utf-8"))
    assert payload["candidates"] == 3
    assert isinstance(payload["outcomes"], list) and payload["outcomes"]
