"""Hop-to-hop message shapes — signal / fetch / fetch_batch / friend_lookup / friend_reply.

Authoritative contract: specs/063-wave-5-consolidated-captured-triad/
contracts/mesh-messaging-protocol.md ("Messages (hop-to-hop)"). Shapes are
logical; this module encodes them as ground JSON payloads carried over ANY
spec-025 link transport (research R3 — transport-agnostic; control and
data share the link, no separate control plane in the first hop).

Wire form: one UTF-8 JSON object per payload, ``{"v": 1, "kind": ...}``.
Message content bytes travel base64-encoded. A malformed or unknown payload
raises :class:`ProtocolError` — an explicit named fault, never a silent
drop (FR-011).
"""

from __future__ import annotations

import base64
import json
from dataclasses import dataclass, field

PROTOCOL_VERSION = 1

#: friend_reply address value meaning "station not in the local registry".
UNKNOWN = "unknown"


class ProtocolError(ValueError):
    """Named fault for a malformed, unknown, or version-mismatched payload."""


@dataclass(frozen=True)
class Signal:
    """Content awaits; carries NO content. Idempotent — re-signalling is harmless."""

    holder_station: str
    mailbox_id: str
    high_water_seq: int


@dataclass(frozen=True)
class Fetch:
    """Pull request from a position; resumable (any from_seq <= high-water)."""

    requester_station: str
    mailbox_id: str
    from_seq: int
    max_count: int


@dataclass(frozen=True)
class BatchMessage:
    """One delivered message inside a fetch_batch, identified by (sender, seq) (R7)."""

    sender_station: str
    sender_seq: int
    content: bytes


@dataclass(frozen=True)
class GapMarker:
    """Explicit hole in a batch — the recipient records it as a gap_event (FR-010).

    A batch NEVER contains a gap silently (contract guarantee 3).
    """

    expected_seq: int
    got_seq: int


@dataclass(frozen=True)
class FetchBatch:
    """Ordered batch of messages (by per-sender seq) with explicit gap markers."""

    mailbox_id: str
    entries: tuple[BatchMessage | GapMarker, ...] = field(default_factory=tuple)
    high_water_seq: int = 0


@dataclass(frozen=True)
class FriendLookup:
    """"Do you know station X?" — local registry only, no transitive search (R8)."""

    asker: str
    target_station: str


@dataclass(frozen=True)
class FriendReply:
    """Answer to a friend_lookup: an address, or :data:`UNKNOWN`."""

    target_station: str
    address: str  # a host/URL/IP, or the UNKNOWN sentinel

    @property
    def is_unknown(self) -> bool:
        return self.address == UNKNOWN


Payload = Signal | Fetch | FetchBatch | FriendLookup | FriendReply

_KINDS: dict[type, str] = {
    Signal: "signal",
    Fetch: "fetch",
    FetchBatch: "fetch_batch",
    FriendLookup: "friend_lookup",
    FriendReply: "friend_reply",
}


def encode(payload: Payload) -> bytes:
    """Encode a payload to its ground JSON wire form (UTF-8 bytes)."""
    kind = _KINDS.get(type(payload))
    if kind is None:
        raise ProtocolError(f"not a protocol payload: {type(payload).__name__}")
    obj: dict = {"v": PROTOCOL_VERSION, "kind": kind}
    if isinstance(payload, Signal):
        obj.update(
            holder_station=payload.holder_station,
            mailbox_id=payload.mailbox_id,
            high_water_seq=payload.high_water_seq,
        )
    elif isinstance(payload, Fetch):
        obj.update(
            requester_station=payload.requester_station,
            mailbox_id=payload.mailbox_id,
            from_seq=payload.from_seq,
            max_count=payload.max_count,
        )
    elif isinstance(payload, FetchBatch):
        entries = []
        for e in payload.entries:
            if isinstance(e, BatchMessage):
                entries.append(
                    {
                        "kind": "msg",
                        "sender_station": e.sender_station,
                        "sender_seq": e.sender_seq,
                        "content_b64": base64.b64encode(e.content).decode("ascii"),
                    }
                )
            else:
                entries.append(
                    {
                        "kind": "gap",
                        "expected_seq": e.expected_seq,
                        "got_seq": e.got_seq,
                    }
                )
        obj.update(
            mailbox_id=payload.mailbox_id,
            entries=entries,
            high_water_seq=payload.high_water_seq,
        )
    elif isinstance(payload, FriendLookup):
        obj.update(asker=payload.asker, target_station=payload.target_station)
    else:  # FriendReply
        obj.update(target_station=payload.target_station, address=payload.address)
    return json.dumps(obj, separators=(",", ":")).encode("utf-8")


