"""codeconv-codegen ``no_emit`` status — one additive column on
``codeconv.dart_codegen``.

Per ``specs/020-trace-equivalence-fidelity`` Stage 4: a first-class
``no_emit`` conversion status, orthogonal to escalated/built, for source
files that are intentionally NOT emitted to C# (e.g. a Dart ``export``
directive with no types — nothing to convert). A ``no_emit`` file is
SATISFIED for readiness (never ``codegen_ready``/pending; never blocks
its downstream) and is NEVER counted as escalated (``no_emit`` wins, even
over an open escalation row).

The column is additive and idempotent: ``ADD COLUMN IF NOT EXISTS … NOT
NULL DEFAULT false`` (no backfill — every existing row reads ``false``,
preserving prior classification). A partial index on the (small)
``no_emit`` set speeds the status rollup / readiness snapshot.

Schema isolation (FR-018 / 012 / SC-007 carry-forward): the only object
touched lives under the ``codeconv`` schema — no ``public``/``dbos`` DDL
(DBOS owns its own tables), no data migration of prior tables.

Migration linearization (single head): ``0008`` is the single current
head; ``0009`` chains directly off it, so ``heads`` reports exactly
``0009`` after add (asserted by ``test_migration_0009_single_head``).

Revision ID: 0009
Revises: 0008
Create Date: 2026-06-03
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0009"
down_revision: Union[str, None] = "0008"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # Additive ``no_emit`` flag on dart_codegen — a first-class status
    # orthogonal to escalated/built (Stage 4). Idempotent, no backfill:
    # every existing row defaults to false, so prior classification is
    # unchanged until a row is explicitly marked.
    op.execute(
        "ALTER TABLE codeconv.dart_codegen "
        "ADD COLUMN IF NOT EXISTS no_emit boolean NOT NULL DEFAULT false;"
    )

    # Partial index on the (small) no_emit set — speeds the status
    # rollup / readiness snapshot's no_emit scan.
    op.execute(
        """
        CREATE INDEX IF NOT EXISTS dart_codegen_no_emit_idx
            ON codeconv.dart_codegen (no_emit)
            WHERE no_emit;
        """
    )


def downgrade() -> None:
    # Drop the partial index, then the additive column (idempotent).
    op.execute("DROP INDEX IF EXISTS codeconv.dart_codegen_no_emit_idx;")
    op.execute(
        "ALTER TABLE codeconv.dart_codegen DROP COLUMN IF EXISTS no_emit;"
    )
