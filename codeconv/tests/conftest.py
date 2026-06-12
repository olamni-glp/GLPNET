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


# Runtime REPLs (feature 020 @needs_runtime). The golden is the Dart REPL;
# the candidate is the converted, trace-instrumented C# REPL under out/csharp.
DART_REPL_EXE = REPO_ROOT / "glp_runtime" / "glp_repl.exe"
DART_REPL_DART = REPO_ROOT / "glp_runtime" / "bin" / "glp_repl.dart"
CSHARP_OUT = REPO_ROOT / "out" / "csharp"


def _dart_repl_available() -> bool:
    """Golden runtime: the prebuilt Windows REPL, or ``dart`` + the source."""
    if DART_REPL_EXE.is_file():
        return True
    return shutil.which("dart") is not None and DART_REPL_DART.is_file()


def _csharp_repl_available() -> bool:
    """Candidate runtime: a built/instrumented converted C# REPL.

    Honors an explicit override (``CODECONV_CSHARP_REPL`` → built
    entrypoint); otherwise looks for a built ``glp_repl`` artifact under
    ``out/csharp/``. Requires ``dotnet`` on PATH. Until US1/US2 builds the
    converted REPL this returns False, so ``@needs_runtime`` e2e tests skip
    (the B1 bootstrapping window — pure oracle modules are tested without it).
    """
    if shutil.which("dotnet") is None:
        return False
    override = os.environ.get("CODECONV_CSHARP_REPL")
    if override:
        return Path(override).exists()
    if not CSHARP_OUT.is_dir():
        return False
    return any(CSHARP_OUT.glob("**/glp_repl*.dll")) or any(
        CSHARP_OUT.glob("**/glp_repl*.exe")
    )


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


needs_runtime = pytest.mark.skipif(
    not (_dart_repl_available() and _csharp_repl_available()),
    reason=(
        "requires the Dart golden REPL AND a built converted C# REPL "
        "(set CODECONV_CSHARP_REPL or build out/csharp)"
    ),
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


def stage_convspec_artifacts(
    repo_root: Path, source_subtree: Optional[Path] = None
) -> list[str]:
    """Write a minimal valid spec-only convspec artifact for every
    ``.dart`` file in the inventory subtree, so the deterministic
    convspec step finds it and returns ``specced`` (→ plan runs → the
    unit completes).

    Tests whose intent is resume / idempotence / stage-order /
    throughput need the pipeline to reach ``completed``; per US2
    Acceptance 1 the only spec-faithful route there is a *persisted*
    per-file spec. Without a staged artifact a correct ``builder run``
    reports ``needs_agent_work`` (the convspec short-circuit) — which is
    exactly the behaviour the feature-018 ``outer_builder_workflow``
    propagation defect used to hide. Returns the rel paths staged.
    """
    sub = source_subtree or (repo_root / "glp_runtime_net")
    staged: list[str] = []
    for src in sorted(sub.rglob("*.dart")):
        rel = src.relative_to(sub).as_posix()
        art = repo_root / ".codeconv" / "conversion-specs" / (rel + ".md")
        art.parent.mkdir(parents=True, exist_ok=True)
        art.write_text(
            "# spec\n\n```yaml\nschema_version: 1\n"
            f"source_path: {rel}\n"
            "constructs:\n  - construct_key: trivial\n    trivial: true\n"
            "conversion_units: []\nescalations: []\n```\n\n"
            "Rationale: trivial.\n",
            encoding="utf-8",
        )
        staged.append(rel)
    return staged


# ---------------------------------------------------------------------------
# Marathon stage-harness fixtures (feature 030 — per-run isolated store)
# ---------------------------------------------------------------------------


def kill_marathon_bridge(store_root: Path) -> None:
    """Best-effort kill of the per-run marathon bridge.

    The keeper's per-run PGLite cluster lives at ``<store_root>/pgdb`` (T007),
    so its sidecar is ``<store_root>/pgdb/bridge.json``. Mirrors
    :func:`kill_bridge` for the off-repo per-run data-dir. Safe to call even if
    no bridge is running.
    """
    sidecar = store_root / "pgdb" / "bridge.json"
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
    deadline = time.time() + 2.0
    while time.time() < deadline and sidecar.is_file():
        time.sleep(0.05)


@pytest.fixture
def marathon_store(tmp_path: Path) -> Iterable[Path]:
    """An isolated per-run marathon store root on the NTFS tmp path, with
    per-run bridge teardown (the proven ``discover_repo`` pattern; FR-027/US3).

    Yields the off-repo ``store_root``. The per-run PGLite cluster lives at
    ``<store_root>/pgdb`` and the JSON mirror at ``<store_root>/json`` (T007/T009).
    ``prereq-patterns/`` is junction-linked into ``store_root`` so the per-run
    bridge (spawned by the keeper with ``repo_root=store_root``) finds the bridge
    script and its ``node_modules``. The spawned bridge is killed at test end so
    each marathon test is fully isolated (no leaked WASM/lock — feature-018).

    Marathon subcommands address this store with ``--data-dir <store_root>``
    (see :func:`marathon_run`); the cluster data-dir ``<store_root>/pgdb`` is
    derived by the keeper.
    """
    store_root = tmp_path / "marathon_run_store"
    store_root.mkdir(parents=True, exist_ok=True)
    if _node_available() and _bridge_script_present():
        _link_prereq_patterns(store_root)
    yield store_root
    kill_marathon_bridge(store_root)


def marathon_run(
    store_root: Path,
    *args: str,
    timeout: float = 180.0,
    check: bool = False,
    extra_env: Optional[dict[str, str]] = None,
) -> subprocess.CompletedProcess[str]:
    """Invoke ``python -m codeconv.cli --data-dir <store_root> marathon …``
    against the per-run isolated store in a fresh subprocess.

    Mirrors :func:`run_codeconv`; each call is a fresh process (fresh in-memory
    DBOS state). The per-run bridge spawned by the first call persists across
    subsequent calls until :func:`kill_marathon_bridge` (the ``marathon_store``
    fixture's teardown).
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
            str(store_root),
            "--data-dir",
            str(store_root),
            "marathon",
            *args,
        ],
        capture_output=True,
        text=True,
        timeout=timeout,
        check=check,
        env=env,
    )
