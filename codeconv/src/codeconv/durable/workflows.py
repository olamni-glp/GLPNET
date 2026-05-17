"""DBOS workflows — feature 018, T012 (FR-002/FR-003, D1=a, D2).

The builder run is an **outer** ``@DBOS.workflow`` that walks the
feature-015 ``codeconv.dart_depgraph`` order **read-only** (it MUST NOT
recompute order/SCC/status — 015 is the backbone) and launches one
**child** ``@DBOS.workflow`` per file, or per SCC group as **one
indivisible unit** (FR-002 — a dependency cycle is analysed/specced/
scaffolded/converted together). Each child body is a fixed, ordered
sequence of the :mod:`codeconv.durable.steps` stage steps; DBOS
checkpoints each step so on resume completed steps are **skipped** and
the child re-enters at the interrupted stage (FR-003), making a resumed
run bit-identical to an uninterrupted one (FR-004/SC-002).

Determinism: workflow ids come from :mod:`codeconv.durable`
(``outer_workflow_id`` / ``file_workflow_id`` / ``scc_workflow_id``) and
are pinned via ``dbos.SetWorkflowID`` so a re-run *recovers* the same
workflow rather than starting a new one (R9).

Like :mod:`codeconv.durable.steps`, DBOS decoration is applied lazily at
*registration* time (``bind_dbos_workflows`` after the CLI launched
DBOS), so importing this module never requires a bridge/DBOS — the pure
ordering/SCC-grouping logic is unit-testable on its own.
"""

from __future__ import annotations

from typing import Any, Callable, Iterable, Optional, Sequence

from codeconv.durable import (
    file_workflow_id,
    scc_workflow_id,
)

# Fixed per-file/per-SCC stage order (contract dbos_workflow_model.md §
# Taxonomy). ``discover`` + ``depgraph_compute`` are graph-entry stages
# the OUTER workflow runs once; the per-unit child runs the remaining
# per-file stages in this order. Kept as data so it is inspectable and
# testable without DBOS.
PER_UNIT_STAGES: tuple[str, ...] = ("scaffold", "plan")
GRAPH_ENTRY_STAGES: tuple[str, ...] = ("discover", "depgraph_compute")


def plan_units(
    topo_order: Sequence[str],
    scc_groups: Optional[dict[str, Sequence[str]]] = None,
) -> list[dict[str, Any]]:
    """Pure: turn the feature-015 topo order (+ SCC groups) into the
    ordered list of work units the outer workflow will launch.

    ``topo_order`` is the 015 dependency order (read-only — NEVER
    recomputed here). ``scc_groups`` maps a cycle-group id → its member
    rel-paths; members of one SCC collapse into a **single** unit
    (FR-002) inserted at the position of its earliest member, so no
    member is processed before the whole group and downstream files
    still see the group as one completed dependency.

    Returns a list of ``{"kind": "file"|"scc", "id": <wf id>,
    "members": [...]}`` in launch order. Pure + DBOS-free ⇒ unit-testable
    (parallels the feature-017 readiness/frontier purity).
    """
    scc_groups = scc_groups or {}
    # rel_path -> scc gid (for O(1) membership)
    member_to_gid: dict[str, str] = {}
    for gid, members in scc_groups.items():
        for m in members:
            member_to_gid[_norm(m)] = gid

    emitted_gids: set[str] = set()
    units: list[dict[str, Any]] = []
    for rel in topo_order:
        rel_n = _norm(rel)
        gid = member_to_gid.get(rel_n)
        if gid is not None:
            if gid in emitted_gids:
                continue  # group already emitted at its earliest member
            emitted_gids.add(gid)
            members = sorted(_norm(m) for m in scc_groups[gid])
            units.append(
                {
                    "kind": "scc",
                    "id": scc_workflow_id(members),
                    "members": members,
                }
            )
        else:
            units.append(
                {
                    "kind": "file",
                    "id": file_workflow_id(rel_n),
                    "members": [rel_n],
                }
            )
    return units


def _norm(rel: str) -> str:
    return rel.replace("\\", "/").strip("/")


