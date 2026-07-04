"""Per-root append-only WAL journal — the responder's source of truth (feature 040, US8; FR-036/SC-010).

One JSON record per line, appended (with fsync) **before** the catalog projection is updated and
**only after** a file is fully received + SHA-256-verified + atomically committed (commit-on-complete;
partial receipts leave no trace). The catalog is fully rebuildable by replaying the WAL with zero
inventory loss (SC-010). Contract: ``contracts/responder-store.md``.
"""

from __future__ import annotations

import json
import os
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Dict, List


@dataclass(frozen=True)
class WalRecord:
    """A single self-describing, replay-idempotent WAL entry."""

    op: str            # "put" | "remove"
    rel: str           # path under the peer landing dir, e.g. "folder/file.bin"
    size: int
    sha256: str
    mtime: int
    peer: str          # the authenticated peer-name-and-UID
    root: str
    target_folder: str
    ts: int

    def to_line(self) -> str:
        return json.dumps(asdict(self), separators=(",", ":"), ensure_ascii=False)

    @classmethod
    def from_obj(cls, obj: dict) -> "WalRecord":
        return cls(
            op=obj["op"], rel=obj["rel"], size=obj.get("size", 0), sha256=obj.get("sha256", ""),
            mtime=obj.get("mtime", 0), peer=obj.get("peer", ""), root=obj.get("root", ""),
            target_folder=obj.get("target_folder", ""), ts=obj.get("ts", 0),
        )

    #: Composite catalog key: the same peer's landing dir + rel path (synchronise compare scope).
    @property
    def key(self) -> "tuple[str, str]":
        return (self.peer, self.rel)


class WalJournal:
    """Append-only journal at ``<data_dir>/roots/<root>/wal.log``."""

    def __init__(self, path: Path) -> None:
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)

    def append(self, record: WalRecord) -> None:
        """Append one record durably (write + flush + fsync) so it survives a crash after commit."""
        with open(self.path, "a", encoding="utf-8") as f:
            f.write(record.to_line() + "\n")
            f.flush()
            os.fsync(f.fileno())

    def replay(self) -> List[WalRecord]:
        """Return every record in order (skips blank/corrupt trailing lines defensively)."""
        if not self.path.exists():
            return []
        out: List[WalRecord] = []
        with open(self.path, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                try:
                    out.append(WalRecord.from_obj(json.loads(line)))
                except (json.JSONDecodeError, KeyError):
                    continue  # a torn last line (crash mid-write) is ignored — never a false inventory
        return out

    def rebuild(self) -> "Dict[tuple[str, str], WalRecord]":
        """Replay the WAL into the current inventory: ``(peer, rel) -> latest put`` (removes delete).

        Replaying twice yields the same result (idempotent) — the authoritative catalog after loss.
        """
        inv: "Dict[tuple[str, str], WalRecord]" = {}
        for rec in self.replay():
            if rec.op == "put":
                inv[rec.key] = rec
            elif rec.op == "remove":
                inv.pop(rec.key, None)
        return inv
