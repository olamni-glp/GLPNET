"""US3 — re-runnable per-stage and per-subagent on failure (SC-003,
FR-006/007/008 + changed-input edge case).

Fallback store: the re-run granularity + append-only failure-history
semantics need no bridge. Subagents are modelled as units; a succeeded
subagent is a completed unit, a failed one is not.
"""

from __future__ import annotations

from .conftest import make_marathon


def test_per_subagent_rerun_isolates_the_failure(marathon_fallback_store) -> None:
    """FR-007/SC-003: re-running a single failed subagent re-executes only it;
    succeeded siblings are untouched."""
    store = marathon_fallback_store
    _m, b = make_marathon(store, slug="rerunsub")
    # 3 subagents: a, b succeeded; c failed (not completed).
    store.write_checkpoint(
        b.id,
        stage="implement",
        wip_unit="c",
        completed_units=["a", "b"],
        remaining_units=["c"],
        workflow_run_id="wf_run_z",
        budget_spent=3,
    )

    from codeconv.marathon.orchestrate import rerun_subagent

    res = rerun_subagent(store, b.id, "c")
    assert res["to_run"] == ["c"]
    assert res["untouched"] == ["a", "b"]  # succeeded siblings untouched


def test_per_subagent_rerun_ignores_a_sibling_blocks_units(
    marathon_fallback_store,
) -> None:
    """Regression (FR-007/SC-003): rerun_subagent must mirror rerun_block's
    block_id guard. read_position returns the marathon-WIDE latest checkpoint,
    so when a sibling block checkpointed later, the failed subagent's block has
    no known completed siblings here — ``untouched`` must be empty, NEVER the
    sibling block's completed units."""
    store = marathon_fallback_store
    m, b1 = make_marathon(store, slug="rerunsubsib", stage="plan", ordinal=1)
    # b1: a, b succeeded (seq 1).
    store.write_checkpoint(
        b1.id,
        stage="plan",
        wip_unit="c",
        completed_units=["a", "b"],
        remaining_units=["c"],
        workflow_run_id=None,
        budget_spent=1,
    )
    # A sibling block in the SAME marathon checkpoints LATER (higher seq), so
    # read_position(marathon) now points at b2 — not b1.
    from codeconv.marathon.cadence import (
        block_id,
        block_kind_for_stage,
        canonical_stage,
    )
    from codeconv.marathon.models import StageBlock

    b2 = store.upsert_block(
        StageBlock(
            id=block_id(m.id, "implement", 2),
            marathon_id=m.id,
            stage=canonical_stage("implement"),
            block_kind=block_kind_for_stage("implement"),
            ordinal=2,
            status="running",
        )
    )
    store.write_checkpoint(
        b2.id,
        stage="implement",
        wip_unit=None,
        completed_units=["x", "y"],
        remaining_units=[],
        workflow_run_id=None,
        budget_spent=2,
    )

    from codeconv.marathon.orchestrate import rerun_subagent

    res = rerun_subagent(store, b1.id, "c")
    assert res["to_run"] == ["c"]
    assert res["untouched"] == []  # must NOT leak b2's ["x", "y"]


def test_per_stage_rerun_starts_from_last_checkpoint(marathon_fallback_store) -> None:
    """FR-006: a per-stage re-run restarts from the last checkpoint (not
    marathon start); completed units are skipped."""
    store = marathon_fallback_store
    _m, b = make_marathon(store, slug="rerunstage")
    store.write_checkpoint(
        b.id,
        stage="implement",
        wip_unit="u3",
        completed_units=["u1", "u2"],
        remaining_units=["u3", "u4"],
        workflow_run_id=None,
        budget_spent=2,
    )

    from codeconv.marathon.orchestrate import rerun_block

    res = rerun_block(store, b.id)
    assert res["from_seq"] == 1  # from the last checkpoint, not 0
    assert res["to_run"] == ["u3", "u4"]
    assert res["completed_units"] == ["u1", "u2"]  # skipped


