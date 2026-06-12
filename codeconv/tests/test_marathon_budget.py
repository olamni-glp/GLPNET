"""US5 budget ceiling — bridge-free Budget + the halt path (T042).

FR-022/SC-006/AS3: advancing past the ceiling halts (safe checkpoint +
``budget_exceeded`` escalation), never overruns; the substrate CHECK
(``budget_spent <= budget_ceiling``) enforces it at the store level too.
"""

from __future__ import annotations

import pytest

from .conftest import needs_bridge

RUN_ID = "us5-budget"


def test_budget_is_a_hard_ceiling_bridge_free():
    from codeconv.marathon.orchestrate import Budget, BudgetCeilingReached

    budget = Budget(100)
    assert budget.add(60) == 60
    assert budget.remaining() == 40
    with pytest.raises(BudgetCeilingReached):
        budget.add(50)  # would overrun — refused
    assert budget.spent() == 60  # spend unchanged after refusal
    with pytest.raises(BudgetCeilingReached):
        budget.set_spent(101)
    assert budget.set_spent(100) == 100
    assert budget.remaining() == 0

    unbounded = Budget(None)
    assert unbounded.remaining() is None
    assert unbounded.add(10_000_000) == 10_000_000  # no ceiling, no refusal


@needs_bridge
def test_halt_path_writes_budget_exceeded_and_never_overruns(marathon_store):
    import sqlalchemy.exc

    from codeconv.marathon.checkpoint import start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.orchestrate import Budget, advance_budget_or_halt
    from codeconv.marathon.stages import register_run
    from codeconv.marathon.store import Repository

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    repo = Repository(env)
    register_run(RUN_ID, stages=["a"], budget_ceiling=1000,
                 budget_unit="tokens", env=env)
    start_stage(RUN_ID, "a", env=env)

    budget = Budget(1000, spent=900)

    # Within ceiling: advances and persists.
    ok = advance_budget_or_halt(
        repo, RUN_ID, budget, 50, stage_name="a",
        completed_units=["u1"], remaining_units=["u2"],
    )
    assert ok == {"halted": False, "spent": 950, "remaining": 50}
    assert repo.get_run(RUN_ID).budget_spent == 950

    # Would overrun: halt — safe checkpoint + budget_exceeded, spend untouched.
    halted = advance_budget_or_halt(
        repo, RUN_ID, budget, 200, stage_name="a",
        completed_units=["u1"], remaining_units=["u2"],
    )
    assert halted["halted"] is True
    assert halted["spent"] == 950  # zero overruns
    assert halted["escalation_id"] is not None
    kinds = [e.kind for e in repo.list_escalations(RUN_ID, open_only=True)]
    assert "budget_exceeded" in kinds
    cps = repo.list_checkpoints(RUN_ID)
    assert cps and cps[-1].remaining_units == ["u2"]  # safe checkpoint written
    assert repo.get_run(RUN_ID).budget_spent == 950

    # Substrate CHECK: the store itself refuses spent > ceiling (SC-006).
    with pytest.raises(sqlalchemy.exc.IntegrityError):
        repo.update_run(RUN_ID, budget_spent=2000)
