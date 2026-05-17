"""T023 [US1] — builder frontier respects 015 dep order + SCC (FR-002/SC-003).

The frontier driver consumes ``codeconv.dart_depgraph`` READ-ONLY and
emits units via the pure, already-tested
:func:`codeconv.durable.workflows.plan_units`. This test pins the
dependency-before invariant and SCC-as-one-indivisible-unit directly on
the pure planner (deterministic, bridge-free — identical inputs ⇒
identical units, the property that underpins FR-004/SC-002 resume).
"""

from __future__ import annotations

from codeconv.durable.workflows import plan_units


def test_no_file_before_its_position_singletons():
    topo = ["lib/a.dart", "lib/b.dart", "lib/c.dart"]
    units = plan_units(topo, {})
    order = [m for u in units for m in u["members"]]
    assert order == topo, f"frontier reordered the 015 topo order: {order}"
    assert all(u["kind"] == "file" for u in units)


def test_scc_is_one_indivisible_unit_at_earliest_member():
    # 015 order; b,c form a cycle group; d depends on the group.
    topo = ["lib/a.dart", "lib/b.dart", "lib/c.dart", "lib/d.dart"]
    units = plan_units(topo, {"7": ["lib/c.dart", "lib/b.dart"]})
    kinds = [(u["kind"], tuple(u["members"])) for u in units]
    assert kinds == [
        ("file", ("lib/a.dart",)),
        ("scc", ("lib/b.dart", "lib/c.dart")),  # one unit, sorted members
        ("file", ("lib/d.dart",)),
    ], kinds
    # The SCC unit appears exactly once, at its earliest member's slot —
    # so no member is launched before the whole group (FR-002), and the
    # downstream file (d) comes strictly after the group.
    scc_idx = next(i for i, u in enumerate(units) if u["kind"] == "scc")
    d_idx = next(
        i for i, u in enumerate(units) if u["members"] == ["lib/d.dart"]
    )
    assert scc_idx < d_idx


def test_determinism_same_inputs_same_units():
    topo = ["x/p.dart", "x/q.dart", "x/r.dart"]
    scc = {"1": ["x/q.dart", "x/r.dart"]}
    assert plan_units(topo, scc) == plan_units(topo, scc)


def test_scc_member_order_invariant():
    topo = ["a.dart", "b.dart", "c.dart"]
    u1 = plan_units(topo, {"g": ["b.dart", "c.dart"]})
    u2 = plan_units(topo, {"g": ["c.dart", "b.dart"]})
    assert u1 == u2  # member discovery order must not change the frontier
