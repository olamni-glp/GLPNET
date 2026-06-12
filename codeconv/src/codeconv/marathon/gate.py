"""Per-stage approval gate over the data-driven stage model (T045, FR-020).

Ported 024 semantics, unchanged: append-only ``approval`` rows keyed by
``stage_id``; superseded decisions are retained (the ``supersedes_id`` chain);
a recorded ``approve`` **short-circuits** the gate on resume (no re-ask).

Stage-status linkage (resume rule 2b): ``present_gate`` flips the stage to
``awaiting_approval`` (the position then names "approve gate for <stage>");
``record_decision('approve')`` flips it back to ``pending`` so resume names
the stage's work next — the short-circuit is thereby pure durable-row state.
"""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any, Optional

from codeconv.marathon.models import Approval
from codeconv.marathon.store import Repository

__all__ = [
    "approval_state",
    "approvals_for",
    "latest_approval",
    "present_gate",
    "record_decision",
    "should_present_gate",
]


def approvals_for(store: Repository, stage_id: int) -> list[Approval]:
    """All approval rows for a stage, ordered by id. Append-only — superseded
    rows are retained."""
    return store.list_approvals(stage_id)


def latest_approval(store: Repository, stage_id: int) -> Optional[Approval]:
    rows = approvals_for(store, stage_id)
    return rows[-1] if rows else None


def approval_state(store: Repository, stage_id: int) -> Optional[str]:
    """``approved`` | ``changed`` | ``awaiting`` | ``None`` (never presented),
    from durable rows alone (FR-020)."""
    latest = latest_approval(store, stage_id)
    if latest is None:
        return None
    if latest.outcome == "approve":
        return "approved"
    if latest.outcome == "change":
        return "changed"
    return "awaiting"


def should_present_gate(store: Repository, stage_id: int) -> bool:
    """Resume short-circuit: a recorded ``approve`` is never re-asked."""
    return approval_state(store, stage_id) != "approved"


def present_gate(
    store: Repository, stage_id: int, *, plan_ref: str
) -> dict[str, Any]:
    """Present the stage's plan for approval. Idempotent: already-approved
    reports ``approved`` (no re-ask); otherwise ensure a single pending row
    and flip the stage to ``awaiting_approval``."""
    state = approval_state(store, stage_id)
    if state == "approved":
        return {"state": "approved", "stage_id": stage_id, "plan_ref": plan_ref}

    rows = approvals_for(store, stage_id)
    pending = [r for r in rows if r.outcome is None]
    if not pending:
        supersedes = (
            rows[-1].id if rows and rows[-1].outcome == "change" else None
        )
        store.insert_approval(
            Approval(
                stage_id=stage_id,
                presented_plan_ref=plan_ref,
                supersedes_id=supersedes,
            )
        )
    store.update_stage(stage_id, status="awaiting_approval")
    return {"state": "awaiting", "stage_id": stage_id, "plan_ref": plan_ref}


def record_decision(
    store: Repository,
    stage_id: int,
    *,
    outcome: str,
    decided_by: Optional[str] = None,
    plan_ref: Optional[str] = None,
) -> dict[str, Any]:
    """Record ``approve`` / ``change``. Finalizes the latest pending row (the
    decision lands on that row — not a history rewrite); with none pending,
    appends a decided row superseding the latest. Append-only."""
    if outcome not in ("approve", "change"):
        raise ValueError(f"outcome must be approve|change, got {outcome!r}")
    rows = approvals_for(store, stage_id)
    pending = [r for r in rows if r.outcome is None]
    if pending:
        approval = store.update_approval(
            pending[-1].id,
            outcome=outcome,
            decided_by=decided_by,
            decided_at=datetime.now(timezone.utc),
        )
    else:
        approval = store.insert_approval(
            Approval(
                stage_id=stage_id,
                presented_plan_ref=plan_ref or "(none)",
                outcome=outcome,
                decided_by=decided_by,
                decided_at=datetime.now(timezone.utc),
                supersedes_id=rows[-1].id if rows else None,
            )
        )
    if outcome == "approve":
        # Short-circuit substrate: the stage leaves awaiting_approval, so the
        # resume position names its work, not the gate (FR-020).
        store.update_stage(stage_id, status="pending")
    return {
        "state": "approved" if outcome == "approve" else "changed",
        "approval_id": approval.id,
        "stage_id": stage_id,
    }
