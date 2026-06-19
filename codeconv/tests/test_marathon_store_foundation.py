"""Foundation tests for the per-run isolated marathon store (T011).

Strategic-integration coverage of Phase 2 (T005–T010), per the store contracts:

1. ``ensure_schema`` is idempotent — run twice against the live per-run
   cluster, clean, all 9 tables present (``contracts/store-schema.sql``).
2. Every row written to PGLite is dual-written to the JSON mirror at the
   same content (T009; mirror payload == payload of the row read back).
3. ``sequence_no`` is monotonic across primary + fallback:
   ``next = max(primary_max, fallback_max) + 1`` (T010, D6/I1).

Plus the T005 isolation invariant: a store root inside a git tree is refused
(FR-027) — bridge-free, asserted before any connect.
"""

from __future__ import annotations

import json

import pytest

from .conftest import needs_bridge

RUN_ID = "t011-foundation"


def _repo_for(store_root):
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.store import Repository

    env = resolve_env(RUN_ID, data_dir=store_root)
    return env, Repository(env)


def test_store_root_inside_repo_refused(repo_root):
    """T005/FR-027: a per-run store root inside the working repo git tree is
    refused at resolve time (bridge-free)."""
    from codeconv.marathon.env import StoreRootInsideRepoError, resolve_env

    inside = repo_root / ".codeconv" / "marathon-refused"
    with pytest.raises(StoreRootInsideRepoError):
        resolve_env(RUN_ID, data_dir=inside)


def test_bridge_script_source_decoupled_from_store_root(repo_root, tmp_path):
    """Fix-A invariant (bridge-free): the bridge SCRIPT is resolved from the
    toolchain checkout, NOT from the off-repo per-run store root.

    Regression guard for the original defect — both ``keeper.start_keeper`` and
    ``store._connect`` passed ``store_root`` as the bridge ``repo_root``, so the
    real CLI looked for ``pglite_bridge.mjs`` *inside the off-repo store* (where
    FR-027 guarantees it never is) and the primary store never started. Only the
    test fixture's now-removed junction masked it."""
    from codeconv.marathon.env import toolchain_repo_root

    rel = "prereq-patterns/pglite/pglite_bridge.mjs"
    root = toolchain_repo_root()
    # The toolchain root actually owns the bridge asset...
    assert (root / rel).is_file(), f"toolchain_repo_root() {root} lacks {rel}"
    assert root == repo_root.resolve()
    # ...and an off-repo store root does NOT (the decoupling the fix restored).
    store_root = tmp_path / "marathon_run_store"
    store_root.mkdir(parents=True, exist_ok=True)
    assert not (store_root / rel).exists()


@needs_bridge
def test_schema_idempotent_and_mirror_parity(marathon_store):
    """T006 idempotency + T007 composition + T008/T009 CRUD dual-write."""
    from sqlalchemy import text

    from codeconv.marathon.models import CheckpointRow, MarathonRun, StageRow
    from codeconv.marathon.store import ensure_schema, row_payload

    env, repo = _repo_for(marathon_store)

    # T007: first connect acquires the per-run bridge and applies the schema.
    engine = repo.engine()
    # T006: run twice more explicitly — must be clean (idempotent DDL).
    ensure_schema(engine)
    ensure_schema(engine)
    with engine.connect() as conn:
        n_tables = conn.execute(
            text(
                "SELECT count(*) FROM information_schema.tables "
                "WHERE table_schema = 'marathon'"
            )
        ).scalar_one()
    assert n_tables == 9

    # T008: run upsert is idempotent re-attach (second call returns the
    # existing row, never clobbers).
    first = repo.upsert_run(MarathonRun(id=RUN_ID, title="t011"))
    again = repo.upsert_run(MarathonRun(id=RUN_ID, title="clobber-attempt"))
    assert first.title == "t011"
    assert again.title == "t011"

    stage = repo.insert_stage(
        StageRow(
            run_id=RUN_ID, stage_index=1, order_key=1.0, name="a", origin="registered"
        )
    )
    assert stage.id is not None

    cp = repo.insert_checkpoint(
        CheckpointRow(run_id=RUN_ID, stage_id=stage.id, sequence_no=0)
    )
    assert cp.sequence_no == 1
    assert cp.store_origin == "primary"

    # T009: mirror content == content of the row read back from PGLite.
    json_root = marathon_store / "json"
    run_mirror = json.loads((json_root / "run.json").read_text(encoding="utf-8"))
    assert run_mirror == row_payload(repo.get_run(RUN_ID))

    stage_mirror = json.loads(
        (json_root / "stages" / "1.json").read_text(encoding="utf-8")
    )
    assert stage_mirror == row_payload(repo.get_stage(RUN_ID, "a"))

    cp_mirror = json.loads(
        (json_root / "checkpoints" / "1.json").read_text(encoding="utf-8")
    )
    (db_cp,) = [c for c in repo.list_checkpoints(RUN_ID) if c.sequence_no == 1]
    assert cp_mirror == row_payload(db_cp)

    # T008: update also re-mirrors at the same content.
    updated = repo.update_stage(stage.id, status="running")
    stage_mirror2 = json.loads(
        (json_root / "stages" / "1.json").read_text(encoding="utf-8")
    )
    assert stage_mirror2 == row_payload(updated)
    assert stage_mirror2["status"] == "running"


@needs_bridge
def test_sequence_monotonic_across_primary_and_fallback(marathon_store):
    """T010: ``next_sequence_no = max(primary_max, fallback_max) + 1``."""
    from codeconv.marathon.models import CheckpointRow, MarathonRun, StageRow

    env, repo = _repo_for(marathon_store)
    repo.upsert_run(MarathonRun(id=RUN_ID, title="t011-seq"))
    stage = repo.insert_stage(
        StageRow(
            run_id=RUN_ID, stage_index=1, order_key=1.0, name="a", origin="registered"
        )
    )

    cp1 = repo.insert_checkpoint(
        CheckpointRow(run_id=RUN_ID, stage_id=stage.id, sequence_no=0)
    )
    assert cp1.sequence_no == 1

    # Simulate a fallback-written checkpoint ahead of the primary (the
    # bridge-outage path writes the mirror alone with store_origin=fallback).
    fallback = marathon_store / "json" / "checkpoints" / "5.json"
    fallback.write_text(
        json.dumps(
            {"run_id": RUN_ID, "sequence_no": 5, "store_origin": "fallback"}
        ),
        encoding="utf-8",
    )

    assert repo.next_sequence_no(RUN_ID) == 6
    cp6 = repo.insert_checkpoint(
        CheckpointRow(run_id=RUN_ID, stage_id=stage.id, sequence_no=0)
    )
    assert cp6.sequence_no == 6
    assert cp6.store_origin == "primary"
