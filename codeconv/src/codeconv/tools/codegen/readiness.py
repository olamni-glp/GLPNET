"""Pure codegen-readiness predicate + topo/SCC batch selection.

Implements ``specs/019-codeconv-codegen/contracts/dbos_codegen_stage.md``
§ Placement ("codegen-readiness gates on all cross-SCC deps being
codegen-complete — mirrors plan-readiness") and FR-002. This module is
PURE: no bridge, no I/O, no ``.dart`` parse. It consumes feature-015's
canonical depgraph snapshot (``cycle_group_id`` / ``topo_level``)
read-only and MUST NOT recompute it. The workflow layer performs all DB
reads/writes and hands this module plain dicts.

It is the direct codegen analogue of feature-017's
``planagents.readiness`` — same four-state classification, same SCC
coordinated-batch + downstream-gating rules — with ``dart_plans`` /
"planned" replaced by ``dart_codegen`` / "codegen-complete" (a row whose
``codegen_completed_at IS NOT NULL`` — set ONLY on a build-passing
accept, data-model § State transitions).

Four-state classification (exactly one per non-orphaned node):

- ``codegen_pending``     : no row in ``codeconv.dart_codegen``, and at
                            least one SCC-external dependency is not yet
                            codegen-complete.
- ``codegen_ready``       : ``codegen_pending`` AND every SCC-external
                            in-subtree dependency is codegen-complete (or
                            the file's SCC has no external deps).
- ``codegen_in_progress`` : a ``dart_codegen`` row exists with
                            ``codegen_completed_at IS NULL``.
- ``codegen_done``        : a ``dart_codegen`` row exists that is
                            SATISFIED — ``codegen_completed_at IS NOT
                            NULL`` (built) OR ``no_emit`` (intentionally
                            not emitted to C#, Stage 4). Both unblock
                            downstream codegen; a ``no_emit`` file is
                            never ready/pending/in_progress.

Intra-SCC edges are ignored for eligibility (mirrors feature-015 FR-006).
An in-progress codegen does NOT unblock downstream files; a ``no_emit``
file DOES (it has nothing to emit, so a dependent never waits on it).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Mapping, Optional


# State constants (the four states).
CODEGEN_PENDING = "codegen_pending"
CODEGEN_READY = "codegen_ready"
CODEGEN_IN_PROGRESS = "codegen_in_progress"
CODEGEN_DONE = "codegen_done"


@dataclass(frozen=True)
class DepNode:
    """Per-file canonical depgraph facts (feature-015, read-only)."""

    topo_level: int
    cycle_group_id: int


@dataclass(frozen=True)
class CodegenRow:
    """Per-file ``codeconv.dart_codegen`` lifecycle facts.

    Presence of an instance for a path means a row exists. ``completed``
    is True iff ``codegen_completed_at IS NOT NULL`` (a build-passing
    accept). ``no_emit`` is True iff the file is intentionally NOT emitted
    to C# (Stage 4). EITHER condition unblocks downstream codegen: a
    ``no_emit`` file is SATISFIED — its downstreams treat it exactly like
    a completed file, and it is itself never ``codegen_ready``/pending.
    """

    completed: bool
    no_emit: bool = False

    @property
    def satisfied(self) -> bool:
        """True iff this row unblocks downstream codegen (built OR no_emit)."""
        return self.completed or self.no_emit


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
    rows: Mapping[str, CodegenRow],
    _members_by_scc_cache: Optional[Mapping[int, list[str]]] = None,
) -> str:
    """Return the codegen state of ``path``.

    Args:
        path: a non-orphaned inventoried file path.
        nodes: path -> :class:`DepNode` (feature-015 canonical facts).
        cross_scc_deps: path -> frozenset of SCC-external in-subtree
            dependency paths (intra-SCC edges already excluded by the
            caller — mirrors feature-015 FR-006).
        rows: path -> :class:`CodegenRow` for files with a
            ``dart_codegen`` row (absent ⇒ no row).

    Raises:
        KeyError: ``path`` not in ``nodes`` (orphaned/unknown — the
            caller MUST exclude orphans before calling).
    """
    if path not in nodes:
        raise KeyError(f"{path!r} not in depgraph node set (orphaned/unknown)")

    row = rows.get(path)
    if row is not None:
        # no_emit OR completed ⇒ done (satisfied); a no_emit file is never
        # in_progress/ready/pending (Stage 4 — it has nothing to emit).
        return CODEGEN_DONE if row.satisfied else CODEGEN_IN_PROGRESS

    # No row ⇒ codegen_pending unless every SCC-external dependency is
    # codegen-complete. Downstream gating at SCC granularity: a file with
    # a cross-SCC dependency on a member of a multi-file SCC ``S`` is
    # codegen-ready only when EVERY member of ``S`` is codegen-complete.
    # For a singleton-SCC dep this reduces to "that file is done".
    members_by_scc = (
        _members_by_scc_cache
        if _members_by_scc_cache is not None
        else _members_by_scc(nodes)
    )
    deps = cross_scc_deps.get(path, frozenset())
    for dep in deps:
        dep_scc = members_by_scc.get(nodes[dep].cycle_group_id, [dep])
        if not _scc_fully_done(dep_scc, rows):
            return CODEGEN_PENDING
    return CODEGEN_READY


def _scc_fully_done(
    members: list[str], rows: Mapping[str, "CodegenRow"]
) -> bool:
    """True iff every member of the SCC is SATISFIED (built OR no_emit).

    A ``no_emit`` member counts as satisfied for downstream readiness —
    it has nothing to emit, so it never blocks a dependent (Stage 4).
    """
    for m in members:
        r = rows.get(m)
        if r is None or not r.satisfied:
            return False
    return True


def classify_all(
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    rows: Mapping[str, CodegenRow],
) -> dict[str, str]:
    """Classify every node. Deterministic; key set == ``nodes`` keys."""
    cache = _members_by_scc(nodes)
    return {
        p: classify(
            p,
            nodes=nodes,
            cross_scc_deps=cross_scc_deps,
            rows=rows,
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
    rows: Mapping[str, CodegenRow],
    limit: int = 7,
) -> list[SelectedUnit]:
    """Return the next batch of codegen-ready units in deterministic order.

    Algorithm (mirrors feature-017 ``planagents.readiness.select_next``):

    1. Classify all nodes.
    2. Candidate set = all ``CODEGEN_READY`` files, PLUS un-started
       members of any partially-started SCC whose external deps are all
       codegen-complete (resume).
    3. Group candidates into units: a singleton file is one unit; all
       members of one ``cycle_group_id`` form one unit (SCC batch).
    4. Order units by ``(min(topo_level) ASC, min(path) ASC)``; SCC
       members lexicographic by ``path``.
    5. Flatten to a file list; take whole units until ``limit`` files
       accumulated — an SCC unit is NEVER split (coordinated-batch
       integrity wins over the soft cap; FR-002).
    6. Never include a file with a ``dart_codegen`` row whose
       ``codegen_completed_at IS NULL`` UNLESS it is an explicit resume
       of a partially-started SCC (so a crashed file is not
       double-spawned — idempotent recovery).

    ``select_next`` is pure: identical inputs ⇒ identical output.
    """
    states = classify_all(
        nodes=nodes, cross_scc_deps=cross_scc_deps, rows=rows
    )
    members_by_scc = _members_by_scc(nodes)

    units: list[SelectedUnit] = []

    for sid in sorted(members_by_scc):
        members = members_by_scc[sid]  # already sorted

        if len(members) == 1:
            m = members[0]
            if states[m] == CODEGEN_READY:
                units.append(
                    SelectedUnit(
                        cycle_group_id=sid,
                        topo_level=nodes[m].topo_level,
                        members=(m,),
                    )
                )
            continue

        # Multi-file SCC: newly-ready iff NO member has a row AND every
        # SCC-external dep is codegen-complete; resumable iff some (not
        # all) members have rows. Either way the emitted members are the
        # un-started ones (a fully-fresh batch ⇒ all members).
        ext = _scc_external_deps(members, cross_scc_deps)
        ext_all_done = all(
            _scc_fully_done(
                members_by_scc.get(nodes[d].cycle_group_id, [d]), rows
            )
            for d in ext
        )
        if not ext_all_done:
            continue

        unstarted = [m for m in members if m not in rows]
        has_row = [m for m in members if m in rows]

        if not has_row:
            emit = list(members)
        elif unstarted:
            emit = unstarted
        else:
            continue

        if emit:
            units.append(
                SelectedUnit(
                    cycle_group_id=sid,
                    topo_level=min(nodes[m].topo_level for m in members),
                    members=tuple(sorted(emit)),
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


def remaining_ready_count(
    *,
    nodes: Mapping[str, DepNode],
    cross_scc_deps: Mapping[str, frozenset[str]],
    rows: Mapping[str, CodegenRow],
    selected: list[SelectedUnit],
) -> int:
    """How many codegen-ready files remain AFTER ``selected``."""
    full = select_next(
        nodes=nodes,
        cross_scc_deps=cross_scc_deps,
        rows=rows,
        limit=10**9,
    )
    total = sum(len(u.members) for u in full)
    taken = sum(len(u.members) for u in selected)
    return max(0, total - taken)


__all__ = [
    "CODEGEN_DONE",
    "CODEGEN_IN_PROGRESS",
    "CODEGEN_PENDING",
    "CODEGEN_READY",
    "CodegenRow",
    "DepNode",
    "SelectedUnit",
    "classify",
    "classify_all",
    "remaining_ready_count",
    "select_next",
]
