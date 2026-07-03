"""GLP-message envelope (L5) + mesh routing + the GLP-REPL ↔ link bridge (FR-008/008b, FR-018).

The wire/message contract (``contracts/wire-contract.md`` §GLP-message envelope) layers a small L5
envelope on top of spec 025's reliability sublayer (L4: framing version+CRC32+fragment, seq/dedup,
reorder, epoch/fencing, backpressure window). **025 owns reliability; this module owns only the
envelope shape, the ground-relay discipline, and the ``to``/``broadcast`` routing decision** — it
does not re-implement sequencing or dedup (FR-018, constitution VIII).

Envelope (verbatim from the contract)::

    { msg_id, from, to, seq, payload }
      msg_id  : unique per message (dedup key in concert with 025 seq)
      from    : endpoint_id of the sending GLP REPL endpoint
      to      : endpoint_id | "broadcast"
      seq     : 025 per-link sequence number
      payload : a GROUND GLP term (025 ground-relay — no _w / _r placeholders cross the wire)

The bridge that actually spawns/pumps a GLP REPL process (``out/csharp/glp_repl`` default) onto a
``stacks.base.Handle`` is **behaviour** and lands in US1 (T019) / US2 mesh (T027); this module is the
shared contract those build on (FR-017 skeleton-before-behaviour).
"""

from __future__ import annotations

import json
import uuid
from dataclasses import dataclass, field
from typing import Iterable, Optional, Sequence

#: The mesh fan-out sentinel for the envelope ``to`` field (FR-008b).
BROADCAST = "broadcast"

#: An endpoint identifier (a participating GLP REPL endpoint).
EndpointId = str

#: spec 025 madGLP globalize placeholders that MUST NOT cross the wire (only ground terms relay —
#: data-model.md §GLP message; typed-glp-manual.md §12 reserved constants ``_w(p,i)`` / ``_r(p,i)``).
_PLACEHOLDER_TOKENS = ("_w(", "_r(")


class GroundRelayViolation(ValueError):
    """Raised when a payload carries a non-ground spec 025 placeholder (``_w(`` / ``_r(``).

    Ground-relay is a spec 025 discipline (FR-018): only ground GLP terms relay. Sending a
    placeholder is a caller bug upstream of the link (the term should have been grounded before
    egress), not a condition to tolerate — so we STOP rather than ship a malformed term.
    """


def assert_ground_relay(payload: str) -> str:
    """Return ``payload`` unchanged iff it carries no ``_w(`` / ``_r(`` placeholder; else raise.

    A conservative, contract-level enforcement of 025 ground-relay at the envelope boundary — the
    authoritative groundness is established by 025's globalize/localize above the link; this guard
    refuses the two reserved placeholder forms the contract names explicitly.
    """
    for token in _PLACEHOLDER_TOKENS:
        if token in payload:
            raise GroundRelayViolation(
                f"payload carries a non-ground placeholder {token!r}; only ground GLP terms relay "
                f"(spec 025 ground-relay, wire-contract.md §GLP-message envelope)"
            )
    return payload


def new_msg_id() -> str:
    """A fresh per-message id (dedup key, in concert with the 025 ``seq``)."""
    return uuid.uuid4().hex


def parse_addressed(text: str, default_to: EndpointId) -> "tuple[EndpointId, str]":
    """Route a composed message to a peer (FR-006): ``@<peer> body`` -> ``(peer, body)``; otherwise
    ``(default_to, text)``.

    The single source of truth for the ``@name`` directed-send convention, shared by the plain link
    console and the ``--tui`` terminal so the two cannot drift. (The terminal previously advertised
    ``@<to>`` in its help but never parsed it -- every message silently went to the default peer.)
    """
    if text.startswith("@"):
        head, _, rest = text[1:].partition(" ")
        return head, rest
    return default_to, text


@dataclass(frozen=True)
class GlpMessage:
    """One L5 GLP-message envelope (``contracts/wire-contract.md``).

    ``sender`` serializes to the wire key ``from`` (a Python keyword). ``seq`` is assigned by the
    025 sublayer at egress; it is ``None`` until the link layer stamps it.
    """

    sender: EndpointId               # wire key: "from"
    to: EndpointId                   # an endpoint_id, or BROADCAST
    payload: str                     # a GROUND GLP term (text)
    msg_id: str = field(default_factory=new_msg_id)
    seq: Optional[int] = None        # 025 per-link sequence number (stamped by the sublayer)

    def __post_init__(self) -> None:
        if not self.sender:
            raise ValueError("GlpMessage.sender (from) must be a non-empty endpoint_id")
        if not self.to:
            raise ValueError("GlpMessage.to must be an endpoint_id or BROADCAST")
        assert_ground_relay(self.payload)

    @property
    def is_broadcast(self) -> bool:
        return self.to == BROADCAST

    def to_wire(self) -> bytes:
        """Serialize to the L5 envelope (UTF-8 JSON) carried inside the 025 frame.

        Uses the contract's exact field names (``from``, not ``sender``).
        """
        obj = {
            "msg_id": self.msg_id,
            "from": self.sender,
            "to": self.to,
            "seq": self.seq,
            "payload": self.payload,
        }
        return json.dumps(obj, separators=(",", ":"), ensure_ascii=False).encode("utf-8")

    @classmethod
    def from_wire(cls, data: bytes) -> "GlpMessage":
        """Parse an L5 envelope (UTF-8 JSON) back into a :class:`GlpMessage`.

        Re-checks ground-relay on ingress (a peer must not have shipped a placeholder).
        """
        obj = json.loads(data.decode("utf-8"))
        missing = {"msg_id", "from", "to", "payload"} - obj.keys()
        if missing:
            raise ValueError(f"malformed GLP-message envelope; missing fields {sorted(missing)}")
        return cls(
            sender=obj["from"],
            to=obj["to"],
            payload=obj["payload"],
            msg_id=obj["msg_id"],
            seq=obj.get("seq"),
        )


def route(message: GlpMessage, endpoints: Iterable[EndpointId]) -> list[EndpointId]:
    """The mesh routing decision (FR-008b): the target endpoint_ids for ``message``.

    - ``to == BROADCAST`` → every participating endpoint **except the sender** (fan-out).
    - otherwise → ``[to]`` iff that endpoint is currently participating, else ``[]``.

    A pure function — the server's actual delivery (over per-link 025 channels) is US2 (T027).
    Self-delivery is excluded for broadcast so a sender does not receive its own fan-out.
    """
    members = list(endpoints)
    if message.is_broadcast:
        return [ep for ep in members if ep != message.sender]
    return [message.to] if message.to in members else []
