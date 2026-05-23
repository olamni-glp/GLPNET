"""T018 — deterministic codegen ingest: build gate, sentinel, replay-safe.

@needs_bridge. Exercises ``run_codegen_ingest`` (== the durable
``run_codegen_step`` core) in-process with an INJECTED build function so
no live ``dotnet`` is required (FR-003/FR-005). Covers:

- ``.cs`` absent ⇒ ``needs_agent_work`` sentinel (NEVER raises).
- invalid (non-C#) ``.cs`` ⇒ ``needs_agent_work``.
- valid ``.cs`` + passing build ⇒ ``built`` (phase-2 terminal write).
- failing build ⇒ ``needs_agent_work`` with parsed errors (a feedback
  fact, not an exception); ``build_status='fail'`` recorded.
- replay-safe: re-ingesting a built file is idempotent (``built`` again,
  one row, ``codegen_completed_at`` unchanged-shape).
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.codegen.artefact import produced_cs_rel
from codeconv.tools.codegen.buildgate import BuildResult
from codeconv.tools.codegen.workflow import run_codegen_ingest

from .conftest import needs_bridge
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


_PASS = lambda _proj: BuildResult(status="pass")  # noqa: E731
_FAIL = lambda _proj: BuildResult(  # noqa: E731
    status="fail", errors=("CS0246: type 'Foo' not found",)
)


def _setup(discover_repo: Path):
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)


def _write_cs(repo_root: Path, rel: str, body: str) -> Path:
    cs = repo_root / produced_cs_rel(rel)
    cs.parent.mkdir(parents=True, exist_ok=True)
    cs.write_text(body, encoding="utf-8")
    return cs


_VALID_CS = "namespace Demo;\npublic sealed class A { public int V; }\n"


@needs_bridge
def test_absent_cs_returns_needs_agent_work(discover_repo: Path) -> None:
    _setup(discover_repo)
    res = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    assert res["outcome"] == "needs_agent_work"
    assert res["needs_agent_work"] is True


@needs_bridge
def test_invalid_cs_returns_needs_agent_work(discover_repo: Path) -> None:
    _setup(discover_repo)
    _write_cs(discover_repo, "lib/a.dart", "# just prose, not C#\n")
    res = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    assert res["outcome"] == "needs_agent_work"
    assert res.get("validation_errors")


@needs_bridge
def test_valid_cs_passing_build_is_built(discover_repo: Path) -> None:
    _setup(discover_repo)
    _write_cs(discover_repo, "lib/a.dart", _VALID_CS)
    res = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    assert res["outcome"] == "built", res
    assert res["target_cs"] == "out/csharp/lib/a.cs"

    # State recorded: completed + build pass.
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(discover_repo, ready_timeout=30.0))
    with eng.connect() as conn:
        row = conn.execute(
            text(
                "SELECT codegen_completed_at, build_status, target_cs_path "
                "FROM codeconv.dart_codegen WHERE path = 'lib/a.dart'"
            )
        ).first()
    assert row is not None
    assert row[0] is not None and row[1] == "pass"
    assert row[2] == "out/csharp/lib/a.cs"


@needs_bridge
def test_failing_build_records_fail_and_needs_agent_work(
    discover_repo: Path,
) -> None:
    _setup(discover_repo)
    _write_cs(discover_repo, "lib/a.dart", _VALID_CS)
    res = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_FAIL,
        no_tombstone_update=True,
    )
    assert res["outcome"] == "needs_agent_work"
    assert res["build_status"] == "fail"
    assert res["build_errors"]  # parsed, not an exception

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(discover_repo, ready_timeout=30.0))
    with eng.connect() as conn:
        row = conn.execute(
            text(
                "SELECT codegen_completed_at, build_status "
                "FROM codeconv.dart_codegen WHERE path = 'lib/a.dart'"
            )
        ).first()
    assert row[0] is None  # not built
    assert row[1] == "fail"


@needs_bridge
def test_reingest_built_is_idempotent(discover_repo: Path) -> None:
    _setup(discover_repo)
    _write_cs(discover_repo, "lib/a.dart", _VALID_CS)
    r1 = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    r2 = run_codegen_ingest(
        discover_repo, rel_path="lib/a.dart", build_fn=_PASS,
        no_tombstone_update=True,
    )
    assert r1["outcome"] == r2["outcome"] == "built"

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(discover_repo, ready_timeout=30.0))
    with eng.connect() as conn:
        n = conn.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.dart_codegen "
                "WHERE path = 'lib/a.dart'"
            )
        ).scalar()
    assert n == 1  # no duplicate rows
