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
from dataclasses import dataclass
from typing import Any, Callable, List, Optional, Union

from glp_quick.rcopy.wizard import ResponderSession
from glp_quick.repl_link import GlpMessage
from glp_quick.terminal import forms, joint, protocol, replpage
from glp_quick.terminal.pages import ME, SHARED, Page, receive_page
from glp_quick.terminal.protocol import decode, is_known
from glp_quick.terminal.routing import PeerSource, Resolution, resolve

#: The mesh self-announce sentinel (feature 036) — a control line, never rendered as chat.
CONNECTED_SENTINEL = "__connected__"

#: Link/OIA state tokens (R6).
LINK_UP = "up"
LINK_CLOSED = "closed"
LINK_FAULTED = "faulted"


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
        #: Index of the page most recently mutated by the receive path — lets the view refresh only
        #: the current page's screen buffer (avoids clobbering an off-screen page / needless scroll).
        self.last_changed_index: Optional[int] = None
        #: The last inbound line from the counterpart — shown in the two-strip layout's response strip.
        self.last_response: str = ""
        #: Mask definitions by page name (US4) — the fillable-form model behind mask pages.
        self.masks: dict = {}
        #: Optional host hook: ``(sender, page, goal) -> None`` answers an inbound ``repl_goal`` (US5).
        #: Set by the view when this endpoint hosts a REPL; unset ⇒ inbound goals are reported, not run.
        self.on_repl_goal: Optional[Callable[[str, str, str], None]] = None
        #: /rcopy responder wiring (US6/US8). ``responder`` set by ``/rcopy init``; ``on_rcopy_reply``
        #: sends a reply payload to a peer; ``rcopy_inbox`` is an active client wizard's response queue.
        self.responder: Any = None
        self._responder_session: Optional[ResponderSession] = None
        self.on_rcopy_reply: Optional[Callable[[str, str], None]] = None
        self.rcopy_inbox: Any = None

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

    def find_page_index(self, name: str) -> Optional[int]:
        """Index of the first page with this name (any owner), or ``None`` — used by joint/form ops."""
        for i, pg in enumerate(self.pages):
            if pg.name == name:
                return i
        return None

    def append_chat_line(self, line: str) -> None:
        """Append one already-rendered line to the CHAT page; mark unread if it is not in view."""
        chat = self.pages[0]
        chat.text += line + "\n"
        if self.current != 0:
            chat.unread = True
        self.last_changed_index = 0

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
            self.last_response = f"{msg.sender}: {text}"
            self.append_chat_line(f"<< {msg.sender}: " + text.replace("\n", "\n   "))
        elif tm.kind == "page":
            # A received page is owned by its authenticated sender (never the self-declared owner in
            # the term), lands as a distinct page, is NOT merged into CHAT, and does NOT steal focus —
            # it only raises the OIA "new page" indicator (FR-010; terminal-protocol §Invariants #1).
            if len(tm.fields) >= 4:
                name, _claimed, kind, text = tm.fields[0], tm.fields[1], str(tm.fields[2]), tm.fields[3]
                idx, _is_new = receive_page(self.pages, msg.sender, name, kind, text)
                if idx == self.current:
                    self.pages[idx].unread = False  # it is already in view — nothing new to flag
                self.last_changed_index = idx
                self.last_response = f"{msg.sender} sent page '{name}'"
            else:
                self.append_chat_line(f"<< {msg.sender}: [malformed page] {msg.payload}")
        elif tm.kind == "pinpoint":
            self._handle_pinpoint(msg.sender, tm)
        elif tm.kind == "form_def":
            self._handle_form_def(msg.sender, tm)
        elif tm.kind == "form_fill":
            self._handle_form_fill(msg.sender, tm)
        elif tm.kind == "repl_goal":
            self._handle_repl_goal(msg.sender, tm)
        elif tm.kind == "repl_result":
            self._handle_repl_result(msg.sender, tm)
        elif tm.kind == "rcopy_offer_query":
            self._rcopy_offer_query(msg.sender)
        elif tm.kind == "rcopy_manifest":
            self._rcopy_manifest(msg.sender, tm)
        elif tm.kind == "rcopy_chunk":
            self._rcopy_chunk(msg.sender, tm)
        elif tm.kind in ("rcopy_offer", "rcopy_verdict", "rcopy_outcome"):
            self._rcopy_client_reply(msg.sender, msg.payload, tm)
        elif tm.kind == "link_status":
            state = str(tm.fields[0]) if tm.fields else "?"
            detail = tm.fields[1] if len(tm.fields) > 1 else ""
            self.append_chat_line(f"** peer link note: {state} ({detail}) **")
        elif tm.kind == protocol.MALFORMED or not is_known(tm.kind):
            # Surface, never swallow (R6): unknown/unparseable terms are reported as info lines.
            self.append_chat_line(f"<< {msg.sender}: [unhandled {tm.kind}] {msg.payload}")
        else:
            # Known kinds handled by later stories (pinpoint/form_*/repl_*/rcopy_*): surface a notice
            # rather than silently dropping (R6). Each story replaces its branch as it lands.
            self.append_chat_line(f"<< {msg.sender}: [{tm.kind}] (handled from a later story)")

    def _handle_pinpoint(self, sender: str, tm) -> None:
        """Apply a counterpart pinpoint to the named local page (US4/FR-012). Rejections are reported."""
        if len(tm.fields) < 7:
            self.append_chat_line(f"** malformed pinpoint from {sender} **")
            return
        name, row, col, h, w = tm.fields[0], tm.fields[1], tm.fields[2], tm.fields[3], tm.fields[4]
        block, classification = tm.fields[5], str(tm.fields[6])
        idx = self.find_page_index(name)
        if idx is None:
            self.append_chat_line(f"** pinpoint from {sender} rejected: no page '{name}' **")
            return
        res = joint.apply_pinpoint(self.pages[idx], row, col, h, w, block, classification)
        if res.ok:
            if idx != self.current:
                self.pages[idx].unread = True
            self.last_changed_index = idx
            self.last_response = f"{sender} pinpointed '{name}'"
        else:
            self.append_chat_line(f"** pinpoint from {sender} rejected ({res.reason}) on '{name}' **")

    def _handle_form_def(self, sender: str, tm) -> None:
        """Receive a mask definition and render it as a peer-owned mask page (US4/FR-015)."""
        if len(tm.fields) < 3:
            self.append_chat_line(f"** malformed form_def from {sender} **")
            return
        name = tm.fields[0]
        mask = forms.from_wire(tm.fields[1], tm.fields[2])
        self.masks[name] = mask
        idx, _new = receive_page(self.pages, sender, name, "mask", forms.render(mask))
        if idx == self.current:
            self.pages[idx].unread = False
        self.last_changed_index = idx
        self.last_response = f"{sender} sent form '{name}'"

    def _handle_form_fill(self, sender: str, tm) -> None:
        """Receive a completed form: apply the fills to the stored mask, re-render, labels intact (FR-015)."""
        if len(tm.fields) < 2:
            self.append_chat_line(f"** malformed form_fill from {sender} **")
            return
        name = tm.fields[0]
        if name not in self.masks:
            self.append_chat_line(f"** form_fill for unknown form '{name}' from {sender} **")
            return
        self.masks[name] = forms.fill(self.masks[name], forms.fills_from_wire(tm.fields[1]))
        idx = self.find_page_index(name)
        if idx is not None:
            self.pages[idx].text = forms.render(self.masks[name])
            if idx != self.current:
                self.pages[idx].unread = True
            self.last_changed_index = idx
        self.last_response = f"{sender} returned form '{name}'"

    def _handle_repl_goal(self, sender: str, tm) -> None:
        """A peer sent a goal to a REPL page hosted here (US5/FR-016). Delegate to the host hook if one
        is registered (it evaluates off-thread + replies); otherwise surface that no REPL is hosted."""
        if len(tm.fields) < 2:
            self.append_chat_line(f"** malformed repl_goal from {sender} **")
            return
        page, goal = tm.fields[0], tm.fields[1]
        if self.on_repl_goal is not None:
            self.on_repl_goal(sender, page, goal)
        else:
            self.append_chat_line(f"** {sender} sent a REPL goal but no REPL is hosted here **")

    def _handle_repl_result(self, sender: str, tm) -> None:
        """The REPL's rendered result for a goal — render it on that REPL page (US5/FR-016)."""
        if len(tm.fields) < 2:
            self.append_chat_line(f"** malformed repl_result from {sender} **")
            return
        page, rendered = tm.fields[0], tm.fields[1]
        idx = self.find_page_index(page)
        if idx is None:
            idx = self.add_page(page, owner=ME, kind="repl")
        replpage.append_result(self.pages[idx], rendered)
        if idx != self.current:
            self.pages[idx].unread = True
        self.last_changed_index = idx
        self.last_response = f"{sender} → REPL '{page}'"

    def set_responder(self, responder: Any) -> None:
        """Configure this endpoint as an ``/rcopy`` responder (``/rcopy init``)."""
        self.responder = responder
        self._responder_session = ResponderSession(responder, self.self_id) if responder else None

    # --- /rcopy responder side (US8) ---------------------------------------------------
    def _rcopy_offer_query(self, sender: str) -> None:
        if self.on_rcopy_reply is None:
            return
        if self._responder_session is None:
            self.on_rcopy_reply(sender, protocol.rcopy_offer([]))  # no service configured here
            return
        self.on_rcopy_reply(sender, self._responder_session.offer_payload(sender))

    def _rcopy_manifest(self, sender: str, tm) -> None:
        if self._responder_session is None or self.on_rcopy_reply is None:
            return
        self.on_rcopy_reply(sender, self._responder_session.manifest_verdict_payload(sender, tm))

    def _rcopy_chunk(self, sender: str, tm) -> None:
        if self._responder_session is None or self.on_rcopy_reply is None:
            return
        payload = self._responder_session.chunk_outcome_payload(sender, tm)
        if payload is not None:
            self.on_rcopy_reply(sender, payload)

    # --- /rcopy client side (US6) ------------------------------------------------------
    def _rcopy_client_reply(self, sender: str, payload: str, tm) -> None:
        if self.rcopy_inbox is not None:
            self.rcopy_inbox.put(payload)  # feed the active wizard worker thread
        else:
            self.append_chat_line(f"<< {sender}: [rcopy {tm.kind}] (no active /rcopy wizard)")

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
