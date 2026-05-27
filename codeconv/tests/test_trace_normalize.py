"""T013 — normalized-trace parsing + relabeling (trace_normalization.md).

PURE, no bridge / no runtime. Exercises the canonical wire-format parsers
(``parse_dart`` / ``parse_csharp`` share the grammar and must produce the SAME
model) and the first-occurrence relabeling / causal-edge derivation core.

The live ``:trace`` / ``:debug`` text adaptation is finalized at T017/T022
(runtime-coupled, B1); this validates the format the instrumented REPL emits.
"""

from __future__ import annotations

from codeconv.tools.equiv.normalize import parse_csharp, parse_dart
from codeconv.tools.equiv.relation import CompareMode, Tier, compare
from codeconv.tools.equiv.trace import EventKind, Status

_WIRE = """
# a tiny run in the canonical wire format
EV 0 BYTECODE_OP opcode=GetStructure pc=0
EV 1 WRITER_BIND writer={IN} shape=ready
EV 2 SUSPEND reader={W} goal=consumer
EV 3 WRITER_BIND writer={W} shape=value reads={IN} writes={W}
EV 4 REACTIVATE writer={W} goal=consumer
OUT succeed X=value
"""


def _wire(in_addr: str, w_addr: str) -> str:
    return _WIRE.format(IN=in_addr, W=w_addr)


def test_parse_produces_expected_event_kinds() -> None:
    t = parse_dart(_wire("0x10", "0x20"))
    kinds = [e.kind for e in t.events]
    assert kinds == [
        EventKind.BYTECODE_OP,
        EventKind.WRITER_BIND,
        EventKind.SUSPEND,
        EventKind.WRITER_BIND,
        EventKind.REACTIVATE,
    ]
    assert t.outcome.status is Status.SUCCEED
    assert ("X", "value") in t.outcome.bindings


def test_bytecode_pc_parsed_as_logical_pc_int() -> None:
    t = parse_dart(_wire("0x10", "0x20"))
    op = t.events[0]
    assert op.payload == {"opcode": "GetStructure", "logical_pc": 0}
    assert isinstance(op.payload["logical_pc"], int)


def test_addresses_relabeled_first_occurrence() -> None:
    t = parse_dart(_wire("0x10", "0x20"))
    # IN seen first ⇒ v0; W seen next ⇒ v1. The two W references share a label.
    in_bind = t.events[1]
    w_bind = t.events[3]
    react = t.events[4]
    assert in_bind.payload["writer"] == "v0"
    assert w_bind.payload["writer"] == "v1"
    assert react.payload["writer"] == "v1"


def test_causal_edge_derived_from_reads_writes() -> None:
    t = parse_dart(_wire("0x10", "0x20"))
    # The W bind reads IN (bound at seq 1) ⇒ caused by seq 1.
    w_bind = t.events[3]
    assert 1 in w_bind.causes
    # The REACTIVATE reads W (bound at seq 3) ⇒ caused by seq 3.
    assert 3 in t.events[4].causes


def test_parse_dart_and_parse_csharp_agree_and_addresses_abstract() -> None:
    # Same structure, different raw addresses, parsed by the two front-ends:
    # the normalized models compare equivalent (addresses are abstracted).
    golden = parse_dart(_wire("0x10", "0x20"))
    candidate = parse_csharp(_wire("0xDEAD", "0xBEEF"))
    assert golden == candidate
    v = compare(golden, candidate, mode=CompareMode.TRACE, tier=Tier.STRICT)
    assert v.equivalent, v.divergence
