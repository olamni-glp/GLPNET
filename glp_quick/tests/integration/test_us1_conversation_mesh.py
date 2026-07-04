"""T013 [US1] — host-gated integration: type-only conversation over the real QUIC+WS mesh.

Approximates the two-host acceptance on a loopback mesh (per spec Assumptions): the terminal's wire
behaviour — ``chat`` encoded/decoded through the one codec, ``@name`` directed delivery, and 037
bare-text backward compatibility — carried over genuine QUIC. Skipped when the C# host dll is not built
(matches ``tests/test_mesh.py``). The prompt_toolkit view itself is exercised by the host-free unit
tier + manual acceptance; here we drive the same L5/L6 seam the view uses.
"""

from __future__ import annotations

import socket

import pytest

from glp_quick import cert as cert_mod
from glp_quick.repl_link import BROADCAST, GlpMessage
from glp_quick.terminal.protocol import chat, decode
from glp_quick.terminal.state import compose_chat
from glp_quick.stacks.csharp import CSharpStackAdapter, host_dll_path

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


def test_chat_and_at_name_and_backcompat_over_real_link(cert_dir):
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, cert_dir, max_clients=3, repl="csharp")
    clients = {cid: ad.start_client("127.0.0.1", port, cert_dir, "csharp") for cid in ("c0", "c1", "c2")}
    try:
        # Register every client id at the server.
        for cid, h in clients.items():
            h.send(GlpMessage(sender=cid, to="server", payload="__connected__"))
            h.send(GlpMessage(sender=cid, to="server", payload=chat(f"hi from {cid}")))
        seen = {server.recv(timeout=10).sender for _ in range(3)}
        assert seen == {"c0", "c1", "c2"}

        # (1) @name directed delivery through the codec: c0 -> c1.
        out = compose_chat("c0", BROADCAST, "@c1 hello c1", peers=["c1", "c2"])
        assert out.message is not None and out.message.to == "c1"
        clients["c0"].send(out.message)
        m = clients["c1"].recv(timeout=10)
        assert m is not None and m.sender == "c0"
        assert decode(m.payload).kind == "chat" and decode(m.payload).fields == ("hello c1",)

        # (2) plain //+Enter transmit = broadcast chat, encoded+decoded through the one codec.
        bc = compose_chat("c0", BROADCAST, "hello mesh", peers=["c1", "c2"])
        clients["c0"].send(bc.message)
        assert decode(clients["c1"].recv(timeout=10).payload).fields == ("hello mesh",)
        assert decode(clients["c2"].recv(timeout=10).payload).fields == ("hello mesh",)

        # (3) 037 backward compatibility: a bare-text peer still interoperates (decodes as chat).
        clients["c0"].send(GlpMessage(sender="c0", to="c1", payload="legacy bare line"))
        lm = clients["c1"].recv(timeout=10)
        assert decode(lm.payload).kind == "chat" and decode(lm.payload).fields == ("legacy bare line",)
    finally:
        for h in clients.values():
            ad.stop(h)
        ad.stop(server)
