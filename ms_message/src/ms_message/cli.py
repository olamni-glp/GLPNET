"""ms-message CLI — originator / recipient / status / dlq (T019/T020/T022).

Command contract: specs/063-wave-5-consolidated-captured-triad/contracts/
mesh-messaging-protocol.md. Wire payloads are the transport-agnostic shapes in
:mod:`ms_message.protocol` (research R3), carried here newline-delimited over
TCP — the default evidence path; the QUIC+WS leg rides the spec-025 link
after US1 (T025). Every wait is bounded by ``--timeout`` (guarantee 7).

Originator flow (guarantees 1/4/5): accept → WAL (durable, fsync) → store →
signal reachable targets on connect; a target that is neither expected
inbound nor resolvable by direct + friend lookup parks in the DLQ with the
canonical reason. Recipient flow (guarantees 2/3): signal → resumable fetch
from the durable delivery position → exactly-once observation (dedup on the
per-sender high-water mark + sparse seen-set) → dense-order verification with
NAMED gap events, never a silent skip.
"""

from __future__ import annotations

import json
import socket
import sys
import threading
import time
from pathlib import Path
from typing import Optional

import typer

from ms_message import protocol
from ms_message.dlq import UNRESOLVABLE, DeadLetterQueue
from ms_message.store import Store
from ms_message.wal import Wal, WalCorrupt

app = typer.Typer(
    name="ms-message",
    help="Durable first-hop mesh messaging: signal-then-fetch with WAL durability (feature 063 US2).",
    no_args_is_help=True,
)

dlq_app = typer.Typer(help="Inspect and re-drive dead letters.", no_args_is_help=True)
app.add_typer(dlq_app, name="dlq")

FETCH_BATCH_MAX = 500


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[3]


def _data_root(override: Optional[Path]) -> Path:
    return Path(override) if override else _repo_root() / "ms_message" / ".data"


def _node(station: str, data_root: Optional[Path]) -> tuple:
    """One node's (wal, store, replayed-state) with the store reconciled to the WAL."""
    wal = Wal(_data_root(data_root) / station)
    try:
        state = wal.replay()
    except WalCorrupt as exc:  # explicit refusal, never silent loss (FR-011)
        typer.echo(f"ms-message: REFUSING to start — journal corrupt: {exc}", err=True)
        raise typer.Exit(code=3)
    store = Store()
    store.reconcile(state, station)
    return wal, store, state


# ------------------------------------------------------------------ TCP carrier (newline-delimited)
def _send_payload(sock: socket.socket, payload: protocol.Payload) -> None:
    sock.sendall(protocol.encode(payload) + b"\n")


class _LineReader:
    def __init__(self, sock: socket.socket) -> None:
        self._sock = sock
        self._buf = b""

    def next_payload(self) -> Optional[protocol.Payload]:
        """One decoded payload, or None on clean EOF. Bounded by the socket timeout."""
        while b"\n" not in self._buf:
            chunk = self._sock.recv(65536)
            if not chunk:
                return None
            self._buf += chunk
        line, _, self._buf = self._buf.partition(b"\n")
        return protocol.decode(line)


def _parse_ep(ep: str) -> tuple:
    host, _, port = ep.rpartition(":")
    return host or "127.0.0.1", int(port)


def build_fetch_batch(wal: Wal, station: str, req: "protocol.Fetch") -> tuple:
    """Answer one fetch from the WAL truth (carrier-agnostic, R3 — shared by
    the TCP serve loop and the QUIC-leg evidence). Returns
    ``(batch, acked, served)``: the wire batch, the identities position-acked
    as fetched (everything below ``from_seq``), and those newly signalled.
    The CALLER journals the marks (WAL first) and mirrors the store."""
    st = wal.replay()
    acked = [(snd, s) for (snd, s), meta in sorted(st.messages.items())
             if snd == station and s < req.from_seq and meta.state in ("journalled", "signalled")]
    entries: list = []
    served: list = []
    expected = req.from_seq
    high_water = max((s for (snd, s) in st.messages if snd == station), default=0)
    for (snd, s), meta in sorted(st.messages.items()):
        if snd != station or meta.mailbox != req.mailbox_id or s < req.from_seq:
            continue
        if meta.state in ("dead", "expired"):
            continue
        if len(entries) >= min(req.max_count, FETCH_BATCH_MAX):
            break
        if s != expected:  # a hole is an EXPLICIT gap marker, never silent (guarantee 3)
            entries.append(protocol.GapMarker(expected_seq=expected, got_seq=s))
        entries.append(protocol.BatchMessage(snd, s, wal.read_content(meta.content_ref)))
        if meta.state == "journalled":
            served.append((snd, s))
        expected = s + 1
    return protocol.FetchBatch(req.mailbox_id, tuple(entries), high_water), acked, served


