"""US1 integration test — register / complete / append / grow total (T012).

Independent Test (spec US1): register ``[a,b,c]``; complete ``a``; append
``d``; resume reports ``done=1/4`` and names ``b`` next — no harness code
change to use a different stage list (FR-001/002/003, SC-002). Plus one CLI
round-trip (``codeconv marathon resume --json``) proving the subprocess /
context-loss path computes the identical position (SC-008 flavour).
"""

from __future__ import annotations

import json

from .conftest import marathon_run, needs_bridge

RUN_ID = "us1-grow"


@needs_bridge
def test_register_complete_append_grow(marathon_store):
    from codeconv.marathon.checkpoint import checkpoint, start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.position import resume_position
    from codeconv.marathon.stages import append_stage, register_run

    env = resolve_env(RUN_ID, data_dir=marathon_store)

    run = register_run(
        RUN_ID, stages=["a", "b", "c"], title="US1 grow", env=env
    )
    assert run.status == "in_progress"

    pos = resume_position(RUN_ID, env=env)
    assert (pos.done, pos.total) == (0, 3)
    assert pos.next_action == "run a"

    # FR-004: started-not-complete is NOT done.
    started = start_stage(RUN_ID, "a", env=env)
    assert started.status == "running"
    assert started.started_at is not None
    pos = resume_position(RUN_ID, env=env)
    assert pos.done == 0
    assert pos.next_action == "run a"

    # Complete `a` (remaining_units == [] flips the stage complete).
    cp = checkpoint(RUN_ID, "a", budget_delta=1000, env=env)
    assert cp.sequence_no == 1

    # Grow the run mid-flight (FR-002): total becomes 4.
    appended = append_stage(RUN_ID, "d", env=env)
    assert appended.origin == "dynamic"
    assert appended.stage_index == 4

    pos = resume_position(RUN_ID, env=env)
    assert (pos.done, pos.total) == (1, 4)
    assert pos.next_action == "run b"
    assert pos.budget_spent == 1000

    # CLI round-trip in a fresh subprocess (total context loss) — identical
    # position from durable rows alone (FR-025 wiring + SC-008 flavour).
    proc = marathon_run(marathon_store, "resume", "--run", RUN_ID, "--json")
    assert proc.returncode == 0, proc.stderr
    payload = json.loads(proc.stdout)
    assert payload["done"] == 1
    assert payload["total"] == 4
    assert payload["next_action"] == "run b"


@needs_bridge
def test_finalize_only_when_all_complete_and_reopen_on_append(marathon_store):
    """T017: finalise requires every *current* stage complete; a later append
    re-opens the run cleanly (edge case 'append during finalisation')."""
    import pytest

    from codeconv.marathon.checkpoint import checkpoint, start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.position import resume_position
    from codeconv.marathon.stages import append_stage, finalize, register_run

    run_id = "us1-finalize"
    env = resolve_env(run_id, data_dir=marathon_store)
    register_run(run_id, stages=["a", "b"], env=env)

    with pytest.raises(ValueError):
        finalize(run_id, env=env)  # b (and a) not complete

    for name in ("a", "b"):
        start_stage(run_id, name, env=env)
        checkpoint(run_id, name, env=env)

    assert resume_position(run_id, env=env).next_action == "finalise run"
    finalized = finalize(run_id, env=env)
    assert finalized.status == "finalized"

    # Append after finalisation re-opens the run.
    append_stage(run_id, "c", env=env)
    pos = resume_position(run_id, env=env)
    assert pos.status == "in_progress"
    assert (pos.done, pos.total) == (2, 3)
    assert pos.next_action == "run c"
