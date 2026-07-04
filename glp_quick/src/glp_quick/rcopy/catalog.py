"""Per-root catalog projection + synchronise SHA-256 compare (feature 040, US8; FR-034/FR-035).

The catalog is the responder's current inventory keyed by ``(peer, rel)`` — a **projection** of the WAL
journal (:mod:`glp_quick.rcopy.wal`), never trusted over it: after loss it is rebuilt by WAL replay
(SC-010). It backs the synchronise decision (skip a manifest file whose SHA-256 matches the catalog
entry under that peer's landing dir + folder) and the quota check (committed bytes per root).
``catalog.json`` is a convenience snapshot only. Contract: ``contracts/responder-store.md``.
"""

from __future__ import annotations

import json
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Dict, Optional, Tuple

from glp_quick.rcopy.wal import WalJournal, WalRecord

Key = Tuple[str, str]  # (peer, rel)


@dataclass(frozen=True)
class CatalogEntry:
    rel: str
    size: int
    sha256: str
    mtime: int
    peer: str
    target_folder: str


class PerRootCatalog:
    def __init__(self, entries: Optional[Dict[Key, CatalogEntry]] = None) -> None:
        self._entries: Dict[Key, CatalogEntry] = dict(entries or {})

    # --- construction / rebuild --------------------------------------------------------
    @classmethod
    def from_wal(cls, wal: WalJournal) -> "PerRootCatalog":
        """Rebuild the catalog by replaying the WAL — the authoritative recovery path (SC-010)."""
        entries = {key: _entry_from_record(rec) for key, rec in wal.rebuild().items()}
        return cls(entries)

    # --- mutation (projection follows a WAL append) ------------------------------------
    def apply(self, rec: WalRecord) -> None:
        if rec.op == "put":
            self._entries[rec.key] = _entry_from_record(rec)
        elif rec.op == "remove":
            self._entries.pop(rec.key, None)

    # --- queries -----------------------------------------------------------------------
    def get(self, peer: str, rel: str) -> Optional[CatalogEntry]:
        return self._entries.get((peer, rel))

    def is_identical(self, peer: str, rel: str, sha256: str) -> bool:
        """Synchronise compare: does this peer already hold ``rel`` with the same SHA-256? (FR-034)."""
        entry = self._entries.get((peer, rel))
        return entry is not None and entry.sha256 == sha256

    def total_bytes(self) -> int:
        return sum(e.size for e in self._entries.values())

    def peer_bytes(self, peer: str) -> int:
        return sum(e.size for k, e in self._entries.items() if k[0] == peer)

    def __len__(self) -> int:
        return len(self._entries)

    def entries(self) -> Dict[Key, CatalogEntry]:
        return dict(self._entries)

    # --- snapshot (convenience only; the WAL is authoritative) -------------------------
    def save(self, path: Path) -> None:
        # A JSON list of entries — (peer, rel) is reconstructed from each entry's own fields, so peer
        # and rel may contain any characters (no separator to escape).
        obj = [asdict(v) for v in self._entries.values()]
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        Path(path).write_text(json.dumps(obj, ensure_ascii=False), encoding="utf-8")

    @classmethod
    def load(cls, path: Path) -> "PerRootCatalog":
        p = Path(path)
        if not p.exists():
            return cls()
        arr = json.loads(p.read_text(encoding="utf-8"))
        entries: Dict[Key, CatalogEntry] = {}
        for v in arr:
            e = CatalogEntry(**v)
            entries[(e.peer, e.rel)] = e
        return cls(entries)


def _entry_from_record(rec: WalRecord) -> CatalogEntry:
    return CatalogEntry(rel=rec.rel, size=rec.size, sha256=rec.sha256, mtime=rec.mtime,
                        peer=rec.peer, target_folder=rec.target_folder)
