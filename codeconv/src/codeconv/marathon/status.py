"""Standardized periodic status report (FR-013, D8, SC-005).

Exactly four fields — **done / issues / tokens (spent + remaining) / to-do** —
built from durable state and persisted to ``marathon.status_reports`` (+ JSON
mirror). During active work the report is emitted on a ~5-min cadence; D8
makes the cadence the orchestration's own loop (a report at every checkpoint
boundary and via the Workflow run's periodic ``log()``), NOT a separate
daemon — so the buildkit-stage skill calls ``marathon status --emit`` at each
checkpoint, comfortably satisfying SC-005's "at least once per 5-min interval."
"""

from __future__ import annotations

import json
from datetime import datetime, timezone
from typing import Any, Optional

from .escalation import open_escalations
from .models import StatusReport


def build_status(store: Any, marathon_id: str) -> StatusReport:
    """Assemble the four-field report from durable state (never a summary):
    done/to-do from the max-seq checkpoint, issues from open escalations,
    tokens from the marathon budget."""
    pos = store.read_position(marathon_id)
    m = store.read_marathon(marathon_id)

    done = list(pos.completed_units) if pos is not None else []
    todo = list(pos.remaining_units) if pos is not None else []
    issues = [
        {"id": e.get("id"), "kind": e.get("kind"), "block_id": e.get("block_id")}
        for e in open_escalations(store, marathon_id)
    ]
    spent = m.budget_spent if m is not None else (pos.budget_spent if pos else 0)
    remaining = (
        (m.budget_ceiling - spent)
        if (m is not None and m.budget_ceiling is not None)
        else None
    )
    return StatusReport(
        marathon_id=marathon_id,
        block_id=pos.block_id if pos is not None else None,
        done=done,
        issues=issues,
        tokens_spent=spent,
        tokens_remaining=remaining,
        todo=todo,
    )


def emit_status(store: Any, marathon_id: str) -> StatusReport:
    """Build + persist a status report (the ``--emit`` cadence driver)."""
    report = build_status(store, marathon_id)
    _persist(store, report)
    return report


def _persist(store: Any, report: StatusReport) -> None:
    from .store import _atomic_write_json

    sid: Optional[int] = None
    created = None
    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        with engine.begin() as conn:
            row = conn.execute(
                text(
                    "INSERT INTO marathon.status_reports "
                    "(marathon_id, block_id, done, issues, tokens_spent, "
                    " tokens_remaining, todo) "
                    "VALUES (:mid, :bid, CAST(:done AS jsonb), CAST(:issues AS jsonb), "
                    " :spent, :remaining, CAST(:todo AS jsonb)) "
                    "RETURNING id, created_at"
                ),
                {
                    "mid": report.marathon_id,
                    "bid": report.block_id,
                    "done": json.dumps(report.done),
                    "issues": json.dumps(report.issues),
                    "spent": report.tokens_spent,
                    "remaining": report.tokens_remaining,
                    "todo": json.dumps(report.todo),
                },
            ).one()
            sid = int(row.id)
            created = row.created_at

    if created is None:
        created = datetime.now(timezone.utc)
    sdir = store._marathon_dir(report.marathon_id) / "status"
    if sid is None:
        existing = (
            [int(p.stem) for p in sdir.glob("*.json") if p.stem.isdigit()]
            if sdir.is_dir()
            else []
        )
        sid = (max(existing) + 1) if existing else 1
    report.id = sid
    report.created_at = created
    _atomic_write_json(sdir / f"{sid}.json", status_to_dict(report))


def status_to_dict(report: StatusReport) -> dict[str, Any]:
    return {
        "id": report.id,
        "marathon_id": report.marathon_id,
        "block_id": report.block_id,
        "done": report.done,
        "issues": report.issues,
        "tokens_spent": report.tokens_spent,
        "tokens_remaining": report.tokens_remaining,
        "todo": report.todo,
        "created_at": report.created_at.isoformat() if report.created_at else None,
    }


def list_status(store: Any, marathon_id: str) -> list[dict[str, Any]]:
    """All persisted status reports for a marathon, oldest first (primary if
    reachable, else JSON mirror)."""
    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        with engine.connect() as conn:
            rows = conn.execute(
                text(
                    "SELECT id, block_id, done, issues, tokens_spent, "
                    "tokens_remaining, todo, created_at "
                    "FROM marathon.status_reports WHERE marathon_id = :mid ORDER BY id"
                ),
                {"mid": marathon_id},
            ).all()
        return [
            {
                "id": int(r.id),
                "block_id": r.block_id,
                "done": r.done,
                "issues": r.issues,
                "tokens_spent": int(r.tokens_spent),
                "tokens_remaining": r.tokens_remaining,
                "todo": r.todo,
                "created_at": r.created_at.isoformat() if r.created_at else None,
            }
            for r in rows
        ]

    sdir = store._marathon_dir(marathon_id) / "status"
    out: list[dict[str, Any]] = []
    if sdir.is_dir():
        for fp in sdir.glob("*.json"):
            try:
                out.append(json.loads(fp.read_text(encoding="utf-8")))
            except (OSError, ValueError):
                continue
    out.sort(key=lambda d: d.get("id", 0))
    return out


__all__ = ["build_status", "emit_status", "list_status", "status_to_dict"]
