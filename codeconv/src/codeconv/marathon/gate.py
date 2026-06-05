"""Per-stage approval gate — append-only, superseded decisions retained
(FR-004/005, D6). A recorded ``approve`` short-circuits the gate on resume
(no re-ask — SC-004).

This module is built in two parts: the **reader** (:func:`approval_state` /
:func:`latest_approval`) lands here for US1 (``marathon resume`` reports the
approval state); the **writer** (:func:`present_gate` / :func:`record_decision`)
lands in US2 (T025/T026).
"""

from __future__ import annotations

import json
from typing import Any, Optional


def _primary_approvals(store: Any, block_id: str) -> list[dict[str, Any]]:
    engine = store._primary()
    if engine is None:
        return []
    from sqlalchemy import text

    with engine.connect() as conn:
        rows = conn.execute(
            text(
                "SELECT id, presented_plan_ref, outcome, decided_by, decided_at, "
                "supersedes_id, created_at FROM marathon.approvals "
                "WHERE block_id = :bid ORDER BY id"
            ),
            {"bid": block_id},
        ).all()
    return [
        {
            "id": int(r.id),
            "block_id": block_id,
            "presented_plan_ref": r.presented_plan_ref,
            "outcome": r.outcome,
            "decided_by": r.decided_by,
            "decided_at": r.decided_at.isoformat() if r.decided_at else None,
            "supersedes_id": r.supersedes_id,
            "created_at": r.created_at.isoformat() if r.created_at else None,
        }
        for r in rows
    ]


def _fallback_approvals(store: Any, block_id: str) -> list[dict[str, Any]]:
    from .store import marathon_id_of_block

    adir = store._marathon_dir(marathon_id_of_block(block_id)) / "approvals"
    out: list[dict[str, Any]] = []
    if adir.is_dir():
        for fp in adir.glob("*.json"):
            try:
                d = json.loads(fp.read_text(encoding="utf-8"))
            except (OSError, ValueError):
                continue
            if d.get("block_id") == block_id:
                out.append(d)
    out.sort(key=lambda d: d.get("id", 0))
    return out


def approvals_for(store: Any, block_id: str) -> list[dict[str, Any]]:
    """All approval rows for a block, ordered by id (primary if reachable,
    else JSON mirror). Append-only — superseded rows are retained (D6)."""
    primary = store._primary()
    if primary is not None:
        return _primary_approvals(store, block_id)
    return _fallback_approvals(store, block_id)


def latest_approval(store: Any, block_id: str) -> Optional[dict[str, Any]]:
    """The most recent approval row for a block, or ``None`` if the gate has
    never been presented."""
    rows = approvals_for(store, block_id)
    return rows[-1] if rows else None


def approval_state(store: Any, block_id: str) -> Optional[str]:
    """The block's gate state from durable rows (never a summary):

    - ``"approved"`` — the latest decision is ``approve`` (short-circuits the
      gate on resume; SC-004);
    - ``"changed"``  — the latest decision is ``change`` (a re-plan is owed);
    - ``"awaiting"`` — a row exists but no decision yet;
    - ``None``       — the gate has not been presented.
    """
    latest = latest_approval(store, block_id)
    if latest is None:
        return None
    outcome = latest.get("outcome")
    if outcome == "approve":
        return "approved"
    if outcome == "change":
        return "changed"
    return "awaiting"


__all__ = [
    "approval_state",
    "approvals_for",
    "latest_approval",
]
