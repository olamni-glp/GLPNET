"""The ``tmsg(...)`` ground-term codec — the **single** encode/decode point for every terminal
message (feature 040), so ``tui`` and ``link_console`` cannot drift (constitution VIII; FR-026; R2).

Authoritative shapes: ``specs/040-rcopy-file-transfer-service/contracts/terminal-protocol.md``.

Every terminal message is one :class:`glp_quick.repl_link.GlpMessage` whose ``payload`` is a **ground
GLP term** ``tmsg(<Kind>, <Field…>)``. ``Kind`` is a lowercase atom; term strings are quoted and
escaped; a bare text payload with no ``tmsg(`` head decodes as ``chat`` (backward compatibility with
the 037 prototype).

Ground-relay (spec 025): no ``_w(`` / ``_r(`` may cross the wire. Those two literal tokens are escaped
**inside string content** by the codec before egress and un-escaped on ingress, so arbitrary page/form
text cannot trip :func:`glp_quick.repl_link.assert_ground_relay` (R2). The codec asserts its own
output is ground-relay-clean as a self-check.

Term model used across encode/decode:

- :class:`Atom`   — a bare lowercase atom (a ``str`` subclass, so ``Atom("plain") == "plain"``).
- ``int``          — an integer term.
- ``str``          — a quoted, escaped string term.
- ``list``         — a ``[…]`` list term.
- :class:`Term`    — a compound ``functor(arg…)``.

:func:`decode` returns a :class:`TerminalMessage` (``kind`` + decoded ``fields``); a malformed or
unrecognized term is **surfaced**, never allowed to crash the receiver (R6).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Tuple

from glp_quick.repl_link import assert_ground_relay

__all__ = [
    "Atom",
    "Term",
    "TerminalMessage",
    "KNOWN_KINDS",
    "MALFORMED",
    "encode_terminal",
    "decode",
    "is_known",
    "chat",
    "page",
    "link_status",
    "pinpoint",
    "form_def",
    "form_fill",
    "repl_goal",
    "repl_result",
    "ParseError",
]


class Atom(str):
    """A bare (unquoted) GLP atom. A ``str`` subclass so ``Atom("need") == "need"`` compares true,
    which keeps the round-trip tests ergonomic."""

    __slots__ = ()

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"Atom({str.__repr__(self)})"


@dataclass(frozen=True)
class Term:
    """A compound term ``functor(arg…)`` (e.g. ``label(1,2,"Name")``)."""

    functor: str
    args: Tuple[Any, ...] = ()


@dataclass(frozen=True)
class TerminalMessage:
    """A decoded ``tmsg(<kind>, <fields…>)``. ``kind`` is the atom text; ``fields`` are the decoded
    arguments (``Atom`` / ``int`` / ``str`` / ``list`` / ``Term``)."""

    kind: str
    fields: Tuple[Any, ...] = ()


class ParseError(ValueError):
    """The payload looked like a ``tmsg(`` term but could not be parsed."""


#: The terminal message kinds this codec recognizes (contracts/terminal-protocol.md §Message kinds).
KNOWN_KINDS = frozenset(
    {
        "chat",
        "page",
        "pinpoint",
        "form_def",
        "form_fill",
        "repl_goal",
        "repl_result",
        "rcopy_offer_query",
        "rcopy_offer",
        "rcopy_manifest",
        "rcopy_verdict",
        "rcopy_chunk",
        "rcopy_outcome",
        "link_status",
    }
)

#: The ``kind`` used when a ``tmsg(`` payload is present but unparseable — surfaced, never crashes (R6).
MALFORMED = "_malformed"


def is_known(kind: str) -> bool:
    """Whether ``kind`` is a recognized terminal message kind (else surface as an info line, R6)."""
    return kind in KNOWN_KINDS


# --------------------------------------------------------------------------------------
# String escaping — self-consistent, and guarantees no bare ``_w(`` / ``_r(`` survives.
# --------------------------------------------------------------------------------------
def _esc(s: str) -> str:
    out = s.replace("\\", "\\\\").replace('"', '\\"')
    out = out.replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t")
    # Neutralize the two ground-relay placeholder heads inside string content by escaping the '('.
    # After the backslash pass above, these tokens contain no backslash, so this inserts a fresh one.
    out = out.replace("_w(", "_w\\(").replace("_r(", "_r\\(")
    return out


def _unesc(s: str) -> str:
    out = []
    i, n = 0, len(s)
    while i < n:
        c = s[i]
        if c == "\\" and i + 1 < n:
            nxt = s[i + 1]
            out.append(
                {"\\": "\\", '"': '"', "n": "\n", "r": "\r", "t": "\t", "(": "(", ")": ")"}.get(nxt, nxt)
            )
            i += 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


# --------------------------------------------------------------------------------------
# Encoder
# --------------------------------------------------------------------------------------
def _enc(value: Any) -> str:
    if isinstance(value, Atom):
        return str(value)
    if isinstance(value, bool):  # guard: bool is an int subclass — reject to avoid silent True→1
        raise TypeError("bool is not a valid GLP term; use an Atom or int")
    if isinstance(value, int):
        return str(value)
    if isinstance(value, str):
        return '"' + _esc(value) + '"'
    if isinstance(value, (list, tuple)) and not isinstance(value, Term):
        return "[" + ",".join(_enc(v) for v in value) + "]"
    if isinstance(value, Term):
        if not value.args:
            return value.functor
        return value.functor + "(" + ",".join(_enc(a) for a in value.args) + ")"
    raise TypeError(f"cannot encode {type(value).__name__} as a GLP term: {value!r}")


def encode_terminal(kind: str, *fields: Any) -> str:
    """Encode ``tmsg(<kind>, <fields…>)`` to a ground GLP term string.

    ``fields`` are Python values (``Atom`` for atoms, ``int``, ``str``, ``list``, ``Term``). The
    result is asserted ground-relay-clean (a failure would be a codec bug, not a caller bug).
    """
    out = _enc(Term("tmsg", (Atom(kind), *fields)))
    return assert_ground_relay(out)


# --------------------------------------------------------------------------------------
# Parser (recursive descent over the term grammar)
# --------------------------------------------------------------------------------------
class _P:
    def __init__(self, s: str) -> None:
        self.s = s
        self.i = 0
        self.n = len(s)

    def _ws(self) -> None:
        while self.i < self.n and self.s[self.i] in " \t\r\n":
            self.i += 1

    def parse(self) -> Any:
        self._ws()
        v = self._term()
        self._ws()
        if self.i != self.n:
            raise ParseError(f"trailing characters at {self.i}: {self.s[self.i:]!r}")
        return v

    def _term(self) -> Any:
        self._ws()
        if self.i >= self.n:
            raise ParseError("unexpected end of term")
        c = self.s[self.i]
        if c == '"':
            return self._string()
        if c == "[":
            return self._list()
        if c == "-" or c.isdigit():
            return self._int()
        if c.isalpha() or c == "_":
            return self._atom_or_compound()
        raise ParseError(f"unexpected character {c!r} at {self.i}")

    def _string(self) -> str:
        assert self.s[self.i] == '"'
        self.i += 1
        raw = []
        while self.i < self.n:
            c = self.s[self.i]
            if c == "\\" and self.i + 1 < self.n:
                raw.append(self.s[self.i : self.i + 2])
                self.i += 2
                continue
            if c == '"':
                self.i += 1
                return _unesc("".join(raw))
            raw.append(c)
            self.i += 1
        raise ParseError("unterminated string")

    def _int(self) -> int:
        start = self.i
        if self.s[self.i] == "-":
            self.i += 1
        if self.i >= self.n or not self.s[self.i].isdigit():
            raise ParseError(f"malformed integer at {start}")
        while self.i < self.n and self.s[self.i].isdigit():
            self.i += 1
        return int(self.s[start : self.i])

    def _ident(self) -> str:
        start = self.i
        while self.i < self.n and (self.s[self.i].isalnum() or self.s[self.i] == "_"):
            self.i += 1
        return self.s[start : self.i]

    def _atom_or_compound(self) -> Any:
        name = self._ident()
        self._ws()
        if self.i < self.n and self.s[self.i] == "(":
            args = self._seq("(", ")")
            return Term(name, tuple(args))
        return Atom(name)

    def _list(self) -> list:
        return self._seq("[", "]")

    def _seq(self, open_ch: str, close_ch: str) -> list:
        assert self.s[self.i] == open_ch
        self.i += 1
        items: list = []
        self._ws()
        if self.i < self.n and self.s[self.i] == close_ch:
            self.i += 1
            return items
        while True:
            items.append(self._term())
            self._ws()
            if self.i >= self.n:
                raise ParseError(f"unterminated {open_ch}{close_ch}")
            c = self.s[self.i]
            if c == ",":
                self.i += 1
                continue
            if c == close_ch:
                self.i += 1
                return items
            raise ParseError(f"expected ',' or {close_ch!r} at {self.i}, got {c!r}")


def decode(payload: str) -> TerminalMessage:
    """Decode a wire payload into a :class:`TerminalMessage`.

    - A payload whose stripped head is not ``tmsg(`` decodes as ``chat`` carrying the **verbatim**
      payload (037 prototype backward compatibility).
    - A ``tmsg(<kind>, …)`` term decodes to that kind and its fields (kind may be unrecognized — the
      caller surfaces it as an info line rather than crashing, R6).
    - A ``tmsg(`` head that fails to parse yields kind :data:`MALFORMED` carrying the raw payload — it
      is **reported**, never silently swallowed (R6).
    """
    if not payload.lstrip().startswith("tmsg("):
        return TerminalMessage("chat", (payload,))
    try:
        term = _P(payload).parse()
    except ParseError:
        return TerminalMessage(MALFORMED, (payload,))
    if not isinstance(term, Term) or term.functor != "tmsg" or not term.args:
        return TerminalMessage(MALFORMED, (payload,))
    kind = term.args[0]
    if not isinstance(kind, Atom):
        return TerminalMessage(MALFORMED, (payload,))
    return TerminalMessage(str(kind), tuple(term.args[1:]))


# --------------------------------------------------------------------------------------
# Per-kind convenience encoders (contracts/terminal-protocol.md §Message kinds)
# --------------------------------------------------------------------------------------
def chat(text: str) -> str:
    """``tmsg(chat, "Text")`` — a plain conversation line (US1)."""
    return encode_terminal("chat", text)


def page(name: str, owner: str, kind: str, text: str) -> str:
    """``tmsg(page, "Name", "Owner", Kind, "Text")`` — a whole page as an owned block (US2).
    ``kind ∈ {plain,mask,repl}``."""
    return encode_terminal("page", name, owner, Atom(kind), text)


def link_status(state: str, detail: str) -> str:
    """``tmsg(link_status, State, "Detail")`` — an out-of-band link-state note for the OIA (R6)."""
    return encode_terminal("link_status", Atom(state), detail)


def pinpoint(page_name: str, row: int, col: int, h: int, w: int, block: str, classification: str) -> str:
    """``tmsg(pinpoint, "Page", Row, Col, H, W, "Block", Class)`` — a joint live overwrite (US4).
    ``classification ∈ {transient,permanent}``."""
    return encode_terminal("pinpoint", page_name, row, col, h, w, block, Atom(classification))


def form_def(page_name: str, labels: list, fields: list) -> str:
    """``tmsg(form_def, "Page", [label(R,C,"L")…], [field(R,C,W)…])`` — define a mask/form (US4).

    ``labels`` is a list of ``(row, col, text)``; ``fields`` a list of ``(row, col, width)``.
    """
    label_terms = [Term("label", (r, c, t)) for (r, c, t) in labels]
    field_terms = [Term("field", (r, c, w)) for (r, c, w) in fields]
    return encode_terminal("form_def", page_name, label_terms, field_terms)


def form_fill(page_name: str, fills: list) -> str:
    """``tmsg(form_fill, "Page", [fill(Idx,"V")…])`` — return a filled form (US4).

    ``fills`` is a list of ``(index, value)``.
    """
    fill_terms = [Term("fill", (idx, v)) for (idx, v) in fills]
    return encode_terminal("form_fill", page_name, fill_terms)


def repl_goal(page_name: str, goal: str) -> str:
    """``tmsg(repl_goal, "Page", "goal.")`` — a GLP goal entered on a REPL page (US5)."""
    return encode_terminal("repl_goal", page_name, goal)


def repl_result(page_name: str, rendered: str) -> str:
    """``tmsg(repl_result, "Page", "Rendered")`` — the REPL's rendered result for a page (US5)."""
    return encode_terminal("repl_result", page_name, rendered)
