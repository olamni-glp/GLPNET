"""WAL + message-file policy (research R4; data-model.md "WAL") — T016.

Append-only WAL records acceptance order and delivery state; message CONTENT
is placed by a configurable target file size:

- small messages (< half the target) share the current ``msg-<n>.dat``;
- ~file-size messages (half target … target) get their own ``msg-own-<id>.dat``;
- larger messages split across ``msg-part-<id>-<k>.dat`` parts of target size.

Recovery = WAL replay: the store is reconciled to the WAL, never the reverse.
A dense per-sender acceptance sequence (1..N, no holes) is asserted at
recovery; a hole is a NAMED loss event (:class:`GapEvent`, FR-010), never a
silent skip. A corrupt journal line is an explicit refusal
(:class:`WalCorrupt`, FR-011) — never silent loss. Acceptance is durable
(fsync) before :meth:`Wal.accept` returns (contract guarantee 1).
"""

from __future__ import annotations

import json
import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

#: Message states (data-model.md "State transitions").
STATES = ("journalled", "signalled", "fetched", "expired", "dead")

#: Retention classes (contract guarantee 6).
RETENTION_CLASSES = ("ephemeral", "time_windowed", "permanent")


class WalCorrupt(Exception):
    """Named fault: the journal (or a referenced message file) is unreadable or
    inconsistent. The tool REFUSES to continue rather than silently losing data
    (FR-011)."""


@dataclass(frozen=True)
class GapEvent:
    """A named per-sender sequence loss discovered at replay/fetch (FR-010)."""

    sender: str
    expected_seq: int
    got_seq: int


@dataclass
class MessageMeta:
    """Replayed per-message state (mirrors msmesh.message)."""

    sender: str
    seq: int
    mailbox: str
    target: str
    size: int
    content_ref: str
    retention: str
    state: str = "journalled"


@dataclass
class WalState:
    """The state a replay reconstructs: messages, delivery positions, gaps."""

    messages: dict = field(default_factory=dict)   # (sender, seq) -> MessageMeta
    positions: dict = field(default_factory=dict)  # (peer, direction) -> {"high_water": int, "seen": list}
    gaps: list = field(default_factory=list)       # [GapEvent]


