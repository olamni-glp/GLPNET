"""Budget ceiling, per-block/per-subagent re-run, Workflow opt-in (T046).

Ported 024 semantics keyed off the new ``stage``/``checkpoint`` rows
(FR-021/022; zero regressions, SC-009):

- :class:`Budget` — mirror of the Workflow tool's budget object; the ceiling
  is a HARD limit (:meth:`Budget.add` / :meth:`Budget.set_spent` refuse to
  cross it — SC-006, 0 overruns).
- :func:`advance_budget_or_halt` — would-exceed ⇒ write a safe checkpoint and
  a ``budget_exceeded`` escalation (the new dedicated kind; 024 had to borrow
  ``stage_flagged``), signal ``halted`` — never overrun, never silent.
- :func:`rerun_block` — resume from the STAGE's last checkpoint, not run
  start; completed units are skipped exactly once (``units_to_execute``).
- :func:`rerun_subagent` — only the named subagent re-runs; the stage's own
  completed siblings are reported untouched, and the stage's Workflow
  ``runId`` is echoed for the cached-prefix re-run cascade (FR-021).
- :func:`require_workflow_optin` — standing grant #2 guard (unchanged).

024's ``finalize_block`` is gone: D9 folded the commit boundary onto the
checkpoint itself (``checkpoint(committed_paths=…)`` in ``checkpoint.py``).
"""

from __future__ import annotations

from typing import Any, Optional

from codeconv.marathon.checkpoint import units_to_execute
from codeconv.marathon.models import CheckpointRow, Escalation, MarathonRun
from codeconv.marathon.store import Repository

__all__ = [
    "Budget",
    "BudgetCeilingReached",
    "WorkflowOptinNotGranted",
    "advance_budget_or_halt",
    "record_run_linkage",
    "require_workflow_optin",
    "rerun_block",
    "rerun_subagent",
    "workflow_optin_ok",
]


class WorkflowOptinNotGranted(RuntimeError):
    """A Workflow run would launch without the opt-in standing grant #2."""


class BudgetCeilingReached(RuntimeError):
    """A spend would push past the run's token ceiling (never overrun)."""

    def __init__(self, *, spent: int, ceiling: int, attempted: int) -> None:
        self.spent = spent
        self.ceiling = ceiling
        self.attempted = attempted
        super().__init__(
            f"budget ceiling {ceiling} would be exceeded: spent {spent} + "
            f"{attempted} > {ceiling}"
        )


class Budget:
    """Mirror of the Workflow tool's budget object (ceiling = hard limit)."""

    def __init__(self, ceiling: Optional[int], spent: int = 0) -> None:
        self.ceiling = ceiling
        self._spent = spent

    @property
    def total(self) -> Optional[int]:
        return self.ceiling

    def spent(self) -> int:
        return self._spent

    def remaining(self) -> Optional[int]:
        if self.ceiling is None:
            return None
        return max(0, self.ceiling - self._spent)

    def would_exceed(self, tokens: int) -> bool:
        return self.ceiling is not None and (self._spent + tokens) > self.ceiling

    def add(self, tokens: int) -> int:
        """Advance spend by ``tokens``; refuse to overrun the ceiling."""
        if tokens < 0:
            raise ValueError("tokens must be non-negative")
        if self.would_exceed(tokens):
            raise BudgetCeilingReached(
                spent=self._spent, ceiling=self.ceiling, attempted=tokens
            )
        self._spent += tokens
        return self._spent

    def set_spent(self, spent: int) -> int:
        """Sync the absolute spend from the live Workflow ``budget.spent()``;
        refuse a value already past the ceiling."""
        if spent < 0:
            raise ValueError("spent must be non-negative")
        if self.ceiling is not None and spent > self.ceiling:
            raise BudgetCeilingReached(
                spent=self._spent,
                ceiling=self.ceiling,
                attempted=spent - self._spent,
            )
        self._spent = spent
        return self._spent


def workflow_optin_ok(run: MarathonRun) -> bool:
    return bool(run.preauth_workflow_optin) and run.preauth_revoked_at is None


def require_workflow_optin(run: MarathonRun) -> None:
    if not workflow_optin_ok(run):
        raise WorkflowOptinNotGranted(
            f"Workflow-tool opt-in not granted for run {run.id!r} "
            f"(grant preauth_workflow_optin on the run)"
        )


