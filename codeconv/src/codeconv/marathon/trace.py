"""Append-only verification-trace substrate (FR-016/017, US7).

Records experiment_input / metric_score / decision per ``(subject,
refine_seq)``; earlier iterations are never overwritten (the UNIQUE
constraint + monotonic ``refine_seq`` enforce US7-AS2). Substrate ONLY — no
optimizer/loop (the GEPA/DSPy optimizer is out of scope, Gabi 2026-06-05);
an external optimizer can reconstruct the ordered history from these rows
with no harness internals (US7-AS3).

The writer is shared by US4's verification spike (``subject=workflow-spike``)
and US7's ``marathon trace`` subcommand.
"""

from __future__ import annotations

import json
import re
from datetime import datetime, timezone
from typing import Any, Optional

from .models import TRACE_DECISIONS, VerificationTrace

_UNSAFE = re.compile(r"[^A-Za-z0-9_-]+")


def _safe_subject(subject: str) -> str:
    """Map a subject to a filesystem-safe token for the JSON mirror name."""
    return _UNSAFE.sub("_", subject)


def next_refine_seq(store: Any, marathon_id: str, subject: str) -> int:
    """The next ordered iteration index for ``(marathon_id, subject)`` — one
    past the current max (earlier iterations are never overwritten)."""
    existing = list_traces(store, marathon_id, subject=subject)
    return max((t["refine_seq"] for t in existing), default=0) + 1


def write_trace(
    store: Any,
    marathon_id: str,
    *,
    subject: str,
    experiment_input: dict[str, Any],
    metric_score: Optional[float] = None,
    decision: str = "accept",
    refine_seq: Optional[int] = None,
) -> VerificationTrace:
    """Append a verification-trace record to the primary (if up) and the JSON
    mirror (always). Allocates the next ``refine_seq`` for the subject unless
    one is supplied. Append-only."""
    if decision not in TRACE_DECISIONS:
        raise ValueError(f"decision must be one of {TRACE_DECISIONS}, got {decision!r}")
    from .store import _atomic_write_json

    if refine_seq is None:
        refine_seq = next_refine_seq(store, marathon_id, subject)

    tid: Optional[int] = None
    created: Optional[datetime] = None
    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        with engine.begin() as conn:
            row = conn.execute(
                text(
                    "INSERT INTO marathon.verification_traces "
                    "(marathon_id, subject, refine_seq, experiment_input, "
                    " metric_score, decision) "
                    "VALUES (:mid, :subj, :seq, CAST(:inp AS jsonb), :score, :dec) "
                    "RETURNING id, created_at"
                ),
                {
                    "mid": marathon_id,
                    "subj": subject,
                    "seq": refine_seq,
                    "inp": json.dumps(experiment_input),
                    "score": metric_score,
                    "dec": decision,
                },
            ).one()
            tid = int(row.id)
            created = row.created_at

    if created is None:
        created = datetime.now(timezone.utc)
    if tid is None:
        # Fallback mode has no bigserial; (subject, refine_seq) is the stable
        # identity — surface refine_seq as the local id so callers get a
        # non-null handle.
        tid = refine_seq

    _atomic_write_json(
        store._marathon_dir(marathon_id)
        / "traces"
        / f"{_safe_subject(subject)}-{refine_seq}.json",
        {
            "id": tid,
            "marathon_id": marathon_id,
            "subject": subject,
            "refine_seq": refine_seq,
            "experiment_input": experiment_input,
            "metric_score": metric_score,
            "decision": decision,
            "created_at": created.isoformat(),
        },
    )
    return VerificationTrace(
        id=tid,
        marathon_id=marathon_id,
        subject=subject,
        refine_seq=refine_seq,
        experiment_input=experiment_input,
        metric_score=metric_score,
        decision=decision,
        created_at=created,
    )


def list_traces(
    store: Any, marathon_id: str, *, subject: Optional[str] = None
) -> list[dict[str, Any]]:
    """Ordered trace history (primary if reachable, else JSON mirror), sorted
    by ``(subject, refine_seq)`` so an external reader reconstructs iteration
    order with no harness internals (US7-AS3)."""
    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        sql = (
            "SELECT id, subject, refine_seq, experiment_input, metric_score, "
            "decision, created_at FROM marathon.verification_traces "
            "WHERE marathon_id = :mid"
        )
        params: dict[str, Any] = {"mid": marathon_id}
        if subject is not None:
            sql += " AND subject = :subj"
            params["subj"] = subject
        sql += " ORDER BY subject, refine_seq"
        with engine.connect() as conn:
            rows = conn.execute(text(sql), params).all()
        return [
            {
                "id": int(r.id),
                "subject": r.subject,
                "refine_seq": int(r.refine_seq),
                "experiment_input": r.experiment_input,
                "metric_score": r.metric_score,
                "decision": r.decision,
                "created_at": r.created_at.isoformat() if r.created_at else None,
            }
            for r in rows
        ]

    tdir = store._marathon_dir(marathon_id) / "traces"
    out: list[dict[str, Any]] = []
    if tdir.is_dir():
        for fp in tdir.glob("*.json"):
            try:
                d = json.loads(fp.read_text(encoding="utf-8"))
            except (OSError, ValueError):
                continue
            if subject is None or d.get("subject") == subject:
                out.append(d)
    out.sort(key=lambda d: (d.get("subject", ""), d.get("refine_seq", 0)))
    return out


__all__ = ["list_traces", "next_refine_seq", "write_trace"]
