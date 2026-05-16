"""codeconv-depgraph DDL — three new tables under the ``codeconv`` schema.

Per ``specs/015-codeconv-depgraph/contracts/depgraph_schema.md`` § ``upgrade()`` SQL:

- ``codeconv.depgraph_runs`` (R5 — traceability table, mirrors
  ``codeconv.discover_runs``).
- ``codeconv.dart_conversions`` (FR-006a — two-phase conversion state).
- ``codeconv.dart_depgraph`` (FR-008 — per-file ordering + readiness).

Plus two helper indexes:

- ``dart_depgraph_ready_idx`` — partial index on the ``ready`` flag,
  speeds the SC-005 "what's ready" lookup.
- ``dart_depgraph_path_topo_idx`` — composite index used by the SC-003
  edge-invariant self-join.

Schema isolation (FR-007 / SC-007 carry-forward from feature 012):
every object created here lives under the ``codeconv`` schema only.

Revision ID: 0002
Revises: 0001
Create Date: 2026-05-11
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0002"
down_revision: Union[str, None] = "0001"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # § 1 — codeconv.depgraph_runs (R5 traceability table)
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.depgraph_runs (
            id                       uuid PRIMARY KEY,
            started_at               timestamptz NOT NULL,
            completed_at             timestamptz,
            mode                     text NOT NULL,
            files_total              integer,
            ready_count              integer,
            in_progress_count        integer,
            converted_count          integer,
            cycle_count              integer,
            warnings                 jsonb NOT NULL DEFAULT '[]'::jsonb,
            CONSTRAINT depgraph_runs_mode_check CHECK (
                mode IN (
                    'compute',
                    'mark-started',
                    'mark-completed',
                    'stamp-tombstones',
                    'rebuild-conversions-from-tombstones'
                )
            )
        );
        """
    )

    # § 2 — codeconv.dart_conversions (FR-006a two-phase state)
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.dart_conversions (
            path                     text PRIMARY KEY,
            started_at               timestamptz NOT NULL,
            completed_at             timestamptz,
            sha256_of_dart_at_start  text NOT NULL,
            target_path              text,
            marked_started_run_id    uuid,
            marked_completed_run_id  uuid,
            CONSTRAINT dart_conversions_path_fk
                FOREIGN KEY (path) REFERENCES codeconv.dart_files(path) ON DELETE CASCADE,
            CONSTRAINT dart_conversions_started_run_fk
                FOREIGN KEY (marked_started_run_id) REFERENCES codeconv.depgraph_runs(id) ON DELETE SET NULL,
            CONSTRAINT dart_conversions_completed_run_fk
                FOREIGN KEY (marked_completed_run_id) REFERENCES codeconv.depgraph_runs(id) ON DELETE SET NULL,
            CONSTRAINT dart_conversions_completed_after_started CHECK (
                completed_at IS NULL OR completed_at >= started_at
            )
        );
        """
    )

    # § 3 — codeconv.dart_depgraph (FR-008 ordering + readiness)
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.dart_depgraph (
            path                     text PRIMARY KEY,
            topo_level               integer NOT NULL,
            cycle_group_id           integer NOT NULL,
            ready                    boolean NOT NULL,
            status                   text NOT NULL,
            dependency_count         integer NOT NULL,
            caller_count             integer NOT NULL,
            computed_at              timestamptz NOT NULL DEFAULT NOW(),
            depgraph_run_id          uuid,
            discover_run_id          uuid,
            CONSTRAINT dart_depgraph_path_fk
                FOREIGN KEY (path) REFERENCES codeconv.dart_files(path) ON DELETE CASCADE,
            CONSTRAINT dart_depgraph_depgraph_run_fk
                FOREIGN KEY (depgraph_run_id) REFERENCES codeconv.depgraph_runs(id) ON DELETE SET NULL,
            CONSTRAINT dart_depgraph_discover_run_fk
                FOREIGN KEY (discover_run_id) REFERENCES codeconv.discover_runs(id) ON DELETE SET NULL,
            CONSTRAINT dart_depgraph_topo_level_nonneg CHECK (topo_level >= 0),
            CONSTRAINT dart_depgraph_dep_count_nonneg CHECK (dependency_count >= 0),
            CONSTRAINT dart_depgraph_caller_count_nonneg CHECK (caller_count >= 0),
            CONSTRAINT dart_depgraph_status_check CHECK (
                status IN ('pending', 'ready', 'in_progress', 'converted')
            ),
            CONSTRAINT dart_depgraph_ready_status_consistent CHECK (
                (ready = TRUE AND status = 'ready')
                OR (ready = FALSE AND status <> 'ready')
            )
        );
        """
    )

    op.execute(
        """
        CREATE INDEX IF NOT EXISTS dart_depgraph_ready_idx
            ON codeconv.dart_depgraph (ready) WHERE ready;
        """
    )

    op.execute(
        """
        CREATE INDEX IF NOT EXISTS dart_depgraph_path_topo_idx
            ON codeconv.dart_depgraph (path, topo_level, cycle_group_id);
        """
    )


def downgrade() -> None:
    op.execute("DROP TABLE IF EXISTS codeconv.dart_depgraph CASCADE;")
    op.execute("DROP TABLE IF EXISTS codeconv.dart_conversions CASCADE;")
    op.execute("DROP TABLE IF EXISTS codeconv.depgraph_runs CASCADE;")
