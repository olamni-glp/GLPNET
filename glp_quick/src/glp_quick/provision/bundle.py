"""GQP1 bundle build + QR chunking/assembly (067 FR-001/FR-007, contracts/payload-contract.md).

Chunk line (ASCII, one per QR code)::

    GQP1|<bundle_id>|<index>/<total>|<crc32>|<slice>            (chunks 1..total-1)
    GQP1|<bundle_id>|<total>/<total>|<crc32>|<slice>|<sha16>    (final chunk)

Assembly is **order-independent** and names exactly what is wrong: a failing CRC identifies the
chunk, a gap identifies the missing indices, and a wrong leading tag fails fast as a version
mismatch rather than being best-effort parsed.
"""

from __future__ import annotations

import base64
import hashlib
import json
import secrets
import zlib
from dataclasses import dataclass
from typing import Iterable

TAG = "GQP1"
VERSION = 1

#: Slice size in base64url characters. Chosen to sit inside QR version 25 at error-correction M
#: (~1200 alphanumeric chars) with generous headroom for the header, so every chunk stays
#: comfortably scannable by a phone camera at display distance.
SLICE_LEN = 800

#: Hard bound on chunk count (FR-007 edge case). Beyond this the producer refuses rather than
#: emitting an unscannable wall of codes.
MAX_CHUNKS = 8

BUNDLE_ID_LEN = 8
_B32 = "abcdefghijklmnopqrstuvwxyz234567"


class AssemblyError(Exception):
    """Assembly failed. ``token`` carries the contract's error token for the consumer."""

    def __init__(self, token: str, detail: str = "") -> None:
        super().__init__(f"{token}{(': ' + detail) if detail else ''}")
        self.token = token
        self.detail = detail


class TooManyChunks(Exception):
    """The bundle would need more than :data:`MAX_CHUNKS` QR codes."""


@dataclass(frozen=True)
class Bundle:
    """The plaintext a joining device needs. Exists only transiently in rendered form."""

    endpoint_host: str
    endpoint_port: int
    trunk_spki_pin: str
    device_cert_pem: str
    device_key_pem: str
    session_id: str

    def to_json(self) -> bytes:
        return json.dumps(
            {
                "v": VERSION,
                "endpoint": {"host": self.endpoint_host, "port": self.endpoint_port},
                "trunk_spki_pin": self.trunk_spki_pin,
                "device_cert_pem": self.device_cert_pem,
                "device_key_pem": self.device_key_pem,
                "session_id": self.session_id,
            },
            separators=(",", ":"),
            sort_keys=True,
        ).encode("utf-8")

    @classmethod
    def from_json(cls, raw: bytes) -> "Bundle":
        obj = json.loads(raw.decode("utf-8"))
        if obj.get("v") != VERSION:
            raise AssemblyError("version_mismatch", f"payload v={obj.get('v')!r}, expected {VERSION}")
        ep = obj["endpoint"]
        return cls(
            endpoint_host=ep["host"],
            endpoint_port=int(ep["port"]),
            trunk_spki_pin=obj["trunk_spki_pin"],
            device_cert_pem=obj["device_cert_pem"],
            device_key_pem=obj["device_key_pem"],
            session_id=obj["session_id"],
        )


def new_bundle_id() -> str:
    return "".join(secrets.choice(_B32) for _ in range(BUNDLE_ID_LEN))


def _crc32(text: str) -> str:
    return format(zlib.crc32(text.encode("ascii")) & 0xFFFFFFFF, "08x")


def _sha16(text: str) -> str:
    return hashlib.sha256(text.encode("ascii")).hexdigest()[:32]


def chunk(envelope: bytes, bundle_id: str | None = None) -> list[str]:
    """Split an encrypted envelope into self-identifying, integrity-checked chunk lines."""
    bundle_id = bundle_id or new_bundle_id()
    payload_b64 = base64.urlsafe_b64encode(envelope).decode("ascii").rstrip("=")
    slices = [payload_b64[i : i + SLICE_LEN] for i in range(0, len(payload_b64), SLICE_LEN)] or [""]
    total = len(slices)
    if total > MAX_CHUNKS:
        raise TooManyChunks(
            f"bundle needs {total} chunks, limit is {MAX_CHUNKS} — refusing to emit an "
            f"unscannable code set"
        )
    sha16 = _sha16(payload_b64)
    lines = []
    for idx, sl in enumerate(slices, start=1):
        line = f"{TAG}|{bundle_id}|{idx}/{total}|{_crc32(sl)}|{sl}"
        if idx == total:
            line = f"{line}|{sha16}"
        lines.append(line)
    return lines


def assemble(lines: Iterable[str]) -> bytes:
    """Reassemble chunk lines (any order) into the envelope bytes.

    Raises :class:`AssemblyError` with the contract's token on any defect.
    """
    seen: dict[int, str] = {}
    total: int | None = None
    bundle_id: str | None = None
    sha16: str | None = None

    for raw in lines:
        line = raw.strip()
        if not line:
            continue
        parts = line.split("|")
        if len(parts) < 5 or parts[0] != TAG:
            raise AssemblyError("version_mismatch", f"unrecognised chunk header {parts[0]!r}")
        _, bid, position, crc, sl = parts[:5]
        if bundle_id is None:
            bundle_id = bid
        elif bid != bundle_id:
            raise AssemblyError("conflicting_chunk", f"bundle id {bid!r} != {bundle_id!r}")
        try:
            idx_s, total_s = position.split("/")
            idx, this_total = int(idx_s), int(total_s)
        except ValueError as exc:
            raise AssemblyError("corrupt_chunk", f"bad position field {position!r}") from exc
        if total is None:
            total = this_total
        elif this_total != total:
            raise AssemblyError("conflicting_chunk", f"total {this_total} != {total}")
        if _crc32(sl) != crc:
            raise AssemblyError("corrupt_chunk", str(idx))
        if idx in seen and seen[idx] != sl:
            raise AssemblyError("conflicting_chunk", str(idx))
        seen[idx] = sl
        if idx == this_total and len(parts) >= 6:
            sha16 = parts[5]

    if total is None:
        raise AssemblyError("missing", "no chunks supplied")

    missing = [str(i) for i in range(1, total + 1) if i not in seen]
    if missing:
        raise AssemblyError("missing", ",".join(missing))

    payload_b64 = "".join(seen[i] for i in range(1, total + 1))
    if sha16 is None:
        raise AssemblyError("bundle_integrity", "final chunk carried no whole-bundle digest")
    if _sha16(payload_b64) != sha16:
        raise AssemblyError("bundle_integrity", "whole-bundle digest mismatch")

    padding = "=" * (-len(payload_b64) % 4)
    return base64.urlsafe_b64decode(payload_b64 + padding)
