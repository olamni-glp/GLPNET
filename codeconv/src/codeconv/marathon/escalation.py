"""Auto-mode escalation policy + the durable escalation-row writer.

Two parts:

- :func:`write_escalation` (added here for US1 — store reconcile's fork path,
  T020): the durable writer. An escalation is recorded to the primary when
  reachable AND always mirrored to the JSON fallback, because some
  escalations (notably ``store_divergence``) arise precisely when the bridge
  is flapping — the record must survive that.
- The auto-mode *policy* (FR-022/023, D11) — exactly two block-points (the
  plan-approval gate + escalations) and the decision table — is consolidated
  in :func:`auto_decision` at Polish (T046).

The JSON fallback layout (data-model.md) does not enumerate an
``escalations/`` dir; this module adds one as the natural mirror so an
escalation is durable in fallback mode too (flagged for Gabi — small,
layout-consistent extension).
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any, Optional


# --- auto-mode policy: exactly two block-points (T046, FR-022/023, D11) -----


@dataclass
class AutoDecision:
    """The outcome of consulting the auto-mode policy for one situation."""

    proceed: bool
    action: str
    block_point: Optional[str] = None  # "gate" | "escalation" when not proceeding
    escalation_kind: Optional[str] = None  # set when block_point == "escalation"


# In auto-mode the harness is autonomous INSIDE an approved block and blocks
# for Gabi at exactly two kinds of point: (a) the plan-approval gate, and
# (b) escalations. Everything else proceeds. (contracts/escalation.md.)
def auto_decision(
    situation: str,
    *,
    retry_budget_remaining: bool = False,
    approved: bool = False,
) -> AutoDecision:
    """Encode the contracts/escalation.md decision table. ``situation`` is one
    of: ``subagent_failure``, ``push_fast_forward``, ``push_non_ff``,
    ``reconcile_fast_forward``, ``reconcile_fork``, ``mutating_block``,
    ``budget_ceiling``, ``stage_flagged``."""
    if situation == "subagent_failure":
        if retry_budget_remaining:
            return AutoDecision(True, "rerun_from_checkpoint")
        return AutoDecision(
            False, "escalate", "escalation", "non_retryable_failure"
        )
    if situation == "push_fast_forward":
        return AutoDecision(True, "commit_push_block")  # grant #1
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
        return AutoDecision(False, "safe_checkpoint_then_halt", "escalation")
    if situation == "stage_flagged":
        return AutoDecision(False, "escalate", "escalation", "stage_flagged")
    raise ValueError(f"unknown auto-mode situation {situation!r}")


def is_block_point(decision: AutoDecision) -> bool:
    """The two block-points are the only places the harness waits for Gabi."""
    return not decision.proceed and decision.block_point in ("gate", "escalation")


def write_escalation(
    store: Any,
    marathon_id: str,
    *,
    kind: str,
    detail: dict[str, Any],
    block_id: Optional[str] = None,
) -> Optional[int]:
    """Append an ``escalations`` row (append-only) to the primary (if up) and
    the JSON mirror (always). Returns the escalation id.

    ``kind`` ∈ {non_retryable_failure, store_divergence, push_blocked,
    stage_flagged} (data-model CHECK). On creation the harness durably
    checkpoints and waits — it never auto-resolves (FR-022)."""
    from .store import _atomic_write_json

    eid: Optional[int] = None
    created: Optional[datetime] = None

    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        with engine.begin() as conn:
            row = conn.execute(
                text(
                    "INSERT INTO marathon.escalations "
                    "(marathon_id, block_id, kind, detail) "
                    "VALUES (:mid, :bid, :kind, CAST(:detail AS jsonb)) "
                    "RETURNING id, created_at"
                ),
                {
                    "mid": marathon_id,
                    "bid": block_id,
                    "kind": kind,
                    "detail": json.dumps(detail),
                },
            ).one()
            eid = int(row.id)
            created = row.created_at

    if created is None:
        created = datetime.now(timezone.utc)

    edir = store._marathon_dir(marathon_id) / "escalations"
    if eid is None:
        existing = (
            [int(p.stem) for p in edir.glob("*.json") if p.stem.isdigit()]
            if edir.is_dir()
            else []
        )
        eid = (max(existing) + 1) if existing else 1

    _atomic_write_json(
        edir / f"{eid}.json",
        {
            "id": eid,
            "marathon_id": marathon_id,
            "block_id": block_id,
            "kind": kind,
            "detail": detail,
            "resolved_at": None,
            "created_at": created.isoformat(),
        },
    )
    return eid


def open_escalations(store: Any, marathon_id: str) -> list[dict[str, Any]]:
    """Return unresolved escalations (primary if reachable, else JSON mirror)
    — used by ``marathon doctor`` (contracts/cli.md)."""
    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        with engine.connect() as conn:
            rows = conn.execute(
                text(
                    "SELECT id, kind, block_id, detail, created_at "
                    "FROM marathon.escalations "
                    "WHERE marathon_id = :mid AND resolved_at IS NULL "
                    "ORDER BY id"
                ),
                {"mid": marathon_id},
            ).all()
        return [
            {
                "id": int(r.id),
                "kind": r.kind,
                "block_id": r.block_id,
                "detail": r.detail,
                "created_at": r.created_at.isoformat() if r.created_at else None,
            }
            for r in rows
        ]

    edir = store._marathon_dir(marathon_id) / "escalations"
    out: list[dict[str, Any]] = []
    if edir.is_dir():
        for fp in sorted(edir.glob("*.json")):
            try:
                d = json.loads(fp.read_text(encoding="utf-8"))
            except (OSError, ValueError):
                continue
            if d.get("resolved_at") is None:
                out.append(d)
    return out


def preauthorizations(marathon: Any) -> dict[str, bool]:
    """The two — and only two — standing grants and their live state (D10).
    Both are revoked together by ``preauth_revoked_at``."""
    revoked = getattr(marathon, "preauth_revoked_at", None) is not None
    return {
        "commit_push": bool(getattr(marathon, "preauth_commit_push", False))
        and not revoked,
        "workflow_optin": bool(getattr(marathon, "preauth_workflow_optin", False))
        and not revoked,
    }


__all__ = [
    "AutoDecision",
    "auto_decision",
    "is_block_point",
    "open_escalations",
    "preauthorizations",
    "write_escalation",
]