# ------------------------------------------------------------------ originator (T019)
@app.command()
def originator(
    station: str = typer.Option(..., "--station", help="This node's ground-station id."),
    listen: Optional[str] = typer.Option(None, "--listen", help="Endpoint to serve fetches on (host:port)."),
    mailbox: str = typer.Option("default", "--mailbox", help="Mailbox/topic to accept content into."),
    to: Optional[str] = typer.Option(None, "--to", help="First-hop target station id."),
    count: Optional[int] = typer.Option(None, "--count", help="Drill mode: journal N generated messages."),
    content: Optional[str] = typer.Option(None, "--content", help="One message body (else stdin lines when no --count)."),
    retention: str = typer.Option("permanent", "--retention", help="ephemeral | time_windowed | permanent."),
    retention_window_s: Optional[int] = typer.Option(None, "--retention-window-s", help="Window for time_windowed."),
    expect_inbound: bool = typer.Option(True, "--expect-inbound/--no-expect-inbound",
                                        help="Target connects to us (known-by-id). With --no-expect-inbound the target must resolve to an address or parks in the DLQ."),
    serve_for: Optional[float] = typer.Option(None, "--serve-for", help="Serve fetches for N seconds then exit (bounded drill run)."),
    timeout: float = typer.Option(30.0, "--timeout", help="Per-wait bound in seconds (guarantee 7)."),
    data_root: Optional[Path] = typer.Option(None, "--data-root", help="WAL/message-file root (default ms_message/.data)."),
) -> None:
    """Accept content into a mailbox for a target; journal (WAL first); signal reachable targets."""
    wal, store, state = _node(station, data_root)
    store.ensure_mailbox(mailbox, owner_station=station, retention_class=retention,
                         retention_window_s=retention_window_s)
    dlq = DeadLetterQueue(wal, store)

    # ---- accept phase: WAL first (guarantee 1), then the store mirror.
    if to is not None:
        next_seq = 1 + max((s for (snd, s) in state.messages if snd == station), default=0)
        bodies: list
        if count is not None:
            bodies = [f"m{i:06d}".encode() for i in range(1, count + 1)]
        elif content is not None:
            bodies = [content.encode()]
        else:
            bodies = [line.rstrip("\r\n").encode() for line in sys.stdin if line.strip()]
        resolvable = True
        if not expect_inbound:
            row = store.lookup_station(to)
            resolvable = bool(row and row.get("address"))
            # direct failed → friend lookup would go over live links; with none up at accept
            # time the target is unresolvable (R8 scopes lookup to the local registry).
        else:
            store.ensure_station(to, source="config")  # known-by-id; reachable on reappearance
        accepted = []
        for body in bodies:
            # WAL first, per message (guarantee 1: durable before acknowledgement)…
            accepted.append(wal.accept(station, next_seq, mailbox, to, body, retention=retention))
            if not resolvable:
                dlq.park(station, next_seq, UNRESOLVABLE)
            next_seq += 1
        # …then ONE hot-tier mirror transaction (the WAL is the truth; see Store._exec_many).
        from ms_message.wal import WalState
        mirror = WalState()
        mirror.messages = {(m.sender, m.seq): m for m in accepted}
        store.reconcile(mirror, station)
        parked = 0 if resolvable else len(bodies)
        typer.echo(f"journalled {len(bodies)} message(s) for {to} in {mailbox!r}"
                   + (f"; {parked} parked to DLQ ({UNRESOLVABLE})" if parked else ""))

    if listen is None:
        return

    # ---- serve phase: signal on connect (guarantee 4), answer fetch / friend_lookup.
    host, port = _parse_ep(listen)
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind((host, port))
    srv.listen(8)
    srv.settimeout(1.0)
    deadline = time.monotonic() + serve_for if serve_for is not None else None
    typer.echo(f"serving {mailbox!r} on {host}:{port}"
               + (f" for {serve_for:.0f}s" if serve_for else ""), err=True)

    def _high_water() -> int:
        st = wal.replay()
        return max((s for (snd, s) in st.messages if snd == station), default=0)

    def _serve_conn(conn: socket.socket) -> None:
        conn.settimeout(timeout)
        reader = _LineReader(conn)
        try:
            _send_payload(conn, protocol.Signal(station, mailbox, _high_water()))
            while True:
                payload = reader.next_payload()
                if payload is None:
                    return
                if isinstance(payload, protocol.Fetch):
                    _answer_fetch(conn, payload)
                elif isinstance(payload, protocol.FriendLookup):
                    row = store.lookup_station(payload.target_station)
                    addr = (row or {}).get("address") or protocol.UNKNOWN
                    _send_payload(conn, protocol.FriendReply(payload.target_station, addr))
                # signals from a peer are idempotent notices; nothing to answer here
        except (socket.timeout, ConnectionError, protocol.ProtocolError) as exc:
            typer.echo(f"conn ended: {type(exc).__name__}: {exc}", err=True)
        finally:
            conn.close()

    def _answer_fetch(conn: socket.socket, req: protocol.Fetch) -> None:
        batch, acked, served = build_fetch_batch(wal, station, req)
        # WAL marks first (the truth); the store mirror is two batch transactions.
        for snd, s in acked:
            wal.mark(station, s, "fetched")
        for snd, s in served:
            wal.mark(station, s, "signalled")
        if acked:
            store.set_states_batch(acked, "fetched")
        if served:
            store.set_states_batch(served, "signalled")
        _send_payload(conn, batch)

    try:
        while deadline is None or time.monotonic() < deadline:
            try:
                conn, _addr = srv.accept()
            except socket.timeout:
                continue
            threading.Thread(target=_serve_conn, args=(conn,), daemon=True).start()
    except KeyboardInterrupt:
        pass
    finally:
        srv.close()


