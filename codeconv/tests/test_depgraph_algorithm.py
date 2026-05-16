"""Pure-stdlib unit tests for the depgraph algorithm.

Maps to ``specs/015-codeconv-depgraph/contracts/depgraph_algorithm.md`` §
"Test obligations" items 1–8.
"""

from __future__ import annotations

import pytest

from codeconv.tools.depgraph.algorithm import DepgraphResult, compute


def test_linear_chain_levels() -> None:
    """A→B→C→D — D leaf (level 0), A at level 3."""
    result = compute(
        nodes=["A", "B", "C", "D"],
        edges=[("A", "B"), ("B", "C"), ("C", "D")],
    )
    assert result.topo_level == {"A": 3, "B": 2, "C": 1, "D": 0}
    # Singleton SCCs only: every cycle_group_id is unique.
    assert len(set(result.cycle_group_id.values())) == 4
    assert result.cycle_count == 0


def test_diamond_levels() -> None:
    """A→B, A→C, B→D, C→D — D leaf, B and C both level 1, A at level 2."""
    result = compute(
        nodes=["A", "B", "C", "D"],
        edges=[("A", "B"), ("A", "C"), ("B", "D"), ("C", "D")],
    )
    assert result.topo_level == {"A": 2, "B": 1, "C": 1, "D": 0}
    assert len(set(result.cycle_group_id.values())) == 4
    assert result.cycle_count == 0


def test_three_cycle_shares_group_and_level() -> None:
    """A→B→C→A — single multi-file SCC, all at level 0."""
    result = compute(
        nodes=["A", "B", "C"],
        edges=[("A", "B"), ("B", "C"), ("C", "A")],
    )
    gid = result.cycle_group_id["A"]
    assert result.cycle_group_id == {"A": gid, "B": gid, "C": gid}
    assert result.topo_level == {"A": 0, "B": 0, "C": 0}
    assert result.cycle_count == 1


def test_three_cycle_plus_tail() -> None:
    """A→B→C→A, D→A — D depends on the cycle; D at level 1."""
    result = compute(
        nodes=["A", "B", "C", "D"],
        edges=[("A", "B"), ("B", "C"), ("C", "A"), ("D", "A")],
    )
    cycle_gid = result.cycle_group_id["A"]
    assert result.cycle_group_id["B"] == cycle_gid
    assert result.cycle_group_id["C"] == cycle_gid
    assert result.cycle_group_id["D"] != cycle_gid
    assert result.topo_level["A"] == 0
    assert result.topo_level["B"] == 0
    assert result.topo_level["C"] == 0
    assert result.topo_level["D"] == 1
    assert result.cycle_count == 1


def test_self_loop_is_singleton() -> None:
    """A→A — singleton SCC (self-loop), cycle_count == 0."""
    result = compute(nodes=["A"], edges=[("A", "A")])
    assert len(set(result.cycle_group_id.values())) == 1
    assert result.topo_level == {"A": 0}
    # A self-loop is an intra-SCC edge: A appears in its own dependencies and callers.
    assert result.dependencies == {"A": ["A"]}
    assert result.callers == {"A": ["A"]}
    # Singleton SCC even with self-loop ⇒ cycle_count excludes it (FR-005).
    assert result.cycle_count == 0


def test_isolated_nodes_all_level_zero() -> None:
    """Three isolated nodes — each at level 0, each its own SCC."""
    result = compute(nodes=["A", "B", "C"], edges=[])
    assert result.topo_level == {"A": 0, "B": 0, "C": 0}
    assert len(set(result.cycle_group_id.values())) == 3
    assert result.cycle_count == 0
    assert result.dependencies == {"A": [], "B": [], "C": []}
    assert result.callers == {"A": [], "B": [], "C": []}


def test_determinism_shuffled_input() -> None:
    """Same graph passed with shuffled input must produce identical output."""
    nodes_a = ["A", "B", "C", "D", "E"]
    edges_a = [("A", "B"), ("B", "C"), ("D", "A"), ("E", "D"), ("C", "D")]
    nodes_b = ["E", "C", "A", "D", "B"]
    edges_b = [("C", "D"), ("E", "D"), ("D", "A"), ("B", "C"), ("A", "B")]
    a = compute(nodes_a, edges_a)
    b = compute(nodes_b, edges_b)
    assert a.cycle_group_id == b.cycle_group_id
    assert a.topo_level == b.topo_level
    assert a.cycle_count == b.cycle_count
    assert a.dependencies == b.dependencies
    assert a.callers == b.callers


def test_unknown_edge_endpoint_raises() -> None:
    """Edge endpoint not in nodes raises ValueError."""
    with pytest.raises(ValueError):
        compute(nodes=["A"], edges=[("A", "X")])
    with pytest.raises(ValueError):
        compute(nodes=["A"], edges=[("X", "A")])
