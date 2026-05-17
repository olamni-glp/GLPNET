"""T030 [US2] — pure convspec-readiness predicate + SCC batch.

Parallels the feature-017 readiness test. PURE (no bridge): the
predicate consumes plain depgraph/spec dicts and is deterministic
(FR-021) — identical inputs ⇒ identical units.
"""

from __future__ import annotations

from codeconv.tools.convspec.readiness import (
    CONVSPEC_IN_PROGRESS,
    CONVSPEC_PENDING,
    CONVSPEC_READY,
    SPECCED,
    DepNode,
    SpecRow,
    classify,
    classify_all,
    select_next,
)


def _nodes(*paths):
    # one node per path, each its own singleton SCC, ascending topo level
    return {p: DepNode(topo_level=i, cycle_group_id=i) for i, p in enumerate(paths)}


def test_states_four_way():
    nodes = _nodes("a.dart", "b.dart")
    deps = {"b.dart": frozenset({"a.dart"})}
    # nothing specced: a ready (no deps), b pending (dep a not specced)
    st = classify_all(nodes=nodes, cross_scc_deps=deps, specs={})
    assert st["a.dart"] == CONVSPEC_READY
    assert st["b.dart"] == CONVSPEC_PENDING
    # a in progress (row, not completed) → still blocks b
    st = classify_all(
        nodes=nodes, cross_scc_deps=deps, specs={"a.dart": SpecRow(False)}
    )
    assert st["a.dart"] == CONVSPEC_IN_PROGRESS
    assert st["b.dart"] == CONVSPEC_PENDING
    # a specced → b becomes ready
    st = classify_all(
        nodes=nodes, cross_scc_deps=deps, specs={"a.dart": SpecRow(True)}
    )
    assert st["a.dart"] == SPECCED
    assert st["b.dart"] == CONVSPEC_READY


def test_select_next_dep_order_and_limit():
    nodes = _nodes("a.dart", "b.dart", "c.dart")
    deps = {"b.dart": frozenset({"a.dart"}), "c.dart": frozenset({"b.dart"})}
    units = select_next(nodes=nodes, cross_scc_deps=deps, specs={}, limit=7)
    # only a is ready initially
    assert [u.members for u in units] == [("a.dart",)]
    # determinism
    assert select_next(
        nodes=nodes, cross_scc_deps=deps, specs={}, limit=7
    ) == units


def test_scc_is_one_indivisible_unit():
    # b,c form a multi-file SCC (shared cycle_group_id=9)
    nodes = {
        "a.dart": DepNode(0, 0),
        "b.dart": DepNode(1, 9),
        "c.dart": DepNode(1, 9),
    }
    units = select_next(nodes=nodes, cross_scc_deps={}, specs={}, limit=7)
    by = {u.members for u in units}
    assert ("a.dart",) in by
    assert ("b.dart", "c.dart") in by  # one indivisible unit, sorted
    scc = next(u for u in units if u.is_scc_batch)
    assert scc.members == ("b.dart", "c.dart")


def test_classify_raises_on_orphan():
    import pytest

    with pytest.raises(KeyError):
        classify(
            "ghost.dart",
            nodes=_nodes("a.dart"),
            cross_scc_deps={},
            specs={},
        )
