"""codeconv-equiv DDL — one new table under the ``codeconv`` schema.

Per ``specs/020-trace-equivalence-fidelity/contracts/equiv_schema.md`` and
``data-model.md``:

- ``codeconv.dart_equivalence`` (FR-018 — two-phase per-(converted-file ×
  in-scope GLP source) trace-equivalence verdict state, parallel to
  feature-019's ``codeconv.dart_codegen``).

Two-phase write discipline (mirrors ``dart_codegen``): ``capture`` writes
``phase='captured'`` + trace hashes (agent/CLI layer); ``compare`` (the
durable step) writes ``phase='compared'`` + ``verdict`` + ``divergence``
deterministically from recorded traces. No row reaches
``verdict='equivalent'`` without ``builds=true`` and the relation passing
(enforced in code — ``tools/equiv``).

The **fidelity score is NOT stored** here — it is recomputed on read by
``tools/equiv/fidelity.py`` from ``builds``/``back_tested``/``trace_captured``
and the per-source ``verdict`` aggregation, so the production gate and the
GEPA optimizer agree by construction (R4, SC-004). This table holds only
the scorer's inputs.

Schema isolation (FR-018 / 012 / SC-007 carry-forward): every object lives
under the ``codeconv`` schema only — no ``public``/``dbos`` objects (DBOS
owns its own tables), no data migration of prior tables. Fresh and additive
(``CREATE TABLE IF NOT EXISTS``); no backfill; idempotent (safe to re-run on
the canonical cluster).

Migration linearization (single head): ``0007`` is the single current head
(the historical dual-``0003`` was already linearized downstream); ``0008``
chains directly off it, so ``heads`` reports exactly ``0008`` after add
(asserted by ``test_migration_single_head``).

Revision ID: 0008
Revises: 0007
Create Date: 2026-05-27
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0008"
down_revision: Union[str, None] = "0007"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # codeconv.dart_equivalence — two-phase per-(converted file × in-scope
    # GLP source) verdict state (data-model.md § Entity, verbatim shape).
    # Enum columns carry CHECK constraints (mirroring dart_codegen's
    # build_status/human_review_score checks). NO fidelity column: the
    # score is computed-on-read (R4/SC-004) — only its inputs are stored.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS codeconv.dart_equivalence (
            id                    bigserial PRIMARY KEY,
            tombstone_key         text NOT NULL,
            source_path           text NOT NULL,
            subsystem             text NOT NULL
                CHECK (subsystem IN
                    ('heap','bytecode','compiler','runtime-core','multiagent')),
            tier                  text NOT NULL
                CHECK (tier IN ('strict','dynamic')),
            compare_mode          text NOT NULL
                CHECK (compare_mode IN ('trace','outcome')),
            split                 text NOT NULL
                CHECK (split IN ('train','held-out')),
            phase                 text NOT NULL
                CHECK (phase IN ('pending','captured','compared')),
            verdict               text
                CHECK (verdict IN ('equivalent','divergent','stale')),
            golden_trace_hash     text,
            candidate_trace_hash  text,
            divergence            jsonb,
            bytecode_diff_empty   boolean,
            builds                boolean NOT NULL DEFAULT false,
            back_tested           boolean NOT NULL DEFAULT false,
            trace_captured        boolean NOT NULL DEFAULT false,
            dart_source_hash      text,
            created_at            timestamptz NOT NULL DEFAULT now(),
            updated_at            timestamptz NOT NULL DEFAULT now()
        );
        """
    )

    # Unique (tombstone_key, source_path): one verdict row per converted
    # file × in-scope source (equiv_schema.md § DDL).
    op.execute(
        """
        CREATE UNIQUE INDEX IF NOT EXISTS dart_equivalence_key_source_uidx
            ON codeconv.dart_equivalence (tombstone_key, source_path);
        """
    )
    # (subsystem, tier): per-subsystem fidelity rollup (equiv status ≤5 s warm).
    op.execute(
        """
        CREATE INDEX IF NOT EXISTS dart_equivalence_subsystem_tier_idx
            ON codeconv.dart_equivalence (subsystem, tier);
        """
    )
    # (verdict): frontier / promotion-gate scans.
    op.execute(
        """
        CREATE INDEX IF NOT EXISTS dart_equivalence_verdict_idx
            ON codeconv.dart_equivalence (verdict);
        """
    )
    # Partial index on stale rows (FR-016 drift re-verify; stale excluded
    # from frac).
    op.execute(
        """
        CREATE INDEX IF NOT EXISTS dart_equivalence_stale_idx
            ON codeconv.dart_equivalence (verdict)
            WHERE verdict = 'stale';
        """
    )


def downgrade() -> None:
    # equiv_schema.md: drop the additive table (CASCADE drops its indexes).
    op.execute("DROP TABLE IF EXISTS codeconv.dart_equivalence CASCADE;")
