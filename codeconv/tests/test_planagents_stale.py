"""Polish — source-drift / --replan (FR-015 / R9).

Maps to spec edge case "source drift after planning" + FR-015. T041.
Gated by ``@needs_bridge``.
"""

from __future__ import annotations

import json
from pathlib import Path

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


def _mk(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "drift.dart").write_text(
        "/// Drift.\nclass Drift {}\n", encoding="utf-8"
    )
    return sub


def _plan(repo_root: Path, path: str):
    assert run_codeconv(
        repo_root, "planagents", "plan-started", path,
        "--no-tombstone-update",
    ).returncode == 0
    assert run_codeconv(
        repo_root, "planagents", "plan-completed", path,
        "--no-tombstone-update",
    ).returncode == 0


def _started_at_sha(repo_root: Path, path: str):
    from sqlalchemy import text

    with _engine(repo_root).connect() as conn:
        return conn.execute(
            text(
                "SELECT sha256_of_dart_at_plan_start, plan_started_at "
                "FROM codeconv.dart_plans WHERE path = :p"
            ),
            {"p": path},
        ).first()


@needs_bridge
def test_drift_reported_stale_and_not_replanned_by_default(
    discover_repo: Path,
) -> None:
    """FR-015: edit a planned file's .dart so sha differs ⇒ `status`
    flags it stale; a default `next` does NOT re-plan it."""
    sub = _mk(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )
    _plan(discover_repo, "lib/drift.dart")

    # Edit the source + re-discover so dart_files.sha256 changes.
    (sub / "lib" / "drift.dart").write_text(
        "/// Drift v2.\nclass Drift { int x = 1; }\n", encoding="utf-8"
    )
    assert run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    ).returncode == 0

    p = run_codeconv(discover_repo, "planagents", "status", "--json")
    assert p.returncode == 0, p.stderr
    s = json.loads(_extract_json(p.stdout))
    assert "lib/drift.dart" in s["stale"], s
    assert s["stale_count"] == 1

    # Default next: drift.dart is `planned` ⇒ NOT re-selected (no silent
    # re-plan; FR-015 "MUST NOT silently treat a stale plan as current"
    # — it is reported, not auto-replanned).
    p = run_codeconv(discover_repo, "planagents", "next", "--json")
    assert p.returncode == 0
    paths = [r["path"] for r in json.loads(_extract_json(p.stdout))["batch"]]
    assert "lib/drift.dart" not in paths


@needs_bridge
def test_replan_stale_reselects_and_updates_row_in_place(
    discover_repo: Path,
) -> None:
    """FR-015 / R9: `--replan stale` re-selects the stale file; a
    subsequent `plan-started --replan` UPDATEs the row in place (new
    started_at + SHA, completed reset) — the row is never deleted."""
    sub = _mk(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )
    _plan(discover_repo, "lib/drift.dart")
    sha0, started0 = _started_at_sha(discover_repo, "lib/drift.dart")

    (sub / "lib" / "drift.dart").write_text(
        "/// Drift v2.\nclass Drift { int x = 1; }\n", encoding="utf-8"
    )
    assert run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    ).returncode == 0

    # --replan stale ⇒ next now re-selects drift.dart.
    p = run_codeconv(
        discover_repo, "planagents", "next", "--replan", "stale", "--json"
    )
    assert p.returncode == 0, p.stderr
    paths = [r["path"] for r in json.loads(_extract_json(p.stdout))["batch"]]
    assert "lib/drift.dart" in paths

    # plan-started --replan UPDATEs the SAME row (not a new one).
    from sqlalchemy import text

    with _engine(discover_repo).connect() as conn:
        n_before = conn.execute(
            text("SELECT COUNT(*) FROM codeconv.dart_plans")
        ).scalar()
    assert run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/drift.dart",
        "--replan", "--no-tombstone-update",
    ).returncode == 0
    sha1, started1 = _started_at_sha(discover_repo, "lib/drift.dart")
    with _engine(discover_repo).connect() as conn:
        n_after = conn.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.dart_plans"
            )
        ).scalar()
        completed = conn.execute(
            text(
                "SELECT plan_completed_at FROM codeconv.dart_plans "
                "WHERE path = 'lib/drift.dart'"
            )
        ).scalar()
    assert n_after == n_before, "replan must UPDATE in place, not add a row"
    assert sha1 != sha0, "sha256_of_dart_at_plan_start must be refreshed"
    assert completed is None, "replan resets plan_completed_at to NULL"
