"""T022 e2e — the differential equivalence oracle over a real captured pair.

PURE, no bridge / no live runtime: the (golden, candidate) traces were captured
ONCE from the Dart golden REPL (``:trace``/``:debug``) and the instrumented C#
candidate REPL (``GLP_EQUIV_TRACE``) and are checked in under ``fixtures/equiv/``.
This test is the deterministic US1 conformance: given the same recorded inputs,
``verdict.compare_recorded`` always yields the same verdict (R12 — capture is the
nondeterministic part and lives in the CLI/skill, the verdict ingest is pure).

The pair is ``append([a],[c],Zs)`` on ``programs/typed_book/recursive/
list_processing/append.glp`` — corpus ``tier: strict`` (a deterministic subsystem),
so equivalence is total-order equality of the full 28-event list plus the final
outcome. This exercises the whole stack end to end:

  parse_dart  (Dart :trace/:debug text → canonical wire → normalized model)
  parse_csharp(C# EquivTrace canonical wire → normalized model)
  relation.compare (STRICT total-order equality + outcome)

It is also the regression guard for finding #3 (the C# OUT binding shape must be
the FULLY dereferenced ``./2(const(a),./2(const(c),const(nil)))``, not the shallow
``./2(var,var)`` a single top-level deref leaves) and for the parse_dart adapter.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.equiv import normalize, verdict
from codeconv.tools.equiv.relation import CompareMode, Tier, compare
from codeconv.tools.equiv.trace import EventKind, Status

_FIXTURES = Path(__file__).parent / "fixtures" / "equiv"
_DART = (_FIXTURES / "append_dart.txt").read_text(encoding="utf-8")
_CSHARP = (_FIXTURES / "append_csharp.txt").read_text(encoding="utf-8")

# The fully-resolved binding both sides must agree on (finding #3): a single
# top-level deref leaves the shallow ./2(var,var); the recursive deref yields this.
_RESOLVED_ZS = "./2(const(a),./2(const(c),const(nil)))"


def test_append_strict_tier_equivalent() -> None:
    """The headline T022 e2e: the captured append pair is trace-equivalent."""
    result = verdict.compare_recorded(
        _DART, _CSHARP, compare_mode="trace", tier="strict"
    )
    assert result.equivalent, result.verdict.divergence
    assert result.verdict.divergence is None
    # Distinct recorded inputs (different dialects) ⇒ distinct content hashes.
    assert result.golden_trace_hash != result.candidate_trace_hash


def test_parse_dart_and_parse_csharp_produce_the_same_28_event_model() -> None:
    """parse_dart (adapter) and parse_csharp (canonical) yield identical models —
    the contract's 'both produce the same model', proven on the real capture."""
    golden = normalize.parse_dart(_DART)
    candidate = normalize.parse_csharp(_CSHARP)
    assert len(golden.events) == len(candidate.events) == 28
    for i, (ge, ce) in enumerate(zip(golden.events, candidate.events)):
        assert ge.kind is ce.kind, f"event {i} kind"
        assert ge.payload == ce.payload, f"event {i} payload"
    assert golden.outcome == candidate.outcome
    # The model is fully relation-equivalent under STRICT, too.
    v = compare(golden, candidate, mode=CompareMode.TRACE, tier=Tier.STRICT)
    assert v.equivalent, v.divergence


def test_event_kind_sequence_is_the_expected_oracle_shape() -> None:
    """The append run's normalized event kinds: spine + the suspend / commit /
    writer-bind dependent events (the GLP three-phase / SRSW observables)."""
    candidate = normalize.parse_csharp(_CSHARP)
    kinds = [e.kind for e in candidate.events]
    assert kinds.count(EventKind.BYTECODE_OP) == 21
    assert kinds.count(EventKind.UNIFY) == 3  # one suspend + two success commits
    assert kinds.count(EventKind.SUSPEND) == 1
    assert kinds.count(EventKind.WRITER_BIND) == 3
    assert candidate.outcome.status is Status.SUCCEED


def test_out_binding_is_fully_dereferenced_on_both_sides() -> None:
    """Finding #3 regression guard: the OUT binding shape is the full ground term
    on BOTH the Dart (canonicalized `[a, c]`) and the C# (recursively dereferenced)
    side — a shallow `./2(var,var)` would be a silent fidelity loss."""
    golden = normalize.parse_dart(_DART)
    candidate = normalize.parse_csharp(_CSHARP)
    assert dict(golden.outcome.bindings)["Zs"] == _RESOLVED_ZS
    assert dict(candidate.outcome.bindings)["Zs"] == _RESOLVED_ZS


def test_negative_control_shallow_out_binding_diverges() -> None:
    """The pre-finding-#3 shallow OUT shape must read as DIVERGENT — proving the
    oracle is sensitive to binding fidelity and not trivially green."""
    shallow = _CSHARP.replace(f"Zs={_RESOLVED_ZS}", "Zs=./2(var,var)")
    assert shallow != _CSHARP  # the substitution actually fired
    result = verdict.compare_recorded(
        _DART, shallow, compare_mode="trace", tier="strict"
    )
    assert not result.equivalent
    assert result.verdict.divergence is not None
    assert result.verdict.divergence.event_kind == "OUTCOME"


def test_negative_control_tampered_spine_op_diverges() -> None:
    """A single altered spine op must read as DIVERGENT at that position — the
    STRICT relation is positional total-order equality of the full event list."""
    tampered = _CSHARP.replace(
        "EV 5 BYTECODE_OP opcode=Ground pc=6",
        "EV 5 BYTECODE_OP opcode=NoReaders pc=6",
    )
    assert tampered != _CSHARP
    result = verdict.compare_recorded(
        _DART, tampered, compare_mode="trace", tier="strict"
    )
    assert not result.equivalent
    div = result.verdict.divergence
    assert div is not None
    assert div.event_kind == EventKind.BYTECODE_OP.value
    assert div.causal_position == 5
