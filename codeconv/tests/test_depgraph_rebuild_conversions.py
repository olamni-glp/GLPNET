"""rebuild-conversions-from-tombstones — inverse of stamp.

Test obligations from
``specs/015-codeconv-depgraph/contracts/tombstone_format_delta.md``
§ "Test obligations" / `test_depgraph_rebuild_conversions.py` (3 items).
T033. Gated by ``@needs_bridge``.
"""

from __future__ import annotations

from pathlib import Path

from .conftest import needs_bridge, run_codeconv
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


def _conv_snapshot(repo_root: Path) -> dict:
    from sqlalchemy import text

    with _engine(repo_root).connect() as conn:
        return {
            r[0]: (r[1], r[2], r[3])
            for r in conn.execute(
                text(
                    "SELECT path, started_at, completed_at, target_path "
                    "FROM codeconv.dart_conversions"
                )
            ).all()
        }


def _wipe_conversions(repo_root: Path) -> None:
    from sqlalchemy import text

    with _engine(repo_root).begin() as conn:
        conn.execute(text("DELETE FROM codeconv.dart_conversions"))


@needs_bridge
def test_round_trip_seed_stamp_wipe_rebuild(discover_repo: Path) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    run_codeconv(
        discover_repo, "depgraph", "mark-completed", "lib/a.dart",
        "--target", "out/A.cs",
    )
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/b.dart")
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    assert (
        run_codeconv(
            discover_repo, "depgraph", "stamp-tombstones"
        ).returncode
        == 0
    )
    before = _conv_snapshot(discover_repo)
    assert set(before) == {"lib/a.dart", "lib/b.dart"}

    _wipe_conversions(discover_repo)
    assert _conv_snapshot(discover_repo) == {}

    proc = run_codeconv(
        discover_repo, "depgraph", "rebuild-conversions-from-tombstones"
    )
    assert proc.returncode == 0, proc.stderr
    after = _conv_snapshot(discover_repo)

    # Same set of paths; completed_at null-ness + target_path round-trip.
    assert set(after) == set(before)
    for p in before:
        b_started, b_completed, b_target = before[p]
        a_started, a_completed, a_target = after[p]
        assert (b_completed is None) == (a_completed is None), p
        assert b_target == a_target, p


@needs_bridge
def test_missing_key_tolerance(discover_repo: Path) -> None:
    """A tombstone with none of the six keys must not error the rebuild;
    that file is silently skipped (no dart_conversions row invented)."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    # No stamp, no mark-* ⇒ every tombstone has the 8 base keys only.
    proc = run_codeconv(
        discover_repo, "depgraph", "rebuild-conversions-from-tombstones",
        "--json",
    )
    assert proc.returncode == 0, proc.stderr
    assert _conv_snapshot(discover_repo) == {}, (
        "no conversion rows should be invented from key-less tombstones"
    )


@needs_bridge
def test_null_value_distinguishability(discover_repo: Path) -> None:
    """`conversion_completed_at: null` ⇒ row with completed_at NULL;
    a tombstone WITHOUT `conversion_started_at` ⇒ NO row."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    # a: in_progress (started, completed null). b/c: never started.
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    assert (
        run_codeconv(
            discover_repo, "depgraph", "stamp-tombstones"
        ).returncode
        == 0
    )
    _wipe_conversions(discover_repo)
    proc = run_codeconv(
        discover_repo, "depgraph", "rebuild-conversions-from-tombstones"
    )
    assert proc.returncode == 0, proc.stderr
    snap = _conv_snapshot(discover_repo)
    assert "lib/a.dart" in snap, "in-progress conversion must round-trip"
    assert snap["lib/a.dart"][1] is None, "completed_at must stay NULL"
    assert "lib/b.dart" not in snap, "never-started file ⇒ no row"
    assert "lib/c.dart" not in snap
