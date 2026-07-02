"""US2 — multi-accept mesh server: ≥3 concurrent isolated clients, to/broadcast routing, isolation,
over-capacity (T025/T026/T027/T028, SC-003/SC-004).

A genuine real-QUIC server accepts several client links over one UDP port and routes L5 envelopes
among the clients (and its own endpoint). Skipped when the C# host dll is not built.
"""

from __future__ import annotations

import socket

import pytest

from glp_quick import cert as cert_mod
from glp_quick.repl_link import GlpMessage, BROADCAST
from glp_quick.stacks.csharp import CSharpStackAdapter, LinkError, host_dll_path

pytestmark = pytest.mark.skipif(
    not host_dll_path().exists(),
    reason="glp_quick_host.dll not built (run: dotnet build csharp/glp_quick_host)",
)


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


def test_three_client_mesh_routing_and_isolation(cert_dir):
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, cert_dir, max_clients=3, repl="csharp")
    clients = {cid: ad.start_client("127.0.0.1", port, cert_dir, "csharp") for cid in ("c0", "c1", "c2")}
    try:
        # Each client announces itself (registers its endpoint_id at the server).
        for cid, h in clients.items():
            h.send(GlpMessage(sender=cid, to="server", payload=f"hello({cid})"))
        seen = {server.recv(timeout=10).sender for _ in range(3)}  # SC-003: 3 concurrent links
        assert seen == {"c0", "c1", "c2"}

        # T027 to-routing: c0 → c1 (peer-to-peer through the mesh)
        clients["c0"].send(GlpMessage(sender="c0", to="c1", payload="direct(hi)"))
        m = clients["c1"].recv(timeout=10)
        assert m is not None and m.sender == "c0" and m.payload == "direct(hi)"

        # T027 broadcast: c0 → all others (+ the server's own endpoint), NOT back to the sender
        clients["c0"].send(GlpMessage(sender="c0", to=BROADCAST, payload="bcast(yo)"))
        assert clients["c1"].recv(timeout=10).payload == "bcast(yo)"
        assert clients["c2"].recv(timeout=10).payload == "bcast(yo)"
        assert server.recv(timeout=10).payload == "bcast(yo)"

        # T028 isolation: drop c2 — c0 ↔ c1 keeps working (siblings unaffected, SC-004)
        ad.stop(clients["c2"])
        clients["c0"].send(GlpMessage(sender="c0", to="c1", payload="after(drop)"))
        m = clients["c1"].recv(timeout=10)
        assert m is not None and m.payload == "after(drop)"
    finally:
        for h in clients.values():
            ad.stop(h)
        ad.stop(server)


def test_over_capacity_rejected(cert_dir):
    """T026: the (N+1)th client is rejected with a clear over_capacity signal."""
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, cert_dir, max_clients=2, repl="csharp")
    a = ad.start_client("127.0.0.1", port, cert_dir, "csharp")
    b = ad.start_client("127.0.0.1", port, cert_dir, "csharp")
    # fill capacity
    a.send(GlpMessage(sender="a", to="server", payload="hi"))
    b.send(GlpMessage(sender="b", to="server", payload="hi"))
    assert {server.recv(timeout=10).sender for _ in range(2)} == {"a", "b"}
    over = ad.start_client("127.0.0.1", port, cert_dir, "csharp")  # the 3rd — over capacity
    try:
        msg = over.recv(timeout=10)  # server sends over_capacity then closes the link
        assert msg is not None and msg.payload == "over_capacity"
    finally:
        ad.stop(over)
        ad.stop(a)
        ad.stop(b)
        ad.stop(server)
