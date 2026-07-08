"""Regression tests for the 036 code-review fixes (2026-07-02; #5/#6 added by feature 049 FR-015).

Host-free checks — no C# host / dotnet needed, so they always run in the unit suite.
"""

from __future__ import annotations

import sys
import threading
from pathlib import Path

from glp_quick import demo as demo_mod
from glp_quick.cli import _validate_profile
from glp_quick.demo import run_demo
from glp_quick.stacks.base import StackAdapter
from glp_quick.stacks.csharp import _EXIT_TOKEN, spawn_handle, terminate_tree

from tests._fakes import FakeHandle


def test_gleam_default_profile_is_a_not_unbuilt_c() -> None:
    # Review finding #2: bare `--stack gleam` (no --profile) must default to the built,
    # documented Profile A — not Profile C, which is opt-in and may be build-blocked.
    assert _validate_profile("gleam", None) == "a"
    assert _validate_profile("gleam", "c") == "c"  # explicit C still honoured
    assert _validate_profile("csharp", None) is None


def test_exit_code_6_maps_to_quic_unsupported() -> None:
    # Review finding #4: host ExitQuicUnsupported == 6 emits `ERR quic_unsupported ...`;
    # the exit-code→token map must agree (was mislabelled alpn_version_mismatch).
    assert _EXIT_TOKEN[6] == "quic_unsupported"


class _DeafHandle(FakeHandle):
    """A FakeHandle whose empty ``recv`` returns immediately — the timeout elapses conceptually,
    without making the unit suite sit through the demo's real 10 s waits."""

    def recv(self, timeout=None):
        return super().recv(timeout=0.01)


class _DeafAdapter(StackAdapter):
    """Every handle comes up but nothing is ever received — the handshake-timeout shape."""

    def name(self):
        return "csharp"

    def profile(self):
        return None

    def capabilities(self):
        return {"real_quic": False, "quic_termination": "in_process"}

    def start_server(self, bind, port, cert, max_clients, repl):
        return _DeafHandle(link_id=f"server@{bind}:{port}")

    def start_client(self, server_addr, port, cert, repl):
        return _DeafHandle(link_id=f"client->{server_addr}:{port}")

    def health(self, handle):
        raise NotImplementedError

    def stop(self, handle):
        pass


def test_demo_handshake_timeout_records_sc001_fail_not_attributeerror(monkeypatch) -> None:
    # Review finding #5: a `None` recv on handshake timeout must be recorded as an SC-001 FAIL
    # verdict — the pre-fix code raised AttributeError on `None.sender` and crashed the demo.
    monkeypatch.setattr(demo_mod, "_adapter", lambda stack, profile="a": _DeafAdapter())
    report = run_demo("127.0.0.1", 1, Path("."), stack="csharp", clients=3)  # no exception
    assert report.results["SC-001 real on-wire QUIC/HTTP-3 handshake (not loopback-sim)"] == "FAIL"
    assert not report.ok


def test_spawn_handle_drains_stdout_before_readiness_wait() -> None:
    # Review finding #6: the endpoint may flood stdout BEFORE signalling READY on stderr; without
    # a reader attached at spawn the pipe fills (~64 KB) and the child blocks mid-write, never
    # emitting READY — the pre-fix spawn_handle then hung forever on stderr.readline().
    child = (
        "import sys, time\n"
        "sys.stdout.write('{' + 'x' * 300000 + '}\\n')\n"  # >> any OS pipe buffer
        "sys.stdout.flush()\n"
        "sys.stderr.write('READY biglink\\n')\n"
        "sys.stderr.flush()\n"
        "time.sleep(30)\n"
    )
    result: dict = {}

    def spawn() -> None:
        result["handle"] = spawn_handle([sys.executable, "-c", child], "READY", peer_ids=[])

    t = threading.Thread(target=spawn, daemon=True)
    t.start()
    t.join(timeout=25)
    assert not t.is_alive(), "spawn_handle deadlocked on a pre-readiness stdout flood (finding #6)"
    h = result["handle"]
    try:
        assert h.link_id == "biglink"
        line = h._rx.get(timeout=10)  # the flooded line was drained, intact, in order
        assert line is not None and len(line) == 300002
    finally:
        terminate_tree(h._proc)
