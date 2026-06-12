"""Per-run keeper lifecycle over ``codeconv.bridge_client`` (US3, T028–T032).

A thin lifecycle — NOT a re-ported ``PGliteSupervisor`` (D1/D5) — that owns the
per-run isolated store:

- ``start_keeper`` — ``acquire_or_discover(..., data_dir=store_root/'pgdb')``;
  surface the sidecar endpoint as ``Endpoint(host, port, pid, data_dir)``;
  register this process as the single consumer (kernel-fd lock).
- ``stop_keeper`` — ``request_force_shutdown`` (non-destructive ``.shutdown``
  marker; bridge flushes + exits next tick) so the next start needs no recovery.
- ``recover_keeper`` — re-acquire to clear stale heartbeat/lock residue
  automatically; refuse (``ConcurrentWriter``) — never kill — a live bridge child.
- ``engine_for`` — auto-start the keeper if none is live, return the engine.

Single-writer + the ``StoreUnavailable``/``IntegrityFailure``/``ConcurrentWriter``
error taxonomy live here (FR-012–016). Stub — implemented in Phase 5 (US3).
"""

from __future__ import annotations

__all__ = ["start_keeper", "stop_keeper", "recover_keeper", "engine_for"]
