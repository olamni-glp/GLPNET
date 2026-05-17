"""T018 — every 015/016/017 capability preserved after T015 (FR-016/SC-005).

D2 hard gate: replacing the six tools' no-op ``register()`` with
delegation to ``codeconv.durable.activate`` must NOT remove or alter any
existing capability. Pure (no bridge): asserts every tool entrypoint is
still importable, the runner still discovers all six tools, each
``register`` is callable and delegates to the shared activation, and the
``run_*`` entrypoint signatures are byte-unchanged (the wrappers call
them VERBATIM — verified independently in ``durable/steps.py``). Deep
behavioural equivalence is covered by the full @needs_bridge suite
(green post-harness-fix).
"""

from __future__ import annotations

import importlib
import inspect

import pytest

# (module, entrypoint) pairs delivered by features 015/016/017 + 012.
_ENTRYPOINTS = [
    ("codeconv.tools.discover.workflow", "run_discover"),
    ("codeconv.tools.depgraph.workflow", "run_compute"),
    ("codeconv.tools.depgraph.workflow", "run_mark_started"),
    ("codeconv.tools.depgraph.workflow", "run_mark_completed"),
    ("codeconv.tools.depgraph.workflow", "run_stamp_tombstones"),
    ("codeconv.tools.depgraph.workflow", "run_rebuild_conversions_from_tombstones"),
    ("codeconv.tools.init.workflow", "run_init"),
    ("codeconv.tools.init.workflow", "run_add_exclude"),
    ("codeconv.tools.init.workflow", "run_remove_exclude"),
    ("codeconv.tools.scaffold.workflow", "run_scaffold"),
    ("codeconv.tools.mirror.workflow", "run_mirror"),
    ("codeconv.tools.planagents.workflow", "run_status"),
    ("codeconv.tools.planagents.workflow", "run_next"),
    ("codeconv.tools.planagents.workflow", "run_plan_started"),
    ("codeconv.tools.planagents.workflow", "run_plan_completed"),
    ("codeconv.tools.planagents.workflow", "run_aggregate_escalations"),
    ("codeconv.tools.planagents.workflow", "run_stamp_tombstones"),
    ("codeconv.tools.planagents.workflow", "run_rebuild_plans_from_tombstones"),
]

_TOOLS = ("discover", "depgraph", "init", "scaffold", "mirror", "planagents")

# Signatures the durable/ step wrappers depend on (must stay verbatim).
_PINNED_SIGNATURES = {
    ("codeconv.tools.depgraph.workflow", "run_compute"): (
        ["repo_root", "data_dir", "json_out", "dry_run", "quiet"]
    ),
    ("codeconv.tools.scaffold.workflow", "run_scaffold"): (
        [
            "repo_root",
            "source_lang",
            "target_lang",
            "force_delete_target",
            "no_tombstone_update",
            "data_dir",
            "quiet",
        ]
    ),
    ("codeconv.tools.planagents.workflow", "run_plan_started"): (
        ["repo_root", "data_dir", "path", "sha256", "replan", "no_tombstone_update"]
    ),
    ("codeconv.tools.planagents.workflow", "run_plan_completed"): (
        ["repo_root", "data_dir", "path", "plan_path", "escalations", "no_tombstone_update"]
    ),
}


@pytest.mark.parametrize("mod_name,fn_name", _ENTRYPOINTS)
def test_entrypoint_still_importable(mod_name: str, fn_name: str) -> None:
    mod = importlib.import_module(mod_name)
    fn = getattr(mod, fn_name, None)
    assert callable(fn), f"{mod_name}.{fn_name} no longer reachable"


def test_runner_discovers_all_six_tools() -> None:
    from codeconv.runner import tool_registry

    names = {t.name for t in tool_registry()}
    for t in _TOOLS:
        assert t in names, f"tool {t} vanished from registry after T015"


def test_register_delegates_to_durable(monkeypatch) -> None:
    """Every tool's register() must call codeconv.durable.activate
    exactly (delegation, not a divergent reimplementation)."""
    import codeconv.durable as durable

    called = []
    monkeypatch.setattr(durable, "activate", lambda app: called.append(app))

    for t in _TOOLS:
        mod = importlib.import_module(f"codeconv.tools.{t}.workflow")
        assert hasattr(mod, "register"), f"{t} lost register()"
        mod.register(object())  # sentinel dbos_app
    assert len(called) == len(_TOOLS), (
        f"expected all {len(_TOOLS)} register()s to delegate to "
        f"durable.activate, got {len(called)}"
    )


def test_unified_surface_reaches_every_capability() -> None:
    """T044 [US3] — every 015/016/017 capability is reachable through
    the unified surface after consolidation (FR-016/SC-005): the runner
    discovers builder + convspec PLUS all six original tools, and the
    durable layer wraps every pipeline-stage entrypoint."""
    from codeconv.runner import tool_registry

    names = {t.name for t in tool_registry()}
    # original six + the two new unified tools — none lost.
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
        assert t in names, f"{t} missing from unified surface"

    # the durable layer wraps every pipeline stage (discover→depgraph→
    # scaffold→convspec→plan) — verbatim entrypoints, D2.
    import codeconv.durable as durable

    durable.reset_registry_for_tests()
    import importlib

    importlib.import_module("codeconv.durable.steps")
    steps = set(durable.registered_steps())
    assert {
        "discover",
        "depgraph_compute",
        "scaffold",
        "convspec",
        "plan",
    } <= steps, steps


@pytest.mark.parametrize("key", list(_PINNED_SIGNATURES))
def test_wrapped_entrypoint_signatures_unchanged(key) -> None:
    """The durable/steps.py wrappers call these VERBATIM — a signature
    drift here would silently break the D2 verbatim contract."""
    mod_name, fn_name = key
    fn = getattr(importlib.import_module(mod_name), fn_name)
    params = [
        p
        for p in inspect.signature(fn).parameters
        if p not in ("args", "kwargs")
    ]
    assert params == _PINNED_SIGNATURES[key], (
        f"{mod_name}.{fn_name} signature drifted: {params} != "
        f"{_PINNED_SIGNATURES[key]} — durable/steps.py wrapper would break"
    )
