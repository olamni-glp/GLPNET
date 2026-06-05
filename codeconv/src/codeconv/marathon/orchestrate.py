"""Workflow-tool composition layer (FR-009/010/012).

One stage-block == one Workflow run. This module owns ONLY what the harness
adds around the Workflow tool — it does NOT re-implement fan-out, per-agent
journaling, or cached-prefix resume (those are the Workflow tool's, driven
by the model at the buildkit-stage skill layer; Python cannot invoke the
Workflow tool). Concretely the harness layer provides:

- the **Workflow opt-in** preauthorization check before a run launches
  (FR-023, the standing grant #2);
- **run-linkage** — recording the Workflow ``runId`` onto the stage-block so
  a same-session retry resumes the cached prefix (FR-009);
- a **Budget** mirror of the Workflow tool's ``budget.total /
  budget.spent() / budget.remaining()`` with ceiling enforcement, so the
  harness can persist/halt at the ceiling with **0 overruns** (FR-012/SC-006,
  reused by US5 T037).

The skill feeds the real ``budget.spent()`` from the live Workflow run into
:meth:`Budget.set_spent`; tests drive :meth:`Budget.add` directly.
"""

from __future__ import annotations

from typing import Any, Optional


class WorkflowOptinNotGranted(RuntimeError):
    """Raised when a stage-block would launch a Workflow run but the opt-in
    standing grant is absent or revoked (FR-023)."""


class BudgetCeilingReached(RuntimeError):
    """Raised when a spend would push past the marathon's token ceiling.

    The caller ends the in-flight unit at a safe checkpoint, then halts or
    escalates — never overruns (FR-012/SC-006)."""

    def __init__(self, *, spent: int, ceiling: int, attempted: int) -> None:
        self.spent = spent
        self.ceiling = ceiling
        self.attempted = attempted
        super().__init__(
            f"budget ceiling {ceiling} would be exceeded: spent {spent} + "
            f"{attempted} > {ceiling}"
        )


