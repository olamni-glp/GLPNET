"""T042 — feature-019 capability preservation (no regression).

Every prior feature's CLI surface (015 depgraph, 016 init/scaffold/
mirror, 017 planagents, 018 builder/convspec) is still registered, AND
the new feature-019 tools (codegen + codegen_opt) are reachable with
their documented entrypoints + durable step. Pure (no bridge).
"""

from __future__ import annotations

import importlib

import codeconv.runner as runner_mod


def test_prior_tool_surface_still_registered() -> None:
    names = {t.name for t in runner_mod.tool_registry()}
    for t in (
        "discover",
        "depgraph",
        "init",
        "scaffold",
        "mirror",
        "planagents",
        "builder",
        "convspec",
    ):
        assert t in names, f"{t} missing from unified surface (regression)"


def test_codegen_tools_registered() -> None:
    names = {t.name for t in runner_mod.tool_registry()}
    assert {"codegen", "codegen_opt"} <= names, names


def test_codegen_entrypoints_present() -> None:
    wf = importlib.import_module("codeconv.tools.codegen.workflow")
    for fn in (
        "run_status",
        "run_next",
        "run_codegen_ingest",
        "run_codegen_step",
        "run_retry",
        "run_aggregate_escalations",
    ):
        assert callable(getattr(wf, fn)), fn
    review = importlib.import_module("codeconv.tools.codegen.review")
    for fn in ("run_record_review", "run_promote_batch", "promotion_gate"):
        assert callable(getattr(review, fn)), fn


def test_codegen_opt_is_not_durable_registered() -> None:
    """codegen_opt must NOT export register_workflows (offline/non-durable)."""
    co = importlib.import_module("codeconv.tools.codegen_opt")
    assert not hasattr(co, "register_workflows")


def test_codegen_durable_step_registered() -> None:
    import codeconv.durable as durable
    import codeconv.durable.steps as steps_mod

    durable.reset_registry_for_tests()
    # reload so registration re-runs regardless of prior import order.
    importlib.reload(steps_mod)
    steps = set(durable.registered_steps())
    # All prior stages + the new codegen stage, none dropped.
    assert {
        "discover",
        "depgraph_compute",
        "scaffold",
        "convspec",
        "plan",
        "codegen",
    } <= steps, steps


def test_production_codegen_path_imports_no_dspy() -> None:
    """The deterministic production path must not pull the optimizer's
    LM stack (R3/R10) — importing codegen.workflow leaves dspy unloaded."""
    import sys

    # Fresh-ish check: if dspy is already loaded by another test, this is
    # informational; the contract is that codegen.workflow does not import
    # it at module load.
    mod = importlib.import_module("codeconv.tools.codegen.workflow")
    src = importlib.util.find_spec("codeconv.tools.codegen.workflow")
    assert mod is not None and src is not None
    # Static guarantee: the module's own imports don't include dspy.
    text = open(src.origin, encoding="utf-8").read()
    assert "import dspy" not in text
    assert "codegen_opt" not in text
