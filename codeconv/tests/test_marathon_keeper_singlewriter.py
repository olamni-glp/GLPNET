"""US3 single-writer — second writer refused, live bridge never killed (T027).

A second concurrent writer is refused (``ConcurrentWriter``) with a message
**distinct** from a recoverable stale condition (FR-015/016, SC-006); reads
from a second process stay legal; recovery fast-path-consumes a live bridge
child — it never kills one.
"""

from __future__ import annotations

import json

from .conftest import marathon_run, needs_bridge

RUN_ID = "us3-writer"


@needs_bridge
def test_second_writer_refused_live_bridge_consumed(marathon_store):
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.keeper import recover_keeper, start_keeper
    from codeconv.marathon.stages import register_run

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    # This process becomes THE writer (the lock is held for process lifetime).
    register_run(RUN_ID, stages=["a", "b"], env=env)
    ep1 = start_keeper(RUN_ID, env=env)

    # A second process attempting a WRITE is refused — blocked, not broken
    # (exit 2), with a message distinct from stale residue.
    proc = marathon_run(marathon_store, "append-stage", "x", "--run", RUN_ID)
    assert proc.returncode == 2, (proc.stdout, proc.stderr)
    message = (proc.stdout + proc.stderr).lower()
    assert "concurrent" in message and "writer" in message
    assert "stale" not in message  # FR-016: distinct from recoverable residue

    # Reads from a second process remain legal (single-WRITER, not -reader).
    read = marathon_run(marathon_store, "resume", "--run", RUN_ID, "--json")
    assert read.returncode == 0, read.stderr
    assert json.loads(read.stdout)["total"] == 2

    # Recovery against a LIVE bridge consumes it — same pid, never killed.
    ep2 = recover_keeper(RUN_ID, env=env)
    assert ep2.pid == ep1.pid
