"""T040 — resume skips completed; re-run no diff; stale drift + retry.

@needs_bridge (FR-008/SC-005). With a stubbed build (no live dotnet):

- a completed file re-ingested stays ``built`` with an unchanged
  ``codegen_completed_at`` (resume skips it; no diff);
- editing the source (sha drift) makes ``status`` report the file
  ``stale`` — never silently treated as current;
- ``retry`` (or ``--respec``) re-opens the row in place (never deletes)
  so it regenerates; a fresh ingest completes it again.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.codegen.artefact import produced_cs_rel
from codeconv.tools.codegen.buildgate import BuildResult
from codeconv.tools.codegen.workflow import (
    run_codegen_ingest,
    run_retry,
    run_status,
)

from .conftest import needs_bridge, run_codeconv
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


_PASS = lambda _proj: BuildResult(status="pass")  # noqa: E731
_VALID_CS = "namespace Demo;\npublic sealed class A { public int V; }\n"


def _setup_and_build(discover_repo: Path) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    # run_status reads codeconv.dart_depgraph (stale detection); compute it.
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    cs = discover_repo / produced_cs_rel("lib/a.dart")
    cs.parent.mkdir(parents=True, exist_ok=True)
    cs.write_text(_VALID_CS, encoding="utf-8")


def _completed_at(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))
    with eng.connect() as conn:
        return conn.execute(
            text(
                "SELECT codegen_completed_at FROM codeconv.dart_codegen "
                "WHERE path = 'lib/a.dart'"
            )
        ).scalar()


@needs_bridge
def test_resume_skips_completed_no_diff(discover_repo: Path) -> None:
    _setup_and_build(discover_repo)
    r1 = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    assert r1["outcome"] == "built"
    first = _completed_at(discover_repo)
    # Resume: re-ingest the already-completed file ⇒ still built, and the
    # terminal timestamp is NOT rewritten (phase-2 UPDATE is WHERE
    # completed_at IS NULL).
    r2 = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    assert r2["outcome"] == "built"
    assert _completed_at(discover_repo) == first


@needs_bridge
def test_stale_drift_reported_then_retry_reopens(discover_repo: Path) -> None:
    _setup_and_build(discover_repo)
    run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )

    # Simulate a source edit: bump dart_files.sha256 → drift.
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(discover_repo, ready_timeout=30.0))
    with eng.begin() as conn:
        conn.execute(
            text(
                "UPDATE codeconv.dart_files SET sha256 = 'EDITED' "
                "WHERE path = 'lib/a.dart'"
            )
        )

    st = run_status(repo_root=discover_repo)
    assert "lib/a.dart" in st["stale"], st

    # retry re-opens in place (row preserved, completed_at cleared).
    rr = run_retry(
        repo_root=discover_repo, path="lib/a.dart", no_tombstone_update=True
    )
    assert rr["action"] == "reopened"
    assert _completed_at(discover_repo) is None

    # A fresh ingest re-completes it (no longer stale: sha re-snapshotted).
    r = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    assert r["outcome"] == "built"
    st2 = run_status(repo_root=discover_repo)
    assert "lib/a.dart" not in st2["stale"]


@needs_bridge
def test_respec_reopens_completed_on_drift(discover_repo: Path) -> None:
    _setup_and_build(discover_repo)
    run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(discover_repo, ready_timeout=30.0))
    with eng.begin() as conn:
        conn.execute(
            text(
                "UPDATE codeconv.dart_files SET sha256 = 'EDITED2' "
                "WHERE path = 'lib/a.dart'"
            )
        )
    # --respec re-opens then re-completes against the new sha in one call.
    r = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", respec=True, build_fn=_PASS,
        no_tombstone_update=True,
    )
    assert r["outcome"] == "built"
    st = run_status(repo_root=discover_repo)
    assert "lib/a.dart" not in st["stale"]
