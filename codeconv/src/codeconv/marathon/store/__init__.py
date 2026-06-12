"""Per-run isolated marathon store (feature 030, greenfield — FR-027/029).

Replaces 024's single-module ``store.py`` (shared-cluster dual store) with a
package: :mod:`schema` (idempotent per-run DDL — ``contracts/store-schema.sql``,
D2) and :mod:`repository` (single-writer data access over the per-run PGLite
cluster + JSON mirror, reconcile — D1/D6). The cluster lives *outside* the repo
at a per-run ``data_dir`` and is reached through the existing, hardened
``codeconv.bridge_client`` (FR-028), never the shared ``<repo>/.pgdb/`` chain.

``reconcile`` is completed by US5 (T049).
"""

from __future__ import annotations

from codeconv.marathon.store.repository import (
    ConcurrentWriter,
    IntegrityFailure,
    Repository,
    StoreUnavailable,
    acquire_writer_lock,
    reconcile,
    release_writer_lock,
    row_payload,
)
from codeconv.marathon.store.schema import ensure_schema

__all__ = [
    "ConcurrentWriter",
    "IntegrityFailure",
    "Repository",
    "StoreUnavailable",
    "acquire_writer_lock",
    "ensure_schema",
    "reconcile",
    "release_writer_lock",
    "row_payload",
]
