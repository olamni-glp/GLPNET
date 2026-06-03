"""Causal / partial-order equivalence relation (FR-003, FR-008, FR-009; T014).

PURE, deterministic, unit-tested without a runtime
(``test_no_lm_on_production_path`` guards the import surface).

Contract: ``specs/020-trace-equivalence-fidelity/contracts/equivalence_relation.md``.

    compare(golden, candidate, mode, tier) -> Verdict{equivalent, divergence}

Three regimes:

* **OUTCOME** mode (bonds, FR-005): equivalent iff final status + canonical
  bindings match. No event comparison — escrow-timer suspension is a valid
  status, compared as-is.

* **TRACE** mode, **STRICT** tier (FR-008): deterministic subsystems have a
  single causal linearization, so the relation degenerates to **total-order
  equality** of the full event list (cheaper, stricter). Compared positionally
  by (kind, payload); first positional mismatch is the divergence.

* **TRACE** mode, **DYNAMIC** tier (FR-009): genuine concurrency. Equivalent
  iff (1) outcomes match, (2) the BYTECODE_OP spine matches positionally (the
  spine is totally ordered within a computation), and (3) the dependent-event
  partial orders are isomorphic — checked via a causal canonical key that is
  invariant under both heap-address relabeling AND reordering of
  causally-independent events. The verification-mode dual (pinned-schedule vs
  accept-any-causal) the dynamic tier ultimately selects is layered on in T040
  (US4) once decided from empirical data; this module provides the sound
  partial-order check the SC-005 batteries require.

"First divergence" = the earliest golden event (in causal/positional order)
with no admissible match in the candidate.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Any, Optional, Union

from codeconv.tools.equiv.trace import Event, EventKind, Outcome, Trace

# Dependent events projected for the partial-order check (the spine is handled
# separately as a totally-ordered subsequence).
_DEPENDENT_KINDS = frozenset(
    {
        EventKind.UNIFY,
        EventKind.SUSPEND,
        EventKind.REACTIVATE,
        EventKind.WRITER_BIND,
    }
)

# Payload fields that hold logical-var labels. Because the two runs are
# relabeled INDEPENDENTLY (first-occurrence), a reorder of independent events
# permutes these labels — so they must NOT be compared by value across runs in
# the partial-order regime; variable identity travels through ``causes`` instead.
_VAR_FIELDS = {"vars", "reader", "writer"}


class CompareMode(str, Enum):
    TRACE = "trace"
    OUTCOME = "outcome"


class Tier(str, Enum):
    STRICT = "strict"
    DYNAMIC = "dynamic"


@dataclass(frozen=True)
class DivergenceRecord:
    """The first trace-divergence (FR-003 entity; also the GEPA reflective
    feedback — a single representation, fidelity_metric/gepa contracts)."""

    event_kind: str
    causal_position: Union[int, str]
    expected: Optional[Union[Event, Outcome]]
    actual: Optional[Union[Event, Outcome]]
    spine_pc: Optional[int] = None


@dataclass(frozen=True)
class Verdict:
    equivalent: bool
    divergence: Optional[DivergenceRecord] = None


_EQUIVALENT = Verdict(equivalent=True, divergence=None)


def compare(
    golden: Trace,
    candidate: Trace,
    mode: CompareMode = CompareMode.TRACE,
    tier: Tier = Tier.STRICT,
) -> Verdict:
    """Equivalence verdict + first divergence (equivalence_relation.md)."""
    if mode is CompareMode.OUTCOME:
        return _compare_outcome(golden.outcome, candidate.outcome)
    if tier is Tier.STRICT:
        return _compare_strict(golden, candidate)
    return _compare_partial_order(golden, candidate)


# --------------------------------------------------------------------------- #
# OUTCOME mode (bonds)
# --------------------------------------------------------------------------- #
def _compare_outcome(golden: Outcome, candidate: Outcome) -> Verdict:
    if golden.status == candidate.status and golden.bindings == candidate.bindings:
        return _EQUIVALENT
    return Verdict(
        equivalent=False,
        divergence=DivergenceRecord(
            event_kind="OUTCOME",
            causal_position="outcome",
            expected=golden,
            actual=candidate,
        ),
    )


# --------------------------------------------------------------------------- #
# STRICT tier — total-order equality of the full event list (FR-008)
# --------------------------------------------------------------------------- #
def _compare_strict(golden: Trace, candidate: Trace) -> Verdict:
    g, c = golden.events, candidate.events
    for i in range(max(len(g), len(c))):
        ge = g[i] if i < len(g) else None
        ce = c[i] if i < len(c) else None
        if ge is None or ce is None or not _event_eq(ge, ce):
            ref = ge or ce
            assert ref is not None  # i < max(len) ⇒ at least one present
            return Verdict(
                equivalent=False,
                divergence=DivergenceRecord(
                    event_kind=ref.kind.value,
                    causal_position=i,
                    expected=ge,
                    actual=ce,
                    spine_pc=_spine_pc(ge) if ge is not None else None,
                ),
            )
    # Events identical ⇒ outcomes must still match.
    return _compare_outcome(golden.outcome, candidate.outcome)


def _event_eq(a: Event, b: Event) -> bool:
    """Total-order equality: same kind + same payload. Logical labels line up
    because a single deterministic linearization yields the same first-occurrence
    relabeling in both runs (that is the whole point of strict-tier relabeling)."""
    return a.kind is b.kind and a.payload == b.payload


# --------------------------------------------------------------------------- #
# DYNAMIC tier — partial-order isomorphism (FR-009)
# --------------------------------------------------------------------------- #
def _compare_partial_order(golden: Trace, candidate: Trace) -> Verdict:
    # (1) outcomes.
    outcome = _compare_outcome(golden.outcome, candidate.outcome)
    # (2) bytecode spine — totally ordered within a computation, compared
    #     positionally (opcode @ logical_pc; no var labels ⇒ label-safe).
    spine = _compare_spine(golden, candidate)
    if not spine.equivalent:
        return spine
    # (3) dependent-event partial orders isomorphic, via causal canonical keys.
    dep = _compare_dependent(golden, candidate)
    if not dep.equivalent:
        return dep
    # Spine + dependent events agree; outcome decides.
    return outcome


def _compare_spine(golden: Trace, candidate: Trace) -> Verdict:
    g, c = golden.bytecode_spine(), candidate.bytecode_spine()
    for i in range(max(len(g), len(c))):
        ge = g[i] if i < len(g) else None
        ce = c[i] if i < len(c) else None
        if ge is None or ce is None or ge.payload != ce.payload:
            ref = ge or ce
            assert ref is not None
            return Verdict(
                equivalent=False,
                divergence=DivergenceRecord(
                    event_kind=EventKind.BYTECODE_OP.value,
                    causal_position=i,
                    expected=ge,
                    actual=ce,
                    spine_pc=_spine_pc(ref),
                ),
            )
    return _EQUIVALENT


def _compare_dependent(golden: Trace, candidate: Trace) -> Verdict:
    g_keyed = _canonical_keys(golden)
    c_keys = {k for _e, k in _canonical_keys(candidate)}
    # First golden dependent event (in causal order) whose canonical key has no
    # match in candidate ⇒ the first divergence.
    c_remaining: dict[Any, int] = {}
    for _e, k in _canonical_keys(candidate):
        c_remaining[k] = c_remaining.get(k, 0) + 1
    for ev, key in g_keyed:
        if c_remaining.get(key, 0) <= 0:
            return Verdict(
                equivalent=False,
                divergence=DivergenceRecord(
                    event_kind=ev.kind.value,
                    causal_position=ev.seq,
                    expected=ev,
                    actual=None,
                    spine_pc=_spine_pc(ev),
                ),
            )
        c_remaining[key] -= 1
    # Candidate must not carry dependent events golden lacks (extra-event
    # divergence) — multisets must be equal, not merely a subset.
    g_keys: dict[Any, int] = {}
    for _e, k in g_keyed:
        g_keys[k] = g_keys.get(k, 0) + 1
    for _e, k in _canonical_keys(candidate):
        if g_keys.get(k, 0) <= 0:
            return Verdict(
                equivalent=False,
                divergence=DivergenceRecord(
                    event_kind=_e.kind.value,
                    causal_position="candidate-extra",
                    expected=None,
                    actual=_e,
                    spine_pc=_spine_pc(_e),
                ),
            )
        g_keys[k] -= 1
    _ = c_keys  # (retained for readability; multiset check above is authoritative)
    return _EQUIVALENT


def _canonical_keys(trace: Trace) -> list[tuple[Event, Any]]:
    """Causal canonical key per dependent event, in causal (seq) order.

    The key encodes (kind, non-var payload, multiset of the canonical keys of
    its causal predecessors). It is invariant under heap-address relabeling
    (var-label fields are dropped — identity is carried by the causal edges) and
    under reordering of causally-independent events (the key depends only on
    causal ancestry, not capture position). Eager / missing binds change an
    event's causal ancestry, so they change the key ⇒ detected as divergent.
    """
    by_seq = trace.by_seq()
    memo: dict[int, Any] = {}

    def key_of(seq: int) -> Any:
        if seq in memo:
            return memo[seq]
        ev = by_seq[seq]
        cause_keys = tuple(sorted((key_of(c) for c in ev.causes), key=repr))
        k = (ev.kind.value, _payload_no_vars(ev.payload), cause_keys)
        memo[seq] = k
        return k

    result: list[tuple[Event, Any]] = []
    for ev in trace.events:
        if ev.kind in _DEPENDENT_KINDS:
            result.append((ev, key_of(ev.seq)))
    return result


def _payload_no_vars(payload: dict[str, Any]) -> tuple[tuple[str, Any], ...]:
    """Payload projection with var-label fields removed (label-permutation safe)."""
    return tuple(
        sorted(
            (k, v) for k, v in payload.items() if k not in _VAR_FIELDS
        )
    )


def _spine_pc(event: Optional[Event]) -> Optional[int]:
    if event is not None and event.kind is EventKind.BYTECODE_OP:
        pc = event.payload.get("logical_pc")
        return int(pc) if pc is not None else None
    return None
