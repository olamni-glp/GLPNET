"""codeconv-builder DDL — four new tables under the ``codeconv`` schema.

Per ``specs/018-codeconv-builder/data-model.md`` § 2 +
``specs/018-codeconv-builder/contracts/builder_schema.md``:

Creation order (FK-satisfiable): ``builder_runs`` → ``research_findings``
→ ``conversion_idioms`` → ``dart_convspecs``.

- ``codeconv.builder_runs``       (run traceability + DBOS-trace join key; R9/R11)
- ``codeconv.research_findings``  (official-docs-authoritative provenance cache;
                                   ``construct_key`` UNIQUE — FR-012/FR-024 cache
                                   invariant: a construct is never re-researched)
- ``codeconv.conversion_idioms``  (persistent codebase-scoped idiom KB; FR-012/SC-007)
- ``codeconv.dart_convspecs``     (per-file convspec two-phase state, parallel
                                   to feature-017 ``codeconv.dart_plans``)

Plus one helper index:

- ``dart_convspecs_open_escalations_idx`` — partial index on
  ``open_escalation_count`` (``WHERE open_escalation_count > 0``) — speeds
  the FR-017 conversion-blocked / status query.

FK / null discipline (contract builder_schema.md § "FK / null discipline"):
``dart_convspecs.path`` FK → ``codeconv.dart_files(path)`` ON DELETE CASCADE
(reuses the proven feature-017 ``dart_plans`` shape — D2). The optional
parents ``dart_convspecs.convspec_run_id`` → ``builder_runs(id)`` and
``conversion_idioms.research_finding_id`` → ``research_findings(id)`` are
NULL-able with ON DELETE SET NULL so a usable row never orphans when the
optional parent is dropped.

Schema isolation (FR-015 / SC-004 carry-forward from features 012/015/016/017):
every object created here lives under the ``codeconv`` schema only — no
``public`` / ``dbos`` objects (DBOS authors its own tables at
``dbos.launch()`` — out of Alembic scope), no data migration (FR-021,
fresh PG17 cluster).

``CREATE TABLE IF NOT EXISTS`` (idempotent, isolation-safe — matches the
0001/0002/0004 migration style). ``downgrade()`` drops the four tables
``IF EXISTS ... CASCADE`` (single multi-table drop; CASCADE resolves the
FKs and the partial index regardless of name order).

Revision ID: 0005
Revises: 0004
Create Date: 2026-05-17
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0005"
down_revision: Union[str, None] = "0004"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # The codeconv schema is created by 0001; defensive IF NOT EXISTS in
    # case 0005 is ever applied stand-alone against a clean cluster.
    op.execute("CREATE SCHEMA IF NOT EXISTS codeconv;")

    # § 1 — codeconv.builder_runs (R9/R11 run traceability; created FIRST
    #        so dart_convspecs.convspec_run_id FK is satisfiable).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.builder_runs (
            id                 bigserial   PRIMARY KEY,
            workspace_id       text        NOT NULL,
            outer_workflow_id  text        NOT NULL UNIQUE,
            started_at         timestamptz NOT NULL DEFAULT now(),
            finished_at        timestamptz,
            code_version       text,
            files_total        integer     NOT NULL DEFAULT 0,
            files_done         integer     NOT NULL DEFAULT 0,
            files_escalated    integer     NOT NULL DEFAULT 0,
            outcome            text,
            CONSTRAINT builder_runs_outcome_check CHECK (
                outcome IS NULL OR outcome IN (
                    'completed',
                    'resumed',
                    'nothing_to_convert',
                    'escalated'
                )
            ),
            CONSTRAINT builder_runs_counts_nonneg CHECK (
                files_total >= 0 AND files_done >= 0 AND files_escalated >= 0
            ),
            CONSTRAINT builder_runs_finished_after_started CHECK (
                finished_at IS NULL OR finished_at >= started_at
            )
        );
        """
    )

    # § 2 — codeconv.research_findings (FR-024 provenance cache;
    #        construct_key UNIQUE ⇒ a construct is never re-researched,
    #        offline-reproducible after first research).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.research_findings (
            id                    bigserial   PRIMARY KEY,
            construct_key         text        NOT NULL UNIQUE,
            query                 text        NOT NULL,
            authoritative_source  text        NOT NULL,
            corroborating_sources jsonb       NOT NULL DEFAULT '[]'::jsonb,
            conclusion            text        NOT NULL,
            retrieved_at          timestamptz NOT NULL DEFAULT now(),
            is_authoritative      boolean     NOT NULL
        );
        """
    )

    # § 3 — codeconv.conversion_idioms (FR-012/SC-007 persistent idiom KB;
    #        construct_key UNIQUE; research_finding_id NULL-able FK).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.conversion_idioms (
            id                  bigserial   PRIMARY KEY,
            construct_key       text        NOT NULL UNIQUE,
            source_form         text        NOT NULL,
            target_form         text        NOT NULL,
            rationale           text        NOT NULL,
            research_finding_id bigint,
            first_seen_path     text        NOT NULL,
            status              text        NOT NULL DEFAULT 'active',
            created_at          timestamptz NOT NULL DEFAULT now(),
            CONSTRAINT conversion_idioms_status_check CHECK (
                status IN ('active', 'conflicted', 'escalated')
            ),
            CONSTRAINT conversion_idioms_research_fk
                FOREIGN KEY (research_finding_id)
                REFERENCES codeconv.research_findings(id) ON DELETE SET NULL
        );
        """
    )

    # § 4 — codeconv.dart_convspecs (per-file convspec two-phase state;
    #        parallel to feature-017 dart_plans — D2 reuse of the proven
    #        shape: *_completed_at written only by the terminal step
    #        action so a half-step is observably incomplete, FR-003).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.dart_convspecs (
            path                          text    PRIMARY KEY,
            convspec_started_at           timestamptz,
            convspec_completed_at         timestamptz,
            spec_path                     text,
            sha256_of_dart_at_spec_start  text,
            open_escalation_count         integer NOT NULL DEFAULT 0,
            convspec_run_id               bigint,
            CONSTRAINT dart_convspecs_path_fk
                FOREIGN KEY (path)
                REFERENCES codeconv.dart_files(path) ON DELETE CASCADE,
            CONSTRAINT dart_convspecs_run_fk
                FOREIGN KEY (convspec_run_id)
                REFERENCES codeconv.builder_runs(id) ON DELETE SET NULL,
            CONSTRAINT dart_convspecs_open_escalations_nonneg
                CHECK (open_escalation_count >= 0),
            CONSTRAINT dart_convspecs_completed_after_started CHECK (
                convspec_completed_at IS NULL
                OR convspec_started_at IS NULL
                OR convspec_completed_at >= convspec_started_at
            )
        );
        """
    )

    # § 5 — partial index speeding the FR-017 conversion-blocked /
    #        status query (mirrors feature-017 dart_plans index).
    op.execute(
        """
        CREATE INDEX IF NOT EXISTS dart_convspecs_open_escalations_idx
            ON codeconv.dart_convspecs (open_escalation_count)
            WHERE open_escalation_count > 0;
        """
    )


def downgrade() -> None:
    # Single multi-table drop per contracts/builder_schema.md
    # § "Downgrade". CASCADE resolves every FK and drops the partial
    # index automatically regardless of the order the tables are named.
    op.execute(
        "DROP TABLE IF EXISTS "
        "codeconv.dart_convspecs, "
        "codeconv.conversion_idioms, "
        "codeconv.research_findings, "
        "codeconv.builder_runs CASCADE;"
    )
