"""Durable per-file provenance records (feature 040, US8; FR-037/SC-009).

An append-only audit log recording every file event — **transferred and rejected** — for 100% of
files, at ``<data_dir>/roots/<root>/provenance.log``. Contract: ``contracts/responder-store.md``.
"""

from __future__ import annotations

import json
import os
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import List, Optional


@dataclass(frozen=True)
class ProvenanceRecord:
    peer: str
    root: str
    target_path: str
    ts_start: int
    ts_commit: int
    sha256: str
    outcome: str            # transferred | skipped_identical | filtered_out | rejected
    reason: Optional[str] = None  # quota | perm | path | verify | None

    def to_line(self) -> str:
        return json.dumps(asdict(self), separators=(",", ":"), ensure_ascii=False)


class ProvenanceLog:
    def __init__(self, path: Path) -> None:
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)

    def record(self, rec: ProvenanceRecord) -> None:
        with open(self.path, "a", encoding="utf-8") as f:
            f.write(rec.to_line() + "\n")
            f.flush()
            os.fsync(f.fileno())

    def all(self) -> List[ProvenanceRecord]:
        if not self.path.exists():
            return []
        out: List[ProvenanceRecord] = []
        with open(self.path, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                try:
                    obj = json.loads(line)
                except json.JSONDecodeError:
                    continue
                out.append(ProvenanceRecord(**obj))
        return out
