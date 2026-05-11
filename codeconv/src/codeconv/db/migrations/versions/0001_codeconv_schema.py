"""codeconv schema initial DDL — per data-model.md § 1.

Creates the ``codeconv`` schema and five tables:
- ``codeconv.dart_files``
- ``codeconv.dart_imports`` (UNIQUE (from_path, to_path))
- ``codeconv.dart_callers`` (UNIQUE (from_path, to_path))
- ``codeconv.dart_files_orphaned``
- ``codeconv.discover_runs``

Schema isolation is mandated by FR-015: codeconv MUST NOT create tables in
``public`` (D2NET's home) or ``dbos`` (DBOS runtime's home). Same-named
table ``dart_files`` exists in both ``public`` (D2NET) and ``codeconv``
(this feature) — they coexist without interference (data-model.md § 7).

Revision ID: 0001
Revises:
Create Date: 2026-05-10
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0001"
down_revision: Union[str, None] = None
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.execute('CREATE SCHEMA IF NOT EXISTS codeconv;')

    # § 1.1 — codeconv.dart_files
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.dart_files (
            path           text PRIMARY KEY,
            name           text NOT NULL,
            purpose        text NOT NULL DEFAULT '',
            key_idea       text NOT NULL DEFAULT '',
            mtime          timestamptz NOT NULL,
            sha256         text NOT NULL,
            discovered_at  timestamptz NOT NULL DEFAULT NOW()
        );
        """
    )

    # § 1.2 — codeconv.dart_imports
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.dart_imports (
            from_path text NOT NULL,
            to_path   text NOT NULL,
            CONSTRAINT dart_imports_uq UNIQUE (from_path, to_path)
        );
        """
    )

    # § 1.3 — codeconv.dart_callers (denormalised inverse of dart_imports)
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.dart_callers (
            from_path text NOT NULL,
            to_path   text NOT NULL,
            CONSTRAINT dart_callers_uq UNIQUE (from_path, to_path)
        );
        """
    )

    # § 1.4 — codeconv.dart_files_orphaned
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.dart_files_orphaned (
            path           text PRIMARY KEY,
            name           text NOT NULL,
            purpose        text NOT NULL DEFAULT '',
            key_idea       text NOT NULL DEFAULT '',
            mtime          timestamptz NOT NULL,
            sha256         text NOT NULL,
            discovered_at  timestamptz NOT NULL,
            orphaned_at    timestamptz NOT NULL DEFAULT NOW()
        );
        """
    )

    # § 1.5 — codeconv.discover_runs
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.discover_runs (
            id                         uuid PRIMARY KEY,
            started_at                 timestamptz NOT NULL,
            completed_at               timestamptz,
            mode                       text NOT NULL,
            files_total                integer,
            files_processed            integer NOT NULL DEFAULT 0,
            files_skipped_idempotent   integer NOT NULL DEFAULT 0,
            warnings                   jsonb NOT NULL DEFAULT '[]'::jsonb
        );
        """
    )


def downgrade() -> None:
    # Schema isolation makes downgrade a single drop.
    op.execute('DROP SCHEMA IF EXISTS codeconv CASCADE;')
