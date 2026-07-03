"""Shared terminal state + the loop-serialized receive-path mutation seam (feature 040, R4/FR-042).

Host-free and prompt_toolkit-free: ``tui`` (full-screen view) and ``link_console`` (plain fallback)
both wire onto this one model so they cannot drift (FR-026). Everything the receive thread touches is
mutated through :meth:`TerminalState.post`, which serializes on the UI event loop when one is bound
(``loop.call_soon_threadsafe``) and under a lock otherwise (tests) — so concurrent inbound messages can
never corrupt or lose page/peer state (FR-042/SC-012), and a link error is **surfaced** to the OIA,
never swallowed (FR-043/FR-044/R6).

Send-path composition (``@name`` resolution + ``chat`` encoding + local echo) is the pure
:func:`compose_chat`, shared by both front ends (T016/T017/T018; parity checked by T059).
"""

from __future__ import annotations

import threading
from dataclasses import dataclass, field
from typing import Any, Callable, List, Optional, Sequence, Union

from glp_quick.repl_link import GlpMessage
from glp_quick.terminal import protocol
from glp_quick.terminal.protocol import decode, is_known
from glp_quick.terminal.routing import PeerSource, Resolution, resolve

#: Owner sentinel for pages this side authored.
ME = "me"
#: The CHAT page is a shared conversation, owned by neither a single peer nor "me".
SHARED = "shared"
#: The mesh self-announce sentinel (feature 036) — a control line, never rendered as chat.
CONNECTED_SENTINEL = "__connected__"

#: Link/OIA state tokens (R6).
LINK_UP = "up"
LINK_CLOSED = "closed"
LINK_FAULTED = "faulted"


@dataclass
class Page:
    """A named, scrollable, editable screen of text (data-model.md §Page).

    ``owner`` is ``ME``, ``SHARED`` (the CHAT page), or a ``PeerId``. ``joint``/``saved_regions`` are
    consumed by US4; ``unread`` raises the OIA "new page" indicator without stealing focus (FR-010).
    """

    name: str
    owner: str = ME
    kind: str = "plain"  # plain | mask | repl
    text: str = ""
    joint: bool = False
    unread: bool = False
    saved_regions: dict = field(default_factory=dict)


