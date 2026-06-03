"""Equiv-readiness predicate + curriculum-ordered batch selection (T023).

PURE — no bridge, no I/O, no LM (guarded by ``test_no_lm_on_production_path``).
Mirrors ``codegen.readiness`` (FR-002 / 015 contract) and ``planagents.readiness``:
the workflow layer reads the snapshot from the bridge and hands this module
plain dicts; this module classifies and orders.

Contract (subsystem_curriculum.md, FR-007/FR-008/FR-014):

  * A file is **equiv-ready** when (a) its own codegen is complete (a converted
    C# unit exists) AND (b) every cross-SCC dependency is codegen-complete AND
    equiv-done (the strict-tier gate: deps must be behaviourally faithful
    before this file's equivalence can be measured against them).
  * A file is **equiv-done** when every in-scope ``dart_equivalence`` row is
    ``verdict='equivalent'`` (outcome-equivalent for bonds; FR-014 promotion
    gate — compile/back-test/human alone do NOT promote).

Curriculum order (decision 8, FR-007): strict subsystems first
(``heap`` → ``bytecode`` → ``compiler`` → ``runtime-core``), ``multiagent``
last; within a subsystem, files in topological order (015, read-only).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Callable, Mapping, Optional


# Equiv-readiness states (exactly one per non-orphaned classified file).
EQUIV_NOT_READY = "equiv_not_ready"     # codegen incomplete OR a dep not done
EQUIV_READY = "equiv_ready"             # ready for capture+compare (no rows yet)
EQUIV_IN_PROGRESS = "equiv_in_progress" # some rows exist, not all equivalent yet
EQUIV_DONE = "equiv_done"               # every in-scope row is `equivalent`


# Curriculum rank — lower = converted earlier. ``multiagent`` LAST (FR-007),
# attacked from strength with the mature prompt (US4).
_SUBSYSTEM_ORDER: dict[str, int] = {
    "heap": 0,
    "bytecode": 1,
    "compiler": 2,
    "runtime-core": 3,
    "multiagent": 4,
}


@dataclass(frozen=True)
class DepNode:
    """Per-file canonical depgraph facts (feature-015, read-only)."""

    topo_level: int
    cycle_group_id: int


@dataclass(frozen=True)
class CodegenStatus:
    """Per-file codegen lifecycle: ``completed`` iff the converted C# unit
    has been built-and-accepted (019's ``codegen_completed_at IS NOT NULL``)."""

    completed: bool


@dataclass(frozen=True)
class EquivStatus:
    """Per-file ``dart_equivalence`` aggregate, injected by the workflow layer.

    ``total_in_scope`` is the number of rows EXPECTED for this file's in-scope
    corpus (the workflow computes it from corpus.py + the source-vs-file
    subsystem-relevance rule); ``equivalent`` / ``divergent`` count what the
    rows actually carry. ``done`` iff every in-scope row is equivalent and none
    is divergent (FR-014; stale rows are excluded from the aggregate by the
    caller per FR-016).
    """

    total_in_scope: int = 0
    equivalent: int = 0
    divergent: int = 0
    captured_not_compared: int = 0

    @property
    def done(self) -> bool:
        return (
            self.total_in_scope > 0
            and self.equivalent == self.total_in_scope
            and self.divergent == 0
        )


SubsystemOf = Callable[[str], Optional[str]]


def classify(
    path: str,
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    codegen: Mapping[str, CodegenStatus],
    equiv: Mapping[str, EquivStatus],
    subsystem_of: SubsystemOf,
) -> str:
    """Return the equiv state of ``path`` (exactly one of the four constants).

    Raises ``KeyError`` if ``path`` is not in ``nodes`` (orphaned/unknown —
    callers MUST exclude orphans before invoking, mirroring codegen/planagents
    readiness)."""
    if path not in nodes:
        raise KeyError(f"{path!r} not in depgraph node set (orphaned/unknown)")

    if subsystem_of(path) is None:
        return EQUIV_NOT_READY

    cg = codegen.get(path)
    if cg is None or not cg.completed:
        return EQUIV_NOT_READY

    for dep in cross_scc_deps.get(path, frozenset()):
        dep_cg = codegen.get(dep)
        if dep_cg is None or not dep_cg.completed:
            return EQUIV_NOT_READY
        dep_eq = equiv.get(dep)
        if dep_eq is None or not dep_eq.done:
            return EQUIV_NOT_READY

    own = equiv.get(path)
    if own is None or own.total_in_scope == 0:
        return EQUIV_READY
    if own.done:
        return EQUIV_DONE
    return EQUIV_IN_PROGRESS


def classify_all(
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    codegen: Mapping[str, CodegenStatus],
    equiv: Mapping[str, EquivStatus],
    subsystem_of: SubsystemOf,
) -> dict[str, str]:
    return {
        p: classify(
            p,
            nodes=nodes,
            cross_scc_deps=cross_scc_deps,
            codegen=codegen,
            equiv=equiv,
            subsystem_of=subsystem_of,
        )
        for p in nodes
    }


def curriculum_key(
    path: str,
    *,
    nodes: Mapping[str, DepNode],
    subsystem_of: SubsystemOf,
) -> tuple[int, int, str]:
    """Sort key: ``(subsystem-rank, topo_level, path)`` — strict first,
    multiagent last; unknown subsystems sort to the end (99)."""
    sub = subsystem_of(path)
    rank = _SUBSYSTEM_ORDER.get(sub or "", 99)
    return (rank, nodes[path].topo_level, path)


def select_next(
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    codegen: Mapping[str, CodegenStatus],
    equiv: Mapping[str, EquivStatus],
    subsystem_of: SubsystemOf,
    limit: int = 7,
) -> list[str]:
    """Next files to verify in curriculum order. PURE; deterministic.

    Includes ``EQUIV_READY`` (no rows yet) AND ``EQUIV_IN_PROGRESS`` (partial
    rows that can advance toward done). ``EQUIV_NOT_READY`` is gated;
    ``EQUIV_DONE`` is already promoted. Limit is a soft cap on file count.
    """
    states = classify_all(
        nodes=nodes,
        cross_scc_deps=cross_scc_deps,
        codegen=codegen,
        equiv=equiv,
        subsystem_of=subsystem_of,
    )
    advance = [EQUIV_READY, EQUIV_IN_PROGRESS]
    candidates = [p for p, s in states.items() if s in advance]
    candidates.sort(key=lambda p: curriculum_key(p, nodes=nodes, subsystem_of=subsystem_of))
    return candidates[:limit]


__all__ = [
    "EQUIV_DONE",
    "EQUIV_IN_PROGRESS",
    "EQUIV_NOT_READY",
    "EQUIV_READY",
    "CodegenStatus",
    "DepNode",
    "EquivStatus",
    "SubsystemOf",
    "classify",
    "classify_all",
    "curriculum_key",
    "select_next",
]
