"""Idempotent per-run schema DDL (T006).

``ensure_schema(engine)`` creates the 9 ``marathon`` tables + CHECKs + indexes
exactly per ``specs/030-marathon-refinement/contracts/store-schema.sql`` in the
*per-run isolated* PGLite cluster — NOT the shared-repo Alembic chain (D2,
Constitution VI-a; the shared head stays ``0010``). Handles the mutually-
referential ``stage.item_id`` ⇄ ``item.blocks_stage_id`` FK by creating tables
first and adding the ``stage.item_id`` FK last.

Stub — implemented in Phase 2 Foundational (T006).
"""

from __future__ import annotations

__all__ = ["ensure_schema"]
