"""Resume position — derived solely from durable rows (US1/US2, T016/T024/T037).

``derive_position(run, stages, checkpoints, open_issues)`` is the **pure** core
(unit-tested bridge-free): it returns a ``ResumePosition(run_id, done, total,
outstanding_issues, budget_spent, budget_unit, next_action, status)`` using only
durable row content + ``order_key``/``status`` — no timestamps, no randomness, no
conversation memory — so it is byte-identical with full context or after total
context loss (SC-008). ``resume_position(run_id, *, env)`` is the I/O wrapper.

Ordering rules per ``contracts/resume-position.md`` (first non-complete stage in
``order_key`` order, with re-drive-commit / approval-gate / mini-stage / blocking-
prereq / empty-list / all-complete / store-fork refinements).

Stub — implemented across US1 (T016), US2 (T024), US4 (T037).
"""

from __future__ import annotations

__all__ = ["derive_position", "resume_position"]
