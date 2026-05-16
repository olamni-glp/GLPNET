"""End-to-end tests for ``codeconv depgraph mark-started`` / ``mark-completed``.

Maps to ``specs/015-codeconv-depgraph/contracts/depgraph_cli.md`` §
``mark-started`` / ``mark-completed`` and ``data-model.md`` § 3 (state
machine). T019. Gated by ``@needs_bridge``.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.discover.tombstone import read_tombstone

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree

try:  # parsing is only needed inside @needs_bridge tests
    import json
except Exception:  # pragma: no cover
    json = None  # type: ignore


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


def _conv_row(repo_root: Path, path: str):
    from sqlalchemy import text

    with _engine(repo_root).connect() as conn:
        return conn.execute(
            text(
                "SELECT started_at, completed_at, sha256_of_dart_at_start, "
                "target_path FROM codeconv.dart_conversions WHERE path = :p"
            ),
            {"p": path},
        ).first()


def _dart_files_sha(repo_root: Path, path: str) -> str:
    from sqlalchemy import text

    with _engine(repo_root).connect() as conn:
        return conn.execute(
            text("SELECT sha256 FROM codeconv.dart_files WHERE path = :p"),
            {"p": path},
        ).scalar()


def _summary(proc) -> dict:
    return json.loads(_extract_json(proc.stdout))


# ---------------------------------------------------------------------------
# mark-started
# ---------------------------------------------------------------------------


@needs_bridge
def test_mark_started_inserts_row_with_started_at(discover_repo: Path) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-started", "lib/a.dart", "--json"
    )
    assert proc.returncode == 0, proc.stderr
    s = _summary(proc)
    assert s["action"] == "started"
    assert s["started_at"]
    row = _conv_row(discover_repo, "lib/a.dart")
    assert row is not None
    assert row[0] is not None  # started_at
    assert row[1] is None  # completed_at NULL


@needs_bridge
def test_mark_started_uses_dart_files_sha256_when_no_arg(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    db_sha = _dart_files_sha(discover_repo, "lib/a.dart")
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-started", "lib/a.dart", "--json"
    )
    assert proc.returncode == 0, proc.stderr
    assert _summary(proc)["sha256_of_dart_at_start"] == db_sha
    assert _conv_row(discover_repo, "lib/a.dart")[2] == db_sha


@needs_bridge
def test_mark_started_uses_sha256_arg_when_provided(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    override = "f" * 64
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-started", "lib/a.dart",
        "--sha256", override, "--json",
    )
    assert proc.returncode == 0, proc.stderr
    assert _conv_row(discover_repo, "lib/a.dart")[2] == override


@needs_bridge
def test_mark_started_idempotent_warns_when_already_started(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(
            discover_repo, "depgraph", "mark-started", "lib/a.dart"
        ).returncode
        == 0
    )
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-started", "lib/a.dart", "--json"
    )
    assert proc.returncode == 0, proc.stderr
    s = _summary(proc)
    assert s["action"] == "noop"
    assert "already started" in s["warning"]


@needs_bridge
def test_mark_started_idempotent_warns_when_already_completed(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    run_codeconv(discover_repo, "depgraph", "mark-completed", "lib/a.dart")
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-started", "lib/a.dart", "--json"
    )
    assert proc.returncode == 0, proc.stderr
    assert "already completed" in _summary(proc)["warning"]


@needs_bridge
def test_mark_started_errors_on_nonexistent_path(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-started", "lib/nope.dart"
    )
    assert proc.returncode == 2, (
        f"nonexistent path must exit 2; got {proc.returncode} "
        f"{proc.stdout}{proc.stderr}"
    )


# ---------------------------------------------------------------------------
# mark-completed
# ---------------------------------------------------------------------------


@needs_bridge
def test_mark_completed_updates_completed_at_and_target_path(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-completed", "lib/a.dart",
        "--target", "out/A.cs", "--json",
    )
    assert proc.returncode == 0, proc.stderr
    s = _summary(proc)
    assert s["action"] == "completed"
    assert s["target_path"] == "out/A.cs"
    row = _conv_row(discover_repo, "lib/a.dart")
    assert row[1] is not None  # completed_at
    assert row[3] == "out/A.cs"  # target_path


@needs_bridge
def test_mark_completed_errors_if_never_started(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-completed", "lib/a.dart"
    )
    assert proc.returncode == 2, (
        f"mark-completed before mark-started must exit 2; got "
        f"{proc.returncode} {proc.stdout}{proc.stderr}"
    )
    assert "started" in (proc.stdout + proc.stderr).lower()


@needs_bridge
def test_mark_completed_idempotent_warns_when_already_completed(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    run_codeconv(discover_repo, "depgraph", "mark-completed", "lib/a.dart")
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-completed", "lib/a.dart", "--json"
    )
    assert proc.returncode == 0, proc.stderr
    assert "already completed" in _summary(proc)["warning"]


@needs_bridge
def test_mark_completed_idempotent_does_not_overwrite_target_path(
    discover_repo: Path,
) -> None:
    """FR-006a: target_path + completed_at are write-once."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    run_codeconv(
        discover_repo, "depgraph", "mark-completed", "lib/a.dart",
        "--target", "first.cs",
    )
    run_codeconv(
        discover_repo, "depgraph", "mark-completed", "lib/a.dart",
        "--target", "second.cs",
    )
    assert _conv_row(discover_repo, "lib/a.dart")[3] == "first.cs"


@needs_bridge
def test_mark_completed_errors_on_nonexistent_path(
    discover_repo: Path,
) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    proc = run_codeconv(
        discover_repo, "depgraph", "mark-completed", "lib/nope.dart"
    )
    assert proc.returncode == 2, (
        f"nonexistent path must exit 2; got {proc.returncode} "
        f"{proc.stdout}{proc.stderr}"
    )


# ---------------------------------------------------------------------------
# tombstone YAML round-trip
# ---------------------------------------------------------------------------


@needs_bridge
def test_mark_started_updates_tombstone_yaml(discover_repo: Path) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    fm = read_tombstone(
        discover_repo / ".codeconv" / "tombstones" / "lib" / "a.dart.md"
    )
    assert fm.get("conversion_started_at")  # present + non-null


@needs_bridge
def test_mark_completed_updates_tombstone_yaml(discover_repo: Path) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    run_codeconv(
        discover_repo, "depgraph", "mark-completed", "lib/a.dart",
        "--target", "out/A.cs",
    )
    fm = read_tombstone(
        discover_repo / ".codeconv" / "tombstones" / "lib" / "a.dart.md"
    )
    assert fm.get("conversion_completed_at")  # present + non-null
    assert fm.get("target_path") == "out/A.cs"
    # mark-started's value must survive the mark-completed rewrite.
    assert fm.get("conversion_started_at")
