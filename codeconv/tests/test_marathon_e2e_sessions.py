"""US1 / SC-009 — restart-safe resume across all four cadence block kinds over
multiple deliberate session boundaries.

This is the scripted, deterministic core of the quickstart's keystone
(SC-001/SC-009): each block kind in the cadence (specify, clarify,
plan_task_analyze, implement_session) is interrupted and resumed via a FRESH
store object (== a new session / post-compaction / post-crash), and the
resume position is recovered from durable state every time — zero
re-instruction. Fallback store, so it needs no bridge; the full live
quickstart run against ``multi-protocol-link-layer`` is the harness's first
real execution.
"""

from __future__ import annotations

from .conftest import make_marathon


def _drop_and_resume(repo_root, marathon_id):
    """Simulate a session boundary: a brand-new store object over the same
    durable dir, then objective resume."""
    from codeconv.marathon.checkpoint import resume
    from codeconv.marathon.store import MarathonStore

    fresh = MarathonStore(repo_root, fallback_only=True)
    return fresh, resume(fresh, marathon_id)


def test_resume_across_all_four_cadence_block_kinds(marathon_fallback_store) -> None:
    from codeconv.marathon.cadence import (
        block_id,
        block_kind_for_stage,
        canonical_stage,
    )
    from codeconv.marathon.models import StageBlock

    store = marathon_fallback_store
    m, _b = make_marathon(store, slug="mlink", stage="specify", ordinal=1)
    repo = store.repo_root

    # The four cadence block kinds, in pipeline order (D9). plan/task/analyze
    # collapse to one canonical "plan" block; implement is one session here.
    plan = [
        ("specify", 1),
        ("clarify", 1),
        ("plan", 1),  # the plan_task_analyze block
        ("implement", 1),  # one implement_session
    ]

    last_seq = 0
    for boundary, (stage, ordinal) in enumerate(plan, start=1):
        bid = block_id(m.id, stage, ordinal)
        store.upsert_block(
            StageBlock(
                id=bid,
                marathon_id=m.id,
                stage=canonical_stage(stage),
                block_kind=block_kind_for_stage(stage),
                ordinal=ordinal,
                status="running",
            )
        )
        # Do partial work in this block: two units done, one in flight.
        store.write_checkpoint(
            bid,
            stage=canonical_stage(stage),
            wip_unit=f"{stage}-u3",
            completed_units=[f"{stage}-u1", f"{stage}-u2"],
            remaining_units=[f"{stage}-u3"],
            workflow_run_id=f"wf_{stage}",
            budget_spent=boundary * 100,
        )

        # Session boundary: drop the store + resume from durable state.
        _fresh, rep = _drop_and_resume(repo, m.id)
        assert rep.found is True, f"resume lost position at the {stage} boundary"
        assert rep.block_id == bid
        assert rep.block_kind == block_kind_for_stage(stage)
        assert rep.completed_units == [f"{stage}-u1", f"{stage}-u2"]
        assert rep.wip_unit == f"{stage}-u3"
        assert rep.sequence_no > last_seq  # strictly monotonic across the marathon
        last_seq = rep.sequence_no

    # Four block kinds, four session boundaries, monotonic to seq 4.
    assert last_seq == 4
