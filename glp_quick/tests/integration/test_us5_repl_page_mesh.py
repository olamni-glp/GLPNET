"""T035 [US5] — host-gated integration: a REPL page evaluates a goal over the real link, and an
agent-sent plain page is editable + returnable (FR-016/FR-017).

Gated on both the C# host and a built ``glp_repl`` (``out/csharp/glp_repl``). The REPL's exact
rendering is best-effort, so the assertion validates the **over-the-link plumbing** — the goal travels,
the bridge is invoked, and the rendered result round-trips onto the requesting REPL page.
"""

from __future__ import annotations

import socket

import pytest

from glp_quick import cert as cert_mod
from glp_quick.repl_link import GlpMessage, default_repl_command
from glp_quick.terminal import protocol
from glp_quick.terminal.replpage import ReplService
from glp_quick.terminal.state import TerminalState
from glp_quick.stacks.csharp import CSharpStackAdapter, host_dll_path

pytestmark = pytest.mark.skipif(
    not host_dll_path().exists() or default_repl_command() is None,
    reason="needs the C# host dll + a built glp_repl (out/csharp/glp_repl)",
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


def test_repl_over_link_and_agent_page_return(cert_dir):
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, cert_dir, max_clients=3, repl="csharp")
    c0 = ad.start_client("127.0.0.1", port, cert_dir, "csharp")   # requester (has the /repl page)
    c1 = ad.start_client("127.0.0.1", port, cert_dir, "csharp")   # REPL host
    svc = ReplService()
    try:
        for cid, h in (("c0", c0), ("c1", c1)):
            h.send(GlpMessage(sender=cid, to="server", payload=f"reg({cid})"))
        assert {server.recv(timeout=10).sender for _ in range(2)} == {"c0", "c1"}

        st0 = TerminalState("c0", "server", peers=["c1"])
        # c0's REPL page sends a goal over the link; c1 evaluates it via the real bridge and replies.
        c0.send(GlpMessage(sender="c0", to="c1", payload=protocol.repl_goal("R1", "true.")))
        gmsg = c1.recv(timeout=15)
        gtm = protocol.decode(gmsg.payload)
        assert gtm.kind == "repl_goal" and gtm.fields[0] == "R1"
        rendered = svc.evaluate(gtm.fields[1])
        assert isinstance(rendered, str) and rendered != ""
        c1.send(GlpMessage(sender="c1", to="c0", payload=protocol.repl_result(gtm.fields[0], rendered)))
        st0.deliver(c0.recv(timeout=15))
        ridx = st0.find_page_index("R1")
        assert ridx is not None and rendered in st0.pages[ridx].text   # result rendered on the page

        # --- FR-017: agent/server-sent plain page, editable + returnable ---
        c1.send(GlpMessage(sender="c1", to="c0", payload=protocol.page("TASK", "c1", "plain", "please fill")))
        st0.deliver(c0.recv(timeout=15))
        pidx = st0.find_page_index("TASK")
        assert st0.pages[pidx].owner == "c1"
        st0.pages[pidx].text += "\ndone by c0"                        # user edits it
        c0.send(GlpMessage(sender="c0", to="c1", payload=protocol.page("TASK", "c0", "plain", st0.pages[pidx].text)))
        back = protocol.decode(c1.recv(timeout=15).payload)
        assert back.kind == "page" and "done by c0" in back.fields[3]  # returned to the sender
    finally:
        svc.stop()
        for h in (c0, c1, server):
            ad.stop(h)
