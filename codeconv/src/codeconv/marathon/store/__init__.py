"""Per-run isolated marathon store (feature 030, greenfield — FR-027/029).

Replaces 024's single-module ``store.py`` (shared-cluster dual store) with a
package: :mod:`schema` (idempotent per-run DDL — ``contracts/store-schema.sql``,
D2) and :mod:`repository` (single-writer data access over the per-run PGLite
cluster + JSON mirror, reconcile — D1/D6). The cluster lives *outside* the repo
at a per-run ``data_dir`` and is reached through the existing, hardened
``codeconv.bridge_client`` (FR-028), never the shared ``<repo>/.pgdb/`` chain.

Filled by Phase 2 (Foundational): T006 (``ensure_schema``) + T007–T010
(repository) + T049 (``reconcile``).
"""

from __future__ import annotations

__all__: list[str] = []
