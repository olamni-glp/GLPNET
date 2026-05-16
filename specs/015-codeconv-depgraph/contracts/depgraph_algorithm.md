# Contract: `codeconv.tools.depgraph.algorithm` — Tarjan SCC + condensation level assignment

This document specifies the API and behaviour of the pure-Python algorithm module that computes the topological ordering. The implementation in `codeconv/src/codeconv/tools/depgraph/algorithm.py` follows it exactly. Any deviation is a bug.

## Source of truth references

- Spec FRs covered: FR-004 (SCC + level invariant), FR-005 (cycle_group_id assignment + cycle_count metric), FR-006 (status derivation, partial — algorithm computes `topo_level` + `cycle_group_id`; status is computed by `workflow.py` from these + `dart_conversions`), FR-015 (determinism)
- Research notes: R1 (Tarjan choice + determinism rules), R8 (idempotence)
- Spec acceptance criteria covered: SC-003 (edge invariant), SC-006 (cycle fixture), SC-002 (byte-identical re-run)

## Module surface

```python
# codeconv/src/codeconv/tools/depgraph/algorithm.py

from dataclasses import dataclass
from typing import Mapping, Sequence


@dataclass(frozen=True)
class DepgraphResult:
    """Pure-function result of running the algorithm on a graph."""

    # path -> SCC id (the cycle_group_id column).
    # Singletons get unique ids; multi-file SCCs share an id.
    cycle_group_id: dict[str, int]

    # path -> topological level in the condensation DAG. 0 = leaves.
    topo_level: dict[str, int]

    # Count of multi-file SCCs (singleton SCCs excluded).
    # This is the cycle_count metric per spec FR-005.
    cycle_count: int

    # path -> sorted list of in-subtree paths this file depends on.
    # (Used downstream to compute dependency_count column.)
    dependencies: dict[str, list[str]]

    # path -> sorted list of in-subtree paths that depend on this file.
    # (Used downstream to compute caller_count column.)
    callers: dict[str, list[str]]


def compute(
    nodes: Sequence[str],
    edges: Sequence[tuple[str, str]],
) -> DepgraphResult:
    """Compute SCC decomposition + condensation topological levels.

    Args:
        nodes: All inventoried file paths (subtree-relative POSIX).
               MUST be the full node set; the algorithm does NOT infer
               nodes from edges (because some inventoried files are
               isolated — neither importer nor imported).
        edges: List of (from_path, to_path) tuples. Both endpoints
               MUST be in `nodes` (caller responsibility — algorithm
               raises ValueError if an edge references an unknown node).
               Self-loops (from_path == to_path) are valid and form
               a single-element SCC with a self-edge.

    Returns:
        DepgraphResult — see fields above.

    Determinism guarantee: two consecutive calls with the same `nodes`
    and `edges` (regardless of input ordering) return byte-identical
    output. See § Determinism below.

    Raises:
        ValueError: edge endpoint not in nodes.
    """
```

## Algorithm

### Step 1 — Normalise input

1. Copy `nodes` to a sorted list `N = sorted(nodes)`.
2. Build the adjacency list `adj: dict[str, list[str]] = {n: [] for n in N}`. For each `(u, v)` in `edges`: if `u` or `v` is not in `N`, raise `ValueError`; else append `v` to `adj[u]`.
3. Sort every adjacency list: `adj[n] = sorted(set(adj[n]))` — also dedups parallel edges (defensive; `dart_imports` already enforces UNIQUE).
4. Build the reverse adjacency list `radj: dict[str, list[str]]` symmetrically.

This step alone guarantees that two equal inputs (regardless of original list ordering) produce the same internal state by the time Tarjan starts.

### Step 2 — Iterative Tarjan SCC

Standard Tarjan, iterative form using an explicit stack to track the DFS recursion. Pseudo-Python:

```python
index_counter = 0
node_index: dict[str, int] = {}
node_lowlink: dict[str, int] = {}
on_stack: set[str] = set()
ssc_stack: list[str] = []   # Tarjan SCC stack
scc_of: dict[str, int] = {}
next_scc_id = 0

for root in N:                                  # rule 2: outer iteration in lex order
    if root in node_index:
        continue
    work_stack: list[tuple[str, int]] = [(root, 0)]
    while work_stack:
        node, child_idx = work_stack[-1]
        if child_idx == 0:
            node_index[node] = index_counter
            node_lowlink[node] = index_counter
            index_counter += 1
            ssc_stack.append(node)
            on_stack.add(node)
        children = adj[node]
        if child_idx < len(children):
            work_stack[-1] = (node, child_idx + 1)
            v = children[child_idx]
            if v not in node_index:
                work_stack.append((v, 0))
            elif v in on_stack:
                node_lowlink[node] = min(node_lowlink[node], node_index[v])
        else:
            if node_lowlink[node] == node_index[node]:
                members: list[str] = []
                while True:
                    w = ssc_stack.pop()
                    on_stack.discard(w)
                    members.append(w)
                    if w == node:
                        break
                # rule 3: members sorted lex; SCC id assigned in completion order
                members.sort()
                for m in members:
                    scc_of[m] = next_scc_id
                next_scc_id += 1
            work_stack.pop()
            if work_stack:
                parent_node, _ = work_stack[-1]
                node_lowlink[parent_node] = min(
                    node_lowlink[parent_node], node_lowlink[node]
                )
```

### Step 3 — Condensation DAG construction