class TerminalState:
    """The shared model behind both front ends.

    Parameters
    ----------
    self_id: this endpoint's id (the envelope ``from``).
    default_to: the default send target (client → ``"server"``, server → ``BROADCAST``).
    peers: the live peer set or a provider of it (usually ``handle.peers``) — for ``@name`` + OIA.
    initial_chat_text: the CHAT page's opening text (the view's banner); host-free tests pass ``""``.
    on_change: optional callback invoked after every mutation (the view's refresh/invalidate hook).
    """

    def __init__(
        self,
        self_id: str,
        default_to: str,
        peers: PeerSource = (),
        *,
        initial_chat_text: str = "",
        on_change: Optional[Callable[[], None]] = None,
    ) -> None:
        self.self_id = self_id
        self.default_to = default_to
        self._peers_source = peers
        self._on_change = on_change
        self._lock = threading.RLock()
        self._loop: Any = None

        self.pages: List[Page] = [Page("CHAT", owner=SHARED, kind="plain", text=initial_chat_text)]
        self.current: int = 0
        self.link_state: str = LINK_UP
        self.link_detail: Optional[str] = None

    # --- the R4 mutation seam ----------------------------------------------------------
    def bind_loop(self, loop: Any) -> None:
        """Bind the asyncio event loop so receive-path mutations serialize onto the UI thread (R4)."""
        self._loop = loop

    def post(self, fn: Callable[[], None]) -> None:
        """Run ``fn`` as a serialized mutation: on the loop thread when bound, else under the lock."""
        if self._loop is not None:
            self._loop.call_soon_threadsafe(self._run_locked, fn)
        else:
            self._run_locked(fn)

    def _run_locked(self, fn: Callable[[], None]) -> None:
        with self._lock:
            fn()
            if self._on_change is not None:
                self._on_change()

    # --- peers / OIA -------------------------------------------------------------------
    def peers(self) -> List[str]:
        src = self._peers_source
        return list(src() if callable(src) else src)

    def current_page(self) -> Page:
        return self.pages[self.current]

    def oia_link_label(self) -> str:
        if self.link_state == LINK_UP:
            return "LINK:up"
        if self.link_state == LINK_CLOSED:
            return "LINK:closed"
        return f"LINK:faulted({self.link_detail})"

    @property
    def link_operable(self) -> bool:
        """The terminal stays locally operable regardless of link state (R6)."""
        return True

    # --- UI-thread page actions (already serialized: only the single UI thread calls these) ---
    def save_current(self, text: str) -> None:
        self.pages[self.current].text = text

    def load(self, i: int) -> Page:
        self.current = i % len(self.pages)
        pg = self.pages[self.current]
        pg.unread = False
        return pg

    def ensure_page(self, name: str, owner: str = ME, kind: str = "plain") -> int:
        for idx, pg in enumerate(self.pages):
            if pg.name == name and pg.owner == owner:
                return idx
        self.pages.append(Page(name, owner=owner, kind=kind))
        return len(self.pages) - 1

    def add_page(self, name: str, owner: str = ME, kind: str = "plain", text: str = "") -> int:
        self.pages.append(Page(name, owner=owner, kind=kind, text=text))
        return len(self.pages) - 1

    def append_chat_line(self, line: str) -> None:
        """Append one already-rendered line to the CHAT page; mark unread if it is not in view."""
        chat = self.pages[0]
        chat.text += line + "\n"
        if self.current != 0:
            chat.unread = True

    # --- receive path (serialized via post) --------------------------------------------
    def deliver(self, msg: Optional[GlpMessage]) -> None:
        """Handle one inbound receive-loop event.

        ``None`` ⇒ graceful close (R6). A :class:`GlpMessage` is decoded and dispatched. Always routed
        through :meth:`post` so it is serialized against every other mutation (FR-042).
        """
        if msg is None:
            self.post(lambda: self._set_link(LINK_CLOSED, None,
                                             "** link closed — terminal stays local (type to keep editing) **"))
            return
        self.post(lambda: self._handle_inbound(msg))

    def report_fault(self, token: str) -> None:
        """The receive loop raised / the link faulted (FR-019 token) — surface it, stay operable (R6)."""
        self.post(lambda: self._set_link(LINK_FAULTED, token,
                                         f"** link faulted ({token}) — terminal stays local **"))

    def _handle_inbound(self, msg: GlpMessage) -> None:
        if msg.payload == CONNECTED_SENTINEL:
            return  # mesh self-announce; registration is the mesh's concern, not a chat line
        tm = decode(msg.payload)
        if tm.kind == "chat":
            text = tm.fields[0] if tm.fields else ""
            self.append_chat_line(f"<< {msg.sender}: " + text.replace("\n", "\n   "))
        elif tm.kind == "link_status":
            state = str(tm.fields[0]) if tm.fields else "?"
            detail = tm.fields[1] if len(tm.fields) > 1 else ""
            self.append_chat_line(f"** peer link note: {state} ({detail}) **")
        elif tm.kind == protocol.MALFORMED or not is_known(tm.kind):
            # Surface, never swallow (R6): unknown/unparseable terms are reported as info lines.
            self.append_chat_line(f"<< {msg.sender}: [unhandled {tm.kind}] {msg.payload}")
        else:
            # Known kinds handled by later stories (page/pinpoint/form_*/repl_*/rcopy_*): surface a
            # notice in the MVP rather than silently dropping (R6). US2+ replace this branch.
            self.append_chat_line(f"<< {msg.sender}: [{tm.kind}] (delivered — shown from US2 on)")

    def _set_link(self, state: str, detail: Optional[str], notice: str) -> None:
        if self.link_state == state and self.link_detail == detail:
            return
        self.link_state = state
        self.link_detail = detail
        self.append_chat_line(notice)


# --------------------------------------------------------------------------------------
# Shared send-path composition (used by both tui and link_console — T016/T017/T018).
# --------------------------------------------------------------------------------------
@dataclass(frozen=True)
class Outbound:
    """The result of composing an outbound chat line.

    ``message`` is ``None`` when nothing should be sent (an unknown ``@name`` or an empty body); in that
    case ``echo`` is a user-facing report. When ``message`` is set, ``echo`` is the local echo to append.
    """

    resolution: Resolution
    message: Optional[GlpMessage]
    echo: str


def compose_chat(self_id: str, default_to: str, text: str, peers: PeerSource) -> Outbound:
    """Resolve ``@name`` (routing) and, if deliverable, build the ``chat`` envelope + local echo.

    Shared by ``tui`` and ``link_console`` so the ``@name`` unknown-report (FR-040) and the ``chat``
    codec routing (FR-026) are identical on both paths — an unknown ``@name`` is reported and **not**
    sent to the default peer.
    """
    text = text.rstrip("\r\n")
    r = resolve(text, default_to, peers)
    if not r.ok:
        return Outbound(r, None, f"?? {r.error}")
    if not r.payload.strip():
        return Outbound(r, None, f"?? nothing to send to @{r.unknown_name or r.target}")
    msg = GlpMessage(sender=self_id, to=r.target, payload=protocol.chat(r.payload))
    tag = f"[{self_id}>{r.target}]" if r.addressed else f"[{self_id}]"
    echo = tag + " " + r.payload.replace("\n", "\n      ")
    return Outbound(r, msg, echo)
