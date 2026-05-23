"""T010 — pure codegen-readiness predicate + SCC batch selection (FR-002).

Mirrors ``test_planagents_readiness`` for the codegen frontier: a file is
codegen-ready iff every SCC-external dependency is codegen-complete
(``codegen_completed_at IS NOT NULL``); an SCC is one indivisible batch;
an in-progress codegen does NOT unblock downstream. Pure — no bridge.
"""

from __future__ import annotations

from codeconv.tools.codegen.readiness import (
    CODEGEN_DONE,
    CODEGEN_IN_PROGRESS,
    CODEGEN_PENDING,
    CODEGEN_READY,
    CodegenRow,
    DepNode,
    classify,
    classify_all,
    remaining_ready_count,
    select_next,
)


def _nodes(spec):
    """spec: {path: (topo_level, cycle_group_id)} -> {path: DepNode}."""
    return {p: DepNode(topo_level=t, cycle_group_id=c) for p, (t, c) in spec.items()}


def test_leaf_with_no_deps_is_ready() -> None:
    nodes = _nodes({"a.dart": (0, 1)})
    cross = {"a.dart": frozenset()}
    assert classify("a.dart", nodes=nodes, cross_scc_deps=cross, rows={}) == (
        CODEGEN_READY
    )


def test_dependent_pending_until_dep_done() -> None:
    nodes = _nodes({"dep.dart": (0, 1), "use.dart": (1, 2)})
    cross = {"dep.dart": frozenset(), "use.dart": frozenset({"dep.dart"})}

    # dep not codegen'd ⇒ use is pending.
    states = classify_all(nodes=nodes, cross_scc_deps=cross, rows={})
    assert states["dep.dart"] == CODEGEN_READY
    assert states["use.dart"] == CODEGEN_PENDING

    # dep in progress (row, not completed) ⇒ still does NOT unblock.
    rows = {"dep.dart": CodegenRow(completed=False)}
    states = classify_all(nodes=nodes, cross_scc_deps=cross, rows=rows)
    assert states["dep.dart"] == CODEGEN_IN_PROGRESS
    assert states["use.dart"] == CODEGEN_PENDING

    # dep done ⇒ use becomes ready.
    rows = {"dep.dart": CodegenRow(completed=True)}
    states = classify_all(nodes=nodes, cross_scc_deps=cross, rows=rows)
    assert states["dep.dart"] == CODEGEN_DONE
    assert states["use.dart"] == CODEGEN_READY


def test_scc_emitted_as_one_batch() -> None:
    # a.dart & b.dart form one SCC (cycle_group_id 1); c.dart depends on
    # the SCC. The SCC is emitted as a single 2-member unit.
    nodes = _nodes({"a.dart": (0, 1), "b.dart": (0, 1), "c.dart": (1, 2)})
    cross = {
        "a.dart": frozenset(),
        "b.dart": frozenset(),
        "c.dart": frozenset({"a.dart"}),
    }
    units = select_next(nodes=nodes, cross_scc_deps=cross, rows={}, limit=7)
    # First unit is the SCC batch (topo 0); c.dart is gated.
    assert units[0].is_scc_batch
    assert units[0].members == ("a.dart", "b.dart")
    assert all("c.dart" not in u.members for u in units)


def test_scc_downstream_gated_until_all_members_done() -> None:
    nodes = _nodes({"a.dart": (0, 1), "b.dart": (0, 1), "c.dart": (1, 2)})
    cross = {
        "a.dart": frozenset(),
        "b.dart": frozenset(),
        "c.dart": frozenset({"a.dart"}),
    }
    # Only one SCC member done ⇒ c still gated.
    rows = {"a.dart": CodegenRow(completed=True)}
    states = classify_all(nodes=nodes, cross_scc_deps=cross, rows=rows)
    assert states["c.dart"] == CODEGEN_PENDING
    # Both done ⇒ c ready.
    rows = {
        "a.dart": CodegenRow(completed=True),
        "b.dart": CodegenRow(completed=True),
    }
    states = classify_all(nodes=nodes, cross_scc_deps=cross, rows=rows)
    assert states["c.dart"] == CODEGEN_READY


def test_scc_unit_not_split_by_limit() -> None:
    # A 3-member SCC with limit=2 is still emitted whole (integrity wins).
    nodes = _nodes(
        {"a.dart": (0, 1), "b.dart": (0, 1), "d.dart": (0, 1)}
    )
    cross = {p: frozenset() for p in nodes}
    units = select_next(nodes=nodes, cross_scc_deps=cross, rows={}, limit=2)
    assert len(units) == 1
    assert units[0].members == ("a.dart", "b.dart", "d.dart")


def test_limit_caps_singleton_units() -> None:
    nodes = _nodes({f"f{i}.dart": (0, i) for i in range(5)})
    cross = {p: frozenset() for p in nodes}
    units = select_next(nodes=nodes, cross_scc_deps=cross, rows={}, limit=3)
    assert sum(len(u.members) for u in units) == 3
    rem = remaining_ready_count(
        nodes=nodes, cross_scc_deps=cross, rows={}, selected=units
    )
    assert rem == 2


def test_deterministic_order() -> None:
    nodes = _nodes(
        {"z.dart": (0, 1), "a.dart": (0, 2), "m.dart": (1, 3)}
    )
    cross = {p: frozenset() for p in nodes}
    u1 = select_next(nodes=nodes, cross_scc_deps=cross, rows={}, limit=7)
    u2 = select_next(nodes=nodes, cross_scc_deps=cross, rows={}, limit=7)
    assert u1 == u2
    # topo 0 before topo 1; within topo 0, a.dart before z.dart.
    flat = [m for u in u1 for m in u.members]
    assert flat == ["a.dart", "z.dart", "m.dart"]
