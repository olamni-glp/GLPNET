"""Resume position — derived solely from durable rows (T016, T024, T037).

``derive_position(run, stages, checkpoints, open_issues)`` is the **pure** core
(unit-tested bridge-free): only durable row content + ``order_key``/``status``
enter the computation — no timestamps, no randomness, no conversation memory —
so the position is identical with full context or after total context loss
(SC-008). ``resume_position(run_id, *, env)`` is the I/O wrapper; ``resume`` is
its library-api alias.

Ordering rules per ``contracts/resume-position.md``:
1. stages in ``order_key`` ascending (ties by ``stage_index``);
2. next action = first non-complete stage — a mini-stage names the item's next
   incomplete mini-stage ("run mini-plan for item-7"); blocking mini-stages
   surface ahead of the blocked stage purely through their ``order_key``
   (rules 3/4 — placed at capture, FR-010);
5. empty stage list ⇒ "register stages";
6. all complete ⇒ "finalise run".
Rule-2 refinements still to land: re-drive-scoped-commit (US4 T037) and
approval-gate short-circuit (US5). ``checkpoints`` is accepted now (contract
signature) and consumed by those refinements.
"""

from __future__ import annotations

from typing import Optional, Sequence

from codeconv.marathon.env import resolve_env
from codeconv.marathon.models import (
    CheckpointRow,
    Issue,
    MarathonEnv,
    MarathonRun,
    ResumePosition,
    StageRow,
)
from codeconv.marathon.store import Repository

__all__ = ["derive_position", "resume", "resume_position"]


def derive_position(
    run: MarathonRun,
    stages: Sequence[StageRow],
    checkpoints: Sequence[CheckpointRow],
    open_issues: Sequence[Issue],
) -> ResumePosition:
    """The pure SC-008 derivation (rules 1, 2, 5, 6)."""
    ordered = sorted(stages, key=lambda s: (s.order_key, s.stage_index))
    total = len(ordered)
    done = sum(1 for s in ordered if s.status == "complete")

    redrive = _pending_commit_stage(run, ordered, checkpoints)
    if redrive is not None:
        # Rule 2a (T037, D9/FR-018 crash-window guard): a complete stage whose
        # last checkpoint intended a commit (named paths, preauth granted) but
        # carries no sha — re-drive that ONE scoped commit before any new work.
        next_action = f"re-drive scoped commit for {redrive.name}"
    elif total == 0:
        next_action = "register stages"  # rule 5
    else:
        first_open = next((s for s in ordered if s.status != "complete"), None)
        if first_open is None:
            next_action = "finalise run"  # rule 6
        elif first_open.item_id is not None and first_open.mini_kind:
            next_action = (
                f"run {first_open.mini_kind.replace('_', '-')} "
                f"for item-{first_open.item_id}"
            )
        else:
            next_action = f"run {first_open.name}"

    return ResumePosition(
        run_id=run.id,
        done=done,
        total=total,
        next_action=next_action,
        status=run.status,
        outstanding_issues=len(open_issues),
        budget_spent=run.budget_spent,
        budget_unit=run.budget_unit,
    )


def _pending_commit_stage(
    run: MarathonRun,
    ordered: Sequence[StageRow],
    checkpoints: Sequence[CheckpointRow],
):
    """The first complete stage (in order) whose LAST checkpoint completed the
    block (remaining==[]) with named ``committed_paths`` but ``commit_sha``
    NULL — the crash window between checkpoint-write and scoped commit.

    Gated on the run's standing grant: without ``preauth_commit_push`` the
    commit was never the harness's to drive, so paths-without-sha is merely
    informational (no false re-drive loop). Pure — no I/O."""
    if not run.preauth_commit_push or run.preauth_revoked_at is not None:
        return None
    last_by_stage: dict[int, CheckpointRow] = {}
    for cp in checkpoints:
        prev = last_by_stage.get(cp.stage_id)
        if prev is None or cp.sequence_no > prev.sequence_no:
            last_by_stage[cp.stage_id] = cp
    for stage in ordered:
        if stage.status != "complete" or stage.id is None:
            continue
        cp = last_by_stage.get(stage.id)
        if (
            cp is not None
            and not cp.remaining_units
            and cp.committed_paths
            and cp.commit_sha is None
        ):
            return stage
    return None


def resume_position(
    run_id: str, *, env: Optional[MarathonEnv] = None
) -> ResumePosition:
    """I/O wrapper: read the durable rows, derive the position."""
    env = env if env is not None else resolve_env(run_id)
    repo = Repository(env)
    run = repo.get_run(run_id)
    if run is None:
        raise ValueError(f"unknown run '{run_id}'; register it first")
    return derive_position(
        run,
        repo.list_stages(run_id),
        repo.list_checkpoints(run_id),
        repo.list_issues(run_id, status="open"),
    )


# library-api.md name: resume(run_id, *, env=None) -> ResumePosition
resume = resume_position
