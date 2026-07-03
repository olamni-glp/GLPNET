"""T012 [US1] — ``@name`` delivers to the named peer; an unknown name is reported (FR-040/SC-011).

Exercises the shared send-path composition (:func:`glp_quick.terminal.state.compose_chat`) against the
in-memory :class:`FakeHandle`, proving the ``--tui`` and ``link_console`` front ends deliver directed
messages and never silently redirect an unknown ``@name`` to the default peer.
"""

from __future__ import annotations

from glp_quick.terminal.protocol import decode
from glp_quick.terminal.state import compose_chat
from tests._fakes import FakeHandle


def test_at_name_delivers_to_named_peer_over_the_handle():
    h = FakeHandle(peers=["bob", "carol"])
    out = compose_chat("me", "server", "@bob hey bob", h.peers)
    assert out.message is not None and out.message.to == "bob"
    assert decode(out.message.payload).kind == "chat"
    assert decode(out.message.payload).fields == ("hey bob",)

    h.send(out.message)
    assert [(m.to, decode(m.payload).fields[0]) for m in h.sent()] == [("bob", "hey bob")]
    # local echo reflects the directed send
    assert out.echo == "[me>bob] hey bob"


def test_unknown_at_name_is_reported_and_not_sent_to_default():
    h = FakeHandle(peers=["bob"])
    out = compose_chat("me", "server", "@zoe hidden", h.peers)
    assert out.message is None            # nothing sent
    assert "zoe" in out.echo              # reported to the user
    # crucially: the message did NOT get redirected to "server" (the default peer)
    h.send(out.message) if out.message else None
    assert h.sent() == []


def test_plain_line_goes_to_default_peer():
    h = FakeHandle(peers=["bob"])
    out = compose_chat("me", "server", "hello everyone", h.peers)
    assert out.message is not None and out.message.to == "server"
    assert out.echo == "[me] hello everyone"


def test_empty_body_after_at_name_reports_and_sends_nothing():
    h = FakeHandle(peers=["bob"])
    out = compose_chat("me", "server", "@bob   ", h.peers)
    assert out.message is None and "nothing to send" in out.echo


def test_explicit_broadcast_is_delivered_as_fanout():
    h = FakeHandle(peers=["bob", "carol"])
    out = compose_chat("me", "server", "@broadcast hi all", h.peers)
    assert out.message is not None and out.message.is_broadcast
    assert decode(out.message.payload).fields == ("hi all",)
