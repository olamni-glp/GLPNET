"""Dirty-subgraph selection for ``depgraph mark-and-recompute`` (feature 062, US1).

Pure, stdlib-only graph logic (mirrors :mod:`algorithm`): given a set of
*marked* file paths and the import-edge list, compute the **dirty set** =
the marked nodes plus every node that (transitively) depends on a marked
node. Only that set is recomputed; every other node's recorded row is
preserved (contract ``specs/062-.../contracts/depgraph-cli.md`` §
``mark-and-recompute``, spec FR-001).

Edge orientation matches :mod:`algorithm`: an edge ``(u, v)`` means *u
imports v* (u depends on v). Therefore the *dependents* of a marked node
``X`` — the files whose readiness/level can change when ``X`` changes — are
reached by walking edges **backwards** (from ``v`` to its importers ``u``).

The result is deterministic (a plain set; callers sort when ordering
matters).
"""

from __future__ import annotations

from typing import Iterable, Sequence


def dirty_set(
    marked: Iterable[str],
    edges: Sequence[tuple[str, str]],
) -> set[str]:
    """Return ``marked`` ∪ all transitive dependents of ``marked``.

    Args:
        marked: file paths the developer marked for recompute.
        edges: ``(from_path, to_path)`` import edges; ``from_path`` depends
            on ``to_path`` (same orientation as :func:`algorithm.compute`).

    Returns:
        The dirty set: every marked node and every node that transitively
        depends on a marked node. Nodes not connected to any marked node
        are excluded (their recorded rows are preserved).
    """
    # Reverse adjacency: dependents[v] = {u : u imports v}.
    dependents: dict[str, set[str]] = {}
    for u, v in edges:
        dependents.setdefault(v, set()).add(u)

    dirty: set[str] = set()
    stack: list[str] = list(marked)
    while stack:
        node = stack.pop()
        if node in dirty:
            continue
        dirty.add(node)
        for importer in dependents.get(node, ()):
            if importer not in dirty:
                stack.append(importer)
    return dirty


__all__ = ["dirty_set"]
