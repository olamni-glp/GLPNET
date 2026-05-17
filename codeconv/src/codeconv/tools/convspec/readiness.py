"""Pure convspec-readiness predicate + SCC batch (feature 018, T029).

Parallels feature-017 ``planagents/readiness.py`` (D2 — same proven
shape, not a new model). PURE: no bridge, no I/O. Consumes feature-015's
canonical depgraph snapshot (``cycle_group_id``/``topo_level``)
read-only and the ``codeconv.dart_convspecs`` two-phase facts; never
recomputes the graph.

Four states (exactly one per non-orphaned node):

- ``convspec_pending``     : no ``dart_convspecs`` row AND some
                             SCC-external dep is not yet ``specced``.
- ``convspec_ready``       : ``pending`` AND every SCC-external in-subtree
                             dep is ``specced`` (or no external deps).
- ``convspec_in_progress`` : a row exists, ``convspec_completed_at`` NULL.
- ``specced``              : a row exists, ``convspec_completed_at`` set.

Intra-SCC edges ignored for eligibility (mirrors feature-015 FR-006);
an in-progress spec does NOT unblock downstream files. An SCC is one
indivisible coordinated batch (FR-002).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Mapping, Optional

CONVSPEC_PENDING = "convspec_pending"
CONVSPEC_READY = "convspec_ready"
CONVSPEC_IN_PROGRESS = "convspec_in_progress"
SPECCED = "specced"


@dataclass(frozen=True)
class DepNode:
    topo_level: int
    cycle_group_id: int


@dataclass(frozen=True)
class SpecRow:
    """Presence ⇒ a ``dart_convspecs`` row exists. ``completed`` iff
    ``convspec_completed_at IS NOT NULL`` (the terminal two-phase write —
    a half-step is observably incomplete, FR-003)."""

    completed: bool


@dataclass(frozen=True)
class SelectedUnit:
    cycle_group_id: int
    topo_level: int
    members: tuple[str, ...]

    @property
    def is_scc_batch(self) -> bool:
        return len(self.members) > 1


def _members_by_scc(nodes: Mapping[str, DepNode]) -> dict[int, list[str]]:
    out: dict[int, list[str]] = {}
    for p, n in nodes.items():
        out.setdefault(n.cycle_group_id, []).append(p)
    for sid in out:
        out[sid].sort()
    return out


def _scc_fully_specced(members: list[str], specs: Mapping[str, SpecRow]) -> bool:
    for m in members:
        r = specs.get(m)
        if r is None or not r.completed:
            return False
    return True


def classify(
    path: str,
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    specs: Mapping[str, SpecRow],
    _members_by_scc_cache: Optional[Mapping[int, list[str]]] = None,
) -> str:
    if path not in nodes:
        raise KeyError(f"{path!r} not in depgraph node set (orphaned/unknown)")
    row = specs.get(path)
    if row is not None:
        return SPECCED if row.completed else CONVSPEC_IN_PROGRESS
    members_by_scc = (
        _members_by_scc_cache
        if _members_by_scc_cache is not None
        else _members_by_scc(nodes)
    )
    for dep in cross_scc_deps.get(path, frozenset()):
        dep_scc = members_by_scc.get(nodes[dep].cycle_group_id, [dep])
        if not _scc_fully_specced(dep_scc, specs):
            return CONVSPEC_PENDING
    return CONVSPEC_READY


def classify_all(
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    specs: Mapping[str, SpecRow],
) -> dict[str, str]:
    cache = _members_by_scc(nodes)
    return {
        p: classify(
            p,
            nodes=nodes,
            cross_scc_deps=cross_scc_deps,
            specs=specs,
            _members_by_scc_cache=cache,
        )
        for p in nodes
    }


def select_next(
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    specs: Mapping[str, SpecRow],
    limit: int = 7,
) -> list[SelectedUnit]:
    """Next batch of convspec-ready units, deterministic order
    ``(min topo_level, min path)``. SCC = one indivisible unit (never
    split — coordinated-batch integrity over the soft cap). Pure:
    identical inputs ⇒ identical output (FR-021)."""
    states = classify_all(
        nodes=nodes, cross_scc_deps=cross_scc_deps, specs=specs
    )
    members_by_scc = _members_by_scc(nodes)
    units: list[SelectedUnit] = []
    for sid in sorted(members_by_scc):
        members = members_by_scc[sid]
        if len(members) == 1:
            m = members[0]
            if states[m] == CONVSPEC_READY:
                units.append(
                    SelectedUnit(sid, nodes[m].topo_level, (m,))
                )
            continue
        ext: set[str] = set()
        for m in members:
            ext.update(cross_scc_deps.get(m, frozenset()))
        if not all(
            _scc_fully_specced(
                members_by_scc.get(nodes[d].cycle_group_id, [d]), specs
            )
            for d in ext
        ):
            continue
        unstarted = [m for m in members if m not in specs]
        has_row = [m for m in members if m in specs]
        if not has_row:
            emit = list(members)
        elif unstarted:
            emit = unstarted
        else:
            continue
        if emit:
            units.append(
                SelectedUnit(
                    sid,
                    min(nodes[m].topo_level for m in members),
                    tuple(sorted(emit)),
                )
            )
    units.sort(key=lambda u: (u.topo_level, min(u.members)))
    out: list[SelectedUnit] = []
    count = 0
    for u in units:
        if count >= limit:
            break
        out.append(u)
        count += len(u.members)
    return out


__all__ = [
    "CONVSPEC_IN_PROGRESS",
    "CONVSPEC_PENDING",
    "CONVSPEC_READY",
    "SPECCED",
    "DepNode",
    "SelectedUnit",
    "SpecRow",
    "classify",
    "classify_all",
    "select_next",
]
