"""Stage lifecycle + durable checkpoint over the data-driven model (T015).

- :func:`start_stage` — flip ``pending→running``, set ``started_at``. A
  started-not-complete stage is NOT counted done (FR-004); re-starting a
  running stage is an idempotent no-op; a complete stage is refused (per-block
  re-run is US5's ``rerun_block``).
- :func:`checkpoint` — append a durable checkpoint (the sole source of truth
  for resume): ``remaining_units == []`` flips the stage ``complete`` and sets
  ``completed_at``; ``stage.last_sequence_no`` tracks the latest checkpoint;
  ``budget_delta`` accumulates onto ``run.budget_spent`` (the substrate CHECK
  ``spent <= ceiling`` guards overruns — SC-006; the graceful halt-escalate
  path is US5's ``advance_budget_or_halt``). Completing an item's
  ``mini_analyze`` flips the parent item ``done`` (D4/FR-007, T024).
  ``message`` is accepted for CLI parity and reserved for the scoped-commit
  boundary (US4 ``gitblock``); ``committed_paths`` are recorded on the row.

``units_to_execute`` (skip-completed, preserved from 024) feeds the per-block
re-run semantics (US5).
"""

from __future__ import annotations

import json
from datetime import datetime, timezone
from typing import Any, Optional, Sequence

from codeconv.marathon.env import resolve_env
from codeconv.marathon.models import (
    CheckpointRow,
    Issue,
    MarathonEnv,
    StageRow,
)
from codeconv.marathon.store import Repository

__all__ = ["checkpoint", "start_stage", "units_to_execute"]


def _repo(run_id: str, env: Optional[MarathonEnv]) -> tuple[MarathonEnv, Repository]:
    env = env if env is not None else resolve_env(run_id)
    return env, Repository(env)


def _stage_or_raise(repo: Repository, run_id: str, name: str) -> StageRow:
    stage = repo.get_stage(run_id, name)
    if stage is None:
        raise ValueError(f"unknown stage '{name}' in run '{run_id}'")
    return stage


def start_stage(
    run_id: str, name: str, *, env: Optional[MarathonEnv] = None
) -> StageRow:
    """Flip a stage to ``running`` and stamp ``started_at`` (FR-004)."""
    env, repo = _repo(run_id, env)
    stage = _stage_or_raise(repo, run_id, name)
    if stage.status == "complete":
        raise ValueError(
            f"stage '{name}' is already complete; re-running a block is "
            f"US5's rerun, not stage-start"
        )
    if stage.status == "running":
        return stage  # idempotent
    return repo.update_stage(
        stage.id, status="running", started_at=datetime.now(timezone.utc)
    )


def checkpoint(
    run_id: str,
    name: str,
    *,
    completed_units: Optional[Sequence[Any]] = None,
    remaining_units: Optional[Sequence[Any]] = None,
    wip_unit: Optional[str] = None,
    budget_delta: int = 0,
    committed_paths: Optional[Sequence[str]] = None,
    issues: Optional[Sequence[str]] = None,
    message: Optional[str] = None,  # reserved for the US4 commit boundary
    env: Optional[MarathonEnv] = None,
) -> CheckpointRow:
    """Append a checkpoint; empty ``remaining_units`` completes the stage."""
    env, repo = _repo(run_id, env)
    run = repo.get_run(run_id)
    if run is None:
        raise ValueError(f"unknown run '{run_id}'; register it first")
    stage = _stage_or_raise(repo, run_id, name)

    cp = repo.insert_checkpoint(
        CheckpointRow(
            run_id=run_id,
            stage_id=stage.id,
            sequence_no=0,  # allocated by the repository (T010 monotonic)
            wip_unit=wip_unit,
            completed_units=list(completed_units or []),
            remaining_units=list(remaining_units or []),
            budget_spent=int(budget_delta),
            committed_paths=list(committed_paths or []),
        )
    )

    stage_fields: dict[str, Any] = {"last_sequence_no": cp.sequence_no}
    block_complete = not cp.remaining_units
    if block_complete:
        stage_fields["status"] = "complete"
        stage_fields["completed_at"] = datetime.now(timezone.utc)
    repo.update_stage(stage.id, **stage_fields)

    # D4/FR-007 (T024): an item is done when its mini_analyze completes; its
    # artifacts in <store_root>/items/<id>/ feed the marathon's implement.
    if block_complete and stage.mini_kind == "mini_analyze" and stage.item_id:
        repo.update_item(stage.item_id, status="done")

    if budget_delta:
        repo.update_run(
            run_id, budget_spent=run.budget_spent + int(budget_delta)
        )

    for summary in issues or []:
        repo.insert_issue(Issue(run_id=run_id, summary=summary, stage_id=stage.id))

    # T039/D9: the scoped commit boundary — checkpoint durably written FIRST,
    # then commit + push (crash in between ⇒ rule-2a re-drive). Runs only under
    # the standing grant; without it the named paths are informational and the
    # driving agent commits itself.
    from codeconv.marathon.gitblock import commit_block, commit_push_granted, push_block

    if cp.committed_paths and commit_push_granted(run):
        default_msg = (
            f"marathon {run_id}: {name} checkpoint {cp.sequence_no}"
        )
        cp = commit_block(
            repo, run, cp, message=message or default_msg, repo_dir=env.repo_dir
        )
        cp = push_block(repo, run_id, cp, repo_dir=env.repo_dir)

    # T038/FR-019: a status report at every stage boundary.
    if block_complete:
        from codeconv.marathon.status import emit_status

        emit_status(run_id, env=env)

    return cp


def _unit_key(unit: Any) -> str:
    """Stable identity for a unit (skip-completed comparison), tolerant of
    scalars and structured (dict/list) units."""
    try:
        return json.dumps(unit, sort_keys=True, default=str)
    except TypeError:
        return str(unit)


def units_to_execute(
    all_units: list[Any], completed_units: list[Any]
) -> list[Any]:
    """The units still to run, in order, with completed ones removed exactly
    once — a resumed run re-executes none of them, and a boundary unit (no
    checkpoint yet) runs exactly once (preserved 024 semantics; US5 rerun)."""
    done = {_unit_key(u) for u in completed_units}
    return [u for u in all_units if _unit_key(u) not in done]
