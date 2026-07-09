"""049 US5 / FR-018 — bounded control-agent whitelist. Pure-logic (no host/dotnet), always runs.

The safety-critical property: the agent dispatches ONLY the fixed whitelist and NEVER executes
anything for an out-of-whitelist command — the network-reachable control channel can't become a
remote shell.
"""
from __future__ import annotations

from pathlib import Path

from glp_quick.control import ControlAgent, WHITELIST


def _agent() -> ControlAgent:
    return ControlAgent(cert_dir=Path("glpquick-cert"))


def test_whitelist_is_exactly_the_declared_bounded_set() -> None:
    assert set(WHITELIST) == {"ping", "status", "mesh_selftest", "echo"}


def test_ping_and_status_and_echo_are_typed_replies() -> None:
    a = _agent()
    assert a.handle("ping", {})["ok"] is True
    assert a.handle("status", {})["ok"] is True
    assert a.handle("echo", {"text": "xyz"})["result"]["text"] == "xyz"


def test_unknown_command_returns_unsupported_and_does_not_execute() -> None:
    a = _agent()
    for bad in ("exec", "shell", "run", "os.system", "eval", "cat", "", None, "rm -rf /"):
        r = a.handle(bad, {"text": "ignored"})
        assert r["ok"] is False and r["error"] == "unsupported", f"{bad!r} must be refused"


def test_no_arbitrary_payload_is_ever_run_as_code() -> None:
    # A malicious "cmd" carrying code-like text must be refused, not evaluated.
    a = _agent()
    r = a.handle("__import__('os').system('echo pwned')", {})
    assert r["ok"] is False and r["error"] == "unsupported"


def test_malformed_args_to_valid_command_returns_typed_error_not_crash() -> None:
    # FR-018: a whitelisted command with malformed args must return a typed error, never raise —
    # a raise would propagate out of run_agent's loop and tear down the long-running control agent.
    a = _agent()
    for bad in ("x", [1, 2], {"a": 1}, None):
        r = a.handle("mesh_selftest", {"clients": bad})
        assert r["ok"] is False and r["error"] == "bad_args", f"clients={bad!r} must be a typed error"
