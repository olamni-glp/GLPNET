"""T050 [US6] — host-gated `/rcopy` end-to-end over the real QUIC+WS mesh (SC-007/SC-009/SC-010).

The client's :func:`run_transfer` drives a real responder over the link via :class:`LinkProxy`; the
responder side runs a :class:`ResponderSession` serve loop on its own handle. Asserts a mixed set
(transferred / filtered / quota-rejected), synchronise skipping byte-identical, force overwriting, and
the catalog being fully rebuilt from the WAL after loss. Skipped when the C# host dll is not built.
"""

from __future__ import annotations

import socket
import threading

import pytest

from glp_quick import cert as cert_mod
from glp_quick.rcopy.filter import ExclusionFilter
from glp_quick.rcopy.responder import Responder
from glp_quick.rcopy.wizard import (
    FileSpec, LinkProxy, ResponderSession, TransferRequest, disk_reader, gather_files, run_transfer,
)
from glp_quick.repl_link import GlpMessage
from glp_quick.terminal.protocol import decode
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


def test_rcopy_end_to_end_over_real_link(cert_dir, tmp_path):
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, cert_dir, max_clients=3, repl="csharp")
    c0 = ad.start_client("127.0.0.1", port, cert_dir, "csharp")   # client (sends files)
    c1 = ad.start_client("127.0.0.1", port, cert_dir, "csharp")   # responder host
    for cid, h in (("c0", c0), ("c1", c1)):
        h.send(GlpMessage(sender=cid, to="server", payload="__connected__"))
        h.send(GlpMessage(sender=cid, to="server", payload="reg"))
    assert {server.recv(timeout=10).sender for _ in range(2)} == {"c0", "c1"}

    # responder on c1: permit c0, small quota so a big file is quota-rejected.
    responder = Responder.init(tmp_path / "resp", [
        {"name": "docs", "path": str(tmp_path / "share"),
         "permitted_peers": ["c0"], "quota": {"kind": "bytes", "limit": 50}}])
    session = ResponderSession(responder, "c1")
    stop = threading.Event()

    def serve():
        while not stop.is_set():
            msg = c1.recv(timeout=0.4)
            if msg is None:
                continue
            tm = decode(msg.payload)
            if tm.kind == "rcopy_offer_query":
                c1.send(GlpMessage(sender="c1", to=msg.sender, payload=session.offer_payload(msg.sender)))
            elif tm.kind == "rcopy_manifest":
                c1.send(GlpMessage(sender="c1", to=msg.sender,
                                   payload=session.manifest_verdict_payload(msg.sender, tm)))
            elif tm.kind == "rcopy_chunk":
                out = session.chunk_outcome_payload(msg.sender, tm)
                if out is not None:
                    c1.send(GlpMessage(sender="c1", to=msg.sender, payload=out))

    threading.Thread(target=serve, daemon=True).start()

    def send(to, payload):
        c0.send(GlpMessage(sender="c0", to=to, payload=payload))

    def recv(timeout):
        m = c0.recv(timeout=timeout)
        return m.payload if m is not None else None

    proxy = LinkProxy(send=send, recv=recv)
    src = tmp_path / "src"
    src.mkdir()
    (src / "new.txt").write_text("hi")            # 2 bytes → transferred
    (src / "skip.tmp").write_text("nope")         # filtered by name
    (src / "big.txt").write_text("x" * 100)       # 100 bytes → quota-rejected (limit 50)

    def _req(mode):
        return TransferRequest("docs", "f",
                               [FileSpec(gather_files(src), ExclusionFilter(name_globs=("*.tmp",)))], mode)

    try:
        res = run_transfer("c1", _req("synchronise"), proxy, disk_reader(src))
        om = {o.rel: (o.outcome, o.reason) for o in res.outcomes}
        assert om["new.txt"][0] == "transferred"
        assert om["skip.tmp"] == ("filtered_out", None)
        assert om["big.txt"] == ("rejected", "quota")

        # re-run synchronise → new.txt is now byte-identical, skipped (SC-009: every file an outcome)
        res2 = run_transfer("c1", _req("synchronise"), proxy, disk_reader(src))
        assert {o.rel: o.outcome for o in res2.outcomes}["new.txt"] == "skipped_identical"

        # force overwrites regardless
        res3 = run_transfer("c1", _req("force"), proxy, disk_reader(src))
        assert {o.rel: o.outcome for o in res3.outcomes}["new.txt"] == "transferred"
    finally:
        stop.set()

    # landed under the peer's landing dir; catalog fully rebuilt from WAL after loss (SC-010)
    landed = tmp_path / "share" / "xfer" / "in" / "c0" / "f" / "new.txt"
    assert landed.exists() and landed.read_bytes() == b"hi"
    responder.reload_catalog_from_wal("docs")
    assert responder.catalog("docs").get("c0", "f/new.txt") is not None
    # provenance recorded for transferred + rejected (SC-009)
    outcomes = {(p.outcome, p.reason) for p in responder.provenance("docs")}
    assert ("transferred", None) in outcomes and ("rejected", "quota") in outcomes

    for h in (c0, c1, server):
        ad.stop(h)
