"""Parseable status line + persisted status report (T038, FR-019).

Grammar (``contracts/status-line.md``, fixed, pipe-delimited):

    marathon <run_id> | done=<d>/<n> | open=<k> | budget=<b>[<unit>] | next=<action>

``<n>`` is the **current** total stage count (grows with append/capture).
:func:`build_status_line` is pure (bridge-free testable) from a
``ResumePosition``; :func:`status_line` derives the position first.
:func:`emit_status` additionally persists a four-field ``status_report`` row —
called at every stage boundary (from ``checkpoint``) and on demand (CLI
``status --emit``), per 024's D8 doctrine: the cadence is the orchestration's
own loop, not a daemon.
"""

from __future__ import annotations

from typing import Optional

from codeconv.marathon.env import resolve_env
from codeconv.marathon.models import MarathonEnv, ResumePosition, StatusReport
from codeconv.marathon.position import resume_position
from codeconv.marathon.store import Repository

__all__ = ["build_status_line", "emit_status", "status_line"]


def build_status_line(pos: ResumePosition) -> str:
    """Pure: the fixed four-field grammar from a derived position."""
    unit = pos.budget_unit or ""
    return (
        f"marathon {pos.run_id} | done={pos.done}/{pos.total} | "
        f"open={pos.outstanding_issues} | budget={pos.budget_spent}{unit} | "
        f"next={pos.next_action}"
    )


def status_line(run_id: str, *, env: Optional[MarathonEnv] = None) -> str:
    return build_status_line(resume_position(run_id, env=env))


def emit_status(
    run_id: str, *, env: Optional[MarathonEnv] = None
) -> StatusReport:
    """Build + persist the four-field report (done / issues / tokens / todo)
    from durable state — never a summary."""
    env = env if env is not None else resolve_env(run_id)
    repo = Repository(env)
    run = repo.get_run(run_id)
    if run is None:
        raise ValueError(f"unknown run '{run_id}'; register it first")
    stages = sorted(
        repo.list_stages(run_id), key=lambda s: (s.order_key, s.stage_index)
    )
    open_issues = repo.list_issues(run_id, status="open")
    first_open = next((s for s in stages if s.status != "complete"), None)
    report = StatusReport(
        run_id=run_id,
        stage_id=first_open.id if first_open is not None else None,
        done=[s.name for s in stages if s.status == "complete"],
        issues=[i.summary for i in open_issues],
        tokens_spent=run.budget_spent,
        tokens_remaining=(
            run.budget_ceiling - run.budget_spent
            if run.budget_ceiling is not None
            else None
        ),
        todo=[s.name for s in stages if s.status != "complete"],
    )
    return repo.insert_status_report(report)
