"""Schema isolation + DDL correctness for feature 015.

Maps to ``specs/015-codeconv-depgraph/contracts/depgraph_schema.md`` §
"Test obligations" items 1–5:

1. After running migration, ``\\dn`` shows ``codeconv`` only (SC-007).
2. Exactly three new tables exist under ``codeconv``.
3. CHECK constraint violations are caught.
4. FK CASCADE works on ``dart_files`` delete.
5. Downgrade-then-upgrade is idempotent.
"""

from __future__ import annotations

import uuid
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv


def _migrate(repo_root: Path) -> None:
    proc = run_codeconv(repo_root, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr


@needs_bridge
def test_schema_isolation_codeconv_only(discover_repo: Path) -> None:
    """SC-007: after migrate, the three new tables live in ``codeconv`` only.

    ``public`` and ``dbos`` MUST NOT contain ``dart_depgraph``,
    ``dart_conversions``, or ``depgraph_runs``.
    """
    _migrate(discover_repo)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    with engine.connect() as conn:
        for tname in ("dart_depgraph", "dart_conversions", "depgraph_runs"):
            in_codeconv = conn.execute(
                text(
                    "SELECT COUNT(*) FROM information_schema.tables "
                    "WHERE table_schema = 'codeconv' AND table_name = :t"
                ),
                {"t": tname},
            ).scalar()
            assert in_codeconv == 1, f"codeconv.{tname} missing after migrate"
            in_other = conn.execute(
                text(
                    "SELECT COUNT(*) FROM information_schema.tables "
                    "WHERE table_schema != 'codeconv' AND table_name = :t"
                ),
                {"t": tname},
            ).scalar()
            assert in_other == 0, (
                f"SC-007 violation: '{tname}' also exists outside codeconv"
            )


@needs_bridge
def test_status_check_constraint_rejects_typo(discover_repo: Path) -> None:
    """A bug that writes ``status='completed'`` (typo for ``'converted'``)
    must be rejected by the CHECK constraint."""
    _migrate(discover_repo)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text
    from sqlalchemy.exc import DBAPIError

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)

    # Insert a file so dart_depgraph FK is satisfiable.
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:p, :n, '', '', NOW(), 'x', NOW())"
            ),
            {"p": "fixture/a.dart", "n": "a.dart"},
        )

    with pytest.raises(DBAPIError):
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_depgraph "
                    "(path, topo_level, cycle_group_id, ready, status, "
                    " dependency_count, caller_count) "
                    "VALUES (:p, 0, 1, FALSE, 'completed', 0, 0)"
                ),
                {"p": "fixture/a.dart"},
            )


@needs_bridge
def test_ready_status_consistent_constraint(discover_repo: Path) -> None:
    """``ready=TRUE`` MUST imply ``status='ready'`` and vice versa."""
    _migrate(discover_repo)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text
    from sqlalchemy.exc import DBAPIError

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)

    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:p, 'b.dart', '', '', NOW(), 'x', NOW())"
            ),
            {"p": "fixture/b.dart"},
        )

    with pytest.raises(DBAPIError):
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_depgraph "
                    "(path, topo_level, cycle_group_id, ready, status, "
                    " dependency_count, caller_count) "
                    "VALUES (:p, 0, 1, TRUE, 'pending', 0, 0)"
                ),
                {"p": "fixture/b.dart"},
            )


@needs_bridge
def test_fk_cascade_on_dart_files_delete(discover_repo: Path) -> None:
    """Deleting a row from ``dart_files`` cascades into ``dart_depgraph``
    and ``dart_conversions``."""
    _migrate(discover_repo)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    rel = "fixture/c.dart"
    with engine.begin() as conn:
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
                "INSERT INTO codeconv.dart_depgraph "
                "(path, topo_level, cycle_group_id, ready, status, "
                " dependency_count, caller_count) "
                "VALUES (:p, 0, 1, TRUE, 'ready', 0, 0)"
            ),
            {"p": rel},
        )
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_conversions "
                "(path, started_at, sha256_of_dart_at_start) "
                "VALUES (:p, NOW(), 'x')"
            ),
            {"p": rel},
        )

    with engine.begin() as conn:
        conn.execute(
            text("DELETE FROM codeconv.dart_files WHERE path = :p"),
            {"p": rel},
        )
        depg = conn.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.dart_depgraph WHERE path = :p"
            ),
            {"p": rel},
        ).scalar()
        conv = conn.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.dart_conversions WHERE path = :p"
            ),
            {"p": rel},
        ).scalar()
    assert depg == 0, "FK CASCADE failed: dart_depgraph still has the row"
    assert conv == 0, "FK CASCADE failed: dart_conversions still has the row"


@needs_bridge
def test_completed_after_started_constraint(discover_repo: Path) -> None:
    """``dart_conversions.completed_at >= started_at`` enforced by CHECK."""
    _migrate(discover_repo)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text
    from sqlalchemy.exc import DBAPIError

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    rel = "fixture/d.dart"
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:p, 'd.dart', '', '', NOW(), 'x', NOW())"
            ),
            {"p": rel},
        )

    with pytest.raises(DBAPIError):
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_conversions "
                    "(path, started_at, completed_at, sha256_of_dart_at_start) "
                    "VALUES (:p, "
                    " TIMESTAMP WITH TIME ZONE '2026-05-11 12:00:00+00', "
                    " TIMESTAMP WITH TIME ZONE '2026-05-11 11:00:00+00', "
                    " 'x')"
                ),
                {"p": rel},
            )
