"""T008 [US-Foundational] — ``@name`` resolution against the live peer set (routing.py, R3/FR-040).

Complements ``tests/test_routing.py`` (which covers the ``parse_addressed`` split): this asserts the
**resolution** step — an unknown ``@name`` is reported, never silently redirected to the default peer.
"""

from __future__ import annotations

from glp_quick.repl_link import BROADCAST
from glp_quick.terminal.routing import resolve


def test_plain_line_goes_to_default_peer():
    r = resolve("hello there", "server", ["bob"])
    assert r.ok and r.target == "server" and r.payload == "hello there" and not r.addressed


def test_at_name_resolves_to_a_live_peer():
    r = resolve("@bob hi bob", "server", ["bob", "carol"])
    assert r.ok and r.target == "bob" and r.payload == "hi bob" and r.addressed


def test_unknown_at_name_is_reported_never_falls_back_to_default():
    r = resolve("@zoe hi", "server", ["bob", "carol"])
    assert not r.ok
    assert r.target is None  # crucially NOT "server" — no silent default-fallback (FR-040)
    assert r.unknown_name == "zoe"
    assert "zoe" in r.error and "bob" in r.error and "carol" in r.error
    assert r.payload == "hi"  # body still parsed out


def test_unknown_at_name_with_no_peers_connected_is_reported():
    r = resolve("@bob hi", "server", [])
    assert not r.ok and r.unknown_name == "bob"
    assert "none connected" in r.error


def test_explicit_broadcast_fans_out():
    r = resolve("@broadcast everyone", "server", ["bob"])
    assert r.ok and r.target == BROADCAST and r.payload == "everyone" and r.addressed


def test_default_peer_allowed_even_when_not_yet_in_mesh_list():
    # A client's default "server" is legitimate before it appears in peers() (registered on contact).
    r = resolve("just chatting", "server", [])
    assert r.ok and r.target == "server"


def test_peers_provider_callable_is_supported():
    r = resolve("@bob hi", "server", lambda: ["bob"])
    assert r.ok and r.target == "bob"


def test_at_name_without_body_yields_empty_payload_but_resolves():
    r = resolve("@bob", "server", ["bob"])
    assert r.ok and r.target == "bob" and r.payload == ""
