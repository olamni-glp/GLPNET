"""Data-driven stage vocabulary (US1, T014/T017).

Replaces 024's hard-coded ``STAGES`` tuple + canonical-collapse cadence (D3):

- :func:`register_run` — write the ``run`` row + its initial ordered ``stage``
  rows (``origin='registered'``; empty stage list is legal — resume then says
  "register stages"). Idempotent re-attach: an existing run is returned
  unchanged and already-present stage names are skipped. Declarative manifest
  registration is deferred (no FR in scope).
- :func:`append_stage` — append a dynamically-discovered stage
  (``stage_index = max+1``, ``order_key = max+1``, ``origin='dynamic'``); the
  total grows and progress reports against the *current* total
  (FR-001/002/003). Appending to a finalized run re-opens it (T017 edge).
- :func:`finalize` — status→finalized only when every *current* stage is
  complete; refuses otherwise (and refuses an empty run — rule 5 says its next
  action is "register stages", not finalise).
"""

from __future__ import annotations

import subprocess
from pathlib import Path
from typing import Optional, Sequence

from codeconv.marathon.env import resolve_env
from codeconv.marathon.models import MarathonEnv, MarathonRun, StageRow
from codeconv.marathon.store import Repository

__all__ = ["append_stage", "finalize", "register_run"]


def _repo(run_id: str, env: Optional[MarathonEnv]) -> tuple[MarathonEnv, Repository]:
    env = env if env is not None else resolve_env(run_id)
    return env, Repository(env)


def _head_commit(repo_dir: Path) -> Optional[str]:
    """The working repo's HEAD at registration (data-model ``run.head_commit``);
    best-effort — None when ``repo_dir`` is not a git tree."""
    try:
        proc = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=str(repo_dir),
            capture_output=True,
            text=True,
            timeout=10,
        )
    except Exception:
        return None
    if proc.returncode != 0:
        return None
    return proc.stdout.strip() or None


def register_run(
    run_id: str,
    *,
    stages: Optional[Sequence[str]] = None,
    title: Optional[str] = None,
    budget_ceiling: Optional[int] = None,
    budget_unit: Optional[str] = None,
    env: Optional[MarathonEnv] = None,
) -> MarathonRun:
    """Register (or re-attach) a run with its initial ordered stage list (FR-001)."""
    env, repo = _repo(run_id, env)

    requested = [name for name in (stages or []) if name]
    duplicates = {n for n in requested if requested.count(n) > 1}
    if duplicates:
        raise ValueError(
            f"duplicate stage name(s) in registration: {sorted(duplicates)}"
        )

    run = repo.upsert_run(
        MarathonRun(
            id=run_id,
            title=title or "",
            budget_ceiling=budget_ceiling,
            budget_unit=budget_unit,
            head_commit=_head_commit(env.repo_dir),
        )
    )

    current = repo.list_stages(run_id)
    existing_names = {s.name for s in current}
    next_index = max((s.stage_index for s in current), default=0)
    next_key = max((s.order_key for s in current), default=0.0)
    for name in requested:
        if name in existing_names:
            continue  # idempotent re-attach
        next_index += 1
        next_key += 1.0
        repo.insert_stage(
            StageRow(
                run_id=run_id,
                stage_index=next_index,
                order_key=next_key,
                name=name,
                origin="registered",
            )
        )
    return run


def append_stage(
    run_id: str, name: str, *, env: Optional[MarathonEnv] = None
) -> StageRow:
    """Append a dynamically-discovered stage; the total grows (FR-002/003)."""
    env, repo = _repo(run_id, env)
    run = repo.get_run(run_id)
    if run is None:
        raise ValueError(f"unknown run '{run_id}'; register it first")
    if run.status == "finalized":
        # Edge case "append during finalisation": re-open cleanly (T017).
        repo.update_run(run_id, status="in_progress")
    current = repo.list_stages(run_id)
    if any(s.name == name for s in current):
        raise ValueError(f"stage '{name}' already exists in run '{run_id}'")
    return repo.insert_stage(
        StageRow(
            run_id=run_id,
            stage_index=max((s.stage_index for s in current), default=0) + 1,
            order_key=max((s.order_key for s in current), default=0.0) + 1.0,
            name=name,
            origin="dynamic",
        )
    )


def finalize(run_id: str, *, env: Optional[MarathonEnv] = None) -> MarathonRun:
    """status→finalized ONLY when every current stage is complete (T017)."""
    env, repo = _repo(run_id, env)
    run = repo.get_run(run_id)
    if run is None:
        raise ValueError(f"unknown run '{run_id}'")
    stages = repo.list_stages(run_id)
    if not stages:
        raise ValueError(
            f"run '{run_id}' has no stages; next action is 'register stages', "
            f"not finalise"
        )
    incomplete = [s.name for s in stages if s.status != "complete"]
    if incomplete:
        raise ValueError(
            f"cannot finalise run '{run_id}': incomplete stage(s) {incomplete}"
        )
    if run.status == "finalized":
        return run
    return repo.update_run(run_id, status="finalized")
