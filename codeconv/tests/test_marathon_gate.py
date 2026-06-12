"""US5 gate — append-only supersedes chain + resume short-circuit (T040).

FR-020/AS1: the decision is durably recorded (append-only; a superseded
decision is retained); resume short-circuits an already-``approved`` gate.
"""

from __future__ import annotations

from .conftest import needs_bridge

RUN_ID = "us5-gate"


@needs_bridge
def test_gate_chain_and_resume_short_circuit(marathon_store):
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.gate import (
        approval_state,
        approvals_for,
        present_gate,
        record_decision,
        should_present_gate,
    )
    from codeconv.marathon.position import resume_position
    from codeconv.marathon.stages import register_run
    from codeconv.marathon.store import Repository

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    repo = Repository(env)
    register_run(RUN_ID, stages=["a", "b"], env=env)
    stage_a = repo.get_stage(RUN_ID, "a")

    # Present: stage flips awaiting_approval; resume names the gate.
    out = present_gate(repo, stage_a.id, plan_ref="plan-v1")
    assert out["state"] == "awaiting"
    assert approval_state(repo, stage_a.id) == "awaiting"
    assert resume_position(RUN_ID, env=env).next_action == "approve gate for a"
    # Idempotent: re-present keeps the single pending row.
    present_gate(repo, stage_a.id, plan_ref="plan-v1")
    assert len(approvals_for(repo, stage_a.id)) == 1

    # change → retained; re-present supersedes the change row.
    record_decision(repo, stage_a.id, outcome="change", decided_by="gabi")
    assert approval_state(repo, stage_a.id) == "changed"
    present_gate(repo, stage_a.id, plan_ref="plan-v2")
    rows = approvals_for(repo, stage_a.id)
    assert len(rows) == 2  # append-only: the changed row is retained
    assert rows[0].outcome == "change"
    assert rows[1].supersedes_id == rows[0].id  # supersedes chain

    # approve → short-circuit: resume names the work, not the gate.
    record_decision(repo, stage_a.id, outcome="approve", decided_by="gabi")
    assert approval_state(repo, stage_a.id) == "approved"
    assert should_present_gate(repo, stage_a.id) is False
    assert resume_position(RUN_ID, env=env).next_action == "run a"
    # Re-present after approval = no new row, reports approved (no re-ask).
    again = present_gate(repo, stage_a.id, plan_ref="plan-v3")
    assert again["state"] == "approved"
    assert len(approvals_for(repo, stage_a.id)) == 2
