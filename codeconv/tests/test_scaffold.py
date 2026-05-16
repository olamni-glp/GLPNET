"""Integration tests for ``codeconv scaffold`` — Feature 016 / US2.

Maps to ``specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_scaffold_cli.md``
(spec FR-013..FR-018) and the US2 independent test.

``@needs_bridge`` integration tests: migrate → init → scaffold via the
CLI subprocess harness, asserting the mirrored target tree, the
``target_path`` recorded into the feature-015 tombstone, idempotence,
staged-write atomicity, destructive-confirmation, prerequisite refusal,
and pair-mismatch refusal.

Kept small per the watchdog discipline; the suite is reconciled by the
orchestrator (PGLite cold-init ~7s; the ``discover_repo`` fixture uses
``tmp_path``).
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _mk_subtree(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib" / "runtime").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text(
        "/// A.\nclass A {}\n", encoding="utf-8"
    )
    (sub / "lib" / "runtime" / "heap.dart").write_text(
        "/// Heap.\nimport '../a.dart';\nclass Heap {}\n", encoding="utf-8"
    )
    (sub / ".dart_tool").mkdir()
    (sub / ".dart_tool" / "junk.dart").write_text(
        "/// junk\nclass J {}\n", encoding="utf-8"
    )
    return sub


def _init(repo_root: Path, *extra: str):
    return run_codeconv(
        repo_root,
        "init",
        "run",
        "--source",
        "glp_runtime_net",
        "--target",
        "out/csharp",
        "--source-lang",
        "dart",
        "--target-lang",
        "csharp",
        "--accept-suggested-exclusions",
        "--non-interactive",
        *extra,
    )


def _scaffold(repo_root: Path, *extra: str):
    return run_codeconv(repo_root, "scaffold", "run", "--json", *extra)


def _read_tombstone(repo_root: Path, rel: str) -> dict:
    from codeconv.tools.discover.tombstone import read_tombstone

    return read_tombstone(
        repo_root / ".codeconv" / "tombstones" / f"{rel}.md"
    )


def _tree(root: Path) -> dict[str, str]:
    out: dict[str, str] = {}
    if not root.is_dir():
        return out
    for p in sorted(root.rglob("*")):
        if p.is_file():
            out[p.relative_to(root).as_posix()] = hashlib.sha256(
                p.read_bytes()
            ).hexdigest()
        elif p.is_dir():
            out[p.relative_to(root).as_posix() + "/"] = "<dir>"
    return out


# ---------------------------------------------------------------------------
# US2 — scaffold the target tree
# ---------------------------------------------------------------------------


@needs_bridge
def test_scaffold_mirrors_tree_with_target_ext_and_workdir(
    discover_repo: Path,
) -> None:
    """FR-013: every non-excluded source file is mirrored into the target
    tree with the pair's `.cs` extension and the `__<base>/` workdir."""
    _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0
    proc = _scaffold(discover_repo)
    assert proc.returncode == 0, f"{proc.stdout}\n{proc.stderr}"

    target = discover_repo / "out" / "csharp"
    files = _tree(target)
    # Mirrored dirs + ext-swap (.dart → .cs).
    assert "lib/a.cs" in files, files
    assert "lib/runtime/heap.cs" in files, files
    # Per-file workdir adjacent to each scaffolded file.
    assert "lib/__a/" in files, files
    assert "lib/runtime/__heap/" in files, files
    # Excluded tool subtree NOT mirrored.
    assert not any("dart_tool" in k for k in files), files
    assert not any("junk" in k for k in files), files


@needs_bridge
def test_scaffold_records_target_path_in_tombstone(
    discover_repo: Path,
) -> None:
    """FR-015: each scaffolded source file's feature-015 tombstone
    ``target_path`` is the produced target rel-path."""
    _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0
    assert _scaffold(discover_repo).returncode == 0

    tomb = _read_tombstone(discover_repo, "lib/a.dart")
    assert tomb.get("target_path") == "lib/a.cs", tomb
    tomb2 = _read_tombstone(discover_repo, "lib/runtime/heap.dart")
    assert tomb2.get("target_path") == "lib/runtime/heap.cs", tomb2


