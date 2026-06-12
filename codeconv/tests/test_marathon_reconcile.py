"""US5 dual-store reconciliation — fast-forward or escalate (T044, FR-024).

PGLite-vs-JSON-mirror by ``sequence_no``: in-sync detected; the stale store
is fast-forwarded; a true fork writes ``store_divergence`` and never silently
picks (resume → exit 2 / ``diverged``).
"""

from __future__ import annotations

import json

from .conftest import needs_bridge

RUN_ID = "us5-reconcile"


@needs_bridge
def test_reconcile_in_sync_fast_forward_and_fork(marathon_store):
    from codeconv.marathon.checkpoint import checkpoint, start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.position import resume_position
    from codeconv.marathon.stages import register_run
    from codeconv.marathon.store import Repository, reconcile

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    repo = Repository(env)
    register_run(RUN_ID, stages=["a", "b"], env=env)
    start_stage(RUN_ID, "a", env=env)
    cp1 = checkpoint(RUN_ID, "a", env=env)  # dual-written → in sync

    result = reconcile(RUN_ID, env=env)
    assert result.status == "in_sync"
    assert (result.primary_seq, result.fallback_seq) == (1, 1)

    # Fallback ahead (a bridge-outage write): hand-plant seq 2 in the mirror;
    # reconcile fast-forwards the PRIMARY from it (FK-safe), then both stores
    # converge byte-wise.
    stage_a = repo.get_stage(RUN_ID, "a")
    (marathon_store / "json" / "checkpoints" / "2.json").write_text(
        json.dumps(
            {
                "run_id": RUN_ID,
                "stage_id": stage_a.id,
                "sequence_no": 2,
                "store_origin": "fallback",
                "completed_units": ["offline-unit"],
                "remaining_units": [],
            }
        ),
        encoding="utf-8",
    )
    result = reconcile(RUN_ID, env=env)
    assert result.status == "fast_forward"
    assert result.winner == "fallback"
    seqs = [c.sequence_no for c in repo.list_checkpoints(RUN_ID)]
    assert seqs == [1, 2]  # primary now carries the offline checkpoint
    recovered = [c for c in repo.list_checkpoints(RUN_ID) if c.sequence_no == 2][0]
    assert recovered.store_origin == "fallback"  # provenance preserved
    assert reconcile(RUN_ID, env=env).status == "in_sync"  # converged

    # A true fork: a shared sequence_no whose content differs. Never silently
    # pick — store_divergence escalation + diverged resume (exit-2 signal).
    mirror_cp1 = marathon_store / "json" / "checkpoints" / "1.json"
    payload = json.loads(mirror_cp1.read_text(encoding="utf-8"))
    payload["budget_spent"] = 999_999
    mirror_cp1.write_text(json.dumps(payload), encoding="utf-8")

    result = reconcile(RUN_ID, env=env)
    assert result.status == "fork"
    assert result.escalation_id is not None
    kinds = [e.kind for e in repo.list_escalations(RUN_ID, open_only=True)]
    assert "store_divergence" in kinds
    # Neither store was rewritten (no silent pick).
    assert json.loads(mirror_cp1.read_text(encoding="utf-8"))["budget_spent"] == 999_999
    (db_cp1,) = [c for c in repo.list_checkpoints(RUN_ID) if c.sequence_no == 1]
    assert db_cp1.budget_spent == cp1.budget_spent

    pos = resume_position(RUN_ID, env=env)
    assert pos.diverged is True
    assert "store divergence" in pos.next_action
