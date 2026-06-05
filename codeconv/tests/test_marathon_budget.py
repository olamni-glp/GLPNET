"""US5 — token budget ceiling halt/escalate with 0 overruns (SC-006).

At the ceiling, work halts at a safe checkpoint and never overruns. Fallback
store — no bridge needed.
"""

from __future__ import annotations

import pytest

from .conftest import make_marathon


def test_budget_add_refuses_to_overrun_ceiling() -> None:
    """The Budget mirror is a HARD ceiling: a spend that would cross it raises,
    never silently overruns (SC-006)."""
    from codeconv.marathon.orchestrate import Budget, BudgetCeilingReached

    budget = Budget(ceiling=2_500)
    budget.add(1_000)
    budget.add(1_000)  # spent 2_000
    assert budget.spent() == 2_000
    assert budget.remaining() == 500
    with pytest.raises(BudgetCeilingReached):
        budget.add(1_000)  # would be 3_000 > 2_500
    assert budget.spent() == 2_000  # unchanged — 0 overrun


def test_unbounded_budget_has_none_remaining() -> None:
    from codeconv.marathon.orchestrate import Budget

    budget = Budget(ceiling=None)
    budget.add(5_000)
    assert budget.spent() == 5_000
    assert budget.remaining() is None  # unbounded


def test_advance_or_halt_ends_at_safe_checkpoint(marathon_fallback_store) -> None:
    """At the ceiling, ``advance_budget_or_halt`` ends the in-flight unit at a
    safe checkpoint and halts — 0 overrun, no abandoned partial unit."""
    store = marathon_fallback_store
    m, b = make_marathon(store, slug="budgetfeat", budget=2_500)

    from codeconv.marathon.orchestrate import Budget, advance_budget_or_halt

    budget = Budget(ceiling=2_500, spent=2_000)
    res = advance_budget_or_halt(
        store,
        m.id,
        budget,
        1_000,  # would push to 3_000 > 2_500
        block_id=b.id,
        stage="implement",
        completed_units=["u1", "u2"],
        remaining_units=["u3"],
    )
    assert res["halted"] is True
    assert res["spent"] == 2_000  # never crossed the ceiling
    assert res["remaining"] == 500

    # A safe checkpoint was written so the in-flight unit ends cleanly.
    history = store.checkpoints(m.id)
    assert len(history) == 1
    assert history[0].completed_units == ["u1", "u2"]
    assert history[0].budget_spent == 2_000


def test_advance_within_budget_persists_spend(marathon_fallback_store) -> None:
    store = marathon_fallback_store
    m, b = make_marathon(store, slug="budgetok", budget=10_000)

    from codeconv.marathon.orchestrate import Budget, advance_budget_or_halt

    budget = Budget(ceiling=10_000)
    res = advance_budget_or_halt(
        store,
        m.id,
        budget,
        3_000,
        block_id=b.id,
        stage="implement",
        completed_units=[],
        remaining_units=["u1"],
    )
    assert res["halted"] is False
    assert res["spent"] == 3_000
    # spend persisted on the marathon row
    assert store.read_marathon(m.id).budget_spent == 3_000
