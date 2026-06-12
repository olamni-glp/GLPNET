"""Emergent work capture + 5-stage mini-pipeline (US2, T021–T024).

``capture_item(run_id, *, kind, title, description, blocks_stage, env)`` records a
typed ``item`` (``kind ∈ ITEM_KINDS``), creates ``<store_root>/items/<id>/``
(FR-008), and expands it into exactly five mini-stages
``mini_specify → mini_clarify → mini_plan → mini_tasks → mini_analyze``
(``origin='mini'``; **no per-item implement** — D4/FR-006) feeding the marathon's
single ``implement`` stage. A blocking ``missing_prerequisite`` with ``blocks_stage``
gets fractional ``order_key``s placed strictly BEFORE the blocked stage (FR-010);
otherwise after the current max. Stacked blocking items get distinct fractional
bands (collision-free). An already-complete target → escalate
``prereq_against_completed_stage``. Never auto-advances (FR-009, default-deny).

Stub — implemented in Phase 4 (US2, T021–T024).
"""

from __future__ import annotations

__all__ = ["capture_item"]