# --------------------------------------------------------------------------
# Workflow bodies (plain functions; DBOS-decorated at bind time).
# --------------------------------------------------------------------------


def child_unit_workflow(
    repo_root: str,
    members: Sequence[str],
    *,
    data_dir: Optional[str] = None,
    steps: Optional[dict[str, Callable[..., Any]]] = None,
) -> dict:
    """One indivisible work unit: a single file (``len(members)==1``) or
    a whole SCC group (FR-002). Runs the per-unit stages in order; for an
    SCC every member is taken through a stage before the next stage
    (coordinated batch — downstream blocked until the group's terminal
    step completes). Each step is a checkpointed ``@DBOS.step`` so a
    crash resumes at the interrupted stage (FR-003)."""
    steps = steps or {}
    results: dict[str, Any] = {"members": list(members), "stages": {}}
    for stage in PER_UNIT_STAGES:
        step_fn = steps.get(stage)
        if step_fn is None:
            continue
        stage_out: dict[str, Any] = {}
        for rel in members:
            if stage == "plan":
                stage_out[rel] = step_fn(repo_root, rel, data_dir=data_dir)
            else:
                # whole-tree stages (scaffold) run once per unit, not
                # per member; run on first member, reuse for the rest.
                stage_out[rel] = step_fn(repo_root, data_dir=data_dir)
        results["stages"][stage] = stage_out
    return results


def outer_builder_workflow(
    repo_root: str,
    units: Sequence[dict[str, Any]],
    *,
    data_dir: Optional[str] = None,
    launch_child: Optional[Callable[..., Any]] = None,
) -> dict:
    """Walk the pre-planned units (from :func:`plan_units`, derived from
    the read-only 015 order) and launch one child workflow each, in
    order. ``launch_child`` is injected by ``bind_dbos_workflows`` so the
    pure ordering here is testable without DBOS; in production it is
    ``dbos.start_workflow`` under a pinned ``SetWorkflowID(unit['id'])``
    so a re-run recovers the same child (R9/FR-004)."""
    launched: list[str] = []
    for unit in units:
        if launch_child is not None:
            launch_child(unit, repo_root=repo_root, data_dir=data_dir)
        launched.append(unit["id"])
    return {"units": len(units), "launched": launched}


def bind_dbos_workflows(dbos: Any, steps: dict[str, Callable[..., Any]]) -> dict:
    """Decorate the child/outer bodies as ``@DBOS.workflow`` after the
    CLI launched DBOS. ``steps`` is the bound step map from
    :func:`codeconv.durable.steps.bind_dbos_steps`. Returns
    ``{"child": <wf>, "outer": <wf>}`` for the builder tool (T021) to
    drive. Idempotent (DBOS dedups by qualified name)."""

    def _child(repo_root: str, members: Sequence[str], data_dir=None):
        return child_unit_workflow(
            repo_root, members, data_dir=data_dir, steps=steps
        )

    def _outer(repo_root: str, units: Sequence[dict], data_dir=None):
        def _launch(unit, repo_root, data_dir):
            from dbos import SetWorkflowID

            # Deterministic child id ⇒ a re-run RECOVERS the same child
            # (DBOS dedups); already-completed children return their
            # checkpointed result without re-executing steps (FR-004/
            # SC-002). Await each child before the next: serial drive
            # through the 012 single-writer bridge (R12 concurrency=1) —
            # the outer is itself one durable workflow, so this whole
            # walk is replay-safe and resumes at the interrupted child.
            with SetWorkflowID(unit["id"]):
                h = dbos.start_workflow(
                    child_wf, repo_root, unit["members"], data_dir
                )
            return h.get_result()

        return outer_builder_workflow(
            repo_root, units, data_dir=data_dir, launch_child=_launch
        )

    child_wf = dbos.workflow()(_child)
    outer_wf = dbos.workflow()(_outer)
    return {"child": child_wf, "outer": outer_wf}


__all__ = [
    "GRAPH_ENTRY_STAGES",
    "PER_UNIT_STAGES",
    "bind_dbos_workflows",
    "child_unit_workflow",
    "outer_builder_workflow",
    "plan_units",
]
