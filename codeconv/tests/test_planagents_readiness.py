"""Pure unit tests for the plan-readiness predicate + SCC batch selection.

Maps to ``specs/017-conversion-plan-agents/contracts/
plan_readiness_algorithm.md`` (FR-003/FR-004/FR-011/FR-021; SC-002/
SC-006). NO ``@needs_bridge`` — ``readiness`` is a pure function over
plain dicts. T010.
"""

from __future__ import annotations

from codeconv.tools.planagents.readiness import (
    PLANNED,
    PLAN_IN_PROGRESS,
    PLAN_PENDING,
    PLAN_READY,
    DepNode,
    PlanRow,
    classify,
    classify_all,
    remaining_ready_count,
    select_next,
)


def _nodes(spec: dict[str, tuple[int, int]]) -> dict[str, DepNode]:
    """spec: path -> (topo_level, cycle_group_id)."""
    return {p: DepNode(topo_level=t, cycle_group_id=c) for p, (t, c) in spec.items()}


def _completed(*paths: str) -> dict[str, PlanRow]:
    return {p: PlanRow(completed=True) for p in paths}


# ---------------------------------------------------------------------------
# classify — FR-004 four-state
# ---------------------------------------------------------------------------


def test_leaf_with_no_deps_is_plan_ready() -> None:
    nodes = _nodes({"a.dart": (0, 1)})
    cross: dict[str, frozenset[str]] = {"a.dart": frozenset()}
    assert classify("a.dart", nodes=nodes, cross_scc_deps=cross, plans={}) == PLAN_READY


def test_isolated_file_is_plan_ready() -> None:
    nodes = _nodes({"iso.dart": (0, 9)})
    cross = {"iso.dart": frozenset()}
    assert (
        classify("iso.dart", nodes=nodes, cross_scc_deps=cross, plans={})
        == PLAN_READY
    )


def test_file_with_unplanned_dep_is_plan_pending() -> None:
    nodes = _nodes({"a.dart": (0, 1), "b.dart": (1, 2)})
    cross = {"a.dart": frozenset(), "b.dart": frozenset({"a.dart"})}
    assert (
        classify("b.dart", nodes=nodes, cross_scc_deps=cross, plans={})
        == PLAN_PENDING
    )


def test_file_becomes_plan_ready_when_dep_planned() -> None:
    nodes = _nodes({"a.dart": (0, 1), "b.dart": (1, 2)})
    cross = {"a.dart": frozenset(), "b.dart": frozenset({"a.dart"})}
    assert (
        classify(
            "b.dart", nodes=nodes, cross_scc_deps=cross, plans=_completed("a.dart")
        )
        == PLAN_READY
    )


def test_in_progress_dep_does_not_unblock_downstream() -> None:
    """FR-004 / US2 AC2: an in-progress plan does NOT unblock downstream."""
    nodes = _nodes({"a.dart": (0, 1), "b.dart": (1, 2)})
    cross = {"a.dart": frozenset(), "b.dart": frozenset({"a.dart"})}
    plans = {"a.dart": PlanRow(completed=False)}  # started, not completed
    assert classify("a.dart", nodes=nodes, cross_scc_deps=cross, plans=plans) == PLAN_IN_PROGRESS
    assert classify("b.dart", nodes=nodes, cross_scc_deps=cross, plans=plans) == PLAN_PENDING


def test_row_completed_is_planned() -> None:
    nodes = _nodes({"a.dart": (0, 1)})
    cross = {"a.dart": frozenset()}
    assert (
        classify(
            "a.dart", nodes=nodes, cross_scc_deps=cross, plans=_completed("a.dart")
        )
        == PLANNED
    )


# ---------------------------------------------------------------------------
# select_next — chain / ordering / SC-002
# ---------------------------------------------------------------------------


def _chain():
    # c -> b -> a (a leaf). Each its own SCC.
    nodes = _nodes({"a.dart": (0, 1), "b.dart": (1, 2), "c.dart": (2, 3)})
    cross = {
        "a.dart": frozenset(),
        "b.dart": frozenset({"a.dart"}),
        "c.dart": frozenset({"b.dart"}),
    }
    return nodes, cross


def test_chain_run1_selects_only_leaf() -> None:
    nodes, cross = _chain()
    units = select_next(nodes=nodes, cross_scc_deps=cross, plans={})
    flat = [m for u in units for m in u.members]
    assert flat == ["a.dart"]  # only A; B and C not plan-ready


def test_chain_advances_one_level_after_completion() -> None:
    nodes, cross = _chain()
    # A planned ⇒ B ready, C not.
    units = select_next(
        nodes=nodes, cross_scc_deps=cross, plans=_completed("a.dart")
    )
    flat = [m for u in units for m in u.members]
    assert flat == ["b.dart"]
    # A,B planned ⇒ C ready.
    units = select_next(
        nodes=nodes, cross_scc_deps=cross, plans=_completed("a.dart", "b.dart")
    )
    assert [m for u in units for m in u.members] == ["c.dart"]


def test_in_progress_a_keeps_b_not_ready() -> None:
    nodes, cross = _chain()
    plans = {"a.dart": PlanRow(completed=False)}
    units = select_next(nodes=nodes, cross_scc_deps=cross, plans=plans)
    # A is in progress (has row) → not re-emitted; B still pending.
    assert units == []


