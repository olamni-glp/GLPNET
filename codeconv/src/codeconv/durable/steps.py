"""DBOS step wrappers — feature 018, T011 (R1/R8, D2 hard gate).

Each pipeline stage is a ``@DBOS.step`` whose body calls the **existing,
already-tested tool entrypoint verbatim** and returns its summary dict.
The wrapper adds NOTHING to the tool's behaviour (D2: "existing
entrypoints called verbatim; the durable layer is additive and isolated
to ``durable/``"). The two-phase ``*_started_at``/``*_completed_at`` +
tombstone projection that each tool already writes IS the business
record; DBOS checkpoints the step's return so replay **skips** completed
steps (FR-003/FR-004).

**Replay-safety (HARD — contract ``dbos_workflow_model.md``).** A step
body is: deterministic args → the existing pure entrypoint (DB/file +
the tool's own two-phase write) → return its dict. There is **no
LLM/web/network call in any step** — the convspec agent work lives in
the skill, gated by the deterministic ``needs_agent_work`` result
(handled in feature-018 US2, not here).

``DBOS`` is imported lazily (mirroring ``codeconv.runner.workflow``) so
this module imports with no bridge/DBOS for the pure registry tests.
The wrappers are registered into the :mod:`codeconv.durable` registry so
T015's per-tool ``register()`` delegations and T012's workflows can
resolve them by stage name.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Callable, Optional

from codeconv.durable import register_step


def _dbos_step(name: str) -> Callable[[Callable[..., Any]], Callable[..., Any]]:
    """Decorate ``fn`` as a ``@DBOS.step`` and register it under ``name``.

    Lazy: the real ``DBOS.step`` decorator is applied at *registration*
    time (when the CLI has launched DBOS and called ``register()``), not
    at import time — so importing this module never requires DBOS.
    """

    def _wrap(fn: Callable[..., Any]) -> Callable[..., Any]:
        register_step(name, fn)
        return fn

    return _wrap


def bind_dbos_steps(dbos: Any) -> dict[str, Callable[..., Any]]:
    """Apply ``dbos.step()`` to every registered stage wrapper.

    Called once by ``durable`` activation (T012/T015) after the CLI has
    launched DBOS. Returns the decorated callables keyed by stage name.
    Idempotent — re-decorating the same callable is harmless because the
    registry stores the *undecorated* function and we decorate a fresh
    reference each call (DBOS dedups by qualified name).
    """
    from codeconv.durable import registered_steps

    bound: dict[str, Callable[..., Any]] = {}
    for stage_name, fn in registered_steps().items():
        bound[stage_name] = dbos.step()(fn)
    return bound


# --------------------------------------------------------------------------
# Stage wrappers. Each delegates VERBATIM to the existing entrypoint.
# Signatures mirror the entrypoints' kwargs (repo_root + the bridge/
# data-dir passthrough + the stage's own args). No behaviour added.
# --------------------------------------------------------------------------


@_dbos_step("discover")
def step_discover(
    repo_root: str,
    *,
    mode: str = "normal",
    dry_run: bool = False,
    bridge_script: Optional[str] = None,
    data_dir: Optional[str] = None,
) -> dict:
    """Inventory stage — calls ``tools.discover.run_discover`` verbatim.

    ``run_discover`` is the one entrypoint with a *positional*
    ``repo_root`` and a ``bridge_script`` kwarg (verified signature
    ``run_discover(repo_root, *, mode, root, dry_run, no_orphan_revival,
    quiet, bridge_script, data_dir)``)."""
    from codeconv.tools.discover.workflow import run_discover

    return run_discover(
        Path(repo_root),
        mode=mode,
        dry_run=dry_run,
        quiet=True,
        bridge_script=Path(bridge_script) if bridge_script else None,
        data_dir=Path(data_dir) if data_dir else None,
    )


@_dbos_step("depgraph_compute")
def step_depgraph_compute(
    repo_root: str,
    *,
    data_dir: Optional[str] = None,
) -> dict:
    """Dependency-graph stage — calls ``tools.depgraph.run_compute``
    verbatim. Verified signature: ``run_compute(*, repo_root, data_dir,
    json_out, dry_run, quiet)`` — keyword-only, no ``bridge_script``
    (the entrypoint auto-discovers the bridge internally). The builder
    consumes the resulting 015 topo/SCC order read-only and MUST NOT
    recompute it elsewhere — this step only (re)materialises it through
    the tool's own entrypoint."""
    from codeconv.tools.depgraph.workflow import run_compute

    return run_compute(
        repo_root=Path(repo_root),
        data_dir=Path(data_dir) if data_dir else None,
        quiet=True,
    )


@_dbos_step("scaffold")
def step_scaffold(
    repo_root: str,
    *,
    data_dir: Optional[str] = None,
) -> dict:
    """Target-tree stage — calls ``tools.scaffold.run_scaffold`` verbatim
    (records each produced target path into per-file tracking, FR-008).
    Verified signature: ``run_scaffold(*, repo_root, source_lang,
    target_lang, force_delete_target, no_tombstone_update, data_dir,
    quiet)`` — keyword-only, no ``bridge_script``."""
    from codeconv.tools.scaffold.workflow import run_scaffold

    return run_scaffold(
        repo_root=Path(repo_root),
        data_dir=Path(data_dir) if data_dir else None,
        quiet=True,
    )


@_dbos_step("convspec")
def step_convspec(
    repo_root: str,
    path: str,
    *,
    data_dir: Optional[str] = None,
) -> dict:
    """Deterministic convspec stage (R3, replay-safe): idiom-KB lookup +
    artifact ingest. Returns ``{"outcome":"specced",...}`` if a valid
    agent-produced artifact is checked in, else the deterministic
    ``{"needs_agent_work": True, ...}`` sentinel — NEVER raises (the
    skill spawns the analysis/research sub-agents on that sentinel and
    re-drives; on re-drive this step finds the artifact and completes).
    Calls the convspec entrypoint verbatim (D2)."""
    from codeconv.tools.convspec.workflow import run_convspec_ingest

    return run_convspec_ingest(
        Path(repo_root),
        rel_path=path,
        data_dir=Path(data_dir) if data_dir else None,
    )


@_dbos_step("plan")
def step_plan(
    repo_root: str,
    path: str,
    *,
    data_dir: Optional[str] = None,
) -> dict:
    """Conversion-plan stage — feature-017 two-phase per-file plan.

    Calls ``planagents.run_plan_started`` then ``run_plan_completed``
    verbatim; the tool owns the two-phase write so a half-step is
    observably incomplete (FR-003). Wrapped, not reimplemented (D2).
    Verified signatures: ``run_plan_started(*, repo_root, data_dir,
    path, sha256, replan, no_tombstone_update)`` and
    ``run_plan_completed(*, repo_root, data_dir, path, plan_path,
    escalations, no_tombstone_update)`` — both keyword-only, ``path=``
    (not ``rel_path``), no ``quiet``."""
    from codeconv.tools.planagents.workflow import (
        run_plan_completed,
        run_plan_started,
    )

    rr = Path(repo_root)
    dd = Path(data_dir) if data_dir else None
    started = run_plan_started(repo_root=rr, data_dir=dd, path=path)
    completed = run_plan_completed(repo_root=rr, data_dir=dd, path=path)
    return {"plan_started": started, "plan_completed": completed}


__all__ = [
    "bind_dbos_steps",
    "step_convspec",
    "step_depgraph_compute",
    "step_discover",
    "step_plan",
    "step_scaffold",
]
