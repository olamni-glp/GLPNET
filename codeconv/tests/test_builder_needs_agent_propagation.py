"""Regression: the convspec ``needs_agent_work`` sentinel MUST reach
``builder run``, and the agent gate MUST be traversable (feature-018
facets 1 & 3, found by the 2026-05-19 genuine live pass).

Facet 1: ``outer_builder_workflow`` discarded the per-unit result, so a
unit short-circuited at convspec was invisible → ``outcome="completed"``
with ZERO specs (violates US2 Acceptance 1).

Facet 3 (Amendment 2 / FR-044): the single per-unit child *returned* on
the sentinel ⇒ terminally SUCCESS ⇒ its checkpointed convspec step
never re-ran ⇒ an agent-written spec could never be ingested. Fixed by
splitting into a deterministic PRE workflow + content-addressed POST
workflow (the outer launches POST only once the artifact exists, under
an id keyed to the artifact digest, so it is a *fresh* deterministic
unit per spec revision).

These pure tests pin the aggregation + the PRE/POST stage contract with
no bridge, so the regression cannot silently return. The full
PRE→artifact-gate→POST cycle through the real bridge is the
``@needs_bridge`` acceptance gate in test_builder_e2e_smoke.py /
test_pipeline_stage_order.py.
"""

from __future__ import annotations

from codeconv.durable.workflows import (
    POST_STAGES,
    PRE_STAGES,
    outer_builder_workflow,
    post_agent_workflow,
    pre_agent_workflow,
)
from codeconv.tools.builder import _scan_needs_agent


# --------------------------------------------------------------------------
# Outer aggregation over the injected process_unit (facet-1 contract).
# --------------------------------------------------------------------------


def _sentinel(rel: str) -> dict:
    return {"needs_agent_work": True, "path": rel, "reason": "no artifact"}


def _awaiting_unit_result(unit, repo_root, data_dir):
    rel = unit["members"][0]
    return {
        "unit_id": unit["id"],
        "members": unit["members"],
        "launched": [f"file:pre:{rel}"],  # only PRE ran; POST gated off
        "needs_agent_work": [_sentinel(rel)],
    }


def _completed_unit_result(unit, repo_root, data_dir):
    rel = unit["members"][0]
    return {
        "unit_id": unit["id"],
        "members": unit["members"],
        "launched": [f"file:pre:{rel}", f"file:post:{rel}:deadbeef"],
        "needs_agent_work": [],
    }


def test_outer_observes_awaiting_unit_and_records_real_wf_ids() -> None:
    units = [
        {"kind": "file", "id": "file:aaa", "members": ["lib/a.dart"]},
        {"kind": "file", "id": "file:bbb", "members": ["lib/b.dart"]},
    ]

    def process(unit, repo_root, data_dir):
        return (
            _awaiting_unit_result(unit, repo_root, data_dir)
            if unit["id"] == "file:aaa"
            else _completed_unit_result(unit, repo_root, data_dir)
        )

    out = outer_builder_workflow(
        "repo", units, data_dir=None, process_unit=process
    )
    assert out["units"] == 2
    # launched records the ACTUAL pre/post wf ids driven, not unit ids.
    assert out["launched"] == [
        "file:pre:lib/a.dart",
        "file:pre:lib/b.dart",
        "file:post:lib/b.dart:deadbeef",
    ]
    assert out["agent_units"] == [_sentinel("lib/a.dart")]


def test_scan_needs_agent_extracts_paths_from_outer_result() -> None:
    units = [{"kind": "file", "id": "file:aaa", "members": ["lib/a.dart"]}]
    out = outer_builder_workflow(
        "repo", units, data_dir=None, process_unit=_awaiting_unit_result
    )
    assert _scan_needs_agent(out) == ["lib/a.dart"]


def test_all_completed_yields_no_agent_work() -> None:
    units = [{"kind": "file", "id": "file:bbb", "members": ["lib/b.dart"]}]
    out = outer_builder_workflow(
        "repo", units, data_dir=None, process_unit=_completed_unit_result
    )
    assert out["agent_units"] == []
    assert _scan_needs_agent(out) == []


# --------------------------------------------------------------------------
# PRE / POST stage contract (Amendment 2 / FR-044) — facet-3.
# --------------------------------------------------------------------------


def _steps(convspec_result):
    """Fake bound-step map. scaffold/plan record a marker; convspec
    returns the supplied per-call result."""
    calls: dict = {"plan": 0, "scaffold": 0}

    def scaffold(repo_root, *, data_dir=None):
        calls["scaffold"] += 1
        return {"outcome": "scaffolded"}

    def convspec(repo_root, rel, *, data_dir=None):
        return convspec_result(rel)

    def plan(repo_root, rel, *, data_dir=None):
        calls["plan"] += 1
        return {"plan_completed": {"ok": True}}

    return {"scaffold": scaffold, "convspec": convspec, "plan": plan}, calls


def test_pre_runs_scaffold_then_convspec_and_surfaces_sentinel() -> None:
    """PRE = scaffold + convspec; with no artifact convspec returns the
    sentinel — PRE records needs_agent_work and (correctly) has no
    plan stage at all (PRE_STAGES ends at convspec)."""
    assert PRE_STAGES == ("scaffold", "convspec")
    steps, calls = _steps(lambda rel: _sentinel(rel))
    res = pre_agent_workflow(
        "repo", ["lib/a.dart"], data_dir=None, steps=steps
    )
    assert calls["scaffold"] == 1
    assert "scaffold" in res["stages"] and "convspec" in res["stages"]
    assert "plan" not in res["stages"]
    assert res["needs_agent_work"] == [_sentinel("lib/a.dart")]
    assert calls["plan"] == 0


def test_post_runs_convspec_then_plan_when_specced() -> None:
    """POST = convspec + plan; artifact present ⇒ convspec specced ⇒
    plan runs ⇒ no agent work."""
    assert POST_STAGES == ("convspec", "plan")
    steps, calls = _steps(
        lambda rel: {"outcome": "specced", "path": rel}
    )
    res = post_agent_workflow(
        "repo", ["lib/a.dart"], data_dir=None, steps=steps
    )
    assert "convspec" in res["stages"] and "plan" in res["stages"]
    assert calls["plan"] == 1
    assert "needs_agent_work" not in res


def test_post_short_circuits_on_invalid_artifact() -> None:
    """An invalid artifact ⇒ convspec re-returns the sentinel ⇒ plan is
    skipped and the unit is re-surfaced (agent must re-spec)."""
    steps, calls = _steps(lambda rel: _sentinel(rel))
    res = post_agent_workflow(
        "repo", ["lib/a.dart"], data_dir=None, steps=steps
    )
    assert res["needs_agent_work"] == [_sentinel("lib/a.dart")]
    assert calls["plan"] == 0
    assert "plan" not in res["stages"]


def test_scc_processed_as_one_unit_through_a_stage() -> None:
    """FR-002: every SCC member is taken through convspec before plan;
    if ANY member needs an agent the whole unit short-circuits."""
    members = ["lib/a.dart", "lib/b.dart"]
    steps, calls = _steps(
        lambda rel: _sentinel(rel)
        if rel == "lib/b.dart"
        else {"outcome": "specced", "path": rel}
    )
    res = post_agent_workflow("repo", members, data_dir=None, steps=steps)
    # convspec ran for both members before any plan; b's sentinel blocks.
    assert set(res["stages"]["convspec"]) == set(members)
    assert calls["plan"] == 0
    assert any(
        s["path"] == "lib/b.dart" for s in res["needs_agent_work"]
    )