def test_cross_scc_ordering_invariant_sc002() -> None:
    """SC-002: A is never selected before its cross-SCC dep B is PLANNED."""
    nodes, cross = _chain()
    for plans in ({}, _completed("a.dart"), _completed("a.dart", "b.dart")):
        units = select_next(nodes=nodes, cross_scc_deps=cross, plans=plans)
        states = classify_all(nodes=nodes, cross_scc_deps=cross, plans=plans)
        for u in units:
            for m in u.members:
                for dep in cross[m]:
                    assert states[dep] == PLANNED, (
                        f"{m} selected before cross-SCC dep {dep} planned"
                    )


# ---------------------------------------------------------------------------
# SCC batch — FR-011 / SC-006
# ---------------------------------------------------------------------------


def _scc_plus_downstream():
    # SCC {A,B,C} share cycle_group_id 5; D depends on A (cross-SCC).
    nodes = _nodes(
        {
            "A.dart": (0, 5),
            "B.dart": (0, 5),
            "C.dart": (0, 5),
            "D.dart": (1, 7),
        }
    )
    cross = {
        "A.dart": frozenset(),
        "B.dart": frozenset(),
        "C.dart": frozenset(),
        "D.dart": frozenset({"A.dart"}),
    }
    return nodes, cross


def test_scc_emitted_as_one_batch_unit() -> None:
    nodes, cross = _scc_plus_downstream()
    units = select_next(nodes=nodes, cross_scc_deps=cross, plans={})
    scc_units = [u for u in units if u.cycle_group_id == 5]
    assert len(scc_units) == 1
    u = scc_units[0]
    assert u.is_scc_batch
    assert u.members == ("A.dart", "B.dart", "C.dart")  # lexicographic
    # D not plan-ready until ALL of A,B,C planned.
    assert all(m != "D.dart" for uu in units for m in uu.members)


def test_downstream_blocked_until_all_scc_members_planned() -> None:
    nodes, cross = _scc_plus_downstream()
    # Only A,B planned (C still missing) ⇒ D NOT ready.
    units = select_next(
        nodes=nodes, cross_scc_deps=cross, plans=_completed("A.dart", "B.dart")
    )
    flat = [m for u in units for m in u.members]
    assert "D.dart" not in flat
    # All three planned ⇒ D ready.
    units = select_next(
        nodes=nodes,
        cross_scc_deps=cross,
        plans=_completed("A.dart", "B.dart", "C.dart"),
    )
    assert [m for u in units for m in u.members] == ["D.dart"]


def test_partial_scc_batch_resume_reselects_only_unstarted() -> None:
    """Edge case 'SCC member subset already planned': interrupted batch.

    A,B have rows (A completed, B in progress), C has no row ⇒ a re-run
    re-selects ONLY C (un-started); A/B are NOT re-spawned; downstream
    stays blocked.
    """
    nodes, cross = _scc_plus_downstream()
    plans = {
        "A.dart": PlanRow(completed=True),
        "B.dart": PlanRow(completed=False),
        # C absent
    }
    units = select_next(nodes=nodes, cross_scc_deps=cross, plans=plans)
    flat = [m for u in units for m in u.members]
    assert flat == ["C.dart"]  # only the un-started member
    # D still blocked (B not completed).
    assert "D.dart" not in flat


def test_scc_unit_never_split_by_limit() -> None:
    """FR-021 / R3: an SCC unit is taken WHOLE even if it exceeds --limit."""
    nodes, cross = _scc_plus_downstream()
    units = select_next(
        nodes=nodes, cross_scc_deps=cross, plans={}, limit=2
    )
    # limit=2 but the 3-member SCC is one unit ⇒ taken whole.
    scc_units = [u for u in units if u.cycle_group_id == 5]
    assert len(scc_units) == 1
    assert len(scc_units[0].members) == 3


# ---------------------------------------------------------------------------
# limit soft-cap + determinism
# ---------------------------------------------------------------------------


def test_limit_soft_caps_singletons() -> None:
    nodes = _nodes({f"f{i}.dart": (0, i + 1) for i in range(10)})
    cross = {p: frozenset() for p in nodes}
    units = select_next(nodes=nodes, cross_scc_deps=cross, plans={}, limit=7)
    assert sum(len(u.members) for u in units) == 7


def test_selection_is_deterministic() -> None:
    nodes = _nodes({f"f{i}.dart": (0, i + 1) for i in range(5)})
    cross = {p: frozenset() for p in nodes}
    a = select_next(nodes=nodes, cross_scc_deps=cross, plans={}, limit=3)
    b = select_next(nodes=nodes, cross_scc_deps=cross, plans={}, limit=3)
    assert a == b
    assert [m for u in a for m in u.members] == sorted(
        [m for u in a for m in u.members]
    )


def test_remaining_ready_count_excludes_selected() -> None:
    nodes = _nodes({f"f{i}.dart": (0, i + 1) for i in range(10)})
    cross = {p: frozenset() for p in nodes}
    sel = select_next(nodes=nodes, cross_scc_deps=cross, plans={}, limit=7)
    rem = remaining_ready_count(
        nodes=nodes, cross_scc_deps=cross, plans={}, selected=sel
    )
    assert rem == 3  # 10 ready, 7 taken
