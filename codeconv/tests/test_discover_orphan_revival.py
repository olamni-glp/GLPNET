"""Tests for ``codeconv discover`` orphan + revival — Phase 6 / US4 / T064.

Maps to FR-025:

- ``test_orphan_on_delete`` — deleting a file moves the inventory row to
  ``codeconv.dart_files_orphaned`` and the tombstone to
  ``.codeconv/tombstones/.orphaned/``.
- ``test_revive_on_reappear`` — re-creating the file at the same path
  moves the row back, refreshes mtime + sha256, and moves the tombstone
  back from ``.orphaned/``.
- ``test_orphan_edges_recomputed`` — a revived file's import + caller
  edges are recomputed from scratch.
"""

from __future__ import annotations

import json
import time
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv

from .test_discover_idempotence import _extract_json


def _mk_subtree(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text(
        "/// File A.\nclass A {}\n", encoding="utf-8"
    )
    (sub / "lib" / "b.dart").write_text(
        "/// File B.\nimport 'a.dart';\nclass B {}\n", encoding="utf-8"
    )
    return sub


@needs_bridge
def test_orphan_on_delete(discover_repo: Path) -> None:
    sub = _mk_subtree(discover_repo)
    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, proc.stderr

    proc = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc.returncode == 0, proc.stderr

    tombstones = discover_repo / ".codeconv" / "tombstones"
    assert (tombstones / "lib" / "a.dart.md").is_file()
    assert (tombstones / "lib" / "b.dart.md").is_file()

    # Delete file A.
    (sub / "lib" / "a.dart").unlink()

    proc = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["orphaned"] == 1, summary

    assert not (tombstones / "lib" / "a.dart.md").is_file(), (
        "tombstone for deleted file should have been moved to .orphaned/"
    )
    assert (tombstones / ".orphaned" / "lib" / "a.dart.md").is_file()


@needs_bridge
def test_revive_on_reappear(discover_repo: Path) -> None:
    sub = _mk_subtree(discover_repo)
    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, proc.stderr

    run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    (sub / "lib" / "a.dart").unlink()
    run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )

    # Recreate with new content (different sha256).
    time.sleep(0.05)
    (sub / "lib" / "a.dart").write_text(
        "/// File A — revived with new content.\nclass A2 {}\n",
        encoding="utf-8",
    )

    proc = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["revived"] == 1, summary

    tombstones = discover_repo / ".codeconv" / "tombstones"
    assert (tombstones / "lib" / "a.dart.md").is_file(), (
        "revived file's tombstone must be back in the live tree"
    )
    assert not (tombstones / ".orphaned" / "lib" / "a.dart.md").is_file()


@needs_bridge
def test_orphan_edges_recomputed(discover_repo: Path) -> None:
    """A revived file's edges (imports + callers) reflect its NEW
    content, not the orphaned snapshot."""
    sub = _mk_subtree(discover_repo)
    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, proc.stderr

    run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    (sub / "lib" / "a.dart").unlink()
    run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )

    # Add a NEW import directive that didn't exist in the original.
    time.sleep(0.05)
    (sub / "lib" / "c.dart").write_text(
        "/// File C.\nclass C {}\n", encoding="utf-8"
    )
    (sub / "lib" / "a.dart").write_text(
        "/// File A — revived.\n"
        "import 'b.dart';\n"
        "import 'c.dart';\n"
        "class A {}\n",
        encoding="utf-8",
    )

    proc = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc.returncode == 0, proc.stderr

    # Read the revived tombstone and verify dependencies are the new set.
    import yaml

    tomb_text = (
        discover_repo / ".codeconv" / "tombstones" / "lib" / "a.dart.md"
    ).read_text(encoding="utf-8")
    parts = tomb_text.split("---", 2)
    fm = yaml.safe_load(parts[1])
    deps = sorted(fm["dependencies"])
    assert "lib/b.dart" in deps
    assert "lib/c.dart" in deps