@needs_bridge
def test_scaffold_missing_tombstone_warns_not_fails(
    discover_repo: Path,
) -> None:
    """FR-015: a missing tombstone for a scaffolded file is a WARNING,
    not a failure — scaffold still produces the target file."""
    _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0
    # Delete one tombstone so scaffold hits the missing-tombstone path.
    (
        discover_repo / ".codeconv" / "tombstones" / "lib" / "a.dart.md"
    ).unlink()

    proc = _scaffold(discover_repo)
    assert proc.returncode == 0, f"{proc.stdout}\n{proc.stderr}"
    summary = json.loads(_extract_json(proc.stdout))
    assert summary.get("tombstones_missing", 0) >= 1, summary
    # The target file is still produced.
    assert (discover_repo / "out" / "csharp" / "lib" / "a.cs").is_file()


@needs_bridge
def test_scaffold_idempotent_no_churn(discover_repo: Path) -> None:
    """SC-002: a re-scaffold on an unchanged inventory yields an identical
    target tree, zero phase duplication, zero tombstone diff."""
    _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0
    assert _scaffold(discover_repo).returncode == 0

    target = discover_repo / "out" / "csharp"
    tomb_root = discover_repo / ".codeconv" / "tombstones"
    t1, tb1 = _tree(target), _tree(tomb_root)

    assert _scaffold(discover_repo).returncode == 0
    assert _tree(target) == t1
    assert _tree(tomb_root) == tb1


@needs_bridge
def test_scaffold_unmanaged_target_with_operator_data_is_protected(
    discover_repo: Path,
) -> None:
    """FR-017 (reconciled): a pre-existing NON-empty target that scaffold
    did NOT produce (no scaffold manifest — holds operator data) is
    refused without --force-delete-target; the operator data survives
    intact (no half-write, no deletion, no staging residue)."""
    _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0

    target = discover_repo / "out" / "csharp"
    target.mkdir(parents=True)
    operator = target / "OPERATOR_DATA.txt"
    operator.write_text("do not lose me", encoding="utf-8")

    proc = _scaffold(discover_repo)
    assert proc.returncode == 2, (proc.returncode, proc.stdout, proc.stderr)
    assert operator.is_file()
    assert operator.read_text(encoding="utf-8") == "do not lose me"
    # No staging dir left behind.
    assert not (
        discover_repo / "out" / "csharp.codeconv-scaffold-tmp"
    ).exists()


@needs_bridge
def test_scaffold_refuses_nonempty_target_without_force(
    discover_repo: Path,
) -> None:
    """FR-017: scaffold into a non-empty target without
    ``--force-delete-target`` is refused (exit 2)."""
    _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0
    target = discover_repo / "out" / "csharp"
    target.mkdir(parents=True)
    (target / "preexisting.txt").write_text("x", encoding="utf-8")

    proc = _scaffold(discover_repo)
    assert proc.returncode == 2, (proc.returncode, proc.stdout, proc.stderr)
    # With the destructive confirmation, it proceeds.
    proc = _scaffold(discover_repo, "--force-delete-target")
    assert proc.returncode == 0, f"{proc.stdout}\n{proc.stderr}"
    assert (target / "lib" / "a.cs").is_file()


@needs_bridge
def test_scaffold_refuses_before_init_exit2(discover_repo: Path) -> None:
    """FR-018: scaffold before init / with an empty inventory is refused
    (exit 2, no output produced)."""
    _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    proc = _scaffold(discover_repo)
    assert proc.returncode == 2, (proc.returncode, proc.stdout, proc.stderr)
    assert not (discover_repo / "out" / "csharp").exists()


@needs_bridge
def test_scaffold_refuses_pair_mismatch(discover_repo: Path) -> None:
    """FR-018/SC-008: a per-invocation pair override disagreeing with the
    workspace-recorded pair is refused (exit 5, no output)."""
    _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0
    proc = run_codeconv(
        discover_repo,
        "scaffold",
        "run",
        "--source-lang",
        "dart",
        "--target-lang",
        "rust",
        "--json",
    )
    assert proc.returncode == 5, (proc.returncode, proc.stdout, proc.stderr)
    assert not (discover_repo / "out" / "csharp").is_dir()


@needs_bridge
def test_scaffold_advances_phase_status(discover_repo: Path) -> None:
    """FR-016: scaffold advances ``codeconv.phase_status['scaffold']`` to
    COMPLETE and ensures ``phase_sequence`` has ``scaffold``."""
    _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0
    assert _scaffold(discover_repo).returncode == 0

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=60.0)
    eng = build_engine(endpoint)
    with eng.connect() as c:
        status = c.execute(
            text(
                "SELECT status FROM codeconv.phase_status "
                "WHERE phase = 'scaffold'"
            )
        ).scalar()
        seq = c.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.phase_sequence "
                "WHERE phase = 'scaffold'"
            )
        ).scalar()
    assert status == "COMPLETE", status
    assert int(seq or 0) == 1, seq
