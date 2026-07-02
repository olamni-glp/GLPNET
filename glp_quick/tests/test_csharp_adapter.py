"""Integration: the C# stack adapter drives a genuine two-process QUIC+WS link (T018/T019/T020/T021).

Two real ``glp_quick_host`` processes complete an actual ``System.Net.Quic`` handshake over loopback
(real UDP/TLS 1.3/ALPN h3) carrying an RFC 6455 WebSocket link, and exchange L5 GLP-message envelopes
full-duplex. Skipped when the host dll is not built or ``dotnet`` is unavailable (so the unit suite
stays green on a machine without the C# build).
"""

from __future__ import annotations

import socket

import pytest

from glp_quick import cert as cert_mod
from glp_quick.repl_link import GlpMessage
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


def test_full_duplex_roundtrip(cert_dir):
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    assert ad.capabilities() == {"real_quic": True, "quic_termination": "in_process"}
    server = ad.start_server("127.0.0.1", port, cert_dir, 3, "csharp")
    client = ad.start_client("127.0.0.1", port, cert_dir, "csharp")
    try:
        client.send(GlpMessage(sender="client", to="server", payload="ping(alice)"))
        got = server.recv(timeout=10)
        assert got is not None and got.payload == "ping(alice)" and got.sender == "client"

        server.send(GlpMessage(sender="server", to="client", payload="pong(bob)"))
        got2 = client.recv(timeout=10)
        assert got2 is not None and got2.payload == "pong(bob)"

        for i in range(5):
            client.send(GlpMessage(sender="client", to="server", payload=f"n({i})"))
        for i in range(5):
            m = server.recv(timeout=10)
            assert m is not None and m.payload == f"n({i})"  # FIFO

        assert ad.health(server).alive and ad.health(client).alive
    finally:
        ad.stop(client)
        ad.stop(server)


def test_cert_mismatch_is_rejected(cert_dir, tmp_path):
    """A client trusting a DIFFERENT cert must be rejected — the cert_mismatch path (FR-019/SC-005)."""
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, cert_dir, 3, "csharp")
    # A second, different shared cert in another dir → its pin won't match the server's.
    other = tmp_path / "other"
    cert_mod.generate_shared_cert(other, days=2)
    try:
        with pytest.raises(LinkError) as ei:
            ad.start_client("127.0.0.1", port, other, "csharp")
        assert ei.value.token in ("cert_mismatch", "server_not_ready")  # handshake refused, no half-open link
    finally:
        ad.stop(server)
