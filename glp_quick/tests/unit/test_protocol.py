"""T005 [US-Foundational] — the ``tmsg(...)`` ground-term codec (protocol.py).

Round-trips every message kind, verifies string escaping (quotes / newlines / backslashes), verifies
ground-relay neutralization of ``_w(`` / ``_r(`` inside user text, and the 037 bare-chat backward
compatibility. Contract: ``contracts/terminal-protocol.md``.
"""

from __future__ import annotations

import pytest

from glp_quick.repl_link import GlpMessage, assert_ground_relay
from glp_quick.terminal import protocol as P
from glp_quick.terminal.protocol import Atom, Term, TerminalMessage, decode


# --- backward compatibility (037 prototype bare chat) -------------------------------------
def test_bare_text_decodes_as_chat_verbatim():
    assert decode("hello world") == TerminalMessage("chat", ("hello world",))


def test_bare_text_preserves_leading_and_trailing_space():
    assert decode("  spaced  ") == TerminalMessage("chat", ("  spaced  ",))


# --- chat round-trip + escaping ----------------------------------------------------------
def test_chat_round_trip():
    m = decode(P.chat("hi there"))
    assert m.kind == "chat" and m.fields == ("hi there",)


@pytest.mark.parametrize(
    "text",
    [
        'he said "hello"',
        "line1\nline2\r\nline3",
        "back\\slash and tab\tend",
        'nested "quote" with \\ and newline\n',
        "",
    ],
)
def test_chat_escaping_round_trip(text):
    enc = P.chat(text)
    assert decode(enc) == TerminalMessage("chat", (text,))
    # the encoded payload is a legal GlpMessage payload (ground-relay clean)
    GlpMessage(sender="me", to="you", payload=enc)


# --- ground-relay neutralization ---------------------------------------------------------
def test_ground_relay_placeholders_in_user_text_are_neutralized_and_restored():
    text = "danger _w(p,i) and _r(q,j) placeholder tokens"
    enc = P.chat(text)
    # No bare placeholder head survives on the wire (would trip assert_ground_relay otherwise).
    assert "_w(" not in enc and "_r(" not in enc
    assert_ground_relay(enc)  # must not raise
    GlpMessage(sender="me", to="you", payload=enc)  # constructs without GroundRelayViolation
    # …and the exact user text is restored on decode.
    assert decode(enc) == TerminalMessage("chat", (text,))


# --- page ---------------------------------------------------------------------------------
def test_page_round_trip():
    enc = P.page("STATUS", "bob", "plain", "the whole\npage text")
    m = decode(enc)
    assert m.kind == "page"
    assert m.fields == ("STATUS", "bob", "plain", "the whole\npage text")
    assert isinstance(m.fields[2], Atom)  # Kind is an atom, not a string


# --- link_status --------------------------------------------------------------------------
def test_link_status_round_trip():
    m = decode(P.link_status("faulted", "epoch_fenced"))
    assert m.kind == "link_status"
    assert m.fields == ("faulted", "epoch_fenced")
    assert isinstance(m.fields[0], Atom)


# --- pinpoint (integers) ------------------------------------------------------------------
def test_pinpoint_round_trip():
    m = decode(P.pinpoint("P1", 3, 5, 2, 40, "overwrite\nblock", "transient"))
    assert m.kind == "pinpoint"
    assert m.fields == ("P1", 3, 5, 2, 40, "overwrite\nblock", "transient")


# --- form_def / form_fill (nested lists of compound terms) --------------------------------
def test_form_def_round_trip():
    enc = P.form_def("FRM", [(1, 2, "Name"), (3, 4, "Age")], [(1, 10, 20), (3, 10, 5)])
    m = decode(enc)
    assert m.kind == "form_def"
    assert m.fields == (
        "FRM",
        [Term("label", (1, 2, "Name")), Term("label", (3, 4, "Age"))],
        [Term("field", (1, 10, 20)), Term("field", (3, 10, 5))],
    )