def test_changed_input_unit_is_new_work(marathon_fallback_store) -> None:
    """Edge case / T032: a unit whose input changed is re-run as new work even
    though a stale completion of the old input exists."""
    store = marathon_fallback_store
    _m, b = make_marathon(store, slug="rerunchg")
    store.write_checkpoint(
        b.id,
        stage="implement",
        wip_unit="b_old",
        completed_units=["a", "b_old"],
        remaining_units=[],
        workflow_run_id=None,
        budget_spent=2,
    )

    from codeconv.marathon.orchestrate import rerun_block

    # The current units replace b_old with b_new (input changed).
    res = rerun_block(store, b.id, all_units=["a", "b_new", "c"])
    assert "a" not in res["to_run"]  # unchanged completion skipped
    assert "b_new" in res["to_run"]  # changed input → new work
    assert "c" in res["to_run"]  # never-run unit


def test_failure_history_is_append_only(marathon_fallback_store) -> None:
    """FR-008: a failed attempt is retained beside the eventual success
    (append-only checkpoint history)."""
    store = marathon_fallback_store
    m, b = make_marathon(store, slug="rerunhist")
    # Attempt 1: x failed (still in remaining, not completed).
    store.write_checkpoint(
        b.id,
        stage="implement",
        wip_unit="x",
        completed_units=[],
        remaining_units=["x"],
        workflow_run_id="wf_run_1",
        budget_spent=1,
    )
    # Attempt 2 (retry): x succeeded (now completed).
    store.write_checkpoint(
        b.id,
        stage="implement",
        wip_unit=None,
        completed_units=["x"],
        remaining_units=[],
        workflow_run_id="wf_run_2",
        budget_spent=2,
    )

    history = store.checkpoints(m.id)
    assert len(history) == 2  # both attempts retained
    assert history[0].completed_units == []  # the failed attempt
    assert history[1].completed_units == ["x"]  # the eventual success


def test_rerun_echoes_workflow_run_id_for_resume(marathon_fallback_store) -> None:
    """FR-009: rerun_subagent + rerun_block echo the block's Workflow runId so
    the skill can pass it as ``resumeFromRunId`` (siblings stay cached, the
    failed unit is the changed step). Without this the skill loses the cached
    prefix and re-burns budget."""
    store = marathon_fallback_store
    _m, b = make_marathon(store, slug="rerunrunid")
    store.write_checkpoint(
        b.id,
        stage="implement",
        wip_unit="c",
        completed_units=["a", "b"],
        remaining_units=["c"],
        workflow_run_id="wf_run_live",
        budget_spent=3,
    )

    from codeconv.marathon.orchestrate import rerun_block, rerun_subagent

    assert rerun_subagent(store, b.id, "c")["workflow_run_id"] == "wf_run_live"
    assert rerun_block(store, b.id)["workflow_run_id"] == "wf_run_live"


def test_rerun_subagent_run_id_not_leaked_from_sibling_block(
    marathon_fallback_store,
) -> None:
    """The echoed runId must be THIS block's — never a later sibling block's
    (mirrors the ``untouched`` guard): read_position returns the marathon-wide
    max-seq checkpoint, which may belong to a sibling."""
    store = marathon_fallback_store
    m, b1 = make_marathon(store, slug="rerunrunidsib", stage="plan", ordinal=1)
    store.write_checkpoint(
        b1.id,
        stage="plan",
        wip_unit="c",
        completed_units=["a"],
        remaining_units=["c"],
        workflow_run_id="wf_b1",
        budget_spent=1,
    )
    from codeconv.marathon.cadence import (
        block_id,
        block_kind_for_stage,
        canonical_stage,
    )
    from codeconv.marathon.models import StageBlock

    b2 = store.upsert_block(
        StageBlock(
            id=block_id(m.id, "implement", 2),
            marathon_id=m.id,
            stage=canonical_stage("implement"),
            block_kind=block_kind_for_stage("implement"),
            ordinal=2,
            status="running",
        )
    )
    store.write_checkpoint(
        b2.id,
        stage="implement",
        wip_unit=None,
        completed_units=["x"],
        remaining_units=[],
        workflow_run_id="wf_b2",
        budget_spent=2,
    )

    from codeconv.marathon.orchestrate import rerun_subagent

    res = rerun_subagent(store, b1.id, "c")
    assert res["workflow_run_id"] is None  # read_position points at b2, not b1
    assert res["untouched"] == []  # and never leaks b2's units
