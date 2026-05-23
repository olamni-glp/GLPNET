"""T006 — schema isolation + DDL correctness for feature 019.

Maps to ``specs/019-codeconv-codegen/contracts/codegen_schema.md`` §
Invariants:

1. After migrate, ``codeconv.dart_codegen`` exists ONLY under
   ``codeconv`` — Alembic authors no ``public``/``dbos`` object (DBOS
   owns its own tables).
2. The ``build_status`` / ``human_review_score`` CHECK constraints reject
   out-of-domain values.
3. FK CASCADE works on a ``dart_files`` delete.
4. ``codegen_completed_at >= codegen_started_at`` enforced by CHECK.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv


def _migrate(repo_root: Path) -> None:
    proc = run_codeconv(repo_root, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr


@needs_bridge
def test_dart_codegen_in_codeconv_only(discover_repo: Path) -> None:
    """The new table lives in ``codeconv`` only — no ``public``/other copy."""
    _migrate(discover_repo)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    with engine.connect() as conn:
        in_codeconv = conn.execute(
            text(
                "SELECT COUNT(*) FROM information_schema.tables "
                "WHERE table_schema = 'codeconv' "
                "AND table_name = 'dart_codegen'"
            )
        ).scalar()
        assert in_codeconv == 1, "codeconv.dart_codegen missing after migrate"
        in_other = conn.execute(
            text(
                "SELECT COUNT(*) FROM information_schema.tables "
                "WHERE table_schema NOT IN ('codeconv') "
                "AND table_name = 'dart_codegen'"
            )
        ).scalar()
        assert in_other == 0, (
            "schema-isolation violation: 'dart_codegen' exists outside codeconv"
        )


@needs_bridge
def test_build_status_check_rejects_typo(discover_repo: Path) -> None:
    """``build_status='passed'`` (typo for ``'pass'``) is rejected."""
    _migrate(discover_repo)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text
    from sqlalchemy.exc import DBAPIError

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    rel = "fixture/cg_a.dart"
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:p, 'cg_a.dart', '', '', NOW(), 'x', NOW())"
            ),
            {"p": rel},
        )
    with pytest.raises(DBAPIError):
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_codegen "
                    "(path, build_status) VALUES (:p, 'passed')"
                ),
                {"p": rel},
            )


@needs_bridge
def test_human_review_score_range_check(discover_repo: Path) -> None:
    """``human_review_score`` must be in 1..5 (a 6 is rejected)."""
    _migrate(discover_repo)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text
    from sqlalchemy.exc import DBAPIError

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    rel = "fixture/cg_b.dart"
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:p, 'cg_b.dart', '', '', NOW(), 'x', NOW())"
            ),
            {"p": rel},
        )
    with pytest.raises(DBAPIError):
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_codegen "
                    "(path, human_review_score) VALUES (:p, 6)"
                ),
                {"p": rel},
            )


@needs_bridge
def test_fk_cascade_on_dart_files_delete(discover_repo: Path) -> None:
    """Deleting a ``dart_files`` row cascades into ``dart_codegen``."""
    _migrate(discover_repo)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    rel = "fixture/cg_c.dart"
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:p, 'cg_c.dart', '', '', NOW(), 'x', NOW())"
            ),
            {"p": rel},
        )
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_codegen "
                "(path, codegen_started_at, sha256_of_dart_at_codegen_start) "
                "VALUES (:p, NOW(), 'x')"
            ),
            {"p": rel},
        )
    with engine.begin() as conn:
        conn.execute(
            text("DELETE FROM codeconv.dart_files WHERE path = :p"),
            {"p": rel},
        )
        cg = conn.execute(
            text("SELECT COUNT(*) FROM codeconv.dart_codegen WHERE path = :p"),
            {"p": rel},
        ).scalar()
    assert cg == 0, "FK CASCADE failed: dart_codegen still has the row"


@needs_bridge
def test_completed_after_started_constraint(discover_repo: Path) -> None:
    """``codegen_completed_at >= codegen_started_at`` enforced by CHECK."""
    _migrate(discover_repo)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text
    from sqlalchemy.exc import DBAPIError

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    rel = "fixture/cg_d.dart"
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:p, 'cg_d.dart', '', '', NOW(), 'x', NOW())"
            ),
            {"p": rel},
        )
    with pytest.raises(DBAPIError):
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_codegen "
                    "(path, codegen_started_at, codegen_completed_at, "
                    " sha256_of_dart_at_codegen_start) "
                    "VALUES (:p, "
                    " TIMESTAMP WITH TIME ZONE '2026-05-23 12:00:00+00', "
                    " TIMESTAMP WITH TIME ZONE '2026-05-23 11:00:00+00', "
                    " 'x')"
                ),
                {"p": rel},
            )
