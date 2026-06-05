"""marathon harness schema — per ``specs/024-marathon-stage-harness/data-model.md``.

Creates the ``marathon`` schema and its eight domain tables:
- ``marathon.marathons``            (the long multi-stage feature)
- ``marathon.stage_blocks``         (1 logical block == 1 Workflow run)
- ``marathon.checkpoints``          (append-only durable position)
- ``marathon.approvals``            (append-only per-block decision)
- ``marathon.status_reports``       (standardized four-field snapshot)
- ``marathon.verification_traces``  (append-only iteration substrate)
- ``marathon.git_blocks``           (per-block commit/push outcome)
- ``marathon.escalations``          (auto-mode block-points for Gabi)

Schema isolation (FR-015 carry-forward / 012 SC-007): the harness owns a
NEW schema ``marathon`` and touches neither ``public`` (legacy D2NET) nor
``dbos`` (DBOS runtime owns its own tables). DBOS runtime tables are
auto-created by ``dbos.launch()`` — out of Alembic scope.

``sequence_no`` is UNIQUE **per marathon** (``UNIQUE (marathon_id,
sequence_no)``), the precise form of contract checkpoint-store.md I1
("strictly monotonic across the whole marathon"); a bare global UNIQUE
would over-constrain a cluster that ever hosts more than one marathon.

Migration linearization (single head): ``0009`` is the current head;
``0010`` chains directly off it, so ``heads`` reports exactly ``0010``
after add (asserted by ``test_migration_0010_single_head``).

Revision ID: 0010
Revises: 0009
Create Date: 2026-06-05
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0010"
down_revision: Union[str, None] = "0009"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.execute("CREATE SCHEMA IF NOT EXISTS marathon;")

    # § Marathon — the long multi-stage feature being driven end-to-end.
    # The budget CHECK is the substrate-level guarantee of SC-006 (0 overruns):
    # a write that would push spend past the ceiling fails loudly rather than
    # silently overrunning.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS marathon.marathons (
            id                      text PRIMARY KEY,
            feature_slug            text NOT NULL,
            feature_branch          text NOT NULL,
            budget_ceiling          bigint,
            budget_spent            bigint NOT NULL DEFAULT 0,
            preauth_commit_push     boolean NOT NULL DEFAULT false,
            preauth_workflow_optin  boolean NOT NULL DEFAULT false,
            preauth_revoked_at      timestamptz,
            auto_mode               boolean NOT NULL DEFAULT false,
            created_at              timestamptz NOT NULL DEFAULT NOW(),
            CONSTRAINT marathons_budget_ck
                CHECK (budget_ceiling IS NULL OR budget_spent <= budget_ceiling)
        );
        """
    )

    # § Stage-block — the cadence grouping mapped 1:1 to one Workflow run;
    # the unit of checkpoint + commit/push.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS marathon.stage_blocks (
            id                text PRIMARY KEY,
            marathon_id       text NOT NULL REFERENCES marathon.marathons(id),
            stage             text NOT NULL,
            block_kind        text NOT NULL,
            ordinal           integer NOT NULL,
            workflow_run_id   text,
            status            text NOT NULL DEFAULT 'pending',
            last_sequence_no  bigint NOT NULL DEFAULT 0,
            started_at        timestamptz,
            completed_at      timestamptz,
            CONSTRAINT stage_blocks_stage_ck CHECK (stage IN
                ('specify','clarify','plan','task','analyze','implement','review')),
            CONSTRAINT stage_blocks_kind_ck CHECK (block_kind IN
                ('specify','clarify','plan_task_analyze','implement_session','review')),
            CONSTRAINT stage_blocks_status_ck CHECK (status IN
                ('pending','awaiting_approval','running','done','failed','escalated'))
        );
        """
    )
    op.execute(
        "CREATE INDEX IF NOT EXISTS stage_blocks_marathon_idx "
        "ON marathon.stage_blocks (marathon_id, ordinal);"
    )

    # § Checkpoint — append-only durable position snapshot. ``read_position``
    # reads the max(sequence_no) row; the index serves that hot path.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS marathon.checkpoints (
            id               bigserial PRIMARY KEY,
            block_id         text NOT NULL REFERENCES marathon.stage_blocks(id),
            marathon_id      text NOT NULL REFERENCES marathon.marathons(id),
            sequence_no      bigint NOT NULL,
            stage            text NOT NULL,
            wip_unit         text,
            completed_units  jsonb NOT NULL DEFAULT '[]'::jsonb,
            remaining_units  jsonb NOT NULL DEFAULT '[]'::jsonb,
            workflow_run_id  text,
            budget_spent     bigint NOT NULL DEFAULT 0,
            store_origin     text NOT NULL,
            created_at       timestamptz NOT NULL DEFAULT NOW(),
            CONSTRAINT checkpoints_seq_uq UNIQUE (marathon_id, sequence_no),
            CONSTRAINT checkpoints_store_ck CHECK (store_origin IN ('primary','fallback'))
        );
        """
    )
    op.execute(
        "CREATE INDEX IF NOT EXISTS checkpoints_resume_idx "
        "ON marathon.checkpoints (marathon_id, sequence_no DESC);"
    )

    # § Approval gate — append-only; a ``change`` supersedes (links back to)
    # the prior, which is retained. An existing ``approve`` short-circuits the
    # gate on resume (SC-004).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS marathon.approvals (
            id               bigserial PRIMARY KEY,
            block_id         text NOT NULL REFERENCES marathon.stage_blocks(id),
            presented_plan_ref text NOT NULL,
            outcome          text,
            decided_by       text,
            decided_at       timestamptz,
            supersedes_id    bigint REFERENCES marathon.approvals(id),
            created_at       timestamptz NOT NULL DEFAULT NOW(),
            CONSTRAINT approvals_outcome_ck
                CHECK (outcome IS NULL OR outcome IN ('approve','change'))
        );
        """
    )
    op.execute(
        "CREATE INDEX IF NOT EXISTS approvals_block_idx "
        "ON marathon.approvals (block_id, id);"
    )

    # § Status report — standardized four-field periodic snapshot (SC-005).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS marathon.status_reports (
            id               bigserial PRIMARY KEY,
            marathon_id      text NOT NULL REFERENCES marathon.marathons(id),
            block_id         text REFERENCES marathon.stage_blocks(id),
            done             jsonb NOT NULL DEFAULT '[]'::jsonb,
            issues           jsonb NOT NULL DEFAULT '[]'::jsonb,
            tokens_spent     bigint NOT NULL DEFAULT 0,
            tokens_remaining bigint,
            todo             jsonb NOT NULL DEFAULT '[]'::jsonb,
            created_at       timestamptz NOT NULL DEFAULT NOW()
        );
        """
    )
    op.execute(
        "CREATE INDEX IF NOT EXISTS status_reports_marathon_idx "
        "ON marathon.status_reports (marathon_id, created_at DESC);"
    )

    # § Verification-trace — append-only iteration substrate. The UNIQUE on
    # (marathon_id, subject, refine_seq) guarantees no earlier iteration is
    # ever overwritten (US7-AS2).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS marathon.verification_traces (
            id               bigserial PRIMARY KEY,
            marathon_id      text NOT NULL REFERENCES marathon.marathons(id),
            subject          text NOT NULL,
            refine_seq       integer NOT NULL,
            experiment_input jsonb NOT NULL DEFAULT '{}'::jsonb,
            metric_score     double precision,
            decision         text NOT NULL,
            created_at       timestamptz NOT NULL DEFAULT NOW(),
            CONSTRAINT verification_traces_uq UNIQUE (marathon_id, subject, refine_seq),
            CONSTRAINT verification_traces_decision_ck
                CHECK (decision IN ('accept','reject'))
        );
        """
    )

    # § Git block — per-block commit/push outcome under the preauthorization.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS marathon.git_blocks (
            id           bigserial PRIMARY KEY,
            block_id     text NOT NULL REFERENCES marathon.stage_blocks(id),
            commit_sha   text,
            staged_files jsonb NOT NULL DEFAULT '[]'::jsonb,
            pushed       boolean NOT NULL DEFAULT false,
            escalation   text,
            created_at   timestamptz NOT NULL DEFAULT NOW()
        );
        """
    )
    op.execute(
        "CREATE INDEX IF NOT EXISTS git_blocks_block_idx "
        "ON marathon.git_blocks (block_id, id);"
    )

    # § Escalation — durable auto-mode block-point requiring Gabi. The partial
    # index serves the ``doctor`` "open escalations" rollup.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS marathon.escalations (
            id           bigserial PRIMARY KEY,
            marathon_id  text NOT NULL REFERENCES marathon.marathons(id),
            block_id     text REFERENCES marathon.stage_blocks(id),
            kind         text NOT NULL,
            detail       jsonb NOT NULL DEFAULT '{}'::jsonb,
            resolved_at  timestamptz,
            created_at   timestamptz NOT NULL DEFAULT NOW(),
            CONSTRAINT escalations_kind_ck CHECK (kind IN
                ('non_retryable_failure','store_divergence','push_blocked','stage_flagged'))
        );
        """
    )
    op.execute(
        """
        CREATE INDEX IF NOT EXISTS escalations_open_idx
            ON marathon.escalations (marathon_id)
            WHERE resolved_at IS NULL;
        """
    )


def downgrade() -> None:
    # Schema isolation makes downgrade a single drop.
    op.execute("DROP SCHEMA IF EXISTS marathon CASCADE;")
