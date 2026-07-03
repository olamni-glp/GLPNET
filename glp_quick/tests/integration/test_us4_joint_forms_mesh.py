"""T030 [US4] — host-gated integration: joint pinpoint + mask/form over the real QUIC+WS mesh.

A counterpart pinpoint applied on the owner's joint page (original recoverable), and a mask defined on
one side, filled on the other, returned with fixed labels intact — all carried through the one codec and
the real receive path (``TerminalState.deliver``). Skipped when the C# host dll is not built.
"""

from __future__ import annotations

import socket

import pytest

from glp_quick import cert as cert_mod
from glp_quick.repl_link import GlpMessage
from glp_quick.terminal import forms, protocol
from glp_quick.terminal.state import TerminalState
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


def test_pinpoint_and_form_round_trip_over_real_link(cert_dir):
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, cert_dir, max_clients=3, repl="csharp")
    c0 = ad.start_client("127.0.0.1", port, cert_dir, "csharp")
    c1 = ad.start_client("127.0.0.1", port, cert_dir, "csharp")
    try:
        for cid, h in (("c0", c0), ("c1", c1)):
            h.send(GlpMessage(sender=cid, to="server", payload=f"reg({cid})"))
        assert {server.recv(timeout=10).sender for _ in range(2)} == {"c0", "c1"}

        # --- joint pinpoint: c0 → c1's joint page, applied through c1's receive path ---
        st1 = TerminalState("c1", "server", peers=["c0"])
        di = st1.add_page("DOC", owner="me", text="alpha\nbravo")
        st1.pages[di].joint = True
        c0.send(GlpMessage(sender="c0", to="c1", payload=protocol.pinpoint("DOC", 0, 0, 1, 5, "HELLO", "transient")))
        st1.deliver(c1.recv(timeout=10))
        doc = st1.pages[st1.find_page_index("DOC")]
        assert doc.text.split("\n")[0] == "HELLO"                       # region overwritten
        assert doc.saved_regions[(0, 0, 1, 5)]["original"] == "alpha"   # original recoverable

        # --- mask/form: c0 defines, c1 fills, c0 receives it back with labels intact ---
        labels, fields = [(0, 0, "Name:")], [(0, 6, 10)]
        mask0 = forms.from_wire(
            [protocol.Term("label", (0, 0, "Name:"))], [protocol.Term("field", (0, 6, 10))]
        )
        st0 = TerminalState("c0", "server", peers=["c1"])
        st0.masks["F"] = mask0
        st0.add_page("F", owner="me", kind="mask", text=forms.render(mask0))

        c0.send(GlpMessage(sender="c0", to="c1", payload=protocol.form_def("F", labels, fields)))
        st1.deliver(c1.recv(timeout=10))
        assert "F" in st1.masks and "Name:" in st1.pages[st1.find_page_index("F")].text

        c1.send(GlpMessage(sender="c1", to="c0", payload=protocol.form_fill("F", [(0, "Zoe")])))
        st0.deliver(c0.recv(timeout=10))
        completed = st0.pages[st0.find_page_index("F")].text
        assert "Name:" in completed and "Zoe" in completed  # labels intact + value returned
    finally:
        for h in (c0, c1, server):
            ad.stop(h)
