"""Data-driven stage vocabulary (US1, T014/T017).

Replaces 024's hard-coded ``STAGES`` tuple + canonical-collapse cadence (D3):

- ``register_run(run_id, *, stages, title, budget_ceiling, budget_unit, env)`` —
  write the ``run`` row + its initial ordered ``stage`` rows (``origin='registered'``;
  empty stage list is legal). Declarative manifest registration is deferred (no FR).
- ``append_stage(run_id, name, *, env)`` — append a dynamically-discovered stage
  (``stage_index = max+1``, ``order_key = max+1``, ``origin='dynamic'``); the total
  grows and progress reports against the *current* total (FR-001/002/003).
- ``finalize(run_id, *, env)`` — status→finalized only when every current stage is
  complete; re-opens cleanly if a later append/capture occurs.

Stub — implemented in Phase 3 (US1, T014/T017).
"""

from __future__ import annotations

__all__ = ["register_run", "append_stage", "finalize"]
