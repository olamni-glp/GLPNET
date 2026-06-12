"""Auto-mode escalation policy + the durable escalation writer (T048).

Ported 024 semantics over the per-run store: :func:`write_escalation` records
to the primary AND the JSON mirror (some escalations — notably
``store_divergence`` — arise precisely when the bridge is flapping; the
record must survive that). The auto-mode *policy* is unchanged: exactly two
block-points (the plan-approval gate + escalations); everything else
proceeds. New kinds vs 024 (data-model): ``budget_exceeded`` (dedicated, no
longer borrowing ``stage_flagged``) and ``prereq_against_completed_stage``
(written by intake)."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Optional

from codeconv.marathon.models import ESCALATION_KINDS, Escalation
from codeconv.marathon.store import Repository

__all__ = [
    "AutoDecision",
    "auto_decision",
    "is_block_point",
    "open_escalations",
    "preauthorizations",
    "write_escalation",
]


# --- auto-mode policy: exactly two block-points ------------------------------


@dataclass
class AutoDecision:
    """The outcome of consulting the auto-mode policy for one situation."""

    proceed: bool
    action: str
    block_point: Optional[str] = None  # "gate" | "escalation" when blocked
    escalation_kind: Optional[str] = None  # set when block_point=="escalation"


def auto_decision(
    situation: str,
    *,
    retry_budget_remaining: bool = False,
    approved: bool = False,
) -> AutoDecision:
    """The decision table. ``situation`` ∈ {subagent_failure,
    push_fast_forward, push_non_ff, reconcile_fast_forward, reconcile_fork,
    mutating_block, budget_ceiling, stage_flagged,
    prereq_against_completed_stage}."""
    if situation == "subagent_failure":
        if retry_budget_remaining:
            return AutoDecision(True, "rerun_from_checkpoint")
        return AutoDecision(False, "escalate", "escalation", "non_retryable_failure")
    if situation == "push_fast_forward":
        return AutoDecision(True, "commit_push_block")  # standing grant #1
    if situation == "push_non_ff":
        return AutoDecision(False, "escalate", "escalation", "push_blocked")
    if situation == "reconcile_fast_forward":
        return AutoDecision(True, "fast_forward_stale_store")
    if situation == "reconcile_fork":
        return AutoDecision(False, "escalate", "escalation", "store_divergence")
    if situation == "mutating_block":
        if approved:
            return AutoDecision(True, "proceed")  # approval on record — no re-ask
        return AutoDecision(False, "present_gate_and_wait", "gate")
    if situation == "budget_ceiling":
        return AutoDecision(
            False, "safe_checkpoint_then_halt", "escalation", "budget_exceeded"
        )
    if situation == "stage_flagged":
        return AutoDecision(False, "escalate", "escalation", "stage_flagged")
    if situation == "prereq_against_completed_stage":
        return AutoDecision(
            False, "escalate", "escalation", "prereq_against_completed_stage"
        )
    raise ValueError(f"unknown auto-mode situation {situation!r}")


def is_block_point(decision: AutoDecision) -> bool:
    """The two block-points are the only places the harness waits for Gabi."""
    return not decision.proceed and decision.block_point in ("gate", "escalation")


# --- durable writer + readers -------------------------------------------------


def write_escalation(
    store: Repository,
    run_id: str,
    *,
    kind: str,
    detail: dict[str, Any],
    stage_id: Optional[int] = None,
) -> Optional[int]:
    """Append an escalation row (primary + JSON mirror — dual-store). On
    creation the harness durably checkpoints and waits — never auto-resolves."""
    if kind not in ESCALATION_KINDS:
        raise ValueError(
            f"kind must be one of {ESCALATION_KINDS}, got {kind!r}"
        )
    esc = store.insert_escalation(
        Escalation(run_id=run_id, kind=kind, stage_id=stage_id, detail=detail)
    )
    return esc.id


def open_escalations(store: Repository, run_id: str) -> list[dict[str, Any]]:
    """Unresolved escalations (primary if reachable, else JSON mirror) —
    surfaced by ``marathon doctor``."""
    return [
        {
            "id": e.id,
            "kind": e.kind,
            "stage_id": e.stage_id,
            "detail": e.detail,
            "created_at": str(e.created_at) if e.created_at else None,
        }
        for e in store.list_escalations(run_id, open_only=True)
    ]


def preauthorizations(run: Any) -> dict[str, bool]:
    """The two — and only two — standing grants and their live state. Both
    are revoked together by ``preauth_revoked_at``."""
    revoked = getattr(run, "preauth_revoked_at", None) is not None
    return {
        "commit_push": bool(getattr(run, "preauth_commit_push", False))
        and not revoked,
        "workflow_optin": bool(getattr(run, "preauth_workflow_optin", False))
        and not revoked,
    }
