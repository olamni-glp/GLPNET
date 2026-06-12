"""Polish — resume-position determinism (T052).

SC-008: "The resume position is identical whether computed with full
conversation context or after total context loss (compaction) — it depends
solely on durable state." contracts/resume-position.md "Determinism": no
timestamps, no random, no conversation memory enter the computation.

Byte-level check over the canonical JSON serialisation of ``ResumePosition``:
(1) warm — same process, same env + cached engine; (2) cold — fresh env and
engine cache in the same process (a compaction survivor re-resolving from
scratch); (3) lost — a separate Python process sharing nothing but the durable
store (total context loss). All three must be byte-identical.
"""

from __future__ import annotations

import json
import subprocess
import sys
from dataclasses import asdict

from .conftest import needs_bridge

RUN_ID = "polish-determinism"

_SUBPROCESS_SCRIPT = """
import json, sys
from dataclasses import asdict
from codeconv.marathon.env import resolve_env
from codeconv.marathon.position import resume_position
env = resolve_env(sys.argv[1], data_dir=sys.argv[2])
pos = resume_position(sys.argv[1], env=env)
sys.stdout.write(json.dumps(asdict(pos), sort_keys=True, default=str))
"""


def _canonical(pos) -> bytes:
    return json.dumps(asdict(pos), sort_keys=True, default=str).encode("utf-8")


@needs_bridge
def test_resume_position_byte_identical_across_context_loss(marathon_store):
    from codeconv.db import engine as db_engine
    from codeconv.marathon.checkpoint import checkpoint, start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.intake import capture_item
    from codeconv.marathon.position import resume_position
    from codeconv.marathon.stages import register_run

    env = resolve_env(RUN_ID, data_dir=marathon_store)

    # Durable state exercising several ordering rules at once: two complete
    # stages (one carrying budget + an open issue), a blocking prereq whose
    # mini-stages route ahead of `c` (rules 2/3), `c` still pending.
    register_run(RUN_ID, stages=["a", "b", "c"], env=env)
    start_stage(RUN_ID, "a", env=env)
    checkpoint(RUN_ID, "a", budget_delta=1200, issues=["flaky fixture"], env=env)
    start_stage(RUN_ID, "b", env=env)
    checkpoint(RUN_ID, "b", env=env)
    item = capture_item(
        RUN_ID,
        kind="missing-prerequisite",
        title="schema migration must land first",
        blocks_stage="c",
        env=env,
    )

    # (1) warm: full in-process context.
    warm = _canonical(resume_position(RUN_ID, env=env))
    payload = json.loads(warm)
    assert (payload["done"], payload["total"]) == (2, 8)
    assert payload["next_action"] == f"run mini-specify for item-{item.id}"

    # (2) cold: fresh env + engine cache, same process (compaction survivor).
    db_engine.reset_engine_cache_for_tests()
    cold_env = resolve_env(RUN_ID, data_dir=marathon_store)
    cold = _canonical(resume_position(RUN_ID, env=cold_env))
    assert cold == warm

    # (3) lost: a separate process — nothing shared but the durable store.
    proc = subprocess.run(
        [sys.executable, "-c", _SUBPROCESS_SCRIPT, RUN_ID, str(marathon_store)],
        capture_output=True,
        text=True,
        timeout=120,
    )
    assert proc.returncode == 0, proc.stderr
    assert proc.stdout.encode("utf-8") == warm
