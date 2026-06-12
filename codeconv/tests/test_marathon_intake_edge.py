"""US2 edge cases — stacked bands, non-blocking order, completed target (T020).

Per ``contracts/emergent-intake.md`` Routing: stacked blocking items get
distinct deterministic fractional bands (no collision); a non-blocking item
orders after the current stage; capturing against an already-complete stage
escalates ``prereq_against_completed_stage`` and creates no item.
"""

from __future__ import annotations

import pytest

from .conftest import needs_bridge

RUN_ID = "us2-edges"


@needs_bridge
def test_stacked_nonblocking_and_completed_target(marathon_store):
    from codeconv.marathon.checkpoint import checkpoint, start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.intake import PrereqAgainstCompletedStage, capture_item
    from codeconv.marathon.stages import register_run
    from codeconv.marathon.store import Repository

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    repo = Repository(env)
    register_run(RUN_ID, stages=["a", "b", "c"], env=env)

    # --- stacked blocking items on the same stage: distinct fractional bands.
    item1 = capture_item(
        RUN_ID, kind="missing-prerequisite", title="prereq one",
        blocks_stage="c", env=env,
    )
    item2 = capture_item(
        RUN_ID, kind="missing-prerequisite", title="prereq two",
        blocks_stage="c", env=env,
    )
    stages = repo.list_stages(RUN_ID)
    blocked = next(s for s in stages if s.name == "c")
    keys1 = sorted(s.order_key for s in stages if s.item_id == item1.id)
    keys2 = sorted(s.order_key for s in stages if s.item_id == item2.id)
    all_keys = keys1 + keys2
    assert len(set(all_keys)) == 10  # no collision
    assert all(k < blocked.order_key for k in all_keys)  # all ahead of c
    # Deterministic stacking: item2's band sits strictly after item1's.
    assert max(keys1) < min(keys2)

    # --- a non-blocking item orders after the current stage (max key + 1..5).
    item3 = capture_item(RUN_ID, kind="bug", title="flaky teardown", env=env)
    stages = repo.list_stages(RUN_ID)
    keys3 = sorted(s.order_key for s in stages if s.item_id == item3.id)
    others_max = max(
        s.order_key for s in stages if s.item_id != item3.id
    )
    assert all(k > others_max for k in keys3)
    assert item3.blocks_stage_id is None  # non-blocking: no blocks linkage

    # --- capture against an already-complete stage: escalate, never reorder.
    start_stage(RUN_ID, "a", env=env)
    checkpoint(RUN_ID, "a", env=env)
    items_before = len(repo.list_items(RUN_ID))
    with pytest.raises(PrereqAgainstCompletedStage) as excinfo:
        capture_item(
            RUN_ID, kind="missing-prerequisite", title="too late",
            blocks_stage="a", env=env,
        )
    assert excinfo.value.escalation_id is not None
    escalations = repo.list_escalations(RUN_ID, open_only=True)
    assert [e.kind for e in escalations] == ["prereq_against_completed_stage"]
    assert len(repo.list_items(RUN_ID)) == items_before  # no item row created