# ------------------------------------------------------------------ recipient (T020)
@app.command()
def recipient(
    station: str = typer.Option(..., "--station", help="This node's ground-station id."),
    from_: str = typer.Option(..., "--from", help="Holder endpoint to receive signals from (host:port)."),
    once: bool = typer.Option(False, "--once", help="Catch up to the signalled high-water then exit (drill mode)."),
    timeout: float = typer.Option(30.0, "--timeout", help="Per-wait bound in seconds (guarantee 7)."),
    data_root: Optional[Path] = typer.Option(None, "--data-root", help="WAL/message-file root (default ms_message/.data)."),
) -> None:
    """Receive signals, fetch at own pace from the durable position, print messages, advance position."""
    wal, store, state = _node(station, data_root)
    host, port = _parse_ep(from_)
    sock = socket.create_connection((host, port), timeout=timeout)
    sock.settimeout(timeout)
    reader = _LineReader(sock)
    try:
        while True:
            payload = reader.next_payload()
            if payload is None:
                typer.echo("holder closed the link", err=True)
                return
            if not isinstance(payload, protocol.Signal):
                continue
            holder, mailbox = payload.holder_station, payload.mailbox_id
            target_hw = payload.high_water_seq
            pos, seen = _position(wal, holder)
            while pos < target_hw:
                _send_payload(sock, protocol.Fetch(station, mailbox, pos + 1, FETCH_BATCH_MAX))
                batch = reader.next_payload()
                if batch is None:
                    typer.echo("holder closed mid-fetch", err=True)
                    return
                if not isinstance(batch, protocol.FetchBatch):
                    raise protocol.ProtocolError(f"expected fetch_batch, got {type(batch).__name__}")
                if not batch.entries:
                    break  # nothing more served yet; re-await a signal
                for entry in batch.entries:
                    if isinstance(entry, protocol.GapMarker):
                        # a NAMED loss, journaled + stored — never a silent skip (FR-010)
                        from ms_message.wal import GapEvent
                        wal.record_gap(holder, entry.expected_seq, entry.got_seq)
                        store.record_gap(GapEvent(holder, entry.expected_seq, entry.got_seq))
                        typer.echo(f"GAP {holder} expected={entry.expected_seq} got={entry.got_seq}", err=True)
                        pos = entry.got_seq - 1
                        continue
                    if entry.sender_seq <= pos or entry.sender_seq in seen:
                        continue  # duplicate delivery — observed exactly once (guarantee 2)
                    sys.stdout.write(entry.content.decode("utf-8", "replace") + "\n")
                    if entry.sender_seq == pos + 1:
                        pos = entry.sender_seq
                        while pos + 1 in seen:  # fold the sparse set into the dense mark
                            seen.remove(pos + 1)
                            pos += 1
                    else:
                        seen.append(entry.sender_seq)
                sys.stdout.flush()
                wal.advance_position(holder, "inbound", pos, seen)
                store.advance_position(holder, "inbound", pos, seen)
            if once:
                typer.echo(f"caught up: position {pos}/{target_hw} from {holder}", err=True)
                return
    except socket.timeout:
        typer.echo(f"bounded wait expired ({timeout:.0f}s) — no silent stall (guarantee 7)", err=True)
        raise typer.Exit(code=4)
    finally:
        sock.close()


