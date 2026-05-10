"""Performance tests for ``codeconv discover`` — Phase 6 / US4 / T068.

Maps to SC-013:

- ``test_fresh_checkout_under_60s`` — ≤ 60 s to walk + parse + tombstone
  the real ``glp_runtime_net/`` (128 files at SC-006 baseline).
- ``test_idempotent_under_5s`` — ≤ 5 s on an unchanged re-run.

Opt-in via ``pytest --run-perf``. Skipped by default per conftest.
"""

from __future__ import annotations

import json
import time
from pathlib import Path

import pytest

from .conftest import (
    REPO_ROOT,
    _link_prereq_patterns,
    kill_bridge,
    needs_bridge,
    run_codeconv,
)
from .test_discover_idempotence import _extract_json


@pytest.fixture
def real_repo_root(tmp_path: Path) -> Path:
    """Use the actual repo's ``glp_runtime_net/`` but isolate ``.pgdb/``.

    To avoid colliding with the developer's live PGLite cluster (and
    the live bridge), we run discover with ``--repo-root`` pointing at
    a tmp_path and symlink the live ``glp_runtime_net/`` into it.
    """
    real_subtree = REPO_ROOT / "glp_runtime_net"
    if not real_subtree.is_dir():
        pytest.skip("glp_runtime_net/ not present in this checkout")

    # Wire up the bridge script + node_modules (junction on Windows).
    _link_prereq_patterns(tmp_path)

    # Symlink the live subtree into the isolated repo root.
    target = tmp_path / "glp_runtime_net"
    try:
        target.symlink_to(real_subtree, target_is_directory=True)
    except (OSError, NotImplementedError):
        # On Windows without dev-mode / admin: copy instead. Slow but
        # correct.
        import shutil

        shutil.copytree(real_subtree, target)
    yield tmp_path
    kill_bridge(tmp_path)


@needs_bridge
@pytest.mark.perf
def test_fresh_checkout_under_60s(real_repo_root: Path) -> None:
    sub = real_repo_root / "glp_runtime_net"
    proc = run_codeconv(real_repo_root, "migrate", timeout=120.0)
    assert proc.returncode == 0, proc.stderr

    t0 = time.monotonic()
    proc = run_codeconv(
        real_repo_root,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
        timeout=120.0,
    )
    elapsed = time.monotonic() - t0
    assert proc.returncode == 0, proc.stderr

    summary = json.loads(_extract_json(proc.stdout))
    # SC-006 baseline is 128 files; allow some drift but assert > 100.
    assert summary["files_walked"] >= 100, summary
    assert elapsed <= 60.0, (
        f"SC-013 fresh-checkout SLO breach: discover took {elapsed:.1f}s "
        f"(must be ≤ 60s)"
    )


@needs_bridge
@pytest.mark.perf
def test_idempotent_under_5s(real_repo_root: Path) -> None:
    sub = real_repo_root / "glp_runtime_net"
    proc = run_codeconv(real_repo_root, "migrate", timeout=120.0)
    assert proc.returncode == 0, proc.stderr

    # Warm the inventory.
    proc = run_codeconv(
        real_repo_root,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
        timeout=120.0,
    )
    assert proc.returncode == 0, proc.stderr

    # Idempotent re-run.
    t0 = time.monotonic()
    proc = run_codeconv(
        real_repo_root,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
        timeout=30.0,
    )
    elapsed = time.monotonic() - t0
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["files_skipped_idempotent"] >= 100, summary
    assert elapsed <= 5.0, (
        f"SC-013 idempotent SLO breach: re-run took {elapsed:.1f}s "
        f"(must be ≤ 5s)"
    )
