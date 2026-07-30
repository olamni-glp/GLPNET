"""DuckLake aging tier behind a narrow seam (research R6) — T021.

Aged metadata (older than the configurable window, default 1 day; terminal
states only) migrates from the PGlite hot tier to DuckDB-over-parquet under
the gitignored ``ms_message/.data/lake/`` dir; catch-up queries UNION
hot + lake. If the ``duckdb`` dependency is absent or misbehaves, the seam
degrades LOUDLY to PGlite-only — a named warning on stderr, never silent
(all contract guarantees except aged-tier query locality are preserved; the
SC-004 drill window is < 1 day, so the drill never depends on the lake).
"""

from __future__ import annotations

import sys
import time
from pathlib import Path
from typing import Optional

#: The named degradation warning (R6 — LOUD, never silent).
DEGRADED_WARNING = ("LAKE DEGRADED: duckdb unavailable — running PGlite-only; "
                    "aged-tier locality lost, all delivery guarantees preserved")


class Lake:
    """The aging tier for one node. All operations are honest about degradation:
    they return ``None`` when degraded (after the loud warning), never a fake
    success count."""

    def __init__(self, root: Path, aging_window_s: int = 86_400) -> None:
        self.root = Path(root)
        self.aging_window_s = aging_window_s
        self._degraded_reason: Optional[str] = None
        self._warned = False

    def _duckdb(self):
        if self._degraded_reason is not None:
            self._warn()
            return None
        try:
            import duckdb  # the [lake] extra
            return duckdb
        except Exception as exc:  # noqa: BLE001 — ANY lake failure degrades loudly, never crashes delivery
            self._degraded_reason = f"{type(exc).__name__}: {exc}"
            self._warn()
            return None

    def _warn(self) -> None:
        if not self._warned:
            print(f"{DEGRADED_WARNING} ({self._degraded_reason})", file=sys.stderr, flush=True)
            self._warned = True

    @property
    def degraded(self) -> bool:
        return self._degraded_reason is not None

    def age_out(self, store) -> Optional[int]:
        """Move terminal-state messages older than the window to parquet, then
        drop them from the hot tier. Returns the migrated count, or ``None``
        when degraded (loudly)."""
        duckdb = self._duckdb()
        if duckdb is None:
            return None
        rows = store._rows(
            """
            SELECT sender_station, sender_seq, mailbox_id, target_station,
                   size_bytes, content_ref, accepted_at, state
              FROM msmesh.message
             WHERE state IN ('fetched', 'expired', 'dead')
               AND accepted_at < NOW() - make_interval(secs => :win)
            """,
            win=self.aging_window_s)
        if not rows:
            return 0
        try:
            self.root.mkdir(parents=True, exist_ok=True)
            out = self.root / f"messages-{int(time.time())}.parquet"
            con = duckdb.connect()
            con.execute(
                "CREATE TABLE aged (sender_station VARCHAR, sender_seq BIGINT, mailbox_id VARCHAR, "
                "target_station VARCHAR, size_bytes BIGINT, content_ref VARCHAR, "
                "accepted_at VARCHAR, state VARCHAR)")
            con.executemany(
                "INSERT INTO aged VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                [(r["sender_station"], r["sender_seq"], r["mailbox_id"], r["target_station"],
                  r["size_bytes"], r["content_ref"], str(r["accepted_at"]), r["state"])
                 for r in rows])
            con.execute(f"COPY aged TO '{out.as_posix()}' (FORMAT PARQUET)")
            con.close()
        except Exception as exc:  # noqa: BLE001
            self._degraded_reason = f"{type(exc).__name__}: {exc}"
            self._warn()
            return None
        for r in rows:  # hot rows leave ONLY after the parquet write landed
            store._exec(
                "DELETE FROM msmesh.message WHERE sender_station = :s AND sender_seq = :q",
                s=r["sender_station"], q=r["sender_seq"])
        return len(rows)

    def catchup_query(self, store, mailbox_id: str) -> list:
        """All messages for ``mailbox_id`` across hot ∪ lake (aged rows carry
        ``tier='lake'``). Degrades loudly to hot-only."""
        hot = store._rows(
            "SELECT sender_station, sender_seq, state FROM msmesh.message "
            "WHERE mailbox_id = :mid ORDER BY sender_station, sender_seq",
            mid=mailbox_id)
        for r in hot:
            r["tier"] = "hot"
        duckdb = self._duckdb()
        if duckdb is None or not any(self.root.glob("messages-*.parquet")):
            return hot
        try:
            con = duckdb.connect()
            aged = con.execute(
                "SELECT sender_station, sender_seq, state FROM "
                f"read_parquet('{(self.root / 'messages-*.parquet').as_posix()}') "
                "WHERE mailbox_id = ? ORDER BY sender_station, sender_seq",
                [mailbox_id]).fetchall()
            con.close()
        except Exception as exc:  # noqa: BLE001
            self._degraded_reason = f"{type(exc).__name__}: {exc}"
            self._warn()
            return hot
        return hot + [
            {"sender_station": s, "sender_seq": q, "state": st, "tier": "lake"}
            for (s, q, st) in aged]