def record_run_linkage(
    store: Repository, checkpoint_id: int, workflow_run_id: str
) -> CheckpointRow:
    """Record the Workflow ``runId`` on the checkpoint so a same-session retry
    resumes its cached prefix (FR-021)."""
    return store.update_checkpoint(checkpoint_id, workflow_run_id=workflow_run_id)


def _last_checkpoint_of_stage(
    store: Repository, run_id: str, stage_id: int
) -> Optional[CheckpointRow]:
    cps = [c for c in store.list_checkpoints(run_id) if c.stage_id == stage_id]
    return max(cps, key=lambda c: c.sequence_no) if cps else None


def rerun_block(
    store: Repository,
    run_id: str,
    stage_name: str,
    *,
    all_units: Optional[list[Any]] = None,
) -> dict[str, Any]:
    """Per-stage re-run from the stage's LAST checkpoint — not run start
    (FR-021). A unit whose input changed (different content → different key)
    is new work and re-included even if a stale completion exists."""
    stage = store.get_stage(run_id, stage_name)
    if stage is None:
        raise ValueError(f"unknown stage '{stage_name}' in run '{run_id}'")
    last = _last_checkpoint_of_stage(store, run_id, stage.id)
    if last is None:
        return {
            "from_seq": 0,
            "to_run": all_units,
            "completed_units": [],
            "workflow_run_id": None,
        }
    completed = list(last.completed_units)
    to_run = (
        units_to_execute(all_units, completed)
        if all_units is not None
        else list(last.remaining_units)
    )
    return {
        "from_seq": last.sequence_no,
        "to_run": to_run,
        "completed_units": completed,
        "workflow_run_id": last.workflow_run_id,
    }


def rerun_subagent(
    store: Repository, run_id: str, stage_name: str, subagent: Any
) -> dict[str, Any]:
    """Isolated per-subagent re-run: ONLY ``subagent`` re-executes; the
    stage's own completed siblings are untouched; the stage's Workflow runId
    is echoed for ``resumeFromRunId`` cached-prefix composition (FR-021)."""
    stage = store.get_stage(run_id, stage_name)
    if stage is None:
        raise ValueError(f"unknown stage '{stage_name}' in run '{run_id}'")
    last = _last_checkpoint_of_stage(store, run_id, stage.id)
    siblings = list(last.completed_units) if last is not None else []
    run_linkage = last.workflow_run_id if last is not None else None
    return {
        "to_run": [subagent],
        "untouched": siblings,
        "workflow_run_id": run_linkage,
    }


def advance_budget_or_halt(
    store: Repository,
    run_id: str,
    budget: Budget,
    tokens: int,
    *,
    stage_name: str,
    completed_units: Optional[list[Any]] = None,
    remaining_units: Optional[list[Any]] = None,
    workflow_run_id: Optional[str] = None,
) -> dict[str, Any]:
    """Spend ``tokens`` against the ceiling, or halt-with-escalation (FR-022).

    Would-exceed ⇒ do NOT overrun: write a safe checkpoint (the in-flight
    unit ends at the last safe state), record a ``budget_exceeded``
    escalation so the halt is visible to doctor/status (never a silent stop),
    and signal ``halted``. Else advance and persist the run's spend."""
    stage = store.get_stage(run_id, stage_name)
    if stage is None:
        raise ValueError(f"unknown stage '{stage_name}' in run '{run_id}'")
    if budget.would_exceed(tokens):
        store.insert_checkpoint(
            CheckpointRow(
                run_id=run_id,
                stage_id=stage.id,
                sequence_no=0,
                completed_units=list(completed_units or []),
                remaining_units=list(remaining_units or []),
                workflow_run_id=workflow_run_id,
                budget_spent=budget.spent(),
            )
        )
        esc = store.insert_escalation(
            Escalation(
                run_id=run_id,
                kind="budget_exceeded",
                stage_id=stage.id,
                detail={
                    "stage": stage_name,
                    "spent": budget.spent(),
                    "ceiling": budget.ceiling,
                    "attempted": tokens,
                },
            )
        )
        return {
            "halted": True,
            "spent": budget.spent(),
            "remaining": budget.remaining(),
            "escalation_id": esc.id,
        }
    budget.add(tokens)
    store.update_run(run_id, budget_spent=budget.spent())
    return {
        "halted": False,
        "spent": budget.spent(),
        "remaining": budget.remaining(),
    }
