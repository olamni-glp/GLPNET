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


# --- writer (US2: T025/T026) -----------------------------------------------


def _approvals_dir(store: Any, block_id: str):
    from .store import marathon_id_of_block

    return store._marathon_dir(marathon_id_of_block(block_id)) / "approvals"


def _append_approval(
    store: Any,
    block_id: str,
    *,
    plan_ref: str,
    outcome: Optional[str],
    decided_by: Optional[str] = None,
    supersedes_id: Optional[int] = None,
) -> int:
    """Append an approval row (primary if up + JSON mirror). ``outcome`` may be
    ``None`` (a presented-but-undecided row) or ``approve``/``change``."""
    from datetime import datetime, timezone

    from .store import _atomic_write_json

    decided_at = (
        datetime.now(timezone.utc) if outcome in ("approve", "change") else None
    )
    aid: Optional[int] = None
    created = None
    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        with engine.begin() as conn:
            row = conn.execute(
                text(
                    "INSERT INTO marathon.approvals "
                    "(block_id, presented_plan_ref, outcome, decided_by, "
                    " decided_at, supersedes_id) "
                    "VALUES (:bid, :plan, :outcome, :by, :dat, :sup) "
                    "RETURNING id, created_at"
                ),
                {
                    "bid": block_id,
                    "plan": plan_ref,
                    "outcome": outcome,
                    "by": decided_by,
                    "dat": decided_at,
                    "sup": supersedes_id,
                },
            ).one()
            aid = int(row.id)
            created = row.created_at

    if created is None:
        created = datetime.now(timezone.utc)
    adir = _approvals_dir(store, block_id)
    if aid is None:
        existing = (
            [int(p.stem) for p in adir.glob("*.json") if p.stem.isdigit()]
            if adir.is_dir()
            else []
        )
        aid = (max(existing) + 1) if existing else 1

    _atomic_write_json(
        adir / f"{aid}.json",
        {
            "id": aid,
            "block_id": block_id,
            "presented_plan_ref": plan_ref,
            "outcome": outcome,
            "decided_by": decided_by,
            "decided_at": decided_at.isoformat() if decided_at else None,
            "supersedes_id": supersedes_id,
            "created_at": created.isoformat(),
        },
    )
    return aid


def _set_outcome(
    store: Any, block_id: str, approval_id: int, *, outcome: str, decided_by: Optional[str]
) -> None:
    """Finalize a presented (pending) row with a terminal outcome (D6 — the
    decision is recorded on that row; not a history rewrite)."""
    from datetime import datetime, timezone

    decided_at = datetime.now(timezone.utc)
    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        with engine.begin() as conn:
            conn.execute(
                text(
                    "UPDATE marathon.approvals SET outcome = :o, decided_by = :by, "
                    "decided_at = :dat WHERE id = :id"
                ),
                {"o": outcome, "by": decided_by, "dat": decided_at, "id": approval_id},
            )
    path = _approvals_dir(store, block_id) / f"{approval_id}.json"
    if path.is_file():
        try:
            d = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return
        d["outcome"] = outcome
        d["decided_by"] = decided_by
        d["decided_at"] = decided_at.isoformat()
        from .store import _atomic_write_json

        _atomic_write_json(path, d)


def present_gate(store: Any, block_id: str, *, plan_ref: str) -> dict[str, Any]:
    """Present the block's plan for approval. Idempotent: if already approved,
    report ``approved`` (no re-ask, SC-004); otherwise ensure a single pending
    row exists and report ``awaiting``."""
    state = approval_state(store, block_id)
    if state == "approved":
        return {"state": "approved", "block_id": block_id, "plan_ref": plan_ref}

    rows = approvals_for(store, block_id)
    pending = [r for r in rows if r.get("outcome") is None]
    if not pending:
        supersedes = (
            rows[-1]["id"] if rows and rows[-1].get("outcome") == "change" else None
        )
        _append_approval(
            store, block_id, plan_ref=plan_ref, outcome=None, supersedes_id=supersedes
        )
    return {"state": "awaiting", "block_id": block_id, "plan_ref": plan_ref}


def record_decision(
    store: Any,
    block_id: str,
    *,
    outcome: str,
    decided_by: Optional[str] = None,
    plan_ref: Optional[str] = None,
) -> dict[str, Any]:
    """Record ``approve`` / ``change`` (FR-004/D6). Finalizes the latest
    pending presented row; if none exists, appends a decided row. Append-only:
    a superseded decision is retained (US2-AS3)."""
    if outcome not in ("approve", "change"):
        raise ValueError(f"outcome must be approve|change, got {outcome!r}")
    rows = approvals_for(store, block_id)
    pending = [r for r in rows if r.get("outcome") is None]
    if pending:
        approval_id = pending[-1]["id"]
        _set_outcome(store, block_id, approval_id, outcome=outcome, decided_by=decided_by)
    else:
        approval_id = _append_approval(
            store,
            block_id,
            plan_ref=plan_ref or "(none)",
            outcome=outcome,
            decided_by=decided_by,
            supersedes_id=(rows[-1]["id"] if rows else None),
        )
    return {
        "state": "approved" if outcome == "approve" else "changed",
        "approval_id": approval_id,
        "block_id": block_id,
    }


def should_present_gate(store: Any, block_id: str) -> bool:
    """Resume short-circuit (FR-005/SC-004): a recorded ``approve`` means the
    gate is NOT re-presented."""
    return approval_state(store, block_id) != "approved"


__all__ = [
    "approval_state",
    "approvals_for",
    "latest_approval",
    "present_gate",
    "record_decision",
    "should_present_gate",
]
