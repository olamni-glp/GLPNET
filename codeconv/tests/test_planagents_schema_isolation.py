"""Schema isolation + DDL correctness + FR-020 runtime write-surface.

Maps to ``specs/017-conversion-plan-agents/contracts/
planagents_schema.md`` § "Verification" (SC-007) AND the analyze C2
remedy (FR-020 runtime write-surface assertion — tasks.md T011):

1. After migrate, ``dart_plans`` / ``planagents_runs`` live in
   ``codeconv`` only (SC-007).
2. ``dart_plans`` has the exact FR-012 columns/constraints.
3. ``open_escalation_count >= 0`` CHECK rejects negatives.
4. FK CASCADE on ``dart_files`` delete.
5. Downgrade-then-upgrade is idempotent.
6. **FR-020 runtime write-surface**: after a full ``next`` /
   ``plan-started`` / ``plan-completed`` / ``aggregate`` / ``stamp`` /
   ``rebuild`` exercise, the seven protected tables are byte-identical
   to a pre-exercise snapshot (ZERO writes to ``dart_files`` /
   ``dart_imports`` / ``dart_callers`` / ``dart_files_orphaned`` /
   ``discover_runs`` / ``dart_depgraph`` / ``dart_conversions``).

Gated by ``@needs_bridge``.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


def _migrate(repo_root: Path) -> None:
    proc = run_codeconv(repo_root, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr


@needs_bridge
def test_schema_isolation_codeconv_only(discover_repo: Path) -> None:
    """SC-007: the two new tables live in ``codeconv`` only."""
    _migrate(discover_repo)
    from sqlalchemy import text

    with _engine(discover_repo).connect() as conn:
        for tname in ("dart_plans", "planagents_runs"):
            inc = conn.execute(
                text(
                    "SELECT COUNT(*) FROM information_schema.tables "
                    "WHERE table_schema = 'codeconv' AND table_name = :t"
                ),
                {"t": tname},
            ).scalar()
            assert inc == 1, f"codeconv.{tname} missing after migrate"
            oth = conn.execute(
                text(
                    "SELECT COUNT(*) FROM information_schema.tables "
                    "WHERE table_schema != 'codeconv' AND table_name = :t"
                ),
                {"t": tname},
            ).scalar()
            assert oth == 0, f"SC-007 violation: {tname} also outside codeconv"


@needs_bridge
def test_dart_plans_columns_and_constraints(discover_repo: Path) -> None:
    """FR-012: exact column set + the nullable/NOT-NULL shape."""
    _migrate(discover_repo)
    from sqlalchemy import text

    with _engine(discover_repo).connect() as conn:
        rows = conn.execute(
            text(
                "SELECT column_name, is_nullable "
                "FROM information_schema.columns "
                "WHERE table_schema='codeconv' AND table_name='dart_plans' "
                "ORDER BY column_name"
            )
        ).all()
    got = {r[0]: r[1] for r in rows}
    assert got == {
        "path": "NO",
        "plan_started_at": "NO",
        "plan_completed_at": "YES",
        "sha256_of_dart_at_plan_start": "NO",
        "plan_path": "YES",
        "open_escalation_count": "NO",
        "plan_run_id": "YES",
    }


@needs_bridge
def test_open_escalation_count_check_rejects_negative(
    discover_repo: Path,
) -> None:
    """CHECK (open_escalation_count >= 0) rejects a negative."""
    _migrate(discover_repo)
    from sqlalchemy import text
    from sqlalchemy.exc import DBAPIError

    with _engine(discover_repo).begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:p, 'n.dart', '', '', NOW(), 'x', NOW())"
            ),
            {"p": "fixture/neg.dart"},
        )
    with pytest.raises(DBAPIError):
        with _engine(discover_repo).begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_plans "
                    "(path, plan_started_at, sha256_of_dart_at_plan_start, "
                    " open_escalation_count) "
                    "VALUES (:p, NOW(), 'x', -1)"
                ),
                {"p": "fixture/neg.dart"},
            )


@needs_bridge
def test_completed_after_started_check(discover_repo: Path) -> None:
    """CHECK (plan_completed_at IS NULL OR >= plan_started_at)."""
    _migrate(discover_repo)
    from sqlalchemy import text
    from sqlalchemy.exc import DBAPIError

    with _engine(discover_repo).begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:p, 'n.dart', '', '', NOW(), 'x', NOW())"
            ),
            {"p": "fixture/ord.dart"},
        )
    with pytest.raises(DBAPIError):
        with _engine(discover_repo).begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_plans "
                    "(path, plan_started_at, plan_completed_at, "
                    " sha256_of_dart_at_plan_start) VALUES "
                    "(:p, TIMESTAMP WITH TIME ZONE '2026-05-16 12:00:00+00', "
                    "    TIMESTAMP WITH TIME ZONE '2026-05-16 11:00:00+00', 'x')"
                ),
                {"p": "fixture/ord.dart"},
            )


@needs_bridge
def test_fk_cascade_on_dart_files_delete(discover_repo: Path) -> None:
    """Deleting a dart_files row cascades into dart_plans."""
    _migrate(discover_repo)
    from sqlalchemy import text

    rel = "fixture/casc.dart"
    with _engine(discover_repo).begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:p, 'c.dart', '', '', NOW(), 'x', NOW())"
            ),
            {"p": rel},
        )
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_plans "
                "(path, plan_started_at, sha256_of_dart_at_plan_start) "
                "VALUES (:p, NOW(), 'x')"
            ),
            {"p": rel},
        )
    with _engine(discover_repo).begin() as conn:
        conn.execute(
            text("DELETE FROM codeconv.dart_files WHERE path = :p"),
            {"p": rel},
        )
        n = conn.execute(
            text("SELECT COUNT(*) FROM codeconv.dart_plans WHERE path = :p"),
            {"p": rel},
        ).scalar()
    assert n == 0, "FK CASCADE failed: dart_plans row survived"


@needs_bridge
def test_downgrade_then_upgrade_idempotent(discover_repo: Path) -> None:
    """T011 / planagents_schema.md § Migration shape: ``alembic
    downgrade -1`` (drops dart_plans + planagents_runs) then ``upgrade
    head`` restores the same schema state — no error, both tables back
    under ``codeconv`` only."""
    _migrate(discover_repo)
    from alembic import command
    from alembic.config import Config

    import codeconv.cli as _cli
    from codeconv.bridge_client import acquire_or_discover

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    here = Path(_cli.__file__).parent
    cfg = Config(str(here / "db" / "alembic.ini"))
    cfg.set_main_option(
        "script_location", str((here / "db" / "migrations").resolve())
    )
    cfg.set_main_option(
        "sqlalchemy.url",
        f"postgresql+psycopg://postgres:postgres@"
        f"{endpoint.host}:{endpoint.port}/postgres",
    )

    from sqlalchemy import text

    # Downgrade one revision (0003 -> 0002): both tables vanish.
    command.downgrade(cfg, "-1")
    with _engine(discover_repo).connect() as conn:
        for t in ("dart_plans", "planagents_runs"):
            assert (
                conn.execute(
                    text(
                        "SELECT COUNT(*) FROM information_schema.tables "
                        "WHERE table_schema='codeconv' AND table_name=:t"
                    ),
                    {"t": t},
                ).scalar()
                == 0
            ), f"{t} should be gone after downgrade -1"

    # Upgrade back to head: both tables restored, codeconv-only.
    command.upgrade(cfg, "head")
    with _engine(discover_repo).connect() as conn:
        for t in ("dart_plans", "planagents_runs"):
            assert (
                conn.execute(
                    text(
                        "SELECT COUNT(*) FROM information_schema.tables "
                        "WHERE table_schema='codeconv' AND table_name=:t"
                    ),
                    {"t": t},
                ).scalar()
                == 1
            ), f"{t} should be restored after upgrade head"
    # Re-upgrade is a no-op (CREATE TABLE IF NOT EXISTS — idempotent).
    command.upgrade(cfg, "head")


def _protected_snapshot(repo_root: Path) -> dict:
    """Row-content snapshot of the 7 FR-020-protected tables."""
    from sqlalchemy import text

    tables = (
        "dart_files",
        "dart_imports",
        "dart_callers",
        "dart_files_orphaned",
        "discover_runs",
        "dart_depgraph",
        "dart_conversions",
    )
    snap: dict[str, list] = {}
    with _engine(repo_root).connect() as conn:
        for t in tables:
            rows = conn.execute(
                text(f"SELECT * FROM codeconv.{t} ORDER BY 1")
            ).all()
            snap[t] = [tuple(str(x) for x in r) for r in rows]
    return snap


@needs_bridge
def test_fr020_runtime_write_surface_untouched(discover_repo: Path) -> None:
    """FR-020 / analyze C2: a full planagents exercise issues ZERO
    writes to the seven protected feature-012/-015 tables."""
    sub = _mk_chain_subtree(discover_repo)  # c->b->a
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )

    before = _protected_snapshot(discover_repo)

    # Exercise the full planagents surface (a is the leaf ⇒ plan-ready).
    assert run_codeconv(discover_repo, "planagents", "status").returncode == 0
    assert run_codeconv(
        discover_repo, "planagents", "next", "--json"
    ).returncode == 0
    assert run_codeconv(
        discover_repo, "planagents", "plan-started", "lib/a.dart"
    ).returncode == 0
    assert run_codeconv(
        discover_repo,
        "planagents",
        "plan-completed",
        "lib/a.dart",
        "--plan-path",
        ".codeconv/conversion-plans/lib/a.dart.md",
        "--escalations",
        "1",
    ).returncode == 0
    assert run_codeconv(
        discover_repo, "planagents", "aggregate-escalations"
    ).returncode == 0
    assert run_codeconv(
        discover_repo, "planagents", "stamp-tombstones"
    ).returncode == 0
    assert run_codeconv(
        discover_repo, "planagents", "rebuild-plans-from-tombstones"
    ).returncode == 0

    after = _protected_snapshot(discover_repo)
    for t in before:
        assert after[t] == before[t], (
            f"FR-020 VIOLATION: codeconv.{t} changed during planagents "
            f"exercise (before={len(before[t])} rows, after={len(after[t])})"
        )
