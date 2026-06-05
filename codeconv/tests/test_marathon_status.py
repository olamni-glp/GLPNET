"""US5 — periodic standardized status report (SC-005).

A status report must carry all four fields (done / issues / tokens spent +
remaining / to-do) and be emittable repeatedly (the ~5-min cadence). Fallback
store — no bridge needed.
"""

from __future__ import annotations

from .conftest import make_marathon


def test_status_has_all_four_fields(marathon_fallback_store) -> None:
    store = marathon_fallback_store
    m, b = make_marathon(store, slug="statusfeat", budget=10_000)
    store.write_checkpoint(
        b.id,
        stage="plan",
        wip_unit="t3",
        completed_units=["t1", "t2"],
        remaining_units=["t3", "t4"],
        workflow_run_id=None,
        budget_spent=4_000,
    )
    store.update_budget_spent(m.id, 4_000)

    from codeconv.marathon.status import build_status

    report = build_status(store, m.id)
    # All four fields present (SC-005).
    assert report.done == ["t1", "t2"]
    assert report.todo == ["t3", "t4"]
    assert report.issues == []
    assert report.tokens_spent == 4_000
    assert report.tokens_remaining == 6_000  # ceiling 10000 - spent 4000


def test_status_emits_a_row_per_cadence_tick(marathon_fallback_store) -> None:
    """Each cadence tick (--emit) persists a report; SC-005's "at least once
    per interval" is satisfied by repeated emission during active work."""
    store = marathon_fallback_store
    m, _b = make_marathon(store, slug="statuscad", budget=10_000)

    from codeconv.marathon.status import emit_status, list_status

    emit_status(store, m.id)
    emit_status(store, m.id)

    rows = list_status(store, m.id)
    assert len(rows) == 2
    for r in rows:  # every persisted report carries all four fields
        assert set(r) >= {"done", "issues", "tokens_spent", "tokens_remaining", "todo"}
