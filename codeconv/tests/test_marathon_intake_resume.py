"""US2 durability parity — resume mid-mini-pipeline (T020a, FR-011).

Interrupt between two mini-stages of an item, then assert resume names the
exact next incomplete mini-stage and re-runs no completed mini-stage; items +
mini-stages enjoy the same checkpoint / resume guarantees as ordinary stages
(edge case "Resume mid-mini-pipeline"). The "interruption" is a fresh env +
a fresh CLI subprocess — total context loss; only durable rows remain.
"""

from __future__ import annotations

import json

from .conftest import marathon_run, needs_bridge

RUN_ID = "us2-resume"


@needs_bridge
def test_resume_mid_mini_pipeline_names_exact_next(marathon_store):
    from codeconv.marathon.checkpoint import checkpoint, start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.intake import capture_item
    from codeconv.marathon.models import MINI_KINDS
    from codeconv.marathon.position import resume_position
    from codeconv.marathon.stages import register_run
    from codeconv.marathon.store import Repository

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    register_run(RUN_ID, stages=["a", "b"], env=env)
    start_stage(RUN_ID, "a", env=env)
    checkpoint(RUN_ID, "a", env=env)

    item = capture_item(
        RUN_ID, kind="missing-prerequisite", title="needed before b",
        blocks_stage="b", env=env,
    )
    first_mini = f"item-{item.id}:mini_specify"

    assert (
        resume_position(RUN_ID, env=env).next_action
        == f"run mini-specify for item-{item.id}"
    )

    # Work the first mini-stage to completion, then "crash".
    start_stage(RUN_ID, first_mini, env=env)
    checkpoint(RUN_ID, first_mini, env=env)

    # Fresh env (new engine/repo: the in-process restart)…
    fresh_env = resolve_env(RUN_ID, data_dir=marathon_store)
    pos = resume_position(RUN_ID, env=fresh_env)
    assert pos.next_action == f"run mini-clarify for item-{item.id}"

    # …and a fresh subprocess (total context loss): identical position (FR-011).
    proc = marathon_run(marathon_store, "resume", "--run", RUN_ID, "--json")
    assert proc.returncode == 0, proc.stderr
    assert json.loads(proc.stdout)["next_action"] == pos.next_action

    # The completed mini-stage is never re-run: drive the remaining four; the
    # named next is always the exact next incomplete mini, in order.
    repo = Repository(fresh_env)
    for mini_kind in MINI_KINDS[1:]:
        stage_name = f"item-{item.id}:{mini_kind}"
        assert resume_position(RUN_ID, env=fresh_env).next_action == (
            f"run {mini_kind.replace('_', '-')} for item-{item.id}"
        )
        start_stage(RUN_ID, stage_name, env=fresh_env)
        checkpoint(RUN_ID, stage_name, env=fresh_env)

    # mini_analyze completed ⇒ item done (D4/FR-007); next is the blocked stage.
    assert repo.get_item(item.id).status == "done"
    assert resume_position(RUN_ID, env=fresh_env).next_action == "run b"
