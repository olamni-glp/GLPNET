"""Tests for ``codeconv discover`` idempotence — Phase 6 / US4 / T063.

Maps to SC-008 / FR-024:

- ``test_second_run_zero_diff_db`` — second run produces zero row diff
  in ``codeconv.dart_files`` / ``dart_imports`` / ``dart_callers``.
- ``test_second_run_zero_diff_tombstones`` — second run produces zero
  file diff in ``.codeconv/tombstones/``.

These are integration tests: they spawn the unified bridge, run
migrations + discover via the codeconv CLI, and assert against the
emitted ``--json`` summary plus the tombstone tree.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv


def _mk_subtree(repo_root: Path) -> Path:
    """Synthetic glp_runtime_net/ with 3 files + intra-subtree imports."""
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text(
        "/// File A — heap kernel.\nclass A {}\n",
        encoding="utf-8",
    )
    (sub / "lib" / "b.dart").write_text(
        "/// File B.\n\nimport 'a.dart';\n\nclass B {}\n",
        encoding="utf-8",
    )
    (sub / "lib" / "c.dart").write_text(
        "/// File C.\n\nimport 'b.dart';\nimport 'a.dart';\n\nclass C {}\n",
        encoding="utf-8",
    )
    return sub


def _tree_digest(root: Path) -> dict[str, str]:
    """Map of relative path → sha256 for stable comparison across runs."""
    out: dict[str, str] = {}
    if not root.is_dir():
        return out
    for p in sorted(root.rglob("*")):
        if p.is_file():
            rel = p.relative_to(root).as_posix()
            out[rel] = hashlib.sha256(p.read_bytes()).hexdigest()
    return out


@needs_bridge
def test_second_run_zero_diff_db(discover_repo: Path) -> None:
    """A re-run on an unchanged source state must skip all files
    (idempotence short-circuit per R15)."""
    sub = _mk_subtree(discover_repo)

    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, f"migrate failed: {proc.stderr}"

    proc1 = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
    )
    assert proc1.returncode == 0, f"first discover failed: {proc1.stderr}"
    summary1 = json.loads(_extract_json(proc1.stdout))
    assert summary1["files_walked"] == 3
    assert summary1["files_processed"] == 3
    assert summary1["files_skipped_idempotent"] == 0

    proc2 = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
    )
    assert proc2.returncode == 0, f"second discover failed: {proc2.stderr}"
    summary2 = json.loads(_extract_json(proc2.stdout))
    assert summary2["files_walked"] == 3
    # Re-run: every file is skipped via mtime+sha256 idempotence.
    assert summary2["files_skipped_idempotent"] == 3
    assert summary2["files_processed"] == 0


@needs_bridge
def test_second_run_zero_diff_tombstones(discover_repo: Path) -> None:
    """Re-run produces byte-identical ``.codeconv/tombstones/`` tree."""
    sub = _mk_subtree(discover_repo)

    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, f"migrate failed: {proc.stderr}"

    proc1 = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
    )
    assert proc1.returncode == 0, f"first discover failed: {proc1.stderr}"

    tombs1 = _tree_digest(discover_repo / ".codeconv" / "tombstones")
    assert tombs1, "first run must produce tombstones"

    proc2 = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
    )
    assert proc2.returncode == 0, f"second discover failed: {proc2.stderr}"

    tombs2 = _tree_digest(discover_repo / ".codeconv" / "tombstones")
    assert tombs1 == tombs2, (
        "tombstone tree changed across idempotent re-run; SC-008 violated"
    )


def _extract_json(stdout: str) -> str:
    """Return the JSON object embedded in ``stdout``.

    The CLI may emit log lines before and after the JSON body. We locate
    the first ``{`` and the matching trailing ``}``.
    """
    start = stdout.find("{")
    end = stdout.rfind("}")
    assert start >= 0 and end > start, f"no JSON in stdout:\n{stdout!r}"
    return stdout[start : end + 1]
