"""``@name`` resolution against the feature-036 authenticated peer set (R3; FR-040/SC-011).

The 037 prototype advertised ``@<to> message`` but only ever *parsed* the prefix (via
:func:`glp_quick.repl_link.parse_addressed`) — it never checked the target against the live mesh, so an
``@typo`` silently went to the default peer. This module adds the missing **resolution** step: an
explicit ``@name`` is resolved against ``handle.peers()``; an unknown name yields a structured
"unknown peer" result that the caller **reports** — it is never silently redirected to the default
peer (FR-040). A plain (unaddressed) line still goes to the default peer, and an explicit
``@broadcast`` fans out.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Callable, Optional, Sequence, Union

from glp_quick.repl_link import BROADCAST, parse_addressed

#: A live peer set, or a zero-arg provider of one (usually ``handle.peers``).
PeerSource = Union[Sequence[str], Callable[[], Sequence[str]]]


@dataclass(frozen=True)
class Resolution:
    """The outcome of resolving a composed line's target.

    ``ok`` — the message may be sent; ``target`` is the resolved endpoint id (or ``BROADCAST``).
    Not ``ok`` — an explicit ``@name`` did not match a live peer; ``error`` is a user-facing report and
    ``unknown_name`` is the name that missed. In both cases ``payload`` is the body (``@name`` stripped).
    ``addressed`` records whether the line carried an explicit ``@`` prefix.
    """

    ok: bool
    target: Optional[str]
    payload: str
    addressed: bool
    error: Optional[str] = None
    unknown_name: Optional[str] = None


def _members(peers: PeerSource) -> list:
    return list(peers() if callable(peers) else peers)


def resolve(text: str, default_to: str, peers: PeerSource) -> Resolution:
    """Resolve a composed line to a send target (FR-040).

    - ``@broadcast body`` → fan-out (``BROADCAST``).
    - ``@peer body`` where ``peer`` is in the live peer set → directed to ``peer``.
    - ``@typo body`` where ``typo`` is **not** a live peer → ``ok=False`` with a report; **never**
      falls back to ``default_to``.
    - ``body`` (no ``@``) → the ``default_to`` peer (the mesh registers it on first contact, so it need
      not already appear in ``peers()``).
    """
    addressed = text.startswith("@")
    to, payload = parse_addressed(text, default_to)
    members = _members(peers)

    if to == BROADCAST:
        return Resolution(True, BROADCAST, payload, addressed)
    if to in members:
        return Resolution(True, to, payload, addressed)
    if not addressed:
        # The default peer (e.g. "server") is legitimate even before it appears in the mesh list.
        return Resolution(True, to, payload, addressed)
    known = ", ".join(members) if members else "(none connected)"
    return Resolution(
        False,
        None,
        payload,
        addressed,
        error=f"unknown peer '{to}' — known peers: {known}",
        unknown_name=to,
    )