class Budget:
    """Mirror of the Workflow tool's budget object (FR-012).

    ``ceiling`` is the per-marathon token target (``None`` = unbounded).
    ``remaining()`` is ``None`` when unbounded. The ceiling is a HARD limit:
    :meth:`add` / :meth:`set_spent` refuse to cross it (SC-006: 0 overruns)."""

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
        refuse a value that already overruns the ceiling."""
        if spent < 0:
            raise ValueError("spent must be non-negative")
        if self.ceiling is not None and spent > self.ceiling:
            raise BudgetCeilingReached(
                spent=self._spent, ceiling=self.ceiling, attempted=spent - self._spent
            )
        self._spent = spent
        return self._spent


def workflow_optin_ok(marathon: Any) -> bool:
    """True iff the Workflow-tool opt-in standing grant is held and not revoked
    (FR-023/D10)."""
    return bool(getattr(marathon, "preauth_workflow_optin", False)) and (
        getattr(marathon, "preauth_revoked_at", None) is None
    )


def require_workflow_optin(marathon: Any) -> None:
    """Guard a Workflow-run launch (run lifecycle step 2,
    workflow-composition.md). Raises if the opt-in is absent/revoked."""
    if not workflow_optin_ok(marathon):
        raise WorkflowOptinNotGranted(
            f"Workflow-tool opt-in not granted for marathon "
            f"{getattr(marathon, 'id', '?')!r} (grant it at `marathon start "
            f"--preauth-workflow`)"
        )


def record_run_linkage(store: Any, block: Any, run_id: str) -> Any:
    """Record the Workflow ``runId`` as run-linkage on the stage-block
    (FR-009) so a same-session retry can resume its cached prefix."""
    block.workflow_run_id = run_id
    store.upsert_block(block)
    return block


# --- re-run (US3: T029/T030/T032) ------------------------------------------


def rerun_block(
    store: Any, block_id: str, *, all_units: Optional[list[Any]] = None
) -> dict[str, Any]:
    """Per-stage re-run from the block's LAST checkpoint — NOT marathon start
    (FR-006). Returns the units still to run (succeeded units skipped) and the
    sequence to resume from.

    When ``all_units`` is supplied, a unit whose *input changed* (different
    content → different key) is treated as **new work** and re-included, even
    if a stale completion of the old input exists (edge case / T032)."""
    from .checkpoint import units_to_execute
    from .store import marathon_id_of_block

    pos = store.read_position(marathon_id_of_block(block_id))
    if pos is None or pos.block_id != block_id:
        # No checkpoint for this block yet → a full run.
        return {"from_seq": 0, "to_run": all_units, "completed_units": []}
    completed = pos.completed_units
    to_run = (
        units_to_execute(all_units, completed)
        if all_units is not None
        else list(pos.remaining_units)
    )
    return {
        "from_seq": pos.sequence_no,
        "to_run": to_run,
        "completed_units": completed,
    }


def rerun_subagent(store: Any, block_id: str, subagent: Any) -> dict[str, Any]:
    """Isolated per-subagent re-run: ONLY ``subagent`` re-executes; succeeded
    siblings are untouched (FR-007/SC-003). The production re-run composes the
    Workflow tool's ``resumeFromRunId`` cached-prefix at the skill layer (the
    failed subagent is the changed step; siblings stay cached); here the
    harness reports the isolated unit + the siblings it must not disturb."""
    from .store import marathon_id_of_block

    pos = store.read_position(marathon_id_of_block(block_id))
    # read_position returns the marathon-WIDE max-sequence checkpoint, which may
    # belong to a sibling block. Mirror rerun_block's guard: only this block's
    # own completed units are its siblings — never a different block's (FR-007).
    siblings = (
        list(pos.completed_units)
        if pos is not None and pos.block_id == block_id
        else []
    )
    return {"to_run": [subagent], "untouched": siblings}


# --- budget ceiling enforcement (US5: T037) --------------------------------


def advance_budget_or_halt(
    store: Any,
    marathon_id: str,
    budget: "Budget",
    tokens: int,
    *,
    block_id: str,
    stage: str,
    completed_units: list[Any],
    remaining_units: list[Any],
    workflow_run_id: Optional[str] = None,
) -> dict[str, Any]:
    """Attempt to spend ``tokens`` against the ceiling (FR-012/D7/SC-006).

    If it would overrun, **do not overrun**: write a safe checkpoint (the
    in-flight unit ends cleanly at the last safe state) and signal ``halted``
    — never abandon a partial unit, never cross the ceiling (0 overruns). Else
    advance the spend and persist it on the marathon."""
    if budget.would_exceed(tokens):
        store.write_checkpoint(
            block_id,
            stage=stage,
            wip_unit=None,
            completed_units=completed_units,
            remaining_units=remaining_units,
            workflow_run_id=workflow_run_id,
            budget_spent=budget.spent(),
        )
        return {
            "halted": True,
            "spent": budget.spent(),
            "remaining": budget.remaining(),
        }
    budget.add(tokens)
    store.update_budget_spent(marathon_id, budget.spent())
    return {"halted": False, "spent": budget.spent(), "remaining": budget.remaining()}


# --- block completion → commit/push boundary (US6: T042) -------------------


def finalize_block(
    store: Any,
    block_id: str,
    *,
    files: list[str],
    message: str,
    repo_root: Any,
    completed_units: list[Any],
    stage: str,
    workflow_run_id: Optional[str] = None,
    budget_spent: int = 0,
) -> dict[str, Any]:
    """Block completion (FR-019): the checkpoint boundary == the commit/push
    boundary == this block. Write the FINAL checkpoint, then commit + push only
    the block's files under the standing grant (a blocked push escalates, never
    forces — gitblock.py)."""
    from .gitblock import commit_block, push_block

    cp = store.write_checkpoint(
        block_id,
        stage=stage,
        wip_unit=None,
        completed_units=completed_units,
        remaining_units=[],
        workflow_run_id=workflow_run_id,
        budget_spent=budget_spent,
    )
    commit = commit_block(
        store, block_id, files=files, message=message, repo_root=repo_root
    )
    push = push_block(store, block_id, repo_root=repo_root)
    return {"checkpoint_seq": cp.sequence_no, "commit": commit, "push": push}


__all__ = [
    "Budget",
    "BudgetCeilingReached",
    "WorkflowOptinNotGranted",
    "advance_budget_or_halt",
    "finalize_block",
    "record_run_linkage",
    "require_workflow_optin",
    "rerun_block",
    "rerun_subagent",
    "workflow_optin_ok",
]
