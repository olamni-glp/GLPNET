"""Pure plan-readiness predicate + topo/SCC batch selection.

Implements ``specs/017-conversion-plan-agents/contracts/
plan_readiness_algorithm.md`` (spec FR-003, FR-004, FR-011, FR-021;
SC-002, SC-006). This module is PURE: no bridge, no I/O, no ``.dart``
parse. It consumes feature-015's canonical depgraph snapshot
(``cycle_group_id`` / ``topo_level``) read-only and MUST NOT recompute
or redefine it. The workflow layer performs all DB reads/writes and
hands this module plain dicts.

Four-state classification (FR-004 — exactly one per non-orphaned node):

- ``plan_pending``      : no row in ``codeconv.dart_plans``, and at least
                          one SCC-external dependency is not yet planned.
- ``plan_ready``        : ``plan_pending`` AND every SCC-external
                          in-subtree dependency is ``planned`` (or the
                          file's SCC has no external deps).
- ``plan_in_progress``  : a ``dart_plans`` row exists with
                          ``plan_completed_at IS NULL``.
- ``planned``           : a ``dart_plans`` row exists with
                          ``plan_completed_at IS NOT NULL``.

Intra-SCC edges are ignored for eligibility (mirrors feature-015 FR-006).
An in-progress plan does NOT unblock downstream files.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Mapping, Optional


# State constants (the four FR-004 states).
PLAN_PENDING = "plan_pending"
PLAN_READY = "plan_ready"
PLAN_IN_PROGRESS = "plan_in_progress"
PLANNED = "planned"


@dataclass(frozen=True)
class DepNode:
    """Per-file canonical depgraph facts (feature-015, read-only).

    ``topo_level`` and ``cycle_group_id`` come straight from
    ``codeconv.dart_depgraph``; this module never recomputes them.
    """

    topo_level: int
    cycle_group_id: int


@dataclass(frozen=True)
class PlanRow:
    """Per-file ``codeconv.dart_plans`` lifecycle facts.

    Presence of an instance for a path means a row exists. ``completed``
    is True iff ``plan_completed_at IS NOT NULL``.
    """

    completed: bool


@dataclass(frozen=True)
class SelectedUnit:
    """One unit returned by :func:`select_next`.

    A singleton file is a unit of one member. A multi-file SCC is one
    unit whose members are all the SCC's paths (lexicographically
    ordered). ``cycle_group_id`` is shared by all members.
    """

    cycle_group_id: int
    topo_level: int
    members: tuple[str, ...]

    @property
    def is_scc_batch(self) -> bool:
        return len(self.members) > 1


def classify(
    path: str,
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    plans: Mapping[str, PlanRow],
    _members_by_scc_cache: Optional[Mapping[int, list[str]]] = None,
) -> str:
    """Return the FR-004 state of ``path``.

    Args:
        path: a non-orphaned inventoried file path.
        nodes: path -> :class:`DepNode` (feature-015 canonical facts).
        cross_scc_deps: path -> frozenset of SCC-external in-subtree
            dependency paths (intra-SCC edges already excluded by the
            caller — mirrors feature-015 FR-006).
        plans: path -> :class:`PlanRow` for files that have a
            ``dart_plans`` row (absent ⇒ no row).

    Raises:
        KeyError: ``path`` not in ``nodes`` (orphaned/unknown — the
            caller MUST exclude orphans before calling, FR-020).
    """
    if path not in nodes:
        raise KeyError(f"{path!r} not in depgraph node set (orphaned/unknown)")

    row = plans.get(path)
    if row is not None:
        return PLANNED if row.completed else PLAN_IN_PROGRESS

    # No row ⇒ plan_pending unless every SCC-external dependency is
    # satisfied. Downstream gating (plan_readiness_algorithm.md § "SCC
    # coordinated-batch rule" → "Downstream gating"): a file with a
    # cross-SCC dependency on a member of a multi-file SCC ``S`` is
    # plan-ready only when EVERY member of ``S`` is PLANNED — not merely
    # the one dep file. (Mirrors feature-015 _derive_statuses, which
    # tests SCC-external deps at SCC granularity.) For a singleton-SCC
    # dep this reduces to "that file is PLANNED".
    members_by_scc = (
        _members_by_scc_cache
        if _members_by_scc_cache is not None
        else _members_by_scc(nodes)
    )
    deps = cross_scc_deps.get(path, frozenset())
    for dep in deps:
        dep_scc = members_by_scc.get(nodes[dep].cycle_group_id, [dep])
        if not _scc_fully_planned(dep_scc, plans):
            return PLAN_PENDING
    return PLAN_READY


def _scc_fully_planned(
    members: list[str], plans: Mapping[str, "PlanRow"]
) -> bool:
    """True iff every member of the SCC has a completed plan."""
    for m in members:
        r = plans.get(m)
        if r is None or not r.completed:
            return False
    return True


def classify_all(
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    plans: Mapping[str, PlanRow],
) -> dict[str, str]:
    """Classify every node. Deterministic; key set == ``nodes`` keys."""
    cache = _members_by_scc(nodes)
    return {
        p: classify(
            p,
            nodes=nodes,
            cross_scc_deps=cross_scc_deps,
            plans=plans,
            _members_by_scc_cache=cache,
        )
        for p in nodes
    }


def _members_by_scc(
    nodes: Mapping[str, DepNode],
) -> dict[int, list[str]]:
    out: dict[int, list[str]] = {}
    for p, n in nodes.items():
        out.setdefault(n.cycle_group_id, []).append(p)
    for sid in out:
        out[sid].sort()
    return out


def _scc_external_deps(
    members: list[str],
    cross_scc_deps: Mapping[str, frozenset[str]],
) -> set[str]:
    ext: set[str] = set()
    for m in members:
        ext.update(cross_scc_deps.get(m, frozenset()))
    return ext


def select_next(
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    plans: Mapping[str, PlanRow],
    limit: int = 7,
) -> list[SelectedUnit]:
    """Return the next batch of plan-ready units in deterministic order.

    Algorithm (``plan_readiness_algorithm.md`` § "Selection order"):

    1. Classify all nodes.
    2. Candidate set = all ``PLAN_READY`` files, PLUS un-started members
       of any partially-started SCC whose external deps are all
       ``PLANNED`` (resume — edge case "SCC member subset already
       planned").
    3. Group candidates into units: a singleton file is one unit; all
       members of one ``cycle_group_id`` form one unit (SCC batch).
    4. Order units by ``(min(topo_level) ASC, min(path) ASC)``; SCC
       members lexicographic by ``path``.
    5. Flatten to a tombstone list; take whole units until ``limit``
       tombstones accumulated — an SCC unit is NEVER split (coordinated
       batch integrity wins over the soft cap; R3).
    6. Never include a file with a ``dart_plans`` row whose
       ``plan_completed_at IS NULL`` UNLESS it is an explicit resume of
       a partially-started SCC (so a crashed-agent file is not
       double-spawned — FR-014 idempotent recovery).

    ``select_next`` is pure: identical inputs ⇒ identical output
    (FR-021 determinism).
    """
    states = classify_all(
        nodes=nodes, cross_scc_deps=cross_scc_deps, plans=plans
    )
    members_by_scc = _members_by_scc(nodes)

    units: list[SelectedUnit] = []

    for sid in sorted(members_by_scc):
        members = members_by_scc[sid]  # already sorted

        if len(members) == 1:
            # Singleton SCC. Eligible iff PLAN_READY (step 2). A
            # PLAN_IN_PROGRESS singleton is never re-emitted (step 6).
            m = members[0]
            if states[m] == PLAN_READY:
                units.append(
                    SelectedUnit(
                        cycle_group_id=sid,
                        topo_level=nodes[m].topo_level,
                        members=(m,),
                    )
                )
            continue

        # Multi-file SCC. It is a NEWLY-ready batch iff NO member has a
        # dart_plans row yet AND every SCC-external dep of any member is
        # PLANNED. It is RESUMABLE iff some (but not all) members have
        # rows, others do not, and the external deps are all PLANNED —
        # then the un-started members are re-selected (partial-batch
        # resume). Either way the unit's members are exactly the
        # un-started members (a fully-fresh batch ⇒ all members).
        ext = _scc_external_deps(members, cross_scc_deps)
        # Downstream gating at SCC granularity: an external dep that is
        # itself a member of a multi-file SCC requires that WHOLE SCC
        # planned (consistent with the corrected classify()).
        ext_all_planned = all(
            _scc_fully_planned(
                members_by_scc.get(nodes[d].cycle_group_id, [d]), plans
            )
            for d in ext
        )
        if not ext_all_planned:
            continue

        unstarted = [m for m in members if m not in plans]
        has_row = [m for m in members if m in plans]

        if not has_row:
            # Fully fresh, externally unblocked ⇒ emit the whole SCC
            # as one coordinated batch (FR-011 / R4).
            emit = list(members)
        elif unstarted:
            # Interrupted batch (partial-batch resume,
            # plan_readiness_algorithm.md § "SCC coordinated-batch rule"):
            # SOME members have rows and OTHERS do not. The un-started
            # members are selectable so the batch can be
            # completed/resumed — UNCONDITIONALLY on whether the rowed
            # members are completed or still in progress. Already-rowed
            # members are NOT re-spawned (step 6 idempotent recovery);
            # downstream stays blocked (classify gates it) until every
            # member is PLANNED.
            emit = unstarted
        else:
            # Every member has a row (in progress and/or done): nothing
            # new to spawn for this SCC — downstream stays gated by
            # classify() until every member is PLANNED.
            continue

        if emit:
            units.append(
                SelectedUnit(
                    cycle_group_id=sid,
                    topo_level=min(nodes[m].topo_level for m in members),
                    members=tuple(sorted(emit)),
                )
            )

    # Step 4 — order units by (min topo_level, min path).
    units.sort(key=lambda u: (u.topo_level, min(u.members)))

    # Step 5 — take whole units until `limit` tombstones accumulated.
    out: list[SelectedUnit] = []
    count = 0
    for u in units:
        if count >= limit:
            break
        out.append(u)
        count += len(u.members)
    return out


def remaining_ready_count(
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    plans: Mapping[str, PlanRow],
    selected: list[SelectedUnit],
) -> int:
    """How many plan-ready tombstones remain AFTER ``selected``.

    Used to populate the ``remaining_ready`` field of the ``next`` JSON
    (``planagents_cli.md``). Counts every PLAN_READY file plus
    resumable un-started SCC members, minus those already in
    ``selected``.
    """
    full = select_next(
        nodes=nodes,
        cross_scc_deps=cross_scc_deps,
        plans=plans,
        limit=10**9,
    )
    total = sum(len(u.members) for u in full)
    taken = sum(len(u.members) for u in selected)
    return max(0, total - taken)


__all__ = [
    "PLANNED",
    "PLAN_IN_PROGRESS",
    "PLAN_PENDING",
    "PLAN_READY",
    "DepNode",
    "PlanRow",
    "SelectedUnit",
    "classify",
    "classify_all",
    "remaining_ready_count",
    "select_next",
]
