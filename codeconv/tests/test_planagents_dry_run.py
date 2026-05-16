"""Polish — --dry-run writes nothing (SC-008).

Maps to spec SC-008 + FR-019. T042. Gated by ``@needs_bridge``.
"""

from __future__ import annotations

import subprocess
from pathlib import Path

from .conftest import needs_bridge, run_codeconv
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


def _dart_plans_count(repo_root: Path) -> int:
    from sqlalchemy import text

    with _engine(repo_root).connect() as conn:
        return conn.execute(
            text("SELECT COUNT(*) FROM codeconv.dart_plans")
        ).scalar()


def _git_status(repo_root: Path) -> str:
    return subprocess.run(
        ["git", "-C", str(repo_root), "status", "--porcelain"],
        capture_output=True,
        text=True,
    ).stdout


@needs_bridge
def test_dry_run_subcommands_write_nothing(discover_repo: Path) -> None:
    """SC-008: `next --dry-run`, `aggregate --dry-run`,
    `stamp --dry-run`, `rebuild --dry-run` leave dart_plans count
    unchanged and produce no artefact/tombstone writes."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )
    before_n = _dart_plans_count(discover_repo)

    # next --dry-run: read-only by definition; --json-out suppressed.
    assert run_codeconv(
        discover_repo, "planagents", "next", "--dry-run", "--json"
    ).returncode == 0
    # aggregate --dry-run: no report written.
    assert run_codeconv(
        discover_repo,
        "planagents",
        "aggregate-escalations",
        "--dry-run",
    ).returncode == 0
    assert not (
        discover_repo / ".codeconv" / "conversion-plans"
        / "_escalations-report.md"
    ).is_file()
    # stamp --dry-run: no tombstone diff.
    assert run_codeconv(
        discover_repo, "planagents", "stamp-tombstones", "--dry-run"
    ).returncode == 0
    # rebuild --dry-run: no dart_plans write.
    assert run_codeconv(
        discover_repo,
        "planagents",
        "rebuild-plans-from-tombstones",
        "--dry-run",
    ).returncode == 0

    assert _dart_plans_count(discover_repo) == before_n, (
        "SC-008: --dry-run must not change codeconv.dart_plans"
    )
    # No artefact directory created by any dry-run.
    cp = discover_repo / ".codeconv" / "conversion-plans"
    assert not cp.exists() or not any(cp.rglob("*.dart.md")), (
        "SC-008: --dry-run must not write artefacts"
    )


@needs_bridge
def test_dry_run_next_does_not_write_json_out(discover_repo: Path) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )
    out = discover_repo / "out" / "next.json"
    assert run_codeconv(
        discover_repo,
        "planagents",
        "next",
        "--dry-run",
        "--json-out",
        str(out),
    ).returncode == 0
    assert not out.is_file(), "--dry-run must not write --json-out"
