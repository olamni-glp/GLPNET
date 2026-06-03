"""T020 (SC-005) — zero false divergences.

PURE, no bridge / no runtime. Two constructed obligations
(equivalence_relation.md § SC-005 obligations):

(a) **heap-address relabeling** — two runs identical in structure but with
    different raw heap addresses must normalize to the same trace and compare
    *equivalent* (the relabeling is what abstracts the address accident).
(b) **independent-goal reordering** — two causally-independent goals run in a
    different relative order must NOT diverge under the partial-order (dynamic)
    relation; only causally-ordered events constrain the comparison.
"""

from __future__ import annotations

from codeconv.tools.equiv.normalize import Addr, RawEvent, RawOutcome, normalize
from codeconv.tools.equiv.relation import CompareMode, Tier, compare
from codeconv.tools.equiv.trace import EventKind, Status


def _succeed(bindings=()) -> RawOutcome:
    return RawOutcome(status=Status.SUCCEED, bindings=bindings)


# --------------------------------------------------------------------------- #
# (a) heap-address relabeling
# --------------------------------------------------------------------------- #
def _run_with_addresses(w_addr: str, in_addr: str) -> "object":
    """A small fixed-structure run, parameterized only by raw address values."""
    events = [
        RawEvent(0, EventKind.BYTECODE_OP, {"opcode": "GetStructure", "logical_pc": 0}),
        RawEvent(1, EventKind.WRITER_BIND, {"writer": Addr(in_addr), "shape": "ready"}),
        RawEvent(2, EventKind.BYTECODE_OP, {"opcode": "PutValue", "logical_pc": 1}),
        RawEvent(
            3,
            EventKind.WRITER_BIND,
            {"writer": Addr(w_addr), "shape": "result"},
            reads=(Addr(in_addr),),
            writes=(Addr(w_addr),),
        ),
        RawEvent(4, EventKind.REACTIVATE, {"writer": Addr(w_addr), "goal": "g1"}),
    ]
    return normalize(events, _succeed())


def test_relabeling_is_stable_within_a_run() -> None:
    # Same raw address used twice ⇒ the same logical var both times.
    t = _run_with_addresses("0x10", "0x20")
    bind = next(e for e in t.events if e.kind is EventKind.WRITER_BIND and e.payload["shape"] == "result")
    react = next(e for e in t.events if e.kind is EventKind.REACTIVATE)
    assert bind.payload["writer"] == react.payload["writer"]


def test_address_relabeling_no_false_divergence() -> None:
    golden = _run_with_addresses("0x10", "0x20")
    candidate = _run_with_addresses("0xAAAA", "0xBBBB")  # only addresses differ
    # Structurally identical after relabeling ⇒ equivalent under the strict
    # (total-order) relation, which is the cheapest correct check here.
    v = compare(golden, candidate, mode=CompareMode.TRACE, tier=Tier.STRICT)
    assert v.equivalent, v.divergence
    # …and the normalized traces are literally equal (relabeling did its job).
    assert golden == candidate


# --------------------------------------------------------------------------- #
# (b) independent-goal reordering
# --------------------------------------------------------------------------- #
def _goal_a(base: int):
    return [
        RawEvent(base + 0, EventKind.WRITER_BIND, {"writer": Addr("A"), "shape": "alpha"}),
        RawEvent(base + 1, EventKind.REACTIVATE, {"writer": Addr("A"), "goal": "ga"},
                 reads=(Addr("A"),)),
    ]


def _goal_b(base: int):
    return [
        RawEvent(base + 0, EventKind.WRITER_BIND, {"writer": Addr("B"), "shape": "beta"}),
        RawEvent(base + 1, EventKind.REACTIVATE, {"writer": Addr("B"), "goal": "gb"},
                 reads=(Addr("B"),)),
    ]


def test_independent_goal_reorder_no_false_divergence() -> None:
    # Golden: goal A then goal B. Candidate: goal B then goal A. No causal edge
    # crosses the two goals, so the partial-order relation must treat them as
    # equivalent.
    golden = normalize(_goal_a(0) + _goal_b(2), _succeed())
    candidate = normalize(_goal_b(0) + _goal_a(2), _succeed())
    v = compare(golden, candidate, mode=CompareMode.TRACE, tier=Tier.DYNAMIC)
    assert v.equivalent, v.divergence
    # The total-order check WOULD (correctly) flag the reorder — confirming the
    # partial-order relaxation is what saves it, not an accident of equality.
    strict = compare(golden, candidate, mode=CompareMode.TRACE, tier=Tier.STRICT)
    assert not strict.equivalent


def test_outcome_mode_ignores_interleaving() -> None:
    # Bonds (outcome mode): only final status + bindings matter; event order is
    # irrelevant (FR-005).
    golden = normalize(_goal_a(0) + _goal_b(2), _succeed(bindings=(("X", "done"),)))
    candidate = normalize(_goal_b(0) + _goal_a(2), _succeed(bindings=(("X", "done"),)))
    v = compare(golden, candidate, mode=CompareMode.OUTCOME)
    assert v.equivalent, v.divergence
