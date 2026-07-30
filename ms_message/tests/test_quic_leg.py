"""T025 — QUIC-leg evidence: one drill pass with the mesh-messaging payloads over
the REAL spec-025 QUIC+WS link (research R3: the shapes are transport-agnostic;
TCP remains the default evidence path).

Two genuine ``glp_quick_host`` processes carry the ms_message signal → fetch →
fetch_batch exchange as link-envelope payloads across a real QUIC/TLS/WebSocket
handshake: N journalled messages delivered exactly once, in order, with the
recipient's durable WAL position doing the dedup — the same guarantees as the
TCP drill, on the QUIC carrier. Skipped when the C# host dll is not built.
"""

from __future__ import annotations

import socket
import threading
import time

import pytest

from glp_quick import cert as cert_mod
from glp_quick.repl_link import GlpMessage
from glp_quick.stacks.csharp import CSharpStackAdapter, host_dll_path

from ms_message import protocol
from ms_message.cli import build_fetch_batch
from ms_message.wal import Wal

pytestmark = pytest.mark.skipif(
    not host_dll_path().exists(),
    reason="glp_quick_host.dll not built (run: dotnet build csharp/glp_quick_host)",
)

N = 100
DEADLINE_S = 60.0


def _free_udp_port() -> int:
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    s.close()
    return port


def _payload(p: protocol.Payload) -> str:
    return protocol.encode(p).decode("utf-8")  # ground JSON text rides the envelope


def test_drill_pass_over_quic_ws(tmp_path):
    # Journal N messages on the holder's WAL (alice → bob), recipient offline.
    holder_wal = Wal(tmp_path / "alice")
    for i in range(1, N + 1):
        holder_wal.accept("alice", i, "news", "bob", f"m{i:06d}".encode())
    recip_wal = Wal(tmp_path / "bob")

    cert_mod.generate_shared_cert(tmp_path / "cert", days=2)
    port = _free_udp_port()
    ad = CSharpStackAdapter()
    server = ad.start_server("127.0.0.1", port, tmp_path / "cert", 3, "csharp")  # holder end
    client = ad.start_client("127.0.0.1", port, tmp_path / "cert", "csharp")     # recipient end
    try:
        client.send(GlpMessage(sender="bob", to="server", payload="__connected__"))

        # ---- holder end: a serving thread answering fetches from the WAL truth.
        stop = threading.Event()

        def holder_loop() -> None:
            while not stop.is_set():
                req_msg = server.recv(timeout=0.5)
                if req_msg is None:
                    continue
                if req_msg.payload == "__connected__":
                    # signal-on-reappearance (guarantee 4): the recipient's announce IS the
                    # reappearance; only now is its id routable in the mesh. Carries NO content.
                    server.send(GlpMessage(sender="server", to=req_msg.sender,
                                           payload=_payload(protocol.Signal("alice", "news", N))))
                    continue
                req = protocol.decode(req_msg.payload.encode("utf-8"))
                if isinstance(req, protocol.Fetch):
                    batch, acked, served = build_fetch_batch(holder_wal, "alice", req)
                    for _snd, s in acked:
                        holder_wal.mark("alice", s, "fetched")
                    for _snd, s in served:
                        holder_wal.mark("alice", s, "signalled")
                    server.send(GlpMessage(sender="server", to="bob", payload=_payload(batch)))

        threading.Thread(target=holder_loop, daemon=True).start()

        # ---- recipient end: signal → resumable fetch loop with the durable position.
        delivered: list = []
        pos, seen = 0, []
        deadline = time.monotonic() + DEADLINE_S
        target_hw = None
        while (target_hw is None or pos < target_hw) and time.monotonic() < deadline:
            msg = client.recv(timeout=5)
            if msg is None or msg.payload == "__connected__":
                continue
            payload = protocol.decode(msg.payload.encode("utf-8"))
            if isinstance(payload, protocol.Signal):
                target_hw = payload.high_water_seq
                client.send(GlpMessage(sender="bob", to="server",
                                       payload=_payload(protocol.Fetch("bob", "news", pos + 1, 40))))
                continue
            assert isinstance(payload, protocol.FetchBatch)
            for entry in payload.entries:
                assert isinstance(entry, protocol.BatchMessage), "unexpected gap over QUIC"
                if entry.sender_seq <= pos or entry.sender_seq in seen:
                    continue  # exactly-once observation
                delivered.append(entry.content.decode())
                pos = entry.sender_seq
            recip_wal.advance_position("alice", "inbound", pos, seen)
            if pos < (target_hw or 0):
                client.send(GlpMessage(sender="bob", to="server",
                                       payload=_payload(protocol.Fetch("bob", "news", pos + 1, 40))))
        stop.set()

        assert delivered == [f"m{i:06d}" for i in range(1, N + 1)], (
            f"QUIC leg: {len(delivered)}/{N} delivered (pos={pos}, hw={target_hw})")
        # The durable position survives — a rerun from the WAL replays to pos == N.
        assert recip_wal.replay().positions[("alice", "inbound")]["high_water"] == N
    finally:
        ad.stop(client)
        ad.stop(server)
