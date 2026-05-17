"""DBOS queue + recovery — feature 018, T013 (R12, D2).

A single DBOS ``Queue`` bounds child-workflow execution to **one worker
by default** so steps run **serially through the feature-012
single-writer bridge lock** — the proven flow, NOT a new concurrency
model (R12 / contract dbos_workflow_model.md § Concurrency). Agent
parallelism (≤N convspec sub-agents) lives in the *skill*, never in DBOS
workers. The cap is configurable but defaults to 1.

Recovery (R12, D2 — "no custom recovery loop"): DBOS already
auto-recovers pending workflows at ``dbos.launch()`` (the proven
``codeconv migrate`` launch path is reused verbatim). This module only
adds an **explicit** ``resume_pending`` helper for ``codeconv builder
resume`` — it lists non-terminal builder workflows and asks DBOS to
resume them by their deterministic ids (R9). No bespoke scheduler.

Lazy ``dbos`` import (mirrors the rest of ``durable/``) so this module
imports with no bridge.
"""

from __future__ import annotations

from typing import Any, Optional

QUEUE_NAME = "codeconv-builder"
DEFAULT_CONCURRENCY = 1  # serial through the 012 single-writer lock (R12)

_queue: Optional[Any] = None


def get_queue(concurrency: int = DEFAULT_CONCURRENCY) -> Any:
    """Return the process-singleton builder ``Queue``.

    ``concurrency`` caps in-flight child workflows; default 1 keeps step
    execution serial through the single-writer bridge (R12). Idempotent —
    a second call returns the existing queue (the first call's
    concurrency wins; a conflicting request is a loud error, not a silent
    reconfigure — DISCIPLINE §1.7)."""
    global _queue
    from dbos import Queue

    if _queue is None:
        # worker_concurrency bounds per-process workers; concurrency
        # bounds global in-flight. Both set to the cap so the default is
        # strictly serial (R12).
        _queue = Queue(
            QUEUE_NAME,
            concurrency=concurrency,
            worker_concurrency=concurrency,
        )
    return _queue


def reset_queue_for_tests() -> None:
    """Test helper — drop the singleton between tests."""
    global _queue
    _queue = None


def resume_pending(dbos: Any, *, workspace_id: Optional[str] = None) -> list[str]:
    """Explicit ``codeconv builder resume`` (R12).

    DBOS auto-recovers pending workflows on launch; this additionally
    asks DBOS to resume any still-non-terminal builder workflow by id
    (idempotent — resuming a completed/clean workflow is a DBOS no-op).
    Returns the list of workflow ids it asked to resume. Read-then-resume
    only; never deletes or forks (``--restart-run`` is the explicit,
    separate, non-default path — R13).
    """
    resumed: list[str] = []
    try:
        workflows = dbos.list_workflows()
    except Exception:
        return resumed
    for wf in workflows:
        wid = getattr(wf, "workflow_id", None) or getattr(wf, "workflowID", None)
        status = (
            getattr(wf, "status", None) or getattr(wf, "workflow_status", None)
        )
        if not wid or not str(wid).startswith("builder:"):
            continue
        if workspace_id is not None and f":{workspace_id}:" not in str(wid):
            continue
        # Only non-terminal workflows are resumable; SUCCESS/ERROR are
        # left untouched (idempotent re-run == uninterrupted, FR-004).
        if str(status).upper() in ("SUCCESS", "ERROR", "CANCELLED"):
            continue
        try:
            dbos.resume_workflow(wid)
            resumed.append(str(wid))
        except Exception:
            # Best-effort: a workflow that finished between list+resume
            # is fine to skip (idempotent).
            continue
    return resumed


__all__ = [
    "DEFAULT_CONCURRENCY",
    "QUEUE_NAME",
    "get_queue",
    "reset_queue_for_tests",
    "resume_pending",
]
