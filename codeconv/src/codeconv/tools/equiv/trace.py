"""Normalized-trace model for the differential equivalence oracle (FR-002).

PURE — no I/O, no runtime, no LM. Unit-tested without any REPL
(``test_no_lm_on_production_path`` guards the import surface). The parsers
that PRODUCE these models from Dart ``:trace`` / C# trace text live in
``normalize.py`` (T013); this module is just the data model + the canonical
fields the relation (``relation.py``, T014) is allowed to compare.

Contract: ``specs/020-trace-equivalence-fidelity/contracts/trace_normalization.md``.

The GLP semantics being measured (CLAUDE.md — do not invent events): the
three-phase HEAD/GUARD/BODY model, SRSW, writer-MGU (only writers bind;
readers depend on the writer that bound them), and three-valued unification
(success | suspend | fail). The five event kinds below are exactly those
observable semantics; if a needed event is absent from Dart ``:trace`` that
is a spec gap → STOP & report, NOT a model workaround.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Optional


class EventKind(str, Enum):
    """The ONLY event kinds compared (trace_normalization.md § Event kinds)."""

    UNIFY = "UNIFY"
    SUSPEND = "SUSPEND"
    REACTIVATE = "REACTIVATE"
    WRITER_BIND = "WRITER_BIND"
    BYTECODE_OP = "BYTECODE_OP"  # the spine (FR-003)


class Status(str, Enum):
    """Final computation status (outcome mode + UNIFY outcome share vocab)."""

    SUCCEED = "succeed"
    SUSPEND = "suspend"
    FAIL = "fail"


@dataclass(frozen=True)
class Event:
    """One normalized trace event.

    ``payload`` is kind-specific and already address-relabeled (heap
    addresses → logical vars ``v_i`` at parse time, so payloads never carry
    raw addresses). ``causes`` holds the ``seq`` ids of data-dependence
    predecessors (the causal edges; writer-MGU: a reader/bind depends on the
    event that bound the writer it reads).

    Canonical payload shapes (the ONLY compared fields):
      UNIFY        -> {"outcome": "success"|"suspend"|"fail", "vars": [logical_var, ...]}
      SUSPEND      -> {"reader": logical_var, "goal": goal_id}
      REACTIVATE   -> {"writer": logical_var, "goal": goal_id}
      WRITER_BIND  -> {"writer": logical_var, "value_shape": canonical_term_shape}
      BYTECODE_OP  -> {"opcode": str, "logical_pc": int}
    """

    seq: int
    kind: EventKind
    payload: dict[str, Any]
    causes: frozenset[int] = field(default_factory=frozenset)

    def __post_init__(self) -> None:
        # Normalize causes to a frozenset of ints regardless of how the
        # parser supplied it (set/list/tuple) — keeps Event hashable and the
        # relation's set comparisons cheap.
        if not isinstance(self.causes, frozenset):
            object.__setattr__(self, "causes", frozenset(int(c) for c in self.causes))


@dataclass(frozen=True)
class Outcome:
    """Outcome-only projection (bonds, FR-005) — NO event list.

    ``bindings`` is a canonical (sorted, address-relabeled) representation of
    the final variable bindings; escrow-timer suspension is a valid status,
    compared as-is.
    """

    status: Status
    bindings: tuple[tuple[str, Any], ...] = ()


@dataclass(frozen=True)
class Trace:
    """An ordered list of events plus the run's final outcome.

    ``events`` is in capture order (the ``seq`` field is the authoritative
    order key). ``outcome`` is always present; ``compare_mode = outcome``
    (bonds) uses ONLY ``outcome`` and ignores ``events``.
    """

    events: tuple[Event, ...]
    outcome: Outcome

    def bytecode_spine(self) -> tuple[Event, ...]:
        """The BYTECODE_OP subsequence — the primary spine (FR-003)."""
        return tuple(e for e in self.events if e.kind is EventKind.BYTECODE_OP)

    def by_seq(self) -> dict[int, Event]:
        return {e.seq: e for e in self.events}