def _position(wal: Wal, holder: str) -> tuple:
    st = wal.replay()
    pos = st.positions.get((holder, "inbound"), {"high_water": 0, "seen": []})
    return pos["high_water"], list(pos["seen"])


# ------------------------------------------------------------------ status (T022)
@app.command()
def status(
    station: Optional[str] = typer.Option(None, "--station", help="Limit the WAL view to one station's node dir."),
    sweep: bool = typer.Option(False, "--sweep", help="Run the retention sweep before summarizing (FR-011b)."),
    data_root: Optional[Path] = typer.Option(None, "--data-root", help="WAL/message-file root."),
) -> None:
    """Journal/position/gap/DLQ summary for the node (hot tier + WAL)."""
    store = Store()
    if sweep:
        expired = store.sweep_retention()
        typer.echo(f"retention sweep: {len(expired)} expired", err=True)
    summary = store.status_summary()
    if station:
        wal = Wal(_data_root(data_root) / station)
        try:
            st = wal.replay()
        except WalCorrupt as exc:
            typer.echo(f"ms-message: journal corrupt: {exc}", err=True)
            raise typer.Exit(code=3)
        summary["wal"] = {
            "station": station,
            "messages": len(st.messages),
            "positions": {f"{p}/{d}": v for (p, d), v in st.positions.items()},
            "gaps": len(st.gaps),
        }
    typer.echo(json.dumps(summary, indent=2, default=str))


# ------------------------------------------------------------------ dlq (T018 surface)
@dlq_app.command("list")
def dlq_list() -> None:
    """List dead letters with their park reasons."""
    rows = Store().list_dlq(include_redriven=True)
    if not rows:
        typer.echo("dlq empty")
        return
    for r in rows:
        stamp = "REDRIVEN" if r.get("redriven_at") else "parked"
        typer.echo(f"{r['sender_station']}#{r['sender_seq']} [{stamp}] {r['reason']}")


@dlq_app.command("redrive")
def dlq_redrive(
    station: str = typer.Option(..., "--station", help="The node whose WAL re-journals the entries."),
    data_root: Optional[Path] = typer.Option(None, "--data-root", help="WAL/message-file root."),
) -> None:
    """Re-drive parked dead letters (back to journalled; same path as fresh acceptance)."""
    wal = Wal(_data_root(data_root) / station)
    redriven = DeadLetterQueue(wal, Store()).redrive()
    typer.echo(f"re-driven {len(redriven)} message(s): "
               + ", ".join(f"{s}#{q}" for s, q in redriven) if redriven else "dlq empty")


if __name__ == "__main__":
    app()
