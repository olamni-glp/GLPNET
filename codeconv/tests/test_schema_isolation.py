"""T006 — feature-018 schema isolation (FR-015 / data-model §2).

After ``0005`` the four new relations (``builder_runs``,
``research_findings``, ``conversion_idioms``, ``dart_convspecs``) live
in the ``codeconv`` schema ONLY; Alembic authors **zero** ``public`` /
``dbos`` objects (DBOS owns its own ``dbos``-schema tables at
``dbos.launch()`` — out of Alembic scope). Mirrors the proven
``test_planagents_schema_isolation`` pattern (D2).
"""

from __future__ import annotations

from pathlib import Path

from .conftest import needs_bridge, run_codeconv

_NEW_TABLES = (
    "builder_runs",
    "research_findings",
    "conversion_idioms",
    "dart_convspecs",
)


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


@needs_bridge
def test_new_tables_codeconv_only(discover_repo: Path) -> None:
    assert run_codeconv(discover_repo, "migrate", timeout=180.0).returncode == 0
    from sqlalchemy import text

    with _engine(discover_repo).connect() as conn:
        for t in _NEW_TABLES:
            inc = conn.execute(
                text(
                    "SELECT COUNT(*) FROM information_schema.tables "
                    "WHERE table_schema='codeconv' AND table_name=:t"
                ),
                {"t": t},
            ).scalar()
            assert inc == 1, f"codeconv.{t} missing after 0005"
            oth = conn.execute(
                text(
                    "SELECT COUNT(*) FROM information_schema.tables "
                    "WHERE table_schema NOT IN ('codeconv') "
                    "AND table_name=:t"
                ),
                {"t": t},
            ).scalar()
            assert oth == 0, f"isolation violation: {t} also outside codeconv"


@needs_bridge
def test_unique_and_fk_constraints_present(discover_repo: Path) -> None:
    """data-model §2: construct_key UNIQUE on research_findings &
    conversion_idioms (cache invariant FR-012/FR-024); outer_workflow_id
    UNIQUE on builder_runs (resume reuses the row, R9)."""
    assert run_codeconv(discover_repo, "migrate", timeout=180.0).returncode == 0
    from sqlalchemy import text

    with _engine(discover_repo).connect() as conn:
        uniques = {
            (r[0], r[1])
            for r in conn.execute(
                text(
                    "SELECT tc.table_name, kcu.column_name "
                    "FROM information_schema.table_constraints tc "
                    "JOIN information_schema.key_column_usage kcu "
                    "  ON tc.constraint_name = kcu.constraint_name "
                    " AND tc.table_schema = kcu.table_schema "
                    "WHERE tc.table_schema='codeconv' "
                    "  AND tc.constraint_type='UNIQUE'"
                )
            ).all()
        }
    assert ("research_findings", "construct_key") in uniques
    assert ("conversion_idioms", "construct_key") in uniques
    assert ("builder_runs", "outer_workflow_id") in uniques


@needs_bridge
def test_dart_convspecs_columns(discover_repo: Path) -> None:
    """data-model §2.1 exact column/nullability shape (two-phase:
    convspec_started_at / convspec_completed_at both NULLABLE here —
    set by the step's terminal action, FR-003)."""
    assert run_codeconv(discover_repo, "migrate", timeout=180.0).returncode == 0
    from sqlalchemy import text

    with _engine(discover_repo).connect() as conn:
        rows = conn.execute(
            text(
                "SELECT column_name, is_nullable "
                "FROM information_schema.columns "
                "WHERE table_schema='codeconv' AND table_name='dart_convspecs' "
                "ORDER BY column_name"
            )
        ).all()
    got = {r[0]: r[1] for r in rows}
    assert got == {
        "path": "NO",
        "convspec_started_at": "YES",
        "convspec_completed_at": "YES",
        "spec_path": "YES",
        "sha256_of_dart_at_spec_start": "YES",
        "open_escalation_count": "NO",
        "convspec_run_id": "YES",
    }, got
