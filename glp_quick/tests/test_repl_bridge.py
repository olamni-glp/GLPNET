"""063 US1 T013 — end-to-end live REPL bridge over the real QUIC+WS link (contract C1, SC-001).

Two real ``glp_quick_host`` processes with ``--repl`` (the host spawns the C# ``glp_repl`` as a
child): a ``tmsg(repl_goal, ...)`` sent from one end is evaluated by the OTHER end's REPL child and
the ``tmsg(repl_result, ...)`` returns to the sender — the 036 "run GLP over the link" promise, live
in both directions. The SC-001 envelope binds link-up + first result to the 5-minute wall bound
(scripted runs land in seconds). Skipped when the C# host dll or ``out/csharp/glp_repl`` is absent.
"""

from __future__ import annotations

import socket
import time
from pathlib import Path

import pytest

from glp_quick import cert as cert_mod
from glp_quick.repl_link import GlpMessage
from glp_quick.stacks.csharp import CSharpStackAdapter, host_dll_path
from glp_quick.terminal import protocol


def _glp_repl_dll() -> Path | None:
    base = Path(__file__).resolve().parents[2] / "out" / "csharp" / "glp_repl" / "bin"
    for config in ("Release", "Debug"):
        dll = base / config / "net10.0" / "glp_repl.dll"
        if dll.exists():
            return dll
    return None


pytestmark = pytest.mark.skipif(
    not host_dll_path().exists() or _glp_repl_dll() is None,
    reason="needs the C# host dll + a built glp_repl (out/csharp/glp_repl)",
)

# SC-001: link-up + first result within 5 minutes wall-clock (scripted runs land in seconds).
SC_001_BOUND_S = 300.0


def _free_udp_port() -> int:
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    s.close()
    return port


@pytest.fixture
def cert_dir(tmp_path):
    cert_mod.generate_shared_cert(tmp_path, days=2)
    return tmp_path


def _await_repl_result(handle, sender: str, page: str, deadline_s: float):
    """Consume envelopes until the repl_result for ``page`` arrives from ``sender``."""
    deadline = time.monotonic() + deadline_s
    while time.monotonic() < deadline:
        msg = handle.recv(timeout=5)
        if msg is None:
            continue
        tm = protocol.decode(msg.payload)
        if msg.sender == sender and tm.kind == "repl_result" and tm.fields and tm.fields[0] == page:
            return tm
    return None


def test_repl_bridge_both_directions_sc001(cert_dir):
    """--repl both ends: each side's goal is evaluated by the OTHER side's REPL child."""
    started = time.monotonic()
    repl = str(_glp_repl_dll())
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, cert_dir, 3, "csharp",
                             repl_path=repl, self_id="server")
    client = ad.start_client("127.0.0.1", port, cert_dir, "csharp",
                             repl_path=repl, self_id="tester")
    try:
        # Announce the client id so the mesh can route to it (the console does this on start).
        client.send(GlpMessage(sender="tester", to="server", payload="__connected__"))

        # Direction 1: the client operator's goal runs on the SERVER's REPL child.
        client.send(GlpMessage(sender="tester", to="server",
                               payload=protocol.repl_goal("P1", "true.")))
        res1 = _await_repl_result(client, "server", "P1", 60)
        assert res1 is not None, "no repl_result returned from the server-side REPL child"
        assert isinstance(res1.fields[1], str) and res1.fields[1] != ""
        elapsed_first = time.monotonic() - started
        assert elapsed_first < SC_001_BOUND_S, f"SC-001 violated: first result after {elapsed_first:.0f}s"

        # Direction 2: the server operator's goal runs on the CLIENT's REPL child.
        server.send(GlpMessage(sender="server", to="tester",
                               payload=protocol.repl_goal("P2", "true.")))
        res2 = _await_repl_result(server, "tester", "P2", 60)
        assert res2 is not None, "no repl_result returned from the client-side REPL child"
        assert isinstance(res2.fields[1], str) and res2.fields[1] != ""

        # Results are never re-fed as goals (repl_result ≠ repl_goal): both links stay healthy.
        assert ad.health(server).alive and ad.health(client).alive
    finally:
        ad.stop(client)
        ad.stop(server)


def test_repl_bridge_chat_is_not_a_goal(cert_dir):
    """A directed chat payload must NOT feed the REPL — only tmsg(repl_goal, ...) does (C1/R2)."""
    repl = str(_glp_repl_dll())
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, cert_dir, 3, "csharp",
                             repl_path=repl, self_id="server")
    client = ad.start_client("127.0.0.1", port, cert_dir, "csharp", self_id="tester")
    try:
        client.send(GlpMessage(sender="tester", to="server", payload="__connected__"))
        client.send(GlpMessage(sender="tester", to="server", payload=protocol.chat("hello there")))
        # A goal AFTER the chat still gets exactly its own result; the chat produced none.
        client.send(GlpMessage(sender="tester", to="server",
                               payload=protocol.repl_goal("P1", "true.")))
        res = _await_repl_result(client, "server", "P1", 60)
        assert res is not None
        # No stray repl_result for the chat line arrives afterwards.
        stray = client.recv(timeout=3)
        if stray is not None:
            tm = protocol.decode(stray.payload)
            assert tm.kind != "repl_result", f"chat payload was fed to the REPL: {stray.payload!r}"
    finally:
        ad.stop(client)
        ad.stop(server)
