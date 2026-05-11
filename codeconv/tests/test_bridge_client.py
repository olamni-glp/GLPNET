"""Tests for ``codeconv.bridge_client`` — Phase 5 / US3 / T050.

Maps to ``specs/012-codeconv-runner/contracts/bridge_lifecycle.md``
acceptance tests (Python side):
- ``test_acquire_or_discover_lock_winner``  (Path A — bridge owner)
- ``test_acquire_or_discover_lock_loser``   (Path B — sidecar consumer)
- ``test_lock_race_fallback``                (concurrent Path A/B race)
- ``test_post_kill_restart``                 (SC-002)
- ``test_ready_timeout``                     (slow / hung bridge)
"""

from __future__ import annotations

import os
import signal
import socket
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

import pytest

from codeconv.bridge_client import (
    BridgeStartupTimeout,
    acquire_or_discover,
)
from codeconv.bridge_client import _try_acquire_lock

from .conftest import needs_bridge


# ---------------------------------------------------------------------------
# Lock mechanics — pure Python (no bridge required)
# ---------------------------------------------------------------------------


def test_lock_winner_blocks_loser(tmp_path: Path) -> None:
    """The portalocker-based mutex really excludes a second acquirer.

    No bridge spawn — verifies the cross-process exclusion primitive in
    isolation so a failure here is not confused with a bridge startup
    problem. Path: same shape (``<repo>/.pgdb.bridge.lock``).
    """
    lock_path = tmp_path / ".pgdb.bridge.lock"
    first = _try_acquire_lock(lock_path)
    assert first is not None, "first acquire must succeed"
    try:
        second = _try_acquire_lock(lock_path)
        assert second is None, "second acquire must fail while first is held"
    finally:
        first.release()
    third = _try_acquire_lock(lock_path)
    assert third is not None, "after release, fresh acquire must succeed"
    third.release()


# ---------------------------------------------------------------------------
# End-to-end with real bridge (skipped if node + bridge script absent)
# ---------------------------------------------------------------------------


@needs_bridge
def test_acquire_or_discover_lock_winner(isolated_repo: Path, bridge_script: Path) -> None:
    """Path A: first caller wins lock, spawns bridge, gets endpoint."""
    endpoint = acquire_or_discover(isolated_repo, ready_timeout=30.0, bridge_script=bridge_script)
    try:
        assert endpoint.owned is True
        assert endpoint.host == "127.0.0.1"
        assert endpoint.port > 0
        assert endpoint.pid > 0
        # Sidecar must exist now.
        sidecar = isolated_repo / ".pgdb" / "bridge.json"
        assert sidecar.is_file(), "bridge must have written bridge.json"
        # Endpoint reachable.
        with socket.create_connection((endpoint.host, endpoint.port), timeout=5):
            pass
    finally:
        _kill_bridge_for(isolated_repo, endpoint.pid)


@needs_bridge
def test_acquire_or_discover_lock_loser(isolated_repo: Path, bridge_script: Path) -> None:
    """Path B: bridge already running -> second caller reads sidecar."""
    first = acquire_or_discover(isolated_repo, ready_timeout=30.0, bridge_script=bridge_script)
    try:
        second = acquire_or_discover(isolated_repo, ready_timeout=30.0, bridge_script=bridge_script)
        assert second.owned is False
        assert (second.host, second.port) == (first.host, first.port)
    finally:
        _kill_bridge_for(isolated_repo, first.pid)


@needs_bridge
def test_lock_race_fallback(isolated_repo: Path, bridge_script: Path) -> None:
    """Two parallel acquires: exactly one Path A, exactly one Path B,
    both end at the same endpoint."""

    def _go() -> object:
        return acquire_or_discover(
            isolated_repo, ready_timeout=30.0, bridge_script=bridge_script
        )

    with ThreadPoolExecutor(max_workers=2) as pool:
        futures = [pool.submit(_go) for _ in range(2)]
        results = [f.result() for f in as_completed(futures)]
    try:
        owners = sum(1 for r in results if r.owned)
        consumers = sum(1 for r in results if not r.owned)
        assert owners == 1, f"expected exactly 1 owner, got {owners}"
        assert consumers == 1, f"expected exactly 1 consumer, got {consumers}"
        assert results[0].port == results[1].port
        assert results[0].host == results[1].host
    finally:
        # All winners share the same bridge pid.
        owner_pid = next(r.pid for r in results if r.owned)
        _kill_bridge_for(isolated_repo, owner_pid)


@needs_bridge
def test_post_kill_restart(isolated_repo: Path, bridge_script: Path) -> None:
    """SC-002 (Python parity): force-kill the bridge, fresh acquire works
    within ~1 s (PGLite warm cache after first init makes the second
    spawn fast; cold init can take ~7 s on Windows so we permit 30 s)."""
    first = acquire_or_discover(isolated_repo, ready_timeout=30.0, bridge_script=bridge_script)
    first_pid = first.pid
    _kill_bridge_for(isolated_repo, first_pid, hard=True)
    # Wait for the kernel to release the lock + the OS to reap PID.
    deadline = time.monotonic() + 5.0
    while time.monotonic() < deadline and _pid_alive(first_pid):
        time.sleep(0.05)
    second = acquire_or_discover(isolated_repo, ready_timeout=30.0, bridge_script=bridge_script)
    try:
        assert second.pid != first_pid
        assert second.owned is True  # we are the new owner
        with socket.create_connection((second.host, second.port), timeout=5):
            pass
    finally:
        _kill_bridge_for(isolated_repo, second.pid)


@needs_bridge
def test_ready_timeout(isolated_repo: Path, bridge_script: Path) -> None:
    """Slow/hung bridge: simulated by giving an absurdly small timeout."""
    with pytest.raises((BridgeStartupTimeout, RuntimeError)):
        acquire_or_discover(isolated_repo, ready_timeout=0.05, bridge_script=bridge_script)
    # The aborted spawn may have left a half-started bridge. Best-effort
    # cleanup: if a sidecar appeared, kill that pid too.
    sidecar = isolated_repo / ".pgdb" / "bridge.json"
    if sidecar.is_file():
        try:
            import json as _json

            data = _json.loads(sidecar.read_text(encoding="utf-8"))
            _kill_pid(int(data.get("pid", 0)))
        except Exception:
            pass


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _kill_bridge_for(repo_root: Path, pid: int, *, hard: bool = False) -> None:
    """Best-effort termination of the bridge PID."""
    if pid <= 0:
        return
    _kill_pid(pid, hard=hard)
    # Wait briefly for sidecar removal / lock release.
    deadline = time.monotonic() + 5.0
    sidecar = repo_root / ".pgdb" / "bridge.json"
    while time.monotonic() < deadline:
        if not _pid_alive(pid):
            break
        time.sleep(0.05)


def _kill_pid(pid: int, *, hard: bool = False) -> None:
    if pid <= 0:
        return
    if os.name == "nt":
        flag = "/F" if hard else "/T"
        try:
            subprocess.run(
                ["taskkill", flag, "/PID", str(pid)],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                check=False,
            )
        except Exception:
            pass
    else:
        try:
            os.kill(pid, signal.SIGKILL if hard else signal.SIGTERM)
        except Exception:
            pass


def _pid_alive(pid: int) -> bool:
    if pid <= 0:
        return False
    if os.name == "nt":
        try:
            res = subprocess.run(
                ["tasklist", "/FI", f"PID eq {pid}", "/FO", "CSV", "/NH"],
                capture_output=True,
                text=True,
                check=False,
            )
            return str(pid) in res.stdout
        except Exception:
            return False
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
