"""Host-free test doubles for the virtual-3270 terminal (feature 040).

:class:`FakeHandle` implements the feature-036 :class:`glp_quick.stacks.base.Handle` seam entirely
in-memory, so the terminal's own behaviours (send/receive/routing/state) are unit-testable **without**
a C# host or a real QUIC endpoint (FR-045/SC-013). It offers an injectable peer set and explicit
close/fault injection so tests can drive the R6 link-state transitions (up → closed → faulted).

Import from a test module as::

    from tests._fakes import FakeHandle
"""

from __future__ import annotations

import queue
import threading
from typing import List, Optional, Sequence

from glp_quick.repl_link import GlpMessage
from glp_quick.stacks.base import Handle


class FakeHandle(Handle):
    """An in-memory :class:`Handle`: captures sends, replays injected inbound messages, and can be
    told to close (``recv`` → ``None``) or fault (``recv`` raises), mirroring FR-019 terminal signals.

    Thread-safe: ``send``/``recv`` and the injection helpers may be called concurrently (the
    receive-path thread-safety test drives several threads through one handle).
    """

    def __init__(
        self,
        link_id: str = "fake-link",
        peers: Sequence[str] = (),
    ) -> None:
        self._link_id = link_id
        self._peers: List[str] = list(peers)
        self._peers_lock = threading.Lock()
        self._inbox: "queue.Queue[GlpMessage]" = queue.Queue()
        self._sent: List[GlpMessage] = []
        self._sent_lock = threading.Lock()
        self._closed = threading.Event()
        self._fault: Optional[BaseException] = None

    # --- Handle contract ---------------------------------------------------------------
    @property
    def link_id(self) -> str:
        return self._link_id

    def send(self, message: GlpMessage) -> None:
        with self._sent_lock:
            self._sent.append(message)

    def recv(self, timeout: Optional[float] = None) -> Optional[GlpMessage]:
        # A faulted link raises a clear terminal signal (never a silent hang) — FR-019/R6.
        if self._fault is not None:
            raise self._fault
        try:
            return self._inbox.get(timeout=timeout if timeout is not None else 0.05)
        except queue.Empty:
            # Empty inbox: None means graceful close once closed, else just "nothing yet".
            return None if self._closed.is_set() else None

    def peers(self) -> Sequence[str]:
        with self._peers_lock:
            return list(self._peers)

    # --- test injection helpers --------------------------------------------------------
    def inject(self, message: GlpMessage) -> None:
        """Queue an inbound message to be returned by the next ``recv``."""
        self._inbox.put(message)

    def inject_payload(self, sender: str, payload: str, to: str = "me") -> None:
        """Convenience: queue an inbound :class:`GlpMessage` from ``sender`` carrying ``payload``."""
        self._inbox.put(GlpMessage(sender=sender, to=to, payload=payload))

    def close(self) -> None:
        """Mark the link gracefully closed — subsequent empty ``recv`` returns ``None`` (R6 ``closed``)."""
        self._closed.set()

    def fault(self, exc: Optional[BaseException] = None) -> None:
        """Make ``recv`` raise ``exc`` (default a generic link error) — R6 ``faulted`` transition."""
        self._fault = exc if exc is not None else RuntimeError("link faulted")

    def set_peers(self, peers: Sequence[str]) -> None:
        with self._peers_lock:
            self._peers = list(peers)

    def sent(self) -> List[GlpMessage]:
        """A snapshot of everything ``send`` has captured, in order."""
        with self._sent_lock:
            return list(self._sent)

    @property
    def is_closed(self) -> bool:
        return self._closed.is_set()
