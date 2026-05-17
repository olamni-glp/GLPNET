"""DBOS workflow-trace read model — feature 018, T014 (D1=a trace half).

``codeconv builder trace`` exposes the **DBOS workflow/step history**
for debugging & planning — the explicit "queryable workflow-trace
analysis" half of D1=a. Per contract ``status_trace_contract.md`` this
is a **read-only projection over DBOS's own state** (no competing event
store — D2): we use the supported DBOS API (``list_workflows`` /
``list_workflow_steps`` / ``get_workflow_status``), which reads
``dbos.workflow_status`` / ``dbos.operation_outputs`` natively, and join
to files/runs via the deterministic ``builder:``/``file:``/``scc:``
workflow-id grammar (R9) and ``codeconv.builder_runs.outer_workflow_id``.

Pure-shaped + lazy ``dbos`` import: the projection *shape* is testable
with a fake DBOS; production passes the launched singleton.
"""

from __future__ import annotations

from typing import Any, Optional

from codeconv.durable import file_workflow_id


def _wf_field(wf: Any, *names: str) -> Any:
    for n in names:
        v = getattr(wf, n, None)
        if v is not None:
            return v
    if isinstance(wf, dict):
        for n in names:
            if n in wf:
                return wf[n]
    return None


def _steps_for(dbos: Any, workflow_id: str) -> list[dict]:
    """Per-step history for one workflow id (stage, status, times,
    attempts) — read-only from DBOS's ``operation_outputs``."""
    try:
        raw = dbos.list_workflow_steps(workflow_id)
    except Exception:
        return []
    steps: list[dict] = []
    for s in raw or []:
        steps.append(
            {
                "stage": _wf_field(s, "function_name", "name", "step_name"),
                "status": _wf_field(s, "status", "outcome"),
                "started": _wf_field(s, "started_at", "created_at"),
                "finished": _wf_field(s, "completed_at", "updated_at"),
                "attempts": _wf_field(s, "attempts", "attempt") or 1,
            }
        )
    return steps


def trace_run(dbos: Any, outer_workflow_id: str) -> dict:
    """Every child workflow + its steps for one builder run (``--run``).

    Joined by the deterministic id grammar: the outer id is
    ``builder:{ws}:{epoch}``; its children are the ``file:``/``scc:``
    workflows launched during it (DBOS records the spawn lineage). For
    debugging/planning per contract status_trace_contract.md.
    """
    out: dict[str, Any] = {"workflow_id": outer_workflow_id, "children": []}
    try:
        workflows = dbos.list_workflows()
    except Exception:
        workflows = []
    for wf in workflows or []:
        wid = str(_wf_field(wf, "workflow_id", "workflowID") or "")
        if not (wid.startswith("file:") or wid.startswith("scc:")):
            continue
        out["children"].append(
            {
                "workflow_id": wid,
                "status": _wf_field(wf, "status", "workflow_status"),
                "steps": _steps_for(dbos, wid),
            }
        )
    # The outer workflow's own step record (the unit launches).
    out["outer_steps"] = _steps_for(dbos, outer_workflow_id)
    return out


def trace_file(dbos: Any, rel_path: str) -> dict:
    """That file's step history across runs (``--file``).

    The per-file child id is deterministic (``file:{sha(rel_path)}`` —
    R9), so the same file maps to the same child id every run; DBOS
    keeps each run's attempt history under it.
    """
    wid = file_workflow_id(rel_path)
    status = None
    try:
        status = dbos.get_workflow_status(wid)
    except Exception:
        status = None
    return {
        "file": rel_path,
        "workflow_id": wid,
        "status": _wf_field(status, "status", "workflow_status")
        if status is not None
        else None,
        "steps": _steps_for(dbos, wid),
    }


__all__ = ["trace_file", "trace_run"]
