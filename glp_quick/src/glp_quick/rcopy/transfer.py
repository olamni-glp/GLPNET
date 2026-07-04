"""Chunked file receive with commit-on-complete (feature 040, US8; FR-039).

A file is written to a temp part, fsynced, SHA-256-verified against the manifest, then **atomically
renamed** into place — the rename is the only commit point (all-or-nothing). An interruption before the
rename leaves only a discardable ``.tmp`` part; nothing is catalogued, counted toward synchronise, or
counted toward quota. Path-safety (FR-033) rejects any target that escapes the landing root
(traversal / symlink). Contract: ``contracts/responder-store.md``.
"""

from __future__ import annotations

import base64
import hashlib
import os
from dataclasses import dataclass
from pathlib import Path
from typing import List, Optional, Tuple

#: Bounded chunk size (raw bytes) — well under the 025 frame/line limit; large files span many chunks.
CHUNK_SIZE = 16384


@dataclass(frozen=True)
class CommitResult:
    ok: bool
    sha256: str = ""
    target: Optional[Path] = None
    reason: str = ""   # "" | "verify" | "path"


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def safe_target(landing_root: Path, rel: str) -> Optional[Path]:
    """Resolve ``landing_root/rel`` and ensure it stays within ``landing_root``.

    Returns the resolved target path, or ``None`` if it escapes the root (``reject(path)``) — including
    ``..`` traversal and symlink escape (``resolve`` follows symlinks before the containment check).
    """
    root = Path(landing_root).resolve()
    target = (root / rel).resolve()
    try:
        target.relative_to(root)
    except ValueError:
        return None
    return target


def commit_file(landing_root: Path, rel: str, data: bytes, expected_sha: str) -> CommitResult:
    """Commit ``data`` to ``landing_root/rel`` all-or-nothing (FR-039). Path-safe + SHA-256-verified."""
    target = safe_target(landing_root, rel)
    if target is None:
        return CommitResult(False, reason="path")
    actual = sha256_bytes(data)
    if expected_sha and actual != expected_sha:
        return CommitResult(False, sha256=actual, reason="verify")  # discard: nothing written to place
    root = Path(landing_root).resolve()
    tmp = root / ".tmp" / (rel + ".part")
    tmp.parent.mkdir(parents=True, exist_ok=True)
    with open(tmp, "wb") as f:
        f.write(data)
        f.flush()
        os.fsync(f.fileno())
    target.parent.mkdir(parents=True, exist_ok=True)
    os.replace(tmp, target)  # atomic rename — the ONLY commit point
    return CommitResult(True, sha256=actual, target=target)


def chunk_bytes(data: bytes, chunk_size: int = CHUNK_SIZE) -> List[Tuple[int, str]]:
    """Split ``data`` into ``(seq, base64)`` chunks for ``rcopy_chunk`` messages."""
    if not data:
        return [(0, "")]
    out: List[Tuple[int, str]] = []
    seq = 0
    for i in range(0, len(data), chunk_size):
        out.append((seq, base64.b64encode(data[i:i + chunk_size]).decode("ascii")))
        seq += 1
    return out


def assemble_chunks(chunks: List[Tuple[int, str]]) -> bytes:
    """Reassemble ``(seq, base64)`` chunks (any order) back into the original bytes."""
    return b"".join(base64.b64decode(b64) for _seq, b64 in sorted(chunks, key=lambda c: c[0]))