def test_form_fill_round_trip():
    m = decode(P.form_fill("FRM", [(0, "Alice"), (1, "30")]))
    assert m.kind == "form_fill"
    assert m.fields == ("FRM", [Term("fill", (0, "Alice")), Term("fill", (1, "30"))])


# --- repl -----------------------------------------------------------------------------------
def test_repl_goal_and_result_round_trip():
    assert decode(P.repl_goal("REPL", "append([1],[2],X).")) == TerminalMessage(
        "repl_goal", ("REPL", "append([1],[2],X).")
    )
    assert decode(P.repl_result("REPL", "X = [1,2]")) == TerminalMessage(
        "repl_result", ("REPL", "X = [1,2]")
    )


# --- rcopy_* control kinds (built via the generic encoder from the contract shapes) --------
def test_rcopy_offer_query_no_fields():
    m = decode(P.encode_terminal("rcopy_offer_query"))
    assert m.kind == "rcopy_offer_query" and m.fields == ()


def test_rcopy_offer_round_trip():
    offer = [Term("root", ("docs", ["a", "b"], 1024)), Term("root", ("code", [], 0))]
    m = decode(P.encode_terminal("rcopy_offer", offer))
    assert m.kind == "rcopy_offer"
    assert m.fields == (offer,)


def test_rcopy_manifest_verdict_chunk_outcome_round_trip():
    manifest = P.encode_terminal(
        "rcopy_manifest", "docs", "reports", Atom("synchronise"),
        [Term("file", ("a.txt", 10, "sha_a")), Term("file", ("b.txt", 20, "sha_b"))],
    )
    m = decode(manifest)
    assert m.kind == "rcopy_manifest"
    assert m.fields == (
        "docs", "reports", "synchronise",
        [Term("file", ("a.txt", 10, "sha_a")), Term("file", ("b.txt", 20, "sha_b"))],
    )

    verdict = P.encode_terminal(
        "rcopy_verdict",
        [Term("verdict", ("a.txt", Atom("need"))),
         Term("verdict", ("b.txt", Term("reject", (Atom("quota"),))))],
    )
    mv = decode(verdict)
    assert mv.kind == "rcopy_verdict"
    assert mv.fields == (
        [Term("verdict", ("a.txt", "need")),
         Term("verdict", ("b.txt", Term("reject", ("quota",))))],
    )

    assert decode(P.encode_terminal("rcopy_chunk", "a.txt", 0, "YmFzZTY0")) == TerminalMessage(
        "rcopy_chunk", ("a.txt", 0, "YmFzZTY0")
    )
    assert decode(P.encode_terminal("rcopy_outcome", "a.txt", Atom("transferred"), Atom("none"))) == \
        TerminalMessage("rcopy_outcome", ("a.txt", "transferred", "none"))


# --- robustness: malformed / unknown are surfaced, never crash (R6) -----------------------
def test_malformed_tmsg_is_surfaced_not_crashing():
    m = decode('tmsg(chat, "unterminated')
    assert m.kind == P.MALFORMED
    assert m.fields == ('tmsg(chat, "unterminated',)


def test_unknown_kind_parses_but_is_not_known():
    m = decode(P.encode_terminal("some_future_kind", "x", 1))
    assert m.kind == "some_future_kind"
    assert m.fields == ("x", 1)
    assert not P.is_known("some_future_kind")


def test_is_known_recognizes_contract_kinds():
    for k in ("chat", "page", "pinpoint", "form_def", "form_fill", "repl_goal", "repl_result",
              "rcopy_offer_query", "rcopy_offer", "rcopy_manifest", "rcopy_verdict",
              "rcopy_chunk", "rcopy_outcome", "link_status"):
        assert P.is_known(k)


def test_bool_field_is_rejected():
    with pytest.raises(TypeError):
        P.encode_terminal("chat", True)
