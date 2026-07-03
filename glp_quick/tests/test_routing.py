"""FR-006 @name directed-routing helper (`parse_addressed`).

Regression guard: the `--tui` terminal advertised `@<to> message` in its help but never parsed it —
every message silently went to the default peer. `parse_addressed` is now the single source of truth
shared by `link_console` and `tui`, so the plain console and the terminal cannot drift.
"""
from glp_quick.repl_link import parse_addressed


def test_no_at_uses_default_peer():
    assert parse_addressed("hello world", "server") == ("server", "hello world")


def test_at_name_routes_to_named_peer():
    assert parse_addressed("@bob hi there", "server") == ("bob", "hi there")


def test_at_name_preserves_multiline_body():
    assert parse_addressed("@bob line1\nline2", "broadcast") == ("bob", "line1\nline2")


def test_at_name_without_body_yields_empty_payload():
    assert parse_addressed("@bob", "server") == ("bob", "")


def test_plain_text_defaults_when_no_at_prefix():
    assert parse_addressed("plain message", "broadcast") == ("broadcast", "plain message")
