"""The uniform per-stack data-plane contract (``StackAdapter``) — FR-009/FR-010.

The Python control plane drives every data-plane stack through this one ABC so the stacks are
interchangeable **at the channel-link contract** (not at QUIC termination). ``csharp`` is the
cross-platform reference (must reach the full real-QUIC LAN demo first, FR-010); ``gleam`` is the
staged second stack in two deployment profiles (A: AtomVM + native QUIC side-process; C: full
BEAM + ``quicer``/MsQuic in-process).

Authoritative source: ``specs/036-http3-quic-ws-link/contracts/stack-adapter-contract.md``.

This module is the **skeleton** (FR-017 skeleton-before-behaviour): it fixes the contract; the
per-stack bodies (``csharp.py``, ``gleam.py``) implement it later, behind the gates (T013/T013a,
FR-010). Nothing here opens a QUIC endpoint.
"""

from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING, Literal, Optional, Sequence

if TYPE_CHECKING:  # avoid an import cycle — repl_link (L5 envelope) sits above the adapters.
    from glp_quick.repl_link import GlpMessage

#: Where genuine QUIC is actually terminated. ``gleam`` reports this **honestly** (Decision 8,
#: constitution II): Profile C terminates in-process; Profile A delegates to a native side-process.
QuicTermination = Literal["in_process", "side_process"]

#: The two stack identities behind the identical CLI/wire surface (FR-010).
StackName = Literal["csharp", "gleam"]

#: Gleam deployment profile (``gleam`` only). A: AtomVM + native QUIC side-process;
#: C: full BEAM + ``quicer``/MsQuic in-process.
GleamProfile = Literal["a", "c"]

#: Which GLP REPL is bridged to the link endpoint (``csharp`` is the mandated default).
ReplKind = Literal["csharp", "dart"]


@dataclass(frozen=True)
class Status:
    """Liveness of a supervised data-plane runtime (the result of :meth:`StackAdapter.health`).

    ``state`` is a Link/Server state-machine token from data-model.md
    (e.g. ``starting`` / ``listening`` / ``serving`` / ``linked`` / ``draining`` /
    ``closed`` / ``failed``); ``alive`` is the coarse up/down used by supervision; ``detail``
    carries a human-readable reason (e.g. an FR-019 failure token on ``failed``).
    """

    state: str
    alive: bool
    detail: Optional[str] = None


class Handle(ABC):
    """One supervised, established link end exposing the GLP-message I/O seam (FR-008).

    ``repl_link.py`` bridges a GLP REPL endpoint to this seam. The handle carries **ground**
    GLP-message envelopes (spec 025 ground-relay discipline) — never raw transport bytes; the
    QUIC/WS framing lives below, in the stack runtime.
    """

    @property
    @abstractmethod
    def link_id(self) -> str:
        """The stable per-link identity (spec 025 ``LinkId``), stringified for the control plane."""

    @abstractmethod
    def send(self, message: "GlpMessage") -> None:
        """Submit one GLP-message envelope toward its ``to`` target (or ``broadcast``).

        Full-duplex (FR-008a): a send may be outstanding while a recv is pending. Ordering,
        dedup, and backpressure are spec 025's concern below this seam (FR-018).
        """

    @abstractmethod
    def recv(self, timeout: Optional[float] = None) -> "Optional[GlpMessage]":
        """Receive the next inbound GLP-message envelope, or ``None`` on graceful link close.

        ``timeout`` is seconds (``None`` = block). A dropped/faulted link surfaces as a clear
        terminal signal (FR-019), never a silent hang.
        """

    @abstractmethod
    def peers(self) -> Sequence[str]:
        """The endpoint_ids reachable over the duplex mesh from this end (FR-008b)."""


class StackAdapter(ABC):
    """The uniform contract every data-plane stack implements (FR-009/FR-010).

    The operator-visible CLI/wire/handshake surface is identical across implementations
    (SC-006); only the body of these methods differs per stack.
    """

    @abstractmethod
    def name(self) -> StackName:
        """``"csharp"`` | ``"gleam"``."""

    @abstractmethod
    def profile(self) -> "Optional[GleamProfile]":
        """``gleam`` only: ``"a"`` | ``"c"``. ``None`` for stacks without profiles (``csharp``)."""

    @abstractmethod
    def capabilities(self) -> dict:
        """At least ``{"real_quic": bool, "quic_termination": "in_process" | "side_process"}``.

        ``gleam`` reports both **honestly** — Profile A attributes ``real_quic`` to its native
        side-process (``side_process``); Profile C terminates QUIC in-process (Decision 8).
        Never claims in-runtime QUIC it does not perform (constitution II).
        """

    @abstractmethod
    def start_server(
        self,
        bind: str,
        port: int,
        cert: Path,
        max_clients: int,
        repl: ReplKind,
        *,
        repl_path: Optional[str] = None,
        self_id: Optional[str] = None,
    ) -> Handle:
        """Launch + supervise the server-role runtime: bind the UDP port, load the shared cert,
        accept up to ``max_clients`` isolated client links (FR-005/FR-011). ``bind`` is a LAN IP
        or machine name; ``cert`` is the shared-cert directory. ``repl_path`` (063 US1, contract
        C1) makes the host spawn that GLP REPL as a child and bridge its stdio over the link
        envelopes; ``self_id`` is the endpoint id the host answers as."""

    @abstractmethod
    def start_client(
        self,
        server_addr: str,
        port: int,
        cert: Path,
        repl: ReplKind,
        *,
        repl_path: Optional[str] = None,
        self_id: Optional[str] = None,
    ) -> Handle:
        """Launch + supervise the client-role runtime: real QUIC handshake (SPKI-pinned to the
        shared cert), bring up the WebSocket link (FR-001/FR-002/FR-003). ``repl_path`` /
        ``self_id`` as in :meth:`start_server` (063 US1 live bridge)."""

    @abstractmethod
    def health(self, handle: Handle) -> Status:
        """Liveness of the supervised runtime behind ``handle``."""

    @abstractmethod
    def stop(self, handle: Handle) -> None:
        """Graceful drain + stop. Sibling links are unaffected (FR-006/SC-004)."""
