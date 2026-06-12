"""Single-writer data access for the per-run store (T007–T010, T049).

Composes the per-run bridge: ``_connect(env)`` →
``codeconv.bridge_client.acquire_or_discover(repo_root=store_root,
data_dir=store_root/'pgdb')`` → a single-writer SQLAlchemy engine
(``pool_size=1``) on the sidecar endpoint → ``ensure_schema`` once (D1, FR-028).

Owns CRUD for ``run``/``stage``/``checkpoint``/``item``/``issue`` (all writes in
``engine.begin()`` transactions), the JSON-mirror dual-write
(``<store_root>/json/<entity>/…``, read-prefers-primary-falls-back), monotonic
``sequence_no`` allocation (``max(primary_max, fallback_max)+1``), and
``reconcile(run_id)`` (PGLite ↔ JSON mirror; fast-forward | fork | in_sync —
never silently picks, FR-024/D6).

Stub — implemented across Phase 2 Foundational (T007–T011) + US5 (T049).
"""

from __future__ import annotations

__all__ = ["reconcile"]
