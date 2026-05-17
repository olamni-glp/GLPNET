"""Pytest configuration for codeconv.

Provides shared fixtures and an opt-in performance-test runner flag
(``--run-perf``) per ``specs/012-codeconv-runner/tasks.md`` T068 / SC-013.
"""

from __future__ import annotations

import json as _json
import os
import shutil
import signal
import socket
import subprocess
import sys
import time
from pathlib import Path
from typing import Iterable, Optional

import pytest


REPO_ROOT = Path(__file__).resolve().parent.parent.parent
BRIDGE_SCRIPT = REPO_ROOT / "prereq-patterns" / "pglite" / "pglite_bridge.mjs"


def pytest_addoption(parser: pytest.Parser) -> None:
    parser.addoption(
        "--run-perf",
        action="store_true",
        default=False,
        help="run @pytest.mark.perf opt-in performance tests (T068 / SC-013)",
    )


def pytest_collection_modifyitems(
    config: pytest.Config, items: list[pytest.Item]
) -> None:
    # Operator progress indicator (feature-018): record the collected
    # total so each test can report "[i/N]". A long serial @needs_bridge
    # run is otherwise silent for many minutes.
    config._codeconv_total = len(items)  # type: ignore[attr-defined]
    config._codeconv_done = 0  # type: ignore[attr-defined]
    if config.getoption("--run-perf"):
        return
    skip_perf = pytest.mark.skip(reason="opt-in via --run-perf")
    for item in items:
        if "perf" in item.keywords:
            item.add_marker(skip_perf)


def pytest_runtest_logreport(report: pytest.TestReport) -> None:
    """Emit one live, immediately-flushed progress line per test.

    Operator-facing progress for long serial bridge runs: reports on the
    ``call`` phase (or on a setup error/skip) with running ``[i/N]``
    count, outcome, the node id, and elapsed seconds. Goes to stderr so
    it interleaves with — but does not corrupt — pytest's own report.
    """
    # Only count a test once: the 'call' phase for run tests; the
    # 'setup' phase when a test is skipped or errors before call.
    if report.when == "call" or (
        report.when == "setup" and report.outcome in ("skipped", "failed")
    ):
        global _CC_DONE, _CC_TOTAL
        _CC_DONE += 1
        outcome = report.outcome.upper()
        dur = getattr(report, "duration", 0.0) or 0.0
        line = (
            f"[codeconv {_CC_DONE}/{_CC_TOTAL or '?'}] "
            f"{outcome:7s} {report.nodeid} ({dur:.1f}s)"
        )
        print(line, file=sys.stderr, flush=True)


def pytest_collection_finish(session: pytest.Session) -> None:
    global _CC_TOTAL, _CC_DONE
    _CC_TOTAL = len(session.items)
    _CC_DONE = 0


# Module-level progress counters (simple + robust across the single
# serial session this suite is designed to run in — bridge tests are
# never xdist-parallel: 012 single-writer lock + PGLite cold-init).
_CC_TOTAL: int = 0
_CC_DONE: int = 0


def _node_available() -> bool:
    return shutil.which("node") is not None


def _bridge_script_present() -> bool:
    return BRIDGE_SCRIPT.is_file()


@pytest.fixture(scope="session")
def repo_root() -> Path:
    return REPO_ROOT


@pytest.fixture(scope="session")
def bridge_script() -> Path:
    return BRIDGE_SCRIPT


needs_bridge = pytest.mark.skipif(
    not (_node_available() and _bridge_script_present()),
    reason="requires node and prereq-patterns/pglite/pglite_bridge.mjs",
)


@pytest.fixture
def isolated_repo(tmp_path: Path, repo_root: Path) -> Iterable[Path]:
    """A throwaway "repo root" with a fresh ``.pgdb/`` cluster, with
    **per-test bridge teardown** (the proven ``discover_repo`` pattern).

    The bridge script lives in the real repo (where node_modules is
    installed); we point ``acquire_or_discover`` at it via the
    ``bridge_script`` keyword argument while letting the *data dir* live
    under ``tmp_path`` so each test is fully isolated.

    Tests should pass ``bridge_script=repo_root/'prereq-patterns'/'pglite'/'pglite_bridge.mjs'``
    when invoking ``acquire_or_discover``.

    **Why the teardown (feature-018 harness fix).** Previously this
    fixture was ``return tmp_path`` with no teardown, so every
    ``@needs_bridge`` test that spawned a bridge here *leaked* the bridge
    node process (holding PGLite/WASM memory + the proper-lockfile lock).
    Across the serial suite the orphans accumulated until PGLite
    cold-init for the next test exceeded the 30 s ``ready_timeout`` — the
    progressive ``BridgeStartupTimeout`` cascade. Killing the spawned
    bridge at test end (best-effort, via the proven :func:`kill_bridge`
    helper — the same discipline ``discover_repo`` already uses) makes
    each ``@needs_bridge`` test truly isolated, so the suite is stable
    run together, not only one-at-a-time.
    """
    yield tmp_path
    # Per-test isolation: kill any bridge this test spawned. Default
    # data-dir is ``<tmp_path>/.pgdb``; an explicit ``data_dir=`` (rare)
    # is a sibling cleaned up with the tmp dir. Best-effort + idempotent.
    kill_bridge(tmp_path)


