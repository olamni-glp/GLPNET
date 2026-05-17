"""Polish — stamp-tombstones / rebuild-plans-from-tombstones round-trip.

Maps to spec FR-013 + SC-003 (byte-identical re-stamp; DB-wipe
recovery). T043. Gated by ``@needs_bridge``.
"""

from __future__ import annotations

import hashlib
from pathlib import Path

from codeconv.tools.discover.tombstone import read_tombstone

from .conftest import needs_bridge, run_codeconv
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


def _tomb(repo_root: Path, *parts: str) -> Path:
    return repo_root.joinpath(".codeconv", "tombstones", *parts)


def _digest(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def _setup(discover_repo: Path):
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )


@needs_bridge
def test_stamp_writes_four_keys_and_is_idempotent(
    discover_repo: Path,
) -> None:
    """FR-013 / SC-003: stamp embeds the four plan-state keys; a
    re-stamp on unchanged DB state is byte-identical."""
    _setup(discover_repo)
    run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart",
        "--no-tombstone-update",
    )
    run_codeconv(
        discover_repo, "planagents", "plan-completed", "lib/a.dart",
        "--plan-path", ".codeconv/conversion-plans/lib/a.dart.md",
        "--escalations", "1", "--no-tombstone-update",
    )
    assert run_codeconv(
        discover_repo, "planagents", "stamp-tombstones"
    ).returncode == 0
    fm = read_tombstone(_tomb(discover_repo, "lib", "a.dart.md"))
    assert fm.get("plan_started_at")
    assert fm.get("plan_completed_at")
    assert fm.get("plan_path") == ".codeconv/conversion-plans/lib/a.dart.md"
    assert fm.get("open_escalation_count") == 1

    before = _digest(_tomb(discover_repo, "lib", "a.dart.md"))
    assert run_codeconv(
        discover_repo, "planagents", "stamp-tombstones"
    ).returncode == 0
    assert _digest(_tomb(discover_repo, "lib", "a.dart.md")) == before, (
        "SC-003: re-stamp on unchanged DB state must be byte-identical"
    )


@needs_bridge
def test_no_plan_row_means_keys_absent(discover_repo: Path) -> None:
    """Null-vs-missing (data-model §2): no dart_plans row ⇒ the four
    keys are ABSENT from the tombstone (not null)."""
    _setup(discover_repo)
    assert run_codeconv(
        discover_repo, "planagents", "stamp-tombstones"
    ).returncode == 0
    # b.dart never planned ⇒ no plan keys.
    fm = read_tombstone(_tomb(discover_repo, "lib", "b.dart.md"))
    for k in (
        "plan_started_at",
        "plan_completed_at",
        "plan_path",
        "open_escalation_count",
    ):
        assert k not in fm, f"{k} must be ABSENT when there is no row"


@needs_bridge
def test_in_progress_keys_present_with_null(discover_repo: Path) -> None:
    """Row with completed NULL ⇒ started present, completed present-null,
    plan_path present-null (data-model §2)."""
    _setup(discover_repo)
    run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart",
        "--no-tombstone-update",
    )
    assert run_codeconv(
        discover_repo, "planagents", "stamp-tombstones"
    ).returncode == 0
    fm = read_tombstone(_tomb(discover_repo, "lib", "a.dart.md"))
    assert fm.get("plan_started_at")  # present + non-null
    assert "plan_completed_at" in fm and fm["plan_completed_at"] is None
    assert "plan_path" in fm and fm["plan_path"] is None
    assert fm.get("open_escalation_count") == 0


@needs_bridge
def test_rebuild_reconstructs_dart_plans_after_wipe(
    discover_repo: Path,
) -> None:
    """FR-013: rebuild repopulates dart_plans from tombstone YAML after
    a simulated DB wipe (sha re-snapshot caveat documented)."""
    _setup(discover_repo)
    run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart"
    )
    run_codeconv(
        discover_repo, "planagents", "plan-completed", "lib/a.dart",
        "--plan-path", ".codeconv/conversion-plans/lib/a.dart.md",
        "--escalations", "2",
    )
    from sqlalchemy import text

    # Simulate DB wipe of dart_plans only.
    with _engine(discover_repo).begin() as conn:
        conn.execute(text("DELETE FROM codeconv.dart_plans"))
        assert conn.execute(
            text("SELECT COUNT(*) FROM codeconv.dart_plans")
        ).scalar() == 0

    assert run_codeconv(
        discover_repo, "planagents", "rebuild-plans-from-tombstones"
    ).returncode == 0
    with _engine(discover_repo).connect() as conn:
        row = conn.execute(
            text(
                "SELECT plan_started_at, plan_completed_at, plan_path, "
                "open_escalation_count "
                "FROM codeconv.dart_plans WHERE path = 'lib/a.dart'"
            )
        ).first()
    assert row is not None
    assert row[0] is not None  # plan_started_at restored
    assert row[1] is not None  # plan_completed_at restored
    assert row[2] == ".codeconv/conversion-plans/lib/a.dart.md"
    assert row[3] == 2  # open_escalation_count restored


@needs_bridge
def test_stamp_dry_run_writes_nothing(discover_repo: Path) -> None:
    _setup(discover_repo)
    run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart",
        "--no-tombstone-update",
    )
    before = _digest(_tomb(discover_repo, "lib", "a.dart.md"))
    assert run_codeconv(
        discover_repo, "planagents", "stamp-tombstones", "--dry-run"
    ).returncode == 0
    assert _digest(_tomb(discover_repo, "lib", "a.dart.md")) == before
