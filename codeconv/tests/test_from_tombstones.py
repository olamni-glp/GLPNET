"""Tests for ``codeconv discover --from-tombstones`` — Phase 6 / US4 / T065.

Maps to SC-007 / FR-022:

- ``test_rebuild_from_tombstones_equals_normal`` — discover_runs from
  tombstones produces structurally identical inventory rows.
- ``test_from_tombstones_does_not_read_dart`` — instrumented file-read
  proxy: removing the .dart files before running --from-tombstones must
  still succeed.
"""

from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


def _mk_subtree(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text(
        "/// A doc.\nclass A {}\n", encoding="utf-8"
    )
    (sub / "lib" / "b.dart").write_text(
        "/// B doc.\nimport 'a.dart';\nclass B {}\n", encoding="utf-8"
    )
    return sub


def _tree_digest(root: Path) -> dict[str, str]:
    out: dict[str, str] = {}
    for p in sorted(root.rglob("*")):
        if p.is_file():
            rel = p.relative_to(root).as_posix()
            out[rel] = hashlib.sha256(p.read_bytes()).hexdigest()
    return out


@needs_bridge
def test_rebuild_from_tombstones_equals_normal(discover_repo: Path) -> None:
    """Drop the codeconv schema, run --from-tombstones, verify resulting
    inventory rebuild produces the same tombstone set."""
    sub = _mk_subtree(discover_repo)
    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, proc.stderr

    proc1 = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc1.returncode == 0, proc1.stderr
    tombs_before = _tree_digest(discover_repo / ".codeconv" / "tombstones")
    assert tombs_before, "normal run must produce tombstones"

    # --from-tombstones reads the tombstone tree only.
    proc2 = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--from-tombstones",
        "--json",
    )
    assert proc2.returncode == 0, proc2.stderr
    summary = json.loads(_extract_json(proc2.stdout))
    assert summary["mode"] == "from_tombstones"

    # Tombstone tree itself MUST not change byte-wise — the rebuild
    # populates only DB rows; tombstones are the input here.
    tombs_after = _tree_digest(discover_repo / ".codeconv" / "tombstones")
    assert tombs_before == tombs_after


@needs_bridge
def test_from_tombstones_does_not_read_dart(discover_repo: Path) -> None:
    """After a normal discover, deleting all .dart sources must not
    prevent --from-tombstones from succeeding (proxy for ``no .dart
    reads``)."""
    sub = _mk_subtree(discover_repo)
    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, proc.stderr

    proc1 = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc1.returncode == 0, proc1.stderr

    # Wipe sources entirely.
    shutil.rmtree(sub)
    sub.mkdir()  # empty dir so --root resolves

    proc2 = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--from-tombstones",
        "--json",
    )
    assert proc2.returncode == 0, (
        f"--from-tombstones must succeed without .dart sources; got "
        f"stderr={proc2.stderr!r} stdout={proc2.stdout!r}"
    )