def _free_port() -> int:
    """Reserve an ephemeral port (released immediately; race-prone but OK
    for the few tests that need it). Tests that DO NOT pass --port get the
    bridge's own ephemeral allocation, which is preferable."""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


# ---------------------------------------------------------------------------
# Subprocess helpers for integration tests (Phase 6 / US4)
# ---------------------------------------------------------------------------


def run_codeconv(
    repo_root: Path,
    *args: str,
    timeout: float = 180.0,
    check: bool = False,
    extra_env: Optional[dict[str, str]] = None,
) -> subprocess.CompletedProcess[str]:
    """Invoke ``python -m codeconv.cli`` against ``repo_root``.

    Each call is a fresh process, which gives DBOS a fresh in-memory
    state. The bridge daemon spawned by the first invocation persists
    across subsequent invocations (per FR-006 auto-spawn-on-demand) until
    the test calls :func:`kill_bridge`.
    """
    env = os.environ.copy()
    if extra_env:
        env.update(extra_env)
    return subprocess.run(
        [
            sys.executable,
            "-m",
            "codeconv.cli",
            "--repo-root",
            str(repo_root),
            *args,
        ],
        capture_output=True,
        text=True,
        timeout=timeout,
        check=check,
        env=env,
    )


def kill_bridge(repo_root: Path) -> None:
    """Best-effort kill of the bridge process for ``repo_root``.

    Reads the PID from ``<repo_root>/.pgdb/bridge.json``. Safe to call
    even if no bridge is running.
    """
    sidecar = repo_root / ".pgdb" / "bridge.json"
    if not sidecar.is_file():
        return
    try:
        pid = int(_json.loads(sidecar.read_text(encoding="utf-8"))["pid"])
    except Exception:
        return
    try:
        if os.name == "nt":
            subprocess.run(
                ["taskkill", "/F", "/PID", str(pid)],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                check=False,
            )
        else:
            os.kill(pid, signal.SIGKILL)
    except Exception:
        pass
    # Wait briefly for sidecar cleanup; bridge unlinks on graceful exit
    # but on SIGKILL it lingers — cleanup happens via lock release.
    deadline = time.time() + 2.0
    while time.time() < deadline and sidecar.is_file():
        time.sleep(0.05)


def _link_prereq_patterns(repo_root: Path) -> None:
    """Make the canonical bridge script reachable inside ``repo_root``.

    The bridge_client looks for ``<repo_root>/prereq-patterns/pglite/
    pglite_bridge.mjs``; tests run with an isolated ``--repo-root`` need
    that path to resolve to the real source-of-truth bridge (which is
    where ``node_modules/`` lives, so ``proper-lockfile`` resolves).

    Strategy: directory junction on Windows (no admin / no dev mode
    needed); ``os.symlink`` everywhere else.
    """
    target_parent = repo_root / "prereq-patterns"
    if target_parent.exists() or target_parent.is_symlink():
        return
    real = REPO_ROOT / "prereq-patterns"
    if not real.is_dir():
        raise RuntimeError(
            f"prereq-patterns/ missing at {real}; cannot wire up isolated test repo"
        )
    if os.name == "nt":
        # Directory junction: works without admin / dev-mode.
        cp = subprocess.run(
            ["cmd", "/c", "mklink", "/J", str(target_parent), str(real)],
            capture_output=True,
            text=True,
        )
        if cp.returncode != 0:
            raise RuntimeError(
                f"mklink /J failed: stdout={cp.stdout!r} stderr={cp.stderr!r}"
            )
    else:
        os.symlink(real, target_parent, target_is_directory=True)


@pytest.fixture
def discover_repo(tmp_path: Path):
    """Isolated repo_root with auto-cleanup of the bridge daemon.

    Sets up a directory junction / symlink for ``prereq-patterns/`` so
    the bridge script and its ``node_modules/`` are reachable. Yields a
    Path. Tests build a synthetic ``glp_runtime_net/`` subtree inside it,
    then call :func:`run_codeconv` to migrate + discover.
    """
    if _node_available() and _bridge_script_present():
        _link_prereq_patterns(tmp_path)
    yield tmp_path
    kill_bridge(tmp_path)
