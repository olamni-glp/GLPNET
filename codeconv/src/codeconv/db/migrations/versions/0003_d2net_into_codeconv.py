"""D2NET workspace tables, de-branded into the ``codeconv`` schema.

Per ``specs/016-codeconv-init-scaffold-langpair/data-model.md`` § 1
(spec FR-020, D5, research R6).

Creates four tables under the ``codeconv`` schema — the de-brand of
D2NET's ``public.{setting,excluded_directories,phase_sequence,
phase_status}``:

- ``codeconv.workspace_settings``   (flat key/value; was ``public.setting``)
- ``codeconv.excluded_directories`` (+ kind CHECK)
- ``codeconv.phase_sequence``       (conversion-phase ordering)
- ``codeconv.phase_status``         (per-phase progress)

It does **NOT** recreate ``public.dart_files`` or any
``scaffold_tracker`` — those are folded into ``codeconv.dart_files``
(feature 012) + the feature-015 tombstone ``target_path`` (D4/D5). No
``public.*`` object is created (FR-020 schema isolation). No data
migration: legacy D2NET ``public.*`` data is transient build state and
is NOT consulted (D1/R6) — a workspace is re-established by running
``codeconv init``.

``CREATE TABLE IF NOT EXISTS`` (idempotent, isolation-safe — matches the
0001/0002 migration style). ``downgrade()`` drops the four tables
``IF EXISTS ... CASCADE`` in reverse creation order.

Revision ID: 0003
Revises: 0002
Create Date: 2026-05-16
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0003"
down_revision: Union[str, None] = "0002"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # The codeconv schema is created by 0001; defensive IF NOT EXISTS in
    # case 0003 is ever applied stand-alone against a clean cluster.
    op.execute('CREATE SCHEMA IF NOT EXISTS codeconv;')

    # § 1.1 — codeconv.workspace_settings (flat key/value; de-brand of
    # D2NET public.setting; flat shape preserved so the migration is a
    # rename, not a redesign).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.workspace_settings (
            key    text PRIMARY KEY,
            value  text
        );
        """
    )

    # § 1.2 — codeconv.excluded_directories (in-scope boundary).
    # kind: 'tool' = from the pair's tool_exclusion_globs(); 'pattern' =
    # archive/backup heuristic (pair-supplied, if any); 'manual' = user.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.excluded_directories (
            path  text PRIMARY KEY,
            kind  text NOT NULL
                  CHECK (kind IN ('tool', 'pattern', 'manual'))
        );
        """
    )

    # § 1.3 — codeconv.phase_sequence (conversion-phase ordering).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.phase_sequence (
            phase     text PRIMARY KEY,
            sequence  integer NOT NULL
        );
        """
    )

    # § 1.4 — codeconv.phase_status (per-phase progress).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.phase_status (
            phase         text PRIMARY KEY,
            status        text NOT NULL,
            last_updated  timestamptz NOT NULL DEFAULT NOW()
        );
        """
    )


def downgrade() -> None:
    # Reverse creation order; CASCADE for isolation-safe teardown.
    op.execute("DROP TABLE IF EXISTS codeconv.phase_status CASCADE;")
    op.execute("DROP TABLE IF EXISTS codeconv.phase_sequence CASCADE;")
    op.execute("DROP TABLE IF EXISTS codeconv.excluded_directories CASCADE;")
    op.execute("DROP TABLE IF EXISTS codeconv.workspace_settings CASCADE;")
