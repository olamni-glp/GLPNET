"""Pytest configuration for codeconv.

Provides shared fixtures and an opt-in performance-test runner flag
(``--run-perf``) per ``specs/012-codeconv-runner/tasks.md`` T068 / SC-013.
"""

from __future__ import annotations

import shutil
import socket
import sys
from pathlib import Path

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
    if config.getoption("--run-perf"):
        return
    skip_perf = pytest.mark.skip(reason="opt-in via --run-perf")
    for item in items:
        if "perf" in item.keywords:
            item.add_marker(skip_perf)


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
def isolated_repo(tmp_path: Path, repo_root: Path) -> Path:
    """A throwaway "repo root" with a fresh ``.pgdb/`` cluster.

    The bridge script lives in the real repo (where node_modules is
    installed); we point ``acquire_or_discover`` at it via the
    ``bridge_script`` keyword argument while letting the *data dir* live
    under ``tmp_path`` so each test is fully isolated.

    Tests should pass ``bridge_script=repo_root/'prereq-patterns'/'pglite'/'pglite_bridge.mjs'``
    when invoking ``acquire_or_discover``.
    """
    return tmp_path


def _free_port() -> int:
    """Reserve an ephemeral port (released immediately; race-prone but OK
    for the few tests that need it). Tests that DO NOT pass --port get the
    bridge's own ephemeral allocation, which is preferable."""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]
