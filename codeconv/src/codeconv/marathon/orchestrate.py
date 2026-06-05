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


__all__ = [
    "Budget",
    "BudgetCeilingReached",
    "WorkflowOptinNotGranted",
    "record_run_linkage",
    "require_workflow_optin",
    "workflow_optin_ok",
]
