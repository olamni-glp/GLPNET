"""Codexreview P1 regression [US6] — inbound ``rcopy_*`` replies are filtered to the active peer.

A different connected mesh peer must not be able to inject an ``rcopy`` verdict/outcome into an in-flight
``/rcopy`` transfer (spoof guard): only the one peer the wizard is talking to (``state.rcopy_peer``) may
feed its response queue. Exercises the ``state.deliver`` → ``_rcopy_client_reply`` client-reply path.
"""

from __future__ import annotations

import queue

from glp_quick.repl_link import GlpMessage
from glp_quick.terminal import protocol
from glp_quick.terminal.state import TerminalState


def _state_with_active_wizard(target: str) -> TerminalState:
    st = TerminalState("me", "server", peers=[target, "mallory-U9"])
    st.rcopy_inbox = queue.Queue()
    st.rcopy_peer = target            # the wizard is mid-transfer with this peer
    return st


def test_reply_from_active_peer_feeds_the_wizard():
    st = _state_with_active_wizard("alice-U1")
    st.deliver(GlpMessage(sender="alice-U1", to="me",
                          payload=protocol.rcopy_outcome("f.txt", "transferred")))
    assert not st.rcopy_inbox.empty()          # the real peer's reply drives the transfer


def test_reply_from_other_peer_is_ignored_not_fed_to_wizard():
    st = _state_with_active_wizard("alice-U1")
    st.deliver(GlpMessage(sender="mallory-U9", to="me",
                          payload=protocol.rcopy_verdict([("f.txt", "need", None)])))
    assert st.rcopy_inbox.empty()              # a spoofed reply never reaches the in-flight transfer
