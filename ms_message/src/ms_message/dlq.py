"""Dead-letter queue (research R8, contract guarantee 5) — T018.

A target unresolvable after direct + friend lookup parks here with a reason;
entries are listable and re-driveable from the CLI. Parking is dual-recorded:
the WAL journals the ``dead`` transition (the durability truth), the store's
``msmesh.dlq`` row carries the reason and the re-drive stamp.

Re-drive returns a parked message to ``journalled`` (WAL first, then store),
so the originator loop resolves + signals it again on its next pass — the
re-driven attempt follows the exact same path as a fresh acceptance.
"""

from __future__ import annotations

from ms_message.store import Store
from ms_message.wal import Wal

#: The contract's canonical park reason (guarantee 5).
UNRESOLVABLE = "unresolvable-target-after-friend-lookup"


class DeadLetterQueue:
    """Park / list / re-drive over one node's WAL + store pair."""

    def __init__(self, wal: Wal, store: Store) -> None:
        self.wal = wal
        self.store = store

    def park(self, sender: str, seq: int, reason: str = UNRESOLVABLE) -> None:
        """Park one message: WAL journals ``dead`` first, then the store row."""
        self.wal.mark(sender, seq, "dead")
        self.store.park_dlq(sender, seq, reason)

    def list(self, include_redriven: bool = False) -> list:
        return self.store.list_dlq(include_redriven=include_redriven)

    def redrive(self) -> list:
        """Re-drive every parked entry: back to ``journalled`` (WAL first),
        stamp ``redriven_at``. Returns the re-driven ``(sender, seq)`` pairs."""
        redriven = []
        for entry in self.store.list_dlq():
            sender, seq = entry["sender_station"], entry["sender_seq"]
            self.wal.mark(sender, seq, "journalled")
            self.store.set_state(sender, seq, "journalled")
            self.store.mark_redriven(entry["id"])
            redriven.append((sender, seq))
        return redriven
