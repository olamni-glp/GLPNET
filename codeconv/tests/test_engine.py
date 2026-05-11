"""Tests for ``codeconv.db.engine`` — Phase 5 / US3 / T052.

- ``test_engine_kwargs_applied`` — pool_size=1, prepare_threshold=None,
  application_name='codeconv' on a SQLAlchemy engine built via build_engine.
- ``test_apply_to_engine_installed`` — read a timestamptz column from a
  DBOS-managed table without crashing psycopg (requires bridge + DBOS).
- ``test_dbos_compat_patch_applied_before_launch`` — assertion-style:
  monkey-patch ``_apply_pglite_compat_patch`` and confirm it's called
  before ``dbos.launch()``.
"""

from __future__ import annotations

import sys
from pathlib import Path
from types import ModuleType, SimpleNamespace
from unittest.mock import patch

import pytest

from codeconv._vendor.pglite_engine_kwargs import pglite_engine_kwargs
from codeconv.bridge_client import BridgeEndpoint
from codeconv.db import engine as engine_mod

from .conftest import needs_bridge


# ---------------------------------------------------------------------------
# Pure-unit: kwargs shape (no bridge required)
# ---------------------------------------------------------------------------


def test_engine_kwargs_applied() -> None:
    kwargs = pglite_engine_kwargs(application_name="codeconv")
    assert kwargs["pool_size"] == 1
    assert kwargs["max_overflow"] == 0
    assert kwargs["pool_pre_ping"] is False
    assert kwargs["connect_args"]["prepare_threshold"] is None
    assert kwargs["connect_args"]["application_name"] == "codeconv"


def test_build_engine_uses_pglite_kwargs(monkeypatch: pytest.MonkeyPatch) -> None:
    """``build_engine`` calls ``create_engine`` with pglite_engine_kwargs and
    runs ``apply_to_engine`` afterwards. No real connection is opened."""
    captured: dict[str, object] = {}

    def fake_create_engine(url: str, **kwargs: object) -> object:
        captured["url"] = url
        captured["kwargs"] = kwargs
        return SimpleNamespace(_is_fake=True)

    apply_calls: list[object] = []

    def fake_apply_to_engine(eng: object) -> None:
        apply_calls.append(eng)

    monkeypatch.setattr(engine_mod, "create_engine", fake_create_engine)
    monkeypatch.setattr(engine_mod, "apply_to_engine", fake_apply_to_engine)

    endpoint = BridgeEndpoint(host="127.0.0.1", port=12345, pid=1, owned=False)
    eng = engine_mod.build_engine(endpoint, application_name="codeconv")

    assert eng is not None
    assert "127.0.0.1:12345" in captured["url"]  # type: ignore[arg-type]
    kwargs = captured["kwargs"]  # type: ignore[assignment]
    assert kwargs["pool_size"] == 1  # type: ignore[index]
    assert kwargs["connect_args"]["application_name"] == "codeconv"  # type: ignore[index]
    assert apply_calls == [eng]  # apply_to_engine MUST follow create_engine


# ---------------------------------------------------------------------------
# DBOS sequencing: patch BEFORE launch
# ---------------------------------------------------------------------------


def test_dbos_compat_patch_applied_before_launch(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Strict order: ``_apply_pglite_compat_patch()`` MUST be called
    before ``DBOS(...).launch()``. We monkey-patch both and record the
    invocation sequence."""
    sequence: list[str] = []

    def fake_patch() -> None:
        sequence.append("patch")

    class FakeDBOS:
        def __init__(self, config: object) -> None:
            sequence.append("construct")
            self.app_db = SimpleNamespace(engine=None)

        def launch(self) -> None:
            sequence.append("launch")

    class FakeDBOSConfig:
        def __init__(self, **kwargs: object) -> None:
            self.kwargs = kwargs

    fake_dbos_module = ModuleType("dbos")
    fake_dbos_module.DBOS = FakeDBOS  # type: ignore[attr-defined]
    fake_dbos_module.DBOSConfig = FakeDBOSConfig  # type: ignore[attr-defined]

    monkeypatch.setitem(sys.modules, "dbos", fake_dbos_module)
    monkeypatch.setattr(engine_mod, "_apply_pglite_compat_patch", fake_patch)
    # Don't actually patch SQLAlchemy events on the fake engine.
    monkeypatch.setattr(engine_mod, "apply_to_engine", lambda _e: None)

    endpoint = BridgeEndpoint(host="127.0.0.1", port=12345, pid=1, owned=False)
    dbos_instance = engine_mod.setup_dbos(endpoint)

    assert isinstance(dbos_instance, FakeDBOS)
    # Patch must come strictly before launch in the recorded sequence.
    patch_idx = sequence.index("patch")
    launch_idx = sequence.index("launch")
    assert patch_idx < launch_idx, f"patch must precede launch; got {sequence}"


# ---------------------------------------------------------------------------
# End-to-end with real bridge + DBOS (skipped if either absent)
# ---------------------------------------------------------------------------


@needs_bridge
def test_apply_to_engine_installed(isolated_repo: Path, bridge_script: Path) -> None:
    """End-to-end: build an engine, run a SELECT that returns a
    timestamptz value, confirm psycopg does not crash.

    Uses a self-contained query against ``information_schema.tables`` —
    every PG installation has it; no need to migrate first.
    """
    pytest.importorskip("psycopg")
    from sqlalchemy import text

    from codeconv.bridge_client import acquire_or_discover

    endpoint = acquire_or_discover(isolated_repo, ready_timeout=30.0, bridge_script=bridge_script)
    eng = engine_mod.build_engine(endpoint)
    try:
        with eng.connect() as conn:
            row = conn.execute(text("SELECT NOW()")).one()
            assert row[0] is not None
    finally:
        # Best-effort kill of bridge.
        import json as _json
        import os
        import signal
        import subprocess

        sidecar = isolated_repo / ".pgdb" / "bridge.json"
        if sidecar.is_file():
            try:
                pid = int(_json.loads(sidecar.read_text())["pid"])
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
