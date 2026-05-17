"""T043/T048/T049/T058 [US3/US4] — unified status (<5 s), DBOS trace,
tombstone↔DB divergence refusal, reproducible-from-durable.
@needs_bridge; builds on the proven e2e helper (migrate→init→discover→
depgraph→builder run).
"""

from __future__ import annotations

import json
import time
from pathlib import Path

from .conftest import needs_bridge, run_codeconv
from ._builder_e2e_helpers import (
    builder_run,
    last_json,
    migrate_discover_depgraph,
    mk_nfile_subtree,
)


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


# ---- T043: unified status reconciles durable state, snapshot <5 s ----
@needs_bridge
def test_status_projection_reconciles_under_5s(discover_repo: Path) -> None:
    sub = mk_nfile_subtree(discover_repo, n=20)
    migrate_discover_depgraph(discover_repo, sub)
    assert last_json(builder_run(discover_repo, timeout=600.0))[
        "outcome"
    ] == "completed"

    t0 = time.monotonic()
    p = run_codeconv(discover_repo, "builder", "status", "--json", timeout=60.0)
    dt = time.monotonic() - t0
    assert p.returncode == 0, p.stderr
    snap = last_json(p)
    assert dt < 5.0, f"status took {dt:.2f}s (> 5 s, SC-009)"
    # counts reconcile: sum of per-state counts == total == file rows.
    total = snap["counts"]["total"]
    assert total == len(snap["files"]) >= 20
    state_sum = sum(
        v for k, v in snap["counts"].items() if k != "total"
    )
    assert state_sum == total, (snap["counts"], total)
    # every file carries a state from the single status.py vocabulary.
    from codeconv.status import FileState

    allowed = {s.value for s in FileState}
    assert all(f["state"] in allowed for f in snap["files"])


# ---- T048: DBOS trace surface per file / per run ----
@needs_bridge
def test_builder_trace_per_file_and_run(discover_repo: Path) -> None:
    sub = mk_nfile_subtree(discover_repo, n=20)
    migrate_discover_depgraph(discover_repo, sub)
    r = last_json(builder_run(discover_repo, timeout=600.0))
    assert r["outcome"] == "completed"

    pf = run_codeconv(
        discover_repo, "builder", "trace", "--file", "lib/f0.dart",
        "--json", timeout=60.0,
    )
    assert pf.returncode == 0, pf.stderr
    tf = last_json(pf)
    assert tf["file"] == "lib/f0.dart"
    assert "workflow_id" in tf and "steps" in tf

    pr = run_codeconv(
        discover_repo, "builder", "trace", "--run",
        r["outer_workflow_id"], "--json", timeout=60.0,
    )
    assert pr.returncode == 0, pr.stderr
    tr = last_json(pr)
    assert tr["workflow_id"] == r["outer_workflow_id"]
    assert "children" in tr


# ---- T049: tombstone↔DB divergence detected, refuses stale (exit 4) ----
@needs_bridge
def test_tombstone_db_divergence_refuses(discover_repo: Path) -> None:
    sub = mk_nfile_subtree(discover_repo, n=20)
    migrate_discover_depgraph(discover_repo, sub)
    assert last_json(builder_run(discover_repo, timeout=600.0))[
        "outcome"
    ] == "completed"

    # Forge divergence: a tombstone claims convspec_completed but there
    # is no completed dart_convspecs row for it.
    from codeconv.tools.discover.tombstone import (
        read_tombstone,
        write_tombstone,
    )

    tdir = discover_repo / ".codeconv" / "tombstones"
    some = sorted(tdir.rglob("*.dart.md"))[0]
    fm = read_tombstone(some)
    fm["convspec_completed_at"] = "2026-05-17T00:00:00Z"
    fm["builder_file_state"] = "specced"
    rel = fm["path"]
    write_tombstone(tdir, rel, fm)
    from sqlalchemy import text

    with _engine(discover_repo).begin() as c:
        c.execute(
            text(
                "DELETE FROM codeconv.dart_convspecs WHERE path = :p"
            ),
            {"p": rel},
        )

    p = run_codeconv(discover_repo, "builder", "run", "--json", timeout=120.0)
    assert p.returncode == 4, (
        f"expected exit 4 (stale), got {p.returncode}: {p.stdout}{p.stderr}"
    )
    out = last_json(p)
    assert out["outcome"] == "stale"
    assert rel in out["diverged"]


# ---- T058: reproducible from durable (FR-021) ----
@needs_bridge
def test_reproducible_from_durable(discover_repo: Path) -> None:
    sub = mk_nfile_subtree(discover_repo, n=20)
    migrate_discover_depgraph(discover_repo, sub)
    r1 = last_json(builder_run(discover_repo, timeout=600.0))
    assert r1["outcome"] == "completed"

    from sqlalchemy import text

    def _snapshot():
        with _engine(discover_repo).connect() as c:
            files = c.execute(
                text("SELECT count(*) FROM codeconv.dart_depgraph")
            ).scalar()
            convs = c.execute(
                text("SELECT count(*) FROM codeconv.dart_convspecs")
            ).scalar()
        return (files, convs)

    before = _snapshot()
    # Wipe derived local artifacts; re-drive recovers from durable
    # truth (deterministic id) — regenerates identically (FR-021).
    djson = discover_repo / ".codeconv" / "depgraph.json"
    if djson.is_file():
        djson.unlink()
    r2 = last_json(builder_run(discover_repo, timeout=300.0))
    assert r2["outcome"] == "completed"
    assert r2["outer_workflow_id"] == r1["outer_workflow_id"]
    assert _snapshot() == before, "durable state not reproducible"