For each `(u, v)` in `edges` where `scc_of[u] != scc_of[v]`, add a condensation edge `scc_of[u] → scc_of[v]`. Dedup. Sort condensation adjacency lists in numeric SCC-id order (deterministic).

### Step 4 — Condensation topological levels (Kahn, reverse-topological)

Compute `topo_level[scc_id]` for every SCC such that for every condensation edge `a → b`, `topo_level[a] > topo_level[b]`. Leaves (SCCs with no outgoing condensation edges — i.e. no in-subtree dependencies outside their own SCC) are level 0.

```python
# rule 4: seed worklist in lex order over (level 0 = no out-edges in condensation)
out_count: dict[int, int] = {s: len(cond_adj[s]) for s in cond_adj}
scc_level: dict[int, int] = {}
ready_q: list[int] = sorted(s for s, c in out_count.items() if c == 0)
while ready_q:
    s = ready_q.pop(0)
    scc_level[s] = max(
        [scc_level[t] for t in cond_adj_orig[s] if t in scc_level] or [-1]
    ) + 1
    # Special: leaves get level 0 by the max([]) + 1 = 0 shape if cond_adj is empty
    # but we need leaves at exactly 0, so:
    if not cond_adj_orig.get(s):
        scc_level[s] = 0
    for pred in rev_cond_adj.get(s, []):
        out_count[pred] -= 1
        if out_count[pred] == 0:
            # rule 4: insert in lex order — actually use a sorted list maintained as insertion
            ...
```

(The pseudo-Python above is illustrative; the production code uses `heapq` to maintain a min-heap of SCC ids — equivalent to lex-ordered insertion.)

### Step 5 — Map back to per-node values

`cycle_group_id[path] = scc_of[path]` for every `path` in `N`.
`topo_level[path] = scc_level[scc_of[path]]` for every `path` in `N`.
`cycle_count = sum(1 for s, members in scc_members.items() if len(members) > 1)` — i.e. count of multi-file SCCs only.

### Step 6 — Compute dependencies + callers per node

For each `n` in `N`: `dependencies[n] = sorted(adj[n])`; `callers[n] = sorted(radj[n])`. These are passed back to `workflow.py` to populate the `dependency_count` and `caller_count` columns (and to populate the per-file JSON rows' `depends_on` / `depended_on_by` arrays per FR-007).

## Determinism (FR-015 / R1 § Determinism rules)

| Rule | Where enforced |
|---|---|
| 1. Adjacency lists sorted before Tarjan | Step 1.3 |
| 2. Outer iteration in lex order | Step 2 `for root in N` |
| 3. SCC members sorted; SCC ids assigned in completion order | Step 2 `members.sort()` and `next_scc_id += 1` |
| 4. Kahn worklist seeded in lex order (min-heap) | Step 4 |
| 5. Final per-file ordering `(topo_level, cycle_group_id, path)` | Caller responsibility (in `workflow.py`'s emit step) |

## Invariants

For every `(u, v)` in `edges`:

**Invariant A** (FR-004 / SC-003): EITHER `topo_level[u] > topo_level[v]` (cross-SCC edge) OR `cycle_group_id[u] == cycle_group_id[v]` (intra-SCC edge).

Proof sketch: if `scc_of[u] == scc_of[v]`, the second disjunct holds. Else the edge is a condensation edge from `scc_of[u]` to `scc_of[v]`; by Step 4's construction, `scc_level[scc_of[u]] > scc_level[scc_of[v]]`; the first disjunct holds.

**Invariant B** (FR-005): `cycle_group_id[path]` is unique for singleton SCCs and shared for multi-file SCCs. The `cycle_count` metric equals the number of multi-file SCCs. Singleton SCCs (including those with self-loops) are NOT counted toward `cycle_count` per spec line 100 — but their `cycle_group_id` value is still unique to them. A self-loop on path X forms a singleton SCC: `cycle_group_id[X]` is unique, `cycle_count` is unchanged by X's self-loop.

**Invariant C** (FR-015 deterministic output): the (cycle_group_id, topo_level) assignment is a function of the abstract graph structure ONLY, not of the input ordering of `nodes` or `edges`. Equivalent graphs produce equivalent assignments; permutations of input lists produce byte-identical outputs.

## Test obligations

The implementation MUST pass tests for:

1. **Linear chain** A→B→C→D: A:level 3, B:level 2, C:level 1, D:level 0; all singleton cycle_groups; `cycle_count == 0`.
2. **Diamond** A→B, A→C, B→D, C→D: A:level 2, B:level 1, C:level 1, D:level 0; all singletons.
3. **3-cycle** A→B→C→A: A, B, C share one `cycle_group_id`, share one `topo_level == 0`; `cycle_count == 1`.
4. **3-cycle plus tail** A→B→C→A, D→A: D:level 1, A/B/C share level 0; cycle_count == 1.
5. **Self-loop** A→A: A is a singleton SCC (single member, unique cycle_group_id), `cycle_count == 0` (self-loop is not multi-file).
6. **Isolated nodes** A, B, C with no edges: all level 0, all singleton.
7. **Determinism**: same graph passed with input lists shuffled — output is byte-identical for `cycle_group_id`, `topo_level`, `dependencies`, `callers`.
8. **Unknown edge endpoint**: `edges=[("A", "X")]` with `nodes=["A"]` — raises `ValueError`.

These tests live in `codeconv/tests/test_depgraph_algorithm.py` and need no bridge (pure pure-stdlib algorithm).
