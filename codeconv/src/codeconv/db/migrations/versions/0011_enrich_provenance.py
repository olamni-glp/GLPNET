"""Feature 035 — additive provenance columns for tombstone enrichment.

Per ``specs/035-semantic-tombstone-enrichment/contracts/migration_0011.md``
and research R-005. Adds two ``text NOT NULL DEFAULT 'absent'`` columns to
``codeconv.dart_files``:

- ``purpose_source``  ∈ {``doc``, ``inferred``, ``absent``}
- ``key_idea_source`` ∈ {``doc``, ``inferred``, ``absent``}

``enrich`` records ``inferred`` when the Claude seam fills a blank field;
``discover``'s mechanical seed records ``doc``/``absent`` from blank-ness.

**Backfill is exact (research R-005).** Mechanical seeding
(``tools/discover/workflow.py:527-528``) is the only current source of
non-blank ``purpose``/``key_idea``, so a non-blank existing value is, by
construction, doc-derived ⇒ ``doc``; a blank value ⇒ ``absent``. The CASE
backfill classifies every pre-existing row correctly with no inference, so
SC-006 (every field's provenance is determinable) holds retroactively.

Additive + idempotent (``IF NOT EXISTS``), single linear head: ``0011``
chains directly off ``0010`` (the current head, ``0010_marathon_schema``),
so ``heads`` reports exactly ``0011`` after add — asserted by
``test_migration_0011_single_head.py`` (Constitution VI-a). The constitution's
"current head" reference advances ``0010 → 0011``; the single linear head
discipline is preserved (no branch/merge).

Revision ID: 0011
Revises: 0010
Create Date: 2026-06-26
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0011"
down_revision: Union[str, None] = "0010"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.execute(
        "ALTER TABLE codeconv.dart_files "
        "ADD COLUMN IF NOT EXISTS purpose_source  text NOT NULL DEFAULT 'absent'"
    )
    op.execute(
        "ALTER TABLE codeconv.dart_files "
        "ADD COLUMN IF NOT EXISTS key_idea_source text NOT NULL DEFAULT 'absent'"
    )
    # Exact backfill (R-005): non-blank ⇒ doc, blank ⇒ absent.
    op.execute(
        """
        UPDATE codeconv.dart_files
           SET purpose_source  = CASE WHEN purpose  = '' THEN 'absent' ELSE 'doc' END,
               key_idea_source = CASE WHEN key_idea = '' THEN 'absent' ELSE 'doc' END
        """
    )


def downgrade() -> None:
    op.execute(
        "ALTER TABLE codeconv.dart_files DROP COLUMN IF EXISTS key_idea_source"
    )
    op.execute(
        "ALTER TABLE codeconv.dart_files DROP COLUMN IF EXISTS purpose_source"
    )
