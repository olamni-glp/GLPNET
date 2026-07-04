"""T020 [US2] — host-gated integration: transmit a page → peer receives it as an owned page.

Over the real QUIC+WS mesh: a ``tmsg(page,…)`` transmitted from one endpoint arrives at the peer,
decodes as a ``page``, and lands as a page owned by the sending peer (not merged into shared chat); the
``/pages`` listing shows owner-by-name on both ends. Skipped when the C# host dll is not built.
"""

from __future__ import annotations

import socket

import pytest

from glp_quick import cert as cert_mod
from glp_quick.repl_link import GlpMessage
from glp_quick.terminal import pages as pagelib
from glp_quick.terminal.pages import Page, receive_page
from glp_quick.terminal.protocol import decode, page as encode_page
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


def test_page_transmit_lands_as_owned_page_over_real_link(cert_dir):
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, cert_dir, max_clients=3, repl="csharp")
    c0 = ad.start_client("127.0.0.1", port, cert_dir, "csharp")
    c1 = ad.start_client("127.0.0.1", port, cert_dir, "csharp")
    try:
        for cid, h in (("c0", c0), ("c1", c1)):
            h.send(GlpMessage(sender=cid, to="server", payload="__connected__"))
            h.send(GlpMessage(sender=cid, to="server", payload="hi"))  # register ids
        assert {server.recv(timeout=10).sender for _ in range(2)} == {"c0", "c1"}

        # c0 transmits a whole page (directed) to c1.
        c0.send(GlpMessage(sender="c0", to="c1", payload=encode_page("PLAN", "c0", "plain", "line1\nline2")))
        m = c1.recv(timeout=10)
        assert m is not None and m.sender == "c0"
        tm = decode(m.payload)
        assert tm.kind == "page" and tm.fields[0] == "PLAN" and tm.fields[3] == "line1\nline2"

        # c1 lands it as a page owned by c0 (not merged into a shared chat page).
        c1_pages = [Page("CHAT", owner="shared")]
        idx, is_new = receive_page(c1_pages, m.sender, tm.fields[0], str(tm.fields[2]), tm.fields[3])
        assert is_new and c1_pages[idx].owner == "c0" and c1_pages[idx].text == "line1\nline2"

        # /pages listing shows owner-by-name on the receiving end.
        listing = pagelib.list_text(c1_pages, current=0)
        assert "PLAN" in listing and "owner=c0" in listing
    finally:
        for h in (c0, c1, server):
            ad.stop(h)
