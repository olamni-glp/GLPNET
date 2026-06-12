"""Bridge-free unit tests for the pure resume-position derivation (T013).

``derive_position(run, stages, checkpoints, open_issues)`` per
``contracts/resume-position.md`` rules 1, 2, 5, 6 — computed solely from
durable row content (SC-008 determinism; no I/O, no bridge).
"""

from __future__ import annotations

from codeconv.marathon.models import MarathonRun, StageRow
from codeconv.marathon.position import derive_position


def _run(**kw) -> MarathonRun:
    return MarathonRun(id="r", **kw)


def _stage(index: int, name: str, status: str = "pending", **kw) -> StageRow:
    return StageRow(
        run_id="r",
        stage_index=index,
        order_key=float(index),
        name=name,
        origin="registered",
        status=status,
        **kw,
    )


def test_started_not_complete_is_not_done():
    """FR-004: a started-but-not-complete stage counts toward next, not done."""
    stages = [_stage(1, "a", status="running"), _stage(2, "b")]
    pos = derive_position(_run(), stages, [], [])
    assert pos.done == 0
    assert pos.total == 2
    assert pos.next_action == "run a"


def test_empty_stage_list_says_register():
    """Rule 5: empty list => next action 'register stages', not 'finalise'."""
    pos = derive_position(_run(), [], [], [])
    assert pos.total == 0
    assert pos.done == 0
    assert pos.next_action == "register stages"


def test_all_complete_says_finalise():
    """Rule 6: all stages complete => next action 'finalise run'."""
    stages = [_stage(1, "a", status="complete"), _stage(2, "b", status="complete")]
    pos = derive_position(_run(), stages, [], [])
    assert pos.done == 2
    assert pos.next_action == "finalise run"


def test_order_key_governs_order_not_index():
    """Rule 1: stages considered in order_key ascending (index breaks ties)."""
    late_index_early_key = StageRow(
        run_id="r",
        stage_index=9,
        order_key=0.5,
        name="hotfix",
        origin="dynamic",
    )
    stages = [_stage(1, "a"), late_index_early_key]
    pos = derive_position(_run(), stages, [], [])
    assert pos.next_action == "run hotfix"


def test_mini_stage_names_items_next_mini():
    """Rule 2: a mini-stage next action names the item's mini stage."""
    mini = StageRow(
        run_id="r",
        stage_index=4,
        order_key=2.5,
        name="item-7:mini_plan",
        origin="mini",
        item_id=7,
        mini_kind="mini_plan",
    )
    stages = [_stage(1, "a", status="complete"), mini, _stage(3, "c")]
    pos = derive_position(_run(), stages, [], [])
    assert pos.next_action == "run mini-plan for item-7"


def test_determinism_identical_rows_identical_position():
    """SC-008: the position is a pure function of the durable rows."""
    stages = [_stage(1, "a", status="complete"), _stage(2, "b", status="running")]
    run = _run(budget_spent=1234, budget_unit="tokens")
    p1 = derive_position(run, stages, [], [])
    p2 = derive_position(run, list(reversed(stages)), [], [])
    assert p1 == p2
    assert p1.budget_spent == 1234
    assert p1.outstanding_issues == 0
