"""msmesh hot-tier access (research R5) — T017 (+ retention sweep, T022).

Stations, mailboxes, messages, delivery_position, dlq, gap_event rows in the
repo's ``.pgdb/`` PGlite cluster (``msmesh`` schema, additive migration 0012),
reached EXCLUSIVELY through the shared codeconv bridge (constitution VI-b:
``codeconv.db.engine.connect`` → ``codeconv.bridge_client`` — never a parallel
bridge stack).

Reconciliation direction is fixed by the data model: the store is reconciled
to the WAL, never the reverse (:meth:`Store.reconcile`).
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Optional

from ms_message.wal import GapEvent, MessageMeta, WalState


def _default_repo_root() -> Path:
    # ms_message/src/ms_message/store.py → repo root is three parents up.
    return Path(__file__).resolve().parents[3]


class Store:
    """One node's msmesh hot tier behind the shared bridge (lazy connect)."""

    def __init__(self, repo_root: Optional[Path] = None, data_dir: Optional[Path] = None) -> None:
        self.repo_root = Path(repo_root) if repo_root else _default_repo_root()
        self.data_dir = Path(data_dir) if data_dir else self.repo_root / ".pgdb"
        self._eng = None

    def _engine(self):
        if self._eng is None:
            from codeconv.db.engine import connect  # lazy: heavy import chain

            self._eng = connect(self.repo_root, data_dir=self.data_dir,
                                application_name="ms_message")
        return self._eng

    def _exec(self, sql: str, **params):
        from sqlalchemy import text

        with self._engine().begin() as conn:
            return conn.execute(text(sql), params)

    def _exec_many(self, statements: list) -> None:
        """Run many ``(sql, params)`` in ONE transaction — the single-writer
        PGlite bridge serializes whole queries, so a per-row transaction storm
        (N begin/commit round-trips) both crawls and widens the window in
        which a concurrent process's dialect-init introspection can land
        mid-transaction. One transaction per logical batch avoids both."""
        from sqlalchemy import text

        with self._engine().begin() as conn:
            for sql, params in statements:
                conn.execute(text(sql), params)

    def _rows(self, sql: str, **params) -> list:
        from sqlalchemy import text

        with self._engine().begin() as conn:
            return [dict(r._mapping) for r in conn.execute(text(sql), params)]

    # ------------------------------------------------------------------ stations / mailboxes
    def ensure_station(self, station_id: str, address: Optional[str] = None,
                       source: str = "config") -> None:
        self._exec(
            """
            INSERT INTO msmesh.station (station_id, address, source)
            VALUES (:sid, :addr, :src)
            ON CONFLICT (station_id) DO UPDATE
               SET address = COALESCE(EXCLUDED.address, msmesh.station.address)
            """,
            sid=station_id, addr=address, src=source)

    def lookup_station(self, station_id: str) -> Optional[dict]:
        rows = self._rows("SELECT * FROM msmesh.station WHERE station_id = :sid", sid=station_id)
        return rows[0] if rows else None

    def ensure_mailbox(self, mailbox_id: str, owner_station: str,
                       retention_class: str = "permanent",
                       retention_window_s: Optional[int] = None) -> None:
        self._exec(
            """
            INSERT INTO msmesh.mailbox (mailbox_id, owner_station, retention_class, retention_window_s)
            VALUES (:mid, :own, :rc, :win)
            ON CONFLICT (mailbox_id) DO NOTHING
            """,
            mid=mailbox_id, own=owner_station, rc=retention_class, win=retention_window_s)

    # ------------------------------------------------------------------ messages
    def upsert_message(self, meta: MessageMeta) -> None:
        self._exec(
            """
            INSERT INTO msmesh.message
                (sender_station, sender_seq, mailbox_id, target_station,
                 size_bytes, content_ref, state)
            VALUES (:sender, :seq, :mbox, :target, :size, :ref, :state)
            ON CONFLICT (sender_station, sender_seq) DO UPDATE
               SET state = EXCLUDED.state, content_ref = EXCLUDED.content_ref
            """,
            sender=meta.sender, seq=meta.seq, mbox=meta.mailbox, target=meta.target,
            size=meta.size, ref=meta.content_ref, state=meta.state)

    def set_state(self, sender: str, seq: int, state: str) -> None:
        self._exec(
            "UPDATE msmesh.message SET state = :st "
            "WHERE sender_station = :sender AND sender_seq = :seq",
            st=state, sender=sender, seq=seq)

    def messages_for(self, mailbox_id: str, states: tuple = ("journalled", "signalled")) -> list:
        return self._rows(
            "SELECT * FROM msmesh.message WHERE mailbox_id = :mid "
            "AND state = ANY(:states) ORDER BY sender_station, sender_seq",
            mid=mailbox_id, states=list(states))

    # ------------------------------------------------------------------ delivery position (R7)
    def advance_position(self, peer: str, direction: str, high_water: int,
                         seen_sparse: Optional[list] = None) -> None:
        self._exec(
            """
            INSERT INTO msmesh.delivery_position (peer_station, direction, high_water_seq, seen_sparse, updated_at)
            VALUES (:peer, :dir, :hw, CAST(:seen AS jsonb), NOW())
            ON CONFLICT (peer_station, direction) DO UPDATE
               SET high_water_seq = EXCLUDED.high_water_seq,
                   seen_sparse = EXCLUDED.seen_sparse,
                   updated_at = NOW()
            """,
            peer=peer, dir=direction, hw=high_water, seen=json.dumps(seen_sparse or []))

    def get_position(self, peer: str, direction: str) -> tuple:
        rows = self._rows(
            "SELECT high_water_seq, seen_sparse FROM msmesh.delivery_position "
            "WHERE peer_station = :peer AND direction = :dir",
            peer=peer, dir=direction)
        if not rows:
            return 0, []
        seen = rows[0]["seen_sparse"]
        if isinstance(seen, str):
            seen = json.loads(seen)
        return rows[0]["high_water_seq"], seen or []

    # ------------------------------------------------------------------ DLQ (R8)
    def park_dlq(self, sender: str, seq: int, reason: str) -> None:
        self.set_state(sender, seq, "dead")
        self._exec(
            "INSERT INTO msmesh.dlq (sender_station, sender_seq, reason) "
            "VALUES (:sender, :seq, :reason)",
            sender=sender, seq=seq, reason=reason)

    def list_dlq(self, include_redriven: bool = False) -> list:
        cond = "" if include_redriven else "WHERE redriven_at IS NULL"
        return self._rows(f"SELECT * FROM msmesh.dlq {cond} ORDER BY parked_at")

    def mark_redriven(self, dlq_id: int) -> None:
        self._exec("UPDATE msmesh.dlq SET redriven_at = NOW() WHERE id = :id", id=dlq_id)

    # ------------------------------------------------------------------ gap events (FR-010)
    def record_gap(self, gap: GapEvent, resolution: str = "unresolved") -> None:
        self._exec(
            "INSERT INTO msmesh.gap_event (peer_station, expected_seq, got_seq, resolution) "
            "VALUES (:peer, :exp, :got, :res)",
            peer=gap.sender, exp=gap.expected_seq, got=gap.got_seq, res=resolution)

    def list_gaps(self) -> list:
        return self._rows("SELECT * FROM msmesh.gap_event ORDER BY detected_at")

    # ------------------------------------------------------------------ reconcile (WAL → store)
    def set_states_batch(self, identities: list, state: str) -> None:
        """One transaction for many ``(sender, seq)`` state transitions."""
        self._exec_many([
            ("UPDATE msmesh.message SET state = :st "
             "WHERE sender_station = :sender AND sender_seq = :seq",
             {"st": state, "sender": s, "seq": q})
            for (s, q) in identities])

    def reconcile(self, state: WalState, station_id: str) -> None:
        """Bring the hot tier in line with the replayed WAL truth (never the
        reverse). Missing rows are inserted; diverging states follow the WAL.
        Runs as ONE transaction (see :meth:`_exec_many`)."""
        stmts: list = [(
            "INSERT INTO msmesh.station (station_id, address, source) "
            "VALUES (:sid, NULL, 'config') ON CONFLICT (station_id) DO NOTHING",
            {"sid": station_id})]
        mailboxes = set()
        for meta in state.messages.values():
            if meta.mailbox not in mailboxes:
                mailboxes.add(meta.mailbox)
                rc = meta.retention if meta.retention in ("ephemeral", "permanent") else "time_windowed"
                stmts.append((
                    "INSERT INTO msmesh.mailbox (mailbox_id, owner_station, retention_class, retention_window_s) "
                    "VALUES (:mid, :own, :rc, NULL) ON CONFLICT (mailbox_id) DO NOTHING",
                    {"mid": meta.mailbox, "own": station_id, "rc": rc}))
            stmts.append((
                "INSERT INTO msmesh.message (sender_station, sender_seq, mailbox_id, target_station, "
                "size_bytes, content_ref, state) VALUES (:sender, :seq, :mbox, :target, :size, :ref, :state) "
                "ON CONFLICT (sender_station, sender_seq) DO UPDATE "
                "SET state = EXCLUDED.state, content_ref = EXCLUDED.content_ref",
                {"sender": meta.sender, "seq": meta.seq, "mbox": meta.mailbox, "target": meta.target,
                 "size": meta.size, "ref": meta.content_ref, "state": meta.state}))
        for (peer, direction), pos in state.positions.items():
            stmts.append((
                "INSERT INTO msmesh.delivery_position (peer_station, direction, high_water_seq, seen_sparse, updated_at) "
                "VALUES (:peer, :dir, :hw, CAST(:seen AS jsonb), NOW()) "
                "ON CONFLICT (peer_station, direction) DO UPDATE "
                "SET high_water_seq = EXCLUDED.high_water_seq, seen_sparse = EXCLUDED.seen_sparse, updated_at = NOW()",
                {"peer": peer, "dir": direction, "hw": pos["high_water"], "seen": json.dumps(pos["seen"])}))
        for gap in state.gaps:
            stmts.append((
                "INSERT INTO msmesh.gap_event (peer_station, expected_seq, got_seq, resolution) "
                "VALUES (:peer, :exp, :got, 'unresolved')",
                {"peer": gap.sender, "exp": gap.expected_seq, "got": gap.got_seq}))
        self._exec_many(stmts)

    # ------------------------------------------------------------------ status summary + retention sweep
    def status_summary(self) -> dict:
        counts = self._rows(
            "SELECT state, COUNT(*) AS n FROM msmesh.message GROUP BY state")
        return {
            "messages_by_state": {r["state"]: r["n"] for r in counts},
            "positions": self._rows("SELECT * FROM msmesh.delivery_position"),
            "gaps_unresolved": len(self._rows(
                "SELECT 1 FROM msmesh.gap_event WHERE resolution = 'unresolved'")),
            "dlq_parked": len(self.list_dlq()),
        }

    def sweep_retention(self) -> list:
        """Expire messages per their mailbox's retention class (FR-011b,
        guarantee 6): ephemeral ⇒ expire once fetched; time_windowed ⇒ expire
        past the window; permanent ⇒ never. Returns the expired identities."""
        expired = self._rows(
            """
            SELECT m.sender_station, m.sender_seq
              FROM msmesh.message m
              JOIN msmesh.mailbox b ON b.mailbox_id = m.mailbox_id
             WHERE (b.retention_class = 'ephemeral' AND m.state = 'fetched')
                OR (b.retention_class = 'time_windowed'
                    AND m.accepted_at < NOW() - make_interval(secs => b.retention_window_s))
            """)
        for row in expired:
            self.set_state(row["sender_station"], row["sender_seq"], "expired")
        return [(r["sender_station"], r["sender_seq"]) for r in expired]
