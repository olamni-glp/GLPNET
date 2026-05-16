"""codeconv-planagents DDL — two new tables under the ``codeconv`` schema.

Per ``specs/017-conversion-plan-agents/contracts/planagents_schema.md``:

- ``codeconv.dart_plans`` (FR-012 — two-phase per-file conversion-plan
  state, parallel to feature-015's ``codeconv.dart_conversions``).
- ``codeconv.planagents_runs`` (research R8 — optional traceability
  table, mirrors ``codeconv.depgraph_runs``).

Plus one helper index:

- ``dart_plans_open_escalations_idx`` — partial index on
  ``open_escalation_count`` (``WHERE open_escalation_count > 0``) — speeds
  the FR-017 conversion-blocked query.

The ``plan_run_id`` FK (``dart_plans.plan_run_id ->
planagents_runs.id``) is added AFTER the runs table is created (same
revision) and is NULL-able, so ``dart_plans`` is usable even if the
optional runs table is later dropped.

Schema isolation (FR-020 / SC-007 carry-forward from features 012/015):
every object created here lives under the ``codeconv`` schema only — no
``public`` / ``dbos`` objects, no data migration of feature-012/-015
tables.

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
    # § 1 — codeconv.planagents_runs (R8 traceability table; created
    #        BEFORE dart_plans so the plan_run_id FK is satisfiable).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.planagents_runs (
            id                       uuid        PRIMARY KEY,
            started_at               timestamptz NOT NULL,
            completed_at             timestamptz,
            mode                     text        NOT NULL,
            files_total              integer,
            plan_ready_count         integer,
            plan_in_progress_count   integer,
            planned_count            integer,
            open_escalations_total   integer,
            warnings                 jsonb       NOT NULL DEFAULT '[]'::jsonb,
            CONSTRAINT planagents_runs_mode_check CHECK (
                mode IN (
                    'status',
                    'next',
                    'plan-started',
                    'plan-completed',
                    'aggregate-escalations',
                    'stamp-tombstones',
                    'rebuild-plans-from-tombstones'
                )
            )
        );
        """
    )

    # § 2 — codeconv.dart_plans (FR-012 two-phase conversion-plan state).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.dart_plans (
            path                          text        PRIMARY KEY,
            plan_started_at               timestamptz NOT NULL,
            plan_completed_at             timestamptz,
            sha256_of_dart_at_plan_start  text        NOT NULL,
            plan_path                     text,
            open_escalation_count         integer     NOT NULL DEFAULT 0,
            plan_run_id                   uuid,
            CONSTRAINT dart_plans_path_fk
                FOREIGN KEY (path)
                REFERENCES codeconv.dart_files(path) ON DELETE CASCADE,
            CONSTRAINT dart_plans_plan_run_fk
                FOREIGN KEY (plan_run_id)
                REFERENCES codeconv.planagents_runs(id) ON DELETE SET NULL,
            CONSTRAINT dart_plans_open_escalations_nonneg
                CHECK (open_escalation_count >= 0),
            CONSTRAINT dart_plans_completed_after_started CHECK (
                plan_completed_at IS NULL
                OR plan_completed_at >= plan_started_at
            )
        );
        """
    )

    # § 3 — partial index speeding the FR-017 conversion-blocked query.
    op.execute(
        """
        CREATE INDEX IF NOT EXISTS dart_plans_open_escalations_idx
            ON codeconv.dart_plans (open_escalation_count)
            WHERE open_escalation_count > 0;
        """
    )


def downgrade() -> None:
    # Single multi-table drop per contracts/planagents_schema.md §
    # "Migration shape" + tasks.md T006. CASCADE resolves the
    # dart_plans.plan_run_id -> planagents_runs FK regardless of the
    # order the two are named, and drops the partial index automatically.
    op.execute(
        "DROP TABLE IF EXISTS codeconv.planagents_runs, "
        "codeconv.dart_plans CASCADE;"
    )
