"""Pure-stdlib Tarjan SCC + condensation level assignment.

Implements the contract in
``specs/015-codeconv-depgraph/contracts/depgraph_algorithm.md``: iterative
Tarjan SCC over an in-subtree import graph, followed by a Kahn-style
condensation-DAG topological level assignment. Output is deterministic by
construction — five rules at every choice point eliminate input-order
sensitivity:

1. Adjacency lists sorted before Tarjan.
2. Outer iteration in lexicographic order.
3. SCC members sorted; SCC ids assigned in completion order.
4. Kahn worklist seeded in lex order (min-heap).
5. (Caller responsibility) final per-file ordering by
   (topo_level, cycle_group_id, path).

The module has no dependency outside the Python standard library.
"""

from __future__ import annotations

import heapq
from dataclasses import dataclass
from typing import Sequence


@dataclass(frozen=True)
class DepgraphResult:
    """Pure-function result of running the algorithm on a graph."""

    # path -> SCC id. Singletons get unique ids; multi-file SCCs share an id.
    cycle_group_id: dict[str, int]

    # path -> topological level in the condensation DAG. 0 = leaves.
    topo_level: dict[str, int]

    # Count of multi-file SCCs (singleton SCCs excluded).
    cycle_count: int

    # path -> sorted list of in-subtree paths this file depends on.
    dependencies: dict[str, list[str]]

    # path -> sorted list of in-subtree paths that depend on this file.
    callers: dict[str, list[str]]


def compute(
    nodes: Sequence[str],
    edges: Sequence[tuple[str, str]],
) -> DepgraphResult:
    """Compute SCC decomposition + condensation topological levels.

    Args:
        nodes: All inventoried file paths (subtree-relative POSIX).
            MUST be the full node set — the algorithm does NOT infer
            nodes from edges (isolated files have neither incoming nor
            outgoing edges and must still be reported).
        edges: List of ``(from_path, to_path)`` tuples. Both endpoints
            MUST be in ``nodes``; ``ValueError`` is raised otherwise.
            Self-loops are valid and form a singleton SCC.

    Returns:
        :class:`DepgraphResult`.

    Raises:
        ValueError: edge endpoint not in nodes.
    """
    # Step 1 — normalise input
    sorted_nodes = sorted(set(nodes))
    node_set = set(sorted_nodes)
    adj: dict[str, list[str]] = {n: [] for n in sorted_nodes}
    radj: dict[str, list[str]] = {n: [] for n in sorted_nodes}
    raw_adj: dict[str, set[str]] = {n: set() for n in sorted_nodes}
    raw_radj: dict[str, set[str]] = {n: set() for n in sorted_nodes}
    for u, v in edges:
        if u not in node_set:
            raise ValueError(f"edge endpoint not in nodes: {u!r}")
        if v not in node_set:
            raise ValueError(f"edge endpoint not in nodes: {v!r}")
        raw_adj[u].add(v)
        raw_radj[v].add(u)
    for n in sorted_nodes:
        adj[n] = sorted(raw_adj[n])
        radj[n] = sorted(raw_radj[n])

    # Step 2 — iterative Tarjan SCC
    index_counter = 0
    node_index: dict[str, int] = {}
    node_lowlink: dict[str, int] = {}
    on_stack: set[str] = set()
    ssc_stack: list[str] = []
    scc_of: dict[str, int] = {}
    scc_members: dict[int, list[str]] = {}
    next_scc_id = 0

    for root in sorted_nodes:
        if root in node_index:
            continue
        # work_stack frames: (node, child_idx)
        work_stack: list[list] = [[root, 0]]
        node_index[root] = index_counter
        node_lowlink[root] = index_counter
        index_counter += 1
        ssc_stack.append(root)
        on_stack.add(root)

        while work_stack:
            node, child_idx = work_stack[-1]
            children = adj[node]
            if child_idx < len(children):
                work_stack[-1][1] = child_idx + 1
                v = children[child_idx]
                if v not in node_index:
                    node_index[v] = index_counter
                    node_lowlink[v] = index_counter
                    index_counter += 1
                    ssc_stack.append(v)
                    on_stack.add(v)
                    work_stack.append([v, 0])
                elif v in on_stack:
                    if node_index[v] < node_lowlink[node]:
                        node_lowlink[node] = node_index[v]
            else:
                if node_lowlink[node] == node_index[node]:
                    members: list[str] = []
                    while True:
                        w = ssc_stack.pop()
                        on_stack.discard(w)
                        members.append(w)
                        if w == node:
                            break
                    members.sort()
                    scc_members[next_scc_id] = members
                    for m in members:
                        scc_of[m] = next_scc_id
                    next_scc_id += 1
                work_stack.pop()
                if work_stack:
                    parent = work_stack[-1][0]
                    if node_lowlink[node] < node_lowlink[parent]:
                        node_lowlink[parent] = node_lowlink[node]

    # Step 3 — condensation DAG
    cond_succ: dict[int, set[int]] = {s: set() for s in scc_members}
    cond_pred: dict[int, set[int]] = {s: set() for s in scc_members}
    for u in sorted_nodes:
        su = scc_of[u]
        for v in adj[u]:
            sv = scc_of[v]
            if su != sv:
                cond_succ[su].add(sv)
                cond_pred[sv].add(su)

    # Step 4 — Kahn-style level assignment (leaves = level 0)
    out_count: dict[int, int] = {s: len(cond_succ[s]) for s in scc_members}
    scc_level: dict[int, int] = {}
    # Min-heap seeded with SCC ids whose condensation out-degree is 0 (leaves).
    heap: list[int] = sorted(s for s, c in out_count.items() if c == 0)
    heapq.heapify(heap)
    while heap:
        s = heapq.heappop(heap)
        if cond_succ[s]:
            scc_level[s] = max(scc_level[t] for t in cond_succ[s]) + 1
        else:
            scc_level[s] = 0
        for pred in cond_pred[s]:
            out_count[pred] -= 1
            if out_count[pred] == 0:
                heapq.heappush(heap, pred)

    # Step 5 — map back to per-node values
    cycle_group_id = {n: scc_of[n] for n in sorted_nodes}
    topo_level = {n: scc_level[scc_of[n]] for n in sorted_nodes}
    cycle_count = sum(1 for members in scc_members.values() if len(members) > 1)

    # Step 6 — dependencies + callers per node
    dependencies = {n: list(adj[n]) for n in sorted_nodes}
    callers = {n: list(radj[n]) for n in sorted_nodes}

    return DepgraphResult(
        cycle_group_id=cycle_group_id,
        topo_level=topo_level,
        cycle_count=cycle_count,
        dependencies=dependencies,
        callers=callers,
    )


__all__ = ["DepgraphResult", "compute"]