class Wal:
    """One node's write-ahead log + message files under ``root`` (gitignored)."""

    def __init__(self, root: Path, target_file_size: int = 64 * 1024) -> None:
        if target_file_size <= 0:
            raise ValueError("target_file_size must be positive")
        self.root = Path(root)
        self.target = target_file_size
        self.root.mkdir(parents=True, exist_ok=True)
        self._log_path = self.root / "wal-1.log"
        self._shared_no = self._next_shared_no()

    # ------------------------------------------------------------------ journal
    def _append(self, record: dict) -> None:
        line = json.dumps(record, separators=(",", ":"), ensure_ascii=False)
        with open(self._log_path, "a", encoding="utf-8") as f:
            f.write(line + "\n")
            f.flush()
            os.fsync(f.fileno())  # durable before acknowledgement (guarantee 1)

    def accept(self, sender: str, seq: int, mailbox: str, target: str,
               content: bytes, retention: str = "permanent") -> MessageMeta:
        """Journal one accepted message: place content per the size policy, then
        append the acceptance record. Durable before return."""
        if retention not in RETENTION_CLASSES:
            raise ValueError(f"unknown retention class {retention!r}")
        content_ref = self._place(sender, seq, content)
        meta = MessageMeta(sender=sender, seq=seq, mailbox=mailbox, target=target,
                           size=len(content), content_ref=content_ref, retention=retention)
        self._append({
            "rec": "accepted", "sender": sender, "seq": seq, "mailbox": mailbox,
            "target": target, "size": len(content), "content_ref": content_ref,
            "retention": retention,
        })
        return meta

    def mark(self, sender: str, seq: int, state: str) -> None:
        """Journal a message state transition (signalled/fetched/expired/dead)."""
        if state not in STATES:
            raise ValueError(f"unknown message state {state!r}")
        self._append({"rec": "state", "sender": sender, "seq": seq, "state": state})

    def advance_position(self, peer: str, direction: str, high_water: int,
                         seen_sparse: Optional[list] = None) -> None:
        """Journal a delivery-position advance (the exactly-once floor, R7)."""
        if direction not in ("inbound", "outbound"):
            raise ValueError(f"direction must be inbound|outbound, got {direction!r}")
        self._append({"rec": "position", "peer": peer, "direction": direction,
                      "high_water": high_water, "seen": seen_sparse or []})

    def record_gap(self, sender: str, expected_seq: int, got_seq: int) -> None:
        """Journal a named gap event observed at fetch time (FR-010)."""
        self._append({"rec": "gap", "sender": sender,
                      "expected_seq": expected_seq, "got_seq": got_seq})

    # ------------------------------------------------------------------ content placement (R4)
    def _next_shared_no(self) -> int:
        existing = sorted(int(p.stem.split("-")[1]) for p in self.root.glob("msg-*.dat")
                          if p.stem.count("-") == 1 and p.stem.split("-")[1].isdigit())
        return existing[-1] if existing else 1

    def _place(self, sender: str, seq: int, content: bytes) -> str:
        size = len(content)
        if size > self.target:  # large: split across parts of target size
            parts = 0
            for k in range(0, size, self.target):
                part = self.root / f"msg-part-{sender}-{seq}-{parts}.dat"
                part.write_bytes(content[k:k + self.target])
                parts += 1
            return f"split:{sender}:{seq}:{parts}"
        if size * 2 >= self.target:  # ~file-size: its own file
            own = self.root / f"msg-own-{sender}-{seq}.dat"
            own.write_bytes(content)
            return f"own:{own.name}"
        # small: append to the current shared file, rotating at the target size
        shared = self.root / f"msg-{self._shared_no}.dat"
        offset = shared.stat().st_size if shared.exists() else 0
        if offset + size > self.target and offset > 0:
            self._shared_no += 1
            shared = self.root / f"msg-{self._shared_no}.dat"
            offset = 0
        with open(shared, "ab") as f:
            f.write(content)
            f.flush()
            os.fsync(f.fileno())
        return f"shared:{shared.name}:{offset}:{size}"

    def read_content(self, content_ref: str) -> bytes:
        """Load a message's content back from its placement ref."""
        kind, _, rest = content_ref.partition(":")
        try:
            if kind == "shared":
                name, off, ln = rest.rsplit(":", 2)
                with open(self.root / name, "rb") as f:
                    f.seek(int(off))
                    data = f.read(int(ln))
                if len(data) != int(ln):
                    raise WalCorrupt(f"shared ref {content_ref!r}: short read {len(data)} < {ln}")
                return data
            if kind == "own":
                return (self.root / rest).read_bytes()
            if kind == "split":
                sender, seq, parts = rest.rsplit(":", 2)
                return b"".join(
                    (self.root / f"msg-part-{sender}-{seq}-{k}.dat").read_bytes()
                    for k in range(int(parts)))
        except OSError as exc:
            raise WalCorrupt(f"content ref {content_ref!r} unreadable: {exc}") from exc
        raise WalCorrupt(f"unknown content ref kind in {content_ref!r}")

    # ------------------------------------------------------------------ replay (recovery)
    def replay(self) -> WalState:
        """Rebuild state from the journal. Corrupt line ⇒ :class:`WalCorrupt`
        (explicit refusal); per-sender acceptance holes ⇒ named gaps in the
        result (FR-010). The store is reconciled to THIS result."""
        state = WalState()
        if not self._log_path.exists():
            return state
        seen_seqs: dict = {}  # sender -> set of accepted seqs
        with open(self._log_path, "r", encoding="utf-8") as f:
            for lineno, raw in enumerate(f, start=1):
                raw = raw.strip()
                if not raw:
                    continue
                try:
                    rec = json.loads(raw)
                    kind = rec["rec"]
                except (json.JSONDecodeError, KeyError, TypeError) as exc:
                    raise WalCorrupt(f"{self._log_path.name}:{lineno}: unreadable record: {exc}") from exc
                if kind == "accepted":
                    key = (rec["sender"], rec["seq"])
                    state.messages[key] = MessageMeta(
                        sender=rec["sender"], seq=rec["seq"], mailbox=rec["mailbox"],
                        target=rec["target"], size=rec["size"],
                        content_ref=rec["content_ref"], retention=rec["retention"])
                    seen_seqs.setdefault(rec["sender"], set()).add(rec["seq"])
                elif kind == "state":
                    key = (rec["sender"], rec["seq"])
                    if key not in state.messages:
                        raise WalCorrupt(
                            f"{self._log_path.name}:{lineno}: state for unjournalled message {key}")
                    state.messages[key].state = rec["state"]
                elif kind == "position":
                    state.positions[(rec["peer"], rec["direction"])] = {
                        "high_water": rec["high_water"], "seen": rec.get("seen", [])}
                elif kind == "gap":
                    state.gaps.append(GapEvent(rec["sender"], rec["expected_seq"], rec["got_seq"]))
                else:
                    raise WalCorrupt(f"{self._log_path.name}:{lineno}: unknown record kind {kind!r}")
        # Dense-sequence assertion (R4): holes in a sender's acceptance run are NAMED.
        for sender, seqs in seen_seqs.items():
            expected = 1
            for got in sorted(seqs):
                if got != expected:
                    state.gaps.append(GapEvent(sender, expected, got))
                expected = got + 1
        return state
