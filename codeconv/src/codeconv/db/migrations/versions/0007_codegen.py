"""codeconv-codegen DDL — one new table under the ``codeconv`` schema.

Per ``specs/019-codeconv-codegen/contracts/codegen_schema.md``:

- ``codeconv.dart_codegen`` (FR-003/FR-005/FR-008 — two-phase per-file
  code-generation state, parallel to feature-015's
  ``codeconv.dart_conversions`` and feature-017's ``codeconv.dart_plans``).

The lifecycle is phase-1 INSERT-ON-CONFLICT (``codegen_started_at`` +
``sha256_of_dart_at_codegen_start``) → UPDATEs (build / test / review /
escalation / promote) → phase-2 ``codegen_completed_at`` (only on build
pass). Rows are NEVER bulk-deleted (R9 carry-forward); a re-spec /
stale-drift re-opens a row in place (``retry`` / ``--respec``).

Schema isolation (FR-020 / SC-007 carry-forward from features
012/015/017): every object created here lives under the ``codeconv``
schema only — no ``public`` / ``dbos`` objects (DBOS owns its own
tables), no data migration of prior tables. The table is fresh and
additive (``CREATE TABLE IF NOT EXISTS``); no backfill.

Revision ID: 0007
Revises: 0006
Create Date: 2026-05-23
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0007"
down_revision: Union[str, None] = "0006"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # codeconv.dart_codegen — two-phase per-file codegen state
    # (codegen_schema.md § DDL, verbatim shape + the dart_files FK and a
    # completed-after-started temporal CHECK, mirroring dart_plans).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.dart_codegen (
            path                            text PRIMARY KEY,
            codegen_started_at              timestamptz,
            codegen_completed_at            timestamptz,
            sha256_of_dart_at_codegen_start text,
            target_cs_path                  text,
            build_status                    text
                CHECK (build_status IN ('pass','fail','not_built')),
            test_pass_rate                  real,
            human_review_score              smallint
                CHECK (human_review_score BETWEEN 1 AND 5),
            open_escalation_count           integer NOT NULL DEFAULT 0,
            batch_id                        text,
            promoted                        boolean NOT NULL DEFAULT false,
            codegen_run_id                  text,
            CONSTRAINT dart_codegen_path_fk
                FOREIGN KEY (path)
                REFERENCES codeconv.dart_files(path) ON DELETE CASCADE,
            CONSTRAINT dart_codegen_open_escalations_nonneg
                CHECK (open_escalation_count >= 0),
            CONSTRAINT dart_codegen_completed_after_started CHECK (
                codegen_completed_at IS NULL
                OR codegen_started_at IS NULL
                OR codegen_completed_at >= codegen_started_at
            )
        );
        """
    )

    # Partial index speeding the FR-007/FR-009 conversion-blocked query
    # (mirrors dart_plans_open_escalations_idx).
    op.execute(
        """
        CREATE INDEX IF NOT EXISTS dart_codegen_open_escalations_idx
            ON codeconv.dart_codegen (open_escalation_count)
            WHERE open_escalation_count > 0;
        """
    )


def downgrade() -> None:
    # codegen_schema.md § DDL: ``DROP TABLE IF EXISTS … CASCADE``
    # (drops the partial index automatically).
    op.execute("DROP TABLE IF EXISTS codeconv.dart_codegen CASCADE;")
