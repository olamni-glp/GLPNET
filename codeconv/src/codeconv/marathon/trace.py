"""Append-only verification-trace substrate over the per-run store (T047).

Ported 024 semantics, unchanged (FR-023): one record per ``(run, subject,
refine_seq)``; earlier iterations are never overwritten (the UNIQUE
constraint + monotonic ``refine_seq``); :func:`list_traces` is ordered by
``(subject, refine_seq)`` so an external reader reconstructs iteration order
with no harness internals. Substrate ONLY — no optimizer/loop (GEPA/DSPy out
of scope; Constitution V keeps any LM in Claude)."""

from __future__ import annotations

from typing import Any, Optional

from codeconv.marathon.models import TRACE_DECISIONS, VerificationTrace
from codeconv.marathon.store import Repository

__all__ = ["list_traces", "next_refine_seq", "write_trace"]


def next_refine_seq(store: Repository, run_id: str, subject: str) -> int:
    """One past the current max for ``(run, subject)`` — never overwrites."""
    existing = store.list_traces(run_id, subject=subject)
    return max((t.refine_seq for t in existing), default=0) + 1


def write_trace(
    store: Repository,
    run_id: str,
    *,
    subject: str,
    experiment_input: dict[str, Any],
    metric_score: Optional[float] = None,
    decision: str = "accept",
    refine_seq: Optional[int] = None,
) -> VerificationTrace:
    """Append a verification-trace record (primary + JSON mirror). Allocates
    the next ``refine_seq`` unless one is supplied; an explicit duplicate is
    refused by the UNIQUE constraint (append-only, never overwritten)."""
    if decision not in TRACE_DECISIONS:
        raise ValueError(
            f"decision must be one of {TRACE_DECISIONS}, got {decision!r}"
        )
    if refine_seq is None:
        refine_seq = next_refine_seq(store, run_id, subject)
    return store.insert_trace(
        VerificationTrace(
            run_id=run_id,
            subject=subject,
            refine_seq=refine_seq,
            experiment_input=experiment_input,
            metric_score=metric_score,
            decision=decision,
        )
    )


def list_traces(
    store: Repository, run_id: str, *, subject: Optional[str] = None
) -> list[VerificationTrace]:
    """Ordered trace history — by ``(subject, refine_seq)`` (FR-023)."""
    return store.list_traces(run_id, subject=subject)
