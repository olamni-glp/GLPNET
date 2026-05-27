"""T021 (SC-005) — zero false equivalences on a seeded divergence battery.

PURE, no bridge / no runtime. Every seeded corruption MUST be reported as
``divergent`` (equivalence_relation.md § SC-005 obligations). The headline case
is a suspended writer **bound eagerly**: the candidate skips the SUSPEND and
binds the writer without the data-dependence the golden has, so its causal
structure differs and the partial-order relation must reject it — landing the
divergence on the eager bind / its reactivation.
"""

from __future__ import annotations

from codeconv.tools.equiv.normalize import Addr, RawEvent, RawOutcome, normalize
from codeconv.tools.equiv.relation import CompareMode, Tier, compare
from codeconv.tools.equiv.trace import EventKind, Status


def _succeed(bindings=()) -> RawOutcome:
    return RawOutcome(status=Status.SUCCEED, bindings=bindings)


# Correct (lazy) execution: input becomes ready, a consumer SUSPENDs on the
# not-yet-bound writer w, a producer (depending on the input) binds w, the
# consumer REACTIVATEs.
def _golden_lazy():
    return normalize(
        [
            RawEvent(0, EventKind.WRITER_BIND, {"writer": Addr("in"), "shape": "ready"}),
            RawEvent(1, EventKind.SUSPEND, {"reader": Addr("w"), "goal": "consumer"}),
            RawEvent(
                2,
                EventKind.WRITER_BIND,
                {"writer": Addr("w"), "shape": "value"},
                reads=(Addr("in"),),
                writes=(Addr("w"),),
            ),
            RawEvent(3, EventKind.REACTIVATE, {"writer": Addr("w"), "goal": "consumer"},
                     reads=(Addr("w"),)),
        ],
        _succeed(),
    )


# Corrupted (eager) execution: w is bound WITHOUT the input dependence and the
# consumer never suspends.
def _candidate_eager():
    return normalize(
        [
            RawEvent(0, EventKind.WRITER_BIND, {"writer": Addr("in"), "shape": "ready"}),
            RawEvent(1, EventKind.WRITER_BIND, {"writer": Addr("w"), "shape": "value"}),
            RawEvent(2, EventKind.REACTIVATE, {"writer": Addr("w"), "goal": "consumer"},
                     reads=(Addr("w"),)),
        ],
        _succeed(),
    )


def test_eager_writer_bind_is_divergent_dynamic() -> None:
    v = compare(_golden_lazy(), _candidate_eager(), mode=CompareMode.TRACE, tier=Tier.DYNAMIC)
    assert not v.equivalent
    # The corruption manifests in the suspend/bind/reactivate region.
    assert v.divergence is not None
    assert v.divergence.event_kind in {
        EventKind.SUSPEND.value,
        EventKind.WRITER_BIND.value,
        EventKind.REACTIVATE.value,
    }


def test_eager_writer_bind_is_divergent_strict() -> None:
    # Under total-order the missing SUSPEND shows up positionally as well.
    v = compare(_golden_lazy(), _candidate_eager(), mode=CompareMode.TRACE, tier=Tier.STRICT)
    assert not v.equivalent
    assert v.divergence is not None


def test_writer_bind_value_mismatch_is_divergent() -> None:
    # A WRITER_BIND that binds a different value-shape must diverge AT the
    # WRITER_BIND event (same causal structure, different bound value).
    golden = normalize(
        [
            RawEvent(0, EventKind.WRITER_BIND, {"writer": Addr("w"), "shape": "accepted"}),
            RawEvent(1, EventKind.REACTIVATE, {"writer": Addr("w"), "goal": "g"},
                     reads=(Addr("w"),)),
        ],
        _succeed(),
    )
    candidate = normalize(
        [
            RawEvent(0, EventKind.WRITER_BIND, {"writer": Addr("w"), "shape": "rejected"}),
            RawEvent(1, EventKind.REACTIVATE, {"writer": Addr("w"), "goal": "g"},
                     reads=(Addr("w"),)),
        ],
        _succeed(),
    )
    v = compare(golden, candidate, mode=CompareMode.TRACE, tier=Tier.DYNAMIC)
    assert not v.equivalent
    assert v.divergence.event_kind == EventKind.WRITER_BIND.value


def test_spine_opcode_mismatch_is_divergent() -> None:
    # The bytecode-op spine is the primary check (FR-003): a different opcode at
    # the same logical PC must diverge, reporting the spine PC.
    golden = normalize(
        [RawEvent(0, EventKind.BYTECODE_OP, {"opcode": "GetStructure", "logical_pc": 0})],
        _succeed(),
    )
    candidate = normalize(
        [RawEvent(0, EventKind.BYTECODE_OP, {"opcode": "GetConstant", "logical_pc": 0})],
        _succeed(),
    )
    v = compare(golden, candidate, mode=CompareMode.TRACE, tier=Tier.DYNAMIC)
    assert not v.equivalent
    assert v.divergence.event_kind == EventKind.BYTECODE_OP.value
    assert v.divergence.spine_pc == 0


def test_outcome_status_mismatch_is_divergent() -> None:
    golden = normalize([], RawOutcome(Status.SUCCEED))
    candidate = normalize([], RawOutcome(Status.FAIL))
    v = compare(golden, candidate, mode=CompareMode.OUTCOME)
    assert not v.equivalent
    assert v.divergence.event_kind == "OUTCOME"