def _require(obj: dict, name: str, typ: type):
    if name not in obj:
        raise ProtocolError(f"missing field {name!r} in {obj.get('kind', '?')} payload")
    value = obj[name]
    # bool is an int subclass; a boolean where an int is required is malformed.
    if not isinstance(value, typ) or (typ is int and isinstance(value, bool)):
        raise ProtocolError(
            f"field {name!r} must be {typ.__name__}, got {type(value).__name__}"
        )
    return value


def _decode_entry(raw: object) -> BatchMessage | GapMarker:
    if not isinstance(raw, dict):
        raise ProtocolError(f"batch entry must be an object, got {type(raw).__name__}")
    entry_kind = _require(raw, "kind", str)
    if entry_kind == "msg":
        content_b64 = _require(raw, "content_b64", str)
        try:
            content = base64.b64decode(content_b64, validate=True)
        except Exception as exc:
            raise ProtocolError(f"invalid content_b64 in batch entry: {exc}") from exc
        return BatchMessage(
            sender_station=_require(raw, "sender_station", str),
            sender_seq=_require(raw, "sender_seq", int),
            content=content,
        )
    if entry_kind == "gap":
        return GapMarker(
            expected_seq=_require(raw, "expected_seq", int),
            got_seq=_require(raw, "got_seq", int),
        )
    raise ProtocolError(f"unknown batch entry kind {entry_kind!r}")


def decode(data: bytes) -> Payload:
    """Decode a wire payload; raise :class:`ProtocolError` on any malformation."""
    try:
        obj = json.loads(data.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ProtocolError(f"payload is not valid UTF-8 JSON: {exc}") from exc
    if not isinstance(obj, dict):
        raise ProtocolError(f"payload must be a JSON object, got {type(obj).__name__}")
    version = _require(obj, "v", int)
    if version != PROTOCOL_VERSION:
        raise ProtocolError(f"unsupported protocol version {version} (expected {PROTOCOL_VERSION})")
    kind = _require(obj, "kind", str)
    if kind == "signal":
        return Signal(
            holder_station=_require(obj, "holder_station", str),
            mailbox_id=_require(obj, "mailbox_id", str),
            high_water_seq=_require(obj, "high_water_seq", int),
        )
    if kind == "fetch":
        return Fetch(
            requester_station=_require(obj, "requester_station", str),
            mailbox_id=_require(obj, "mailbox_id", str),
            from_seq=_require(obj, "from_seq", int),
            max_count=_require(obj, "max_count", int),
        )
    if kind == "fetch_batch":
        raw_entries = _require(obj, "entries", list)
        return FetchBatch(
            mailbox_id=_require(obj, "mailbox_id", str),
            entries=tuple(_decode_entry(e) for e in raw_entries),
            high_water_seq=_require(obj, "high_water_seq", int),
        )
    if kind == "friend_lookup":
        return FriendLookup(
            asker=_require(obj, "asker", str),
            target_station=_require(obj, "target_station", str),
        )
    if kind == "friend_reply":
        return FriendReply(
            target_station=_require(obj, "target_station", str),
            address=_require(obj, "address", str),
        )
    raise ProtocolError(f"unknown payload kind {kind!r}")
