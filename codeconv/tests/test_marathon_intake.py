"""US2 integration test — capture routes a blocking prereq ahead (T019).

Independent Test (spec US2): capture a ``missing-prerequisite`` blocking stage
``c``; resume names ``mini-specify`` for that item next and orders its
mini-stages before ``c``; the total grew by 5; nothing auto-advances
(FR-005/006/009/010, SC-003/SC-004).
"""

from __future__ import annotations

from pathlib import Path

from .conftest import needs_bridge

RUN_ID = "us2-intake"


@needs_bridge
def test_blocking_prereq_routes_ahead_of_blocked_stage(marathon_store):
    from codeconv.marathon.checkpoint import checkpoint, start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.intake import capture_item
    from codeconv.marathon.models import MINI_KINDS
    from codeconv.marathon.position import resume_position
    from codeconv.marathon.stages import register_run
    from codeconv.marathon.store import Repository

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    repo = Repository(env)

    register_run(RUN_ID, stages=["a", "b", "c"], env=env)
    for name in ("a", "b"):
        start_stage(RUN_ID, name, env=env)
        checkpoint(RUN_ID, name, env=env)

    item = capture_item(
        RUN_ID,
        kind="missing-prerequisite",
        title="schema migration must land first",
        blocks_stage="c",
        env=env,
    )
    assert item.kind == "missing_prerequisite"
    assert item.status == "open"
    # FR-008: compact per-item artifact dir exists inside the store root.
    assert Path(item.artifacts_dir).is_dir()
    assert Path(item.artifacts_dir) == marathon_store / "items" / str(item.id)

    stages = repo.list_stages(RUN_ID)
    assert len(stages) == 8  # 3 registered + 5 mini — total grew by 5

    blocked = next(s for s in stages if s.name == "c")
    minis = [s for s in stages if s.item_id == item.id]
    assert [s.mini_kind for s in sorted(minis, key=lambda s: s.order_key)] == list(
        MINI_KINDS
    )
    # FR-010: every mini-stage sorts strictly BEFORE the blocked stage.
    assert all(s.order_key < blocked.order_key for s in minis)
    # FR-009 default-deny: nothing auto-advanced.
    assert all(s.status == "pending" for s in minis)

    pos = resume_position(RUN_ID, env=env)
    assert (pos.done, pos.total) == (2, 8)
    assert pos.next_action == f"run mini-specify for item-{item.id}"
