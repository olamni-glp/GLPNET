"""US2 — per-stage plan → approval → durable gate (SC-004, US2-AS3).

Fallback store: the gate's durability + no-reask + supersede-retain semantics
need no bridge.
"""

from __future__ import annotations

from .conftest import make_marathon


def test_gate_blocks_until_approved_then_no_reask(marathon_fallback_store) -> None:
    """SC-004: the gate is awaiting until approved; once approved it is
    re-requested 0 times across resumes."""
    store = marathon_fallback_store
    _m, b = make_marathon(store, slug="gatefeat")

    from codeconv.marathon.gate import (
        approval_state,
        present_gate,
        record_decision,
        should_present_gate,
    )

    r = present_gate(store, b.id, plan_ref="plan-v1")
    assert r["state"] == "awaiting"
    assert approval_state(store, b.id) == "awaiting"
    assert should_present_gate(store, b.id) is True

    record_decision(store, b.id, outcome="approve", decided_by="gabi")
    assert approval_state(store, b.id) == "approved"
    assert should_present_gate(store, b.id) is False

    # Across 3 "restarts" (fresh store objects) the approval is honored, not
    # re-asked (SC-004: re-requested 0 times).
    from codeconv.marathon.store import MarathonStore

    for _ in range(3):
        restarted = MarathonStore(store.repo_root, fallback_only=True)
        assert approval_state(restarted, b.id) == "approved"
        assert should_present_gate(restarted, b.id) is False


def test_change_supersedes_but_retains_prior(marathon_fallback_store) -> None:
    """US2-AS3: a change supersedes the prior decision but retains it (append-
    only); a new plan must then be approved."""
    store = marathon_fallback_store
    _m, b = make_marathon(store, slug="gatechg")

    from codeconv.marathon.gate import (
        approval_state,
        approvals_for,
        present_gate,
        record_decision,
    )

    present_gate(store, b.id, plan_ref="plan-v1")
    record_decision(store, b.id, outcome="change", decided_by="gabi", plan_ref="plan-v2")
    present_gate(store, b.id, plan_ref="plan-v2")  # re-present the changed plan

    rows = approvals_for(store, b.id)
    outcomes = [r["outcome"] for r in rows]
    assert "change" in outcomes  # the prior decision is retained
    assert outcomes[-1] is None  # a fresh pending row awaits the new plan
    assert approval_state(store, b.id) == "awaiting"

    record_decision(store, b.id, outcome="approve", decided_by="gabi")
    assert approval_state(store, b.id) == "approved"
    # the superseded change row is still present (append-only history)
    assert "change" in [r["outcome"] for r in approvals_for(store, b.id)]
