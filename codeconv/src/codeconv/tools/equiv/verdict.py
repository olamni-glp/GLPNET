"""Deterministic verdict from recorded traces (the standalone US1 core, T019).

PURE — no bridge, no LM, no REPL (``test_no_lm_on_production_path`` guards the
import surface). Composes the pure normalizer (``normalize.py``) and the pure
relation (``relation.py``) into a single reproducible function over recorded
wire-format text, plus a JSON projection of the divergence record (which is
ALSO the GEPA reflective feedback — one representation, per the gepa/fidelity
contracts).

This is what ``equiv compare`` applies and what the durable ``equiv`` step
(T024) will wrap — given the same recorded inputs both yield the same verdict.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Optional, Union

from codeconv.tools.equiv import normalize, relation
from codeconv.tools.equiv.bytecode_diff import BytecodeDiff, EmittedOp, diff_bytecode
from codeconv.tools.equiv.recorded import text_hash, text_to_emitted
from codeconv.tools.equiv.relation import CompareMode, DivergenceRecord, Tier, Verdict
from codeconv.tools.equiv.trace import Event, Outcome


@dataclass(frozen=True)
class CompareResult:
    """The deterministic compare outcome + the row fields ``compare`` persists."""

    verdict: Verdict
    golden_trace_hash: str
    candidate_trace_hash: str

    @property
    def equivalent(self) -> bool:
        return self.verdict.equivalent


def _to_mode(compare_mode: str) -> CompareMode:
    return CompareMode.OUTCOME if compare_mode == "outcome" else CompareMode.TRACE


def _to_tier(tier: str) -> Tier:
    return Tier.DYNAMIC if tier == "dynamic" else Tier.STRICT


def compare_recorded(
    golden_text: str,
    candidate_text: str,
    *,
    compare_mode: str,
    tier: str,
) -> CompareResult:
    """Parse both recorded traces and apply the relation — reproducible.

    ``compare_mode`` / ``tier`` are the corpus source's tags (``trace``/
    ``outcome``; ``strict``/``dynamic``). OUTCOME mode ignores ``tier``."""
    golden = normalize.parse_dart(golden_text)
    candidate = normalize.parse_csharp(candidate_text)
    verdict = relation.compare(
        golden, candidate, mode=_to_mode(compare_mode), tier=_to_tier(tier)
    )
    return CompareResult(
        verdict=verdict,
        golden_trace_hash=text_hash(golden_text),
        candidate_trace_hash=text_hash(candidate_text),
    )


def compare_bytecode(golden_bc_text: str, candidate_bc_text: str) -> BytecodeDiff:
    """Apply the emitted-bytecode diff to recorded ``.bc`` artifacts (FR-004)."""
    return diff_bytecode(text_to_emitted(golden_bc_text), text_to_emitted(candidate_bc_text))


# --------------------------------------------------------------------------- #
# JSON projection of the divergence record (gate evidence == GEPA feedback).  #
# --------------------------------------------------------------------------- #
def _event_to_dict(ev: Event) -> dict:
    return {
        "seq": ev.seq,
        "kind": ev.kind.value,
        "payload": ev.payload,
        "causes": sorted(ev.causes),
    }


def _outcome_to_dict(o: Outcome) -> dict:
    return {"status": o.status.value, "bindings": [list(b) for b in o.bindings]}


def _side_to_dict(side: Optional[Union[Event, Outcome]]) -> Any:
    if side is None:
        return None
    if isinstance(side, Event):
        return _event_to_dict(side)
    return _outcome_to_dict(side)


def divergence_to_dict(d: DivergenceRecord) -> dict:
    """JSON-able trace-divergence record (the ``dart_equivalence.divergence``
    jsonb value AND the GEPA reflective-feedback text — single representation)."""
    return {
        "event_kind": d.event_kind,
        "causal_position": d.causal_position,
        "expected": _side_to_dict(d.expected),
        "actual": _side_to_dict(d.actual),
        "spine_pc": d.spine_pc,
    }


def bytecode_diff_to_dict(bd: BytecodeDiff) -> dict:
    def op(o: Optional[EmittedOp]) -> Any:
        if o is None:
            return None
        return {"logical_pc": o.logical_pc, "opcode": o.opcode, "operands": list(o.operands)}

    return {
        "empty": bd.empty,
        "first_diff_pc": bd.first_diff_pc,
        "expected": op(bd.expected),
        "actual": op(bd.actual),
    }


__all__ = [
    "CompareResult",
    "compare_recorded",
    "compare_bytecode",
    "divergence_to_dict",
    "bytecode_diff_to_dict",
]
