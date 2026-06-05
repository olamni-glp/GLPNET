"""US1 — dual-store reconciliation, fallback mode, and boundary safety
(SC-007, I5/I6/I7/I9).

The pure-Python parts (fallback mode I5, boundary I9) run with no bridge. The
reconcile fast-forward (I6/SC-007) and fork (I7) genuinely need BOTH physical
stores, so they are ``@needs_bridge``: a primary-backed store + a fallback-only
store over the same repo simulate an outage and a divergence.
"""

from __future__ import annotations

from .conftest import make_marathon, needs_bridge


def test_active_store_reports_fallback_and_preserves_resume(
    marathon_fallback_store,
) -> None:
    """I5: with the primary unreachable, ``active_store`` reports ``fallback``
    and resume capability is preserved (no loss)."""
    store = marathon_fallback_store
    m, b = make_marathon(store, slug="fbfeat")
    store.write_checkpoint(
        b.id,
        stage="plan",
        wip_unit="u1",
        completed_units=[],
        remaining_units=["u1"],
        workflow_run_id=None,
        budget_spent=1,
    )
    assert store.active_store() == "fallback"

    from codeconv.marathon.checkpoint import resume

    assert resume(store, m.id).found is True


def test_boundary_unit_runs_exactly_once() -> None:
    """I9: a boundary unit (not yet checkpointed complete) is neither skipped
    nor double-executed."""
    from codeconv.marathon.checkpoint import units_to_execute

    all_units = ["b1", "b2", "b3"]
    # b3 is the boundary unit — not yet recorded complete → runs once.
    assert units_to_execute(all_units, ["b1", "b2"]) == ["b3"]
    # once b3 is recorded complete it is skipped (not double-run).
    assert units_to_execute(all_units, ["b1", "b2", "b3"]) == []


@needs_bridge
def test_reconcile_fast_forwards_stale_primary(marathon_bridge_repo) -> None:
    """SC-007 / I6: a fallback episode advances the JSON store beyond the
    primary; on reconcile the strictly-higher sequence wins and fast-forwards
    the stale primary."""
    from codeconv.marathon.store import MarathonStore

    repo = marathon_bridge_repo
    primary_store = MarathonStore(repo)  # bridge reachable
    m, b = make_marathon(primary_store, slug="fffeat")
    assert primary_store.active_store() == "primary"

    for i in (1, 2):  # mirrored to both stores
        primary_store.write_checkpoint(
            b.id,
            stage="plan",
            wip_unit=f"u{i}",
            completed_units=list(range(i)),
            remaining_units=[i],
            workflow_run_id=None,
            budget_spent=i,
        )

    # Outage: a fallback-only store advances JSON to seq 3, 4.
    fb = MarathonStore(repo, fallback_only=True)
    for i in (3, 4):
        fb.write_checkpoint(
            b.id,
            stage="plan",
            wip_unit=f"u{i}",
            completed_units=list(range(i)),
            remaining_units=[i],
            workflow_run_id=None,
            budget_spent=i,
        )

    rec = primary_store.reconcile(m.id)
    assert rec.status == "fast_forward"
    assert rec.winner == "fallback"
    # The primary now carries the fallback's newer checkpoints.
    assert primary_store._max_seq(primary_store._primary_checkpoints(m.id)) == 4


@needs_bridge
def test_reconcile_fork_escalates_no_silent_pick(marathon_bridge_repo) -> None:
    """I7 / D5: both stores advance to the same seq past a common checkpoint
    with different content → reconcile returns FORK + writes a
    ``store_divergence`` escalation; it never silently picks one."""
    from codeconv.marathon.escalation import open_escalations
    from codeconv.marathon.models import Checkpoint
    from codeconv.marathon.store import MarathonStore

    repo = marathon_bridge_repo
    ps = MarathonStore(repo)
    m, b = make_marathon(ps, slug="forkfeat")

    # seq 1 — common, mirrored to both stores.
    ps.write_checkpoint(
        b.id,
        stage="plan",
        wip_unit="common",
        completed_units=[],
        remaining_units=["x"],
        workflow_run_id=None,
        budget_spent=1,
    )

    # Fallback independently advances seq 2 with content X (JSON only).
    fb = MarathonStore(repo, fallback_only=True)
    fb.write_checkpoint(
        b.id,
        stage="plan",
        wip_unit="fork-fallback",
        completed_units=[],
        remaining_units=["x"],
        workflow_run_id=None,
        budget_spent=2,
    )

    # Primary independently gets seq 2 with DIFFERENT content Y.
    ps._append_primary(
        Checkpoint(
            block_id=b.id,
            marathon_id=m.id,
            sequence_no=2,
            stage="plan",
            wip_unit="fork-primary",
            completed_units=[],
            remaining_units=["x"],
            budget_spent=2,
            store_origin="primary",
        )
    )

    rec = ps.reconcile(m.id)
    assert rec.status == "fork"
    assert rec.escalation_id is not None
    escs = open_escalations(ps, m.id)
    assert any(e["kind"] == "store_divergence" for e in escs)
