"""Resume-position byte-identity from durable state alone (T052, SC-008).

The position must be **byte-identical** when computed twice from the same
durable rows — once with full context and once after total context loss.
Two layers:

* bridge-free: ``derive_position`` over a rich row set vs independently
  reconstructed (and reshuffled) row objects — canonical JSON bytes equal;
* ``@needs_bridge``: the same run read three ways — the in-session env, a
  freshly resolved env (in-process context loss), and a fresh CLI subprocess
  (total context loss) — all three positions serialize to the same bytes.
"""

from __future__ import annotations

import json
from dataclasses import asdict

from .conftest import marathon_run, needs_bridge

RUN_ID = "sc008-determinism"


def _canonical(position_dict: dict) -> bytes:
    return json.dumps(position_dict, sort_keys=True, default=str).encode()


def _rich_rows():
    """One of each interesting durable shape: registered + dynamic + mini
    stages, a complete stage whose last checkpoint re-drives a scoped commit
    (preauth granted, paths named, sha missing), and an open issue."""
    from codeconv.marathon.models import (
        CheckpointRow,
        Issue,
        MarathonRun,
        StageRow,
    )

    run = MarathonRun(
        id=RUN_ID,
        budget_spent=4321,
        budget_unit="tokens",
        preauth_commit_push=True,
    )
    stages = [
        StageRow(
            run_id=RUN_ID, stage_index=1, order_key=1.0, name="a",
            origin="registered", status="complete", id=11,
        ),
        StageRow(
            run_id=RUN_ID, stage_index=2, order_key=2.0, name="b",
            origin="registered", status="running", id=12,
        ),
        StageRow(
            run_id=RUN_ID, stage_index=4, order_key=2.5, name="item-7:mini_plan",
            origin="mini", item_id=7, mini_kind="mini_plan", id=14,
        ),
        StageRow(
            run_id=RUN_ID, stage_index=3, order_key=3.0, name="c",
            origin="dynamic", id=13,
        ),
    ]
    checkpoints = [
        CheckpointRow(
            run_id=RUN_ID, stage_id=11, sequence_no=1,
            committed_paths=["src/x.py"], commit_sha=None,
        ),
    ]
    issues = [Issue(run_id=RUN_ID, summary="open issue", id=21)]
    return run, stages, checkpoints, issues


def test_derive_position_bytes_identical_from_reconstructed_rows():
    """SC-008 (pure): rebuilt row objects in a different order produce the
    byte-identical position — nothing but durable content enters."""
    from codeconv.marathon.position import derive_position

    run1, stages1, cps1, issues1 = _rich_rows()
    pos1 = derive_position(run1, stages1, cps1, issues1)

    # Independent reconstruction (fresh objects), order shuffled.
    run2, stages2, cps2, issues2 = _rich_rows()
    pos2 = derive_position(
        run2, list(reversed(stages2)), list(reversed(cps2)), issues2
    )

    assert _canonical(asdict(pos1)) == _canonical(asdict(pos2))
    # The scenario exercises the rule-2a re-drive branch deterministically.
    assert pos1.next_action == "re-drive scoped commit for a"


@needs_bridge
def test_resume_bytes_identical_across_fresh_env_and_subprocess(marathon_store):
    """SC-008 (durable): session env, fresh env, fresh subprocess — the same
    durable rows yield the byte-identical position all three ways."""
    from codeconv.marathon.checkpoint import checkpoint, start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.position import resume_position
    from codeconv.marathon.stages import append_stage, register_run

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    register_run(RUN_ID, stages=["a", "b"], title="SC-008", env=env)
    start_stage(RUN_ID, "a", env=env)
    checkpoint(RUN_ID, "a", budget_delta=42, env=env)
    append_stage(RUN_ID, "c", env=env)

    # Full context: the env this session has been driving.
    pos_session = resume_position(RUN_ID, env=env)
    assert (pos_session.done, pos_session.total) == (1, 3)

    # In-process context loss: a freshly resolved env (no cached engine).
    fresh_env = resolve_env(RUN_ID, data_dir=marathon_store)
    pos_fresh = resume_position(RUN_ID, env=fresh_env)

    # Total context loss: a fresh CLI subprocess over the same store.
    proc = marathon_run(marathon_store, "position", "--run", RUN_ID, "--json")
    assert proc.returncode == 0, proc.stderr
    pos_subprocess = json.loads(proc.stdout)

    expected = _canonical(asdict(pos_session))
    assert _canonical(asdict(pos_fresh)) == expected
    assert _canonical(pos_subprocess) == expected
