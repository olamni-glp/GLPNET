"""US5 re-run — per-block from last checkpoint, isolated subagent (T041).

FR-021/AS2: ``rerun_block`` resumes from the block's LAST checkpoint (not run
start); ``rerun_subagent`` re-runs only the named subagent and reports the
untouched siblings (+ the Workflow runId for the cached-prefix cascade).
"""

from __future__ import annotations

from .conftest import needs_bridge

RUN_ID = "us5-rerun"


@needs_bridge
def test_rerun_block_and_subagent(marathon_store):
    from codeconv.marathon.checkpoint import checkpoint, start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.orchestrate import (
        record_run_linkage,
        rerun_block,
        rerun_subagent,
    )
    from codeconv.marathon.stages import register_run
    from codeconv.marathon.store import Repository

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    repo = Repository(env)
    register_run(RUN_ID, stages=["a"], env=env)

    # No checkpoint yet → a full run.
    fresh = rerun_block(repo, RUN_ID, "a", all_units=["u1", "u2", "u3"])
    assert fresh["from_seq"] == 0
    assert fresh["to_run"] == ["u1", "u2", "u3"]

    start_stage(RUN_ID, "a", env=env)
    cp = checkpoint(
        RUN_ID, "a",
        completed_units=["u1", "u2"], remaining_units=["u3"], env=env,
    )
    record_run_linkage(repo, cp.id, "wf_cached123")

    # Resume from the block's LAST checkpoint — completed skipped exactly once.
    picture = rerun_block(repo, RUN_ID, "a", all_units=["u1", "u2", "u3"])
    assert picture["from_seq"] == cp.sequence_no
    assert picture["to_run"] == ["u3"]
    assert picture["completed_units"] == ["u1", "u2"]
    assert picture["workflow_run_id"] == "wf_cached123"
    # Without all_units, the durable remaining_units drive the re-run.
    assert rerun_block(repo, RUN_ID, "a")["to_run"] == ["u3"]
    # A changed-input unit (new key) is new work, even with a stale completion.
    changed = rerun_block(repo, RUN_ID, "a", all_units=["u1*", "u2", "u3"])
    assert changed["to_run"] == ["u1*", "u3"]

    # Isolated subagent re-run: only the named one; siblings untouched.
    isolated = rerun_subagent(repo, RUN_ID, "a", "u3")
    assert isolated["to_run"] == ["u3"]
    assert isolated["untouched"] == ["u1", "u2"]
    assert isolated["workflow_run_id"] == "wf_cached123"
