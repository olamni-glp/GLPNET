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
# Taxonomy, Amendment 2 / FR-044). ``discover`` + ``depgraph_compute``
# are graph-entry stages the OUTER workflow runs once. The per-unit work
# is split across the agent gate into two DBOS workflows:
#   PRE  (deterministic, terminal, idempotent): scaffold + convspec
#         — convspec writes ``convspec_started_at`` and, with no
#         artifact, returns the needs_agent_work sentinel (surfaced by
#         the outer aggregation; the wf still completes — a wf that
#         returned cannot re-run a checkpointed step).
#   POST (content-addressed; launched only once the artifact exists):
#         convspec (re-invoked verbatim → now finds the artifact →
#         specced) + plan.
# ``PER_UNIT_STAGES`` is retained as the *logical* full per-unit order
# (docs + the pure stage-order test): PRE then POST, deduped.
PRE_STAGES: tuple[str, ...] = ("scaffold", "convspec")
POST_STAGES: tuple[str, ...] = ("convspec", "plan")
PER_UNIT_STAGES: tuple[str, ...] = ("scaffold", "convspec", "plan")
GRAPH_ENTRY_STAGES: tuple[str, ...] = ("discover", "depgraph_compute")
# Stages invoked once per FILE (need a rel-path arg); others run once
# per unit (whole-tree, e.g. scaffold).
_PER_FILE_STAGES: frozenset[str] = frozenset({"convspec", "plan"})

# Feature 019 (decision B, 2026-05-23): codegen is a SEPARATELY-DRIVEN
# durable phase, NOT auto-chained into the builder's default per-unit
# sequence. The ``codegen`` DBOS step IS registered (codeconv.durable.
# steps.step_codegen — replay-safe, available for explicit/future
# chaining) but is intentionally absent from PER_UNIT_STAGES/POST_STAGES
# so a ``builder run`` keeps its clean "completed-at-plan" semantics.
# Codegen is driven via ``/codeconv-codegen`` (build + human-review
# gate). See specs/019-codeconv-codegen/contracts/dbos_codegen_stage.md
# § Placement (amended).
#
# Feature 020 (T024/T025): the ``equiv`` stage is sequenced AFTER
# ``codegen`` in the logical pipeline (a converted file must EXIST and
# BUILD before its trace-equivalence is measurable). It mirrors codegen's
# separately-driven placement — the ``equiv`` DBOS step IS registered
# (codeconv.durable.steps.step_equiv — replay-safe deterministic ingest
# of recorded traces, R12) and is intentionally ABSENT from
# PER_UNIT_STAGES/POST_STAGES so a ``builder run`` does not auto-spawn
# REPLs. The frontier is driven via ``/codeconv-equiv`` (the agent layer
# owns the nondeterministic capture — dual-REPL spawn — and re-invokes
# the step on the typed ``needs_agent_work`` sentinel). See
# specs/020-trace-equivalence-fidelity/contracts/dbos_equiv_stage.md.


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


def _run_stages(
    repo_root: str,
    members: Sequence[str],
    stages: Sequence[str],
    steps: dict[str, Callable[..., Any]],
    data_dir: Optional[str],
) -> dict:
    """Run an ordered stage list over one indivisible unit (a file or a
    whole SCC group — FR-002: every member is taken through a stage
    before the next stage). Per-file stages loop members; whole-tree
    stages (scaffold) run once. Short-circuits — and records
    ``needs_agent_work`` — if any member's stage returns the
    deterministic convspec sentinel (no/invalid artifact): later stages
    (plan) MUST NOT run until the spec exists. Each step is a
    checkpointed ``@DBOS.step`` so a crash resumes at the interrupted
    stage within this workflow (FR-003)."""
    results: dict[str, Any] = {"members": list(members), "stages": {}}
    for stage in stages:
        step_fn = steps.get(stage)
        if step_fn is None:
            continue
        stage_out: dict[str, Any] = {}
        if stage in _PER_FILE_STAGES:
            for rel in members:
                stage_out[rel] = step_fn(repo_root, rel, data_dir=data_dir)
        else:
            stage_out["_unit"] = step_fn(repo_root, data_dir=data_dir)
        results["stages"][stage] = stage_out
        if any(
            isinstance(v, dict) and v.get("needs_agent_work")
            for v in stage_out.values()
        ):
            results["needs_agent_work"] = [
                v for v in stage_out.values()
                if isinstance(v, dict) and v.get("needs_agent_work")
            ]
            return results
    return results


def pre_agent_workflow(
    repo_root: str,
    members: Sequence[str],
    *,
    data_dir: Optional[str] = None,
    steps: Optional[dict[str, Callable[..., Any]]] = None,
) -> dict:
    """Deterministic PRE-agent unit (Amendment 2 / FR-044): scaffold +
    convspec. Completes **terminally & idempotently** — convspec writes
    ``convspec_started_at`` (idempotent) and, with no artifact, returns
    the needs_agent_work sentinel recorded in the result (the outer
    aggregation surfaces it; the wf does NOT stay non-terminal — a wf
    that returned cannot re-run a checkpointed step, which is exactly the
    facet-3 defect this split fixes). Recovered identically on resume
    (R9/FR-003/FR-004/SC-002)."""
    return _run_stages(
        repo_root, members, PRE_STAGES, steps or {}, data_dir
    )


def post_agent_workflow(
    repo_root: str,
    members: Sequence[str],
    *,
    data_dir: Optional[str] = None,
    steps: Optional[dict[str, Callable[..., Any]]] = None,
) -> dict:
    """Content-addressed POST-agent unit (Amendment 2 / FR-044): convspec
    (re-invoked verbatim — the artifact now exists ⇒ ``specced``) +
    plan. The outer launches this ONLY once the artifact(s) exist, under
    a workflow id keyed to the artifact digest, so it is a *fresh*
    deterministic unit per spec revision (a re-spec ⇒ new id ⇒ fresh
    ingest) while remaining replay-safe within one artifact (SC-002).
    An invalid artifact ⇒ convspec returns the sentinel again ⇒ plan is
    skipped and the unit is re-surfaced for the agent."""
    return _run_stages(
        repo_root, members, POST_STAGES, steps or {}, data_dir
    )


def outer_builder_workflow(
    repo_root: str,
    units: Sequence[dict[str, Any]],
    *,
    data_dir: Optional[str] = None,
    process_unit: Optional[Callable[..., Any]] = None,
) -> dict:
    """Walk the pre-planned units (from :func:`plan_units`, read-only 015
    order) and drive each through the PRE → artifact-gate → POST cycle
    via the injected ``process_unit`` (so the pure ordering/aggregation
    here is testable without DBOS; the real one in
    :func:`bind_dbos_workflows` launches the two workflows under pinned
    ids and gates POST on artifact presence).

    The outer **observes** each unit's result and surfaces every
    ``needs_agent_work`` sentinel in ``agent_units`` (contract
    ``dbos_workflow_model.md`` § needs_agent_work — "the workflow
    observes needs_agent_work"). Without this the convspec short-circuit
    is invisible to ``builder run`` and the run wrongly reports
    ``completed`` with zero specs persisted (US2 Acceptance 1 — the
    facet-1 defect). ``launched`` records the actual pre/post wf ids
    driven; deterministic given the unit order + replay-safe results."""
    launched: list[str] = []
    agent_units: list = []
    for unit in units:
        if process_unit is None:
            launched.append(unit["id"])
            continue
        res = process_unit(unit, repo_root=repo_root, data_dir=data_dir)
        if isinstance(res, dict):
            launched.extend(res.get("launched") or [unit["id"]])
            naw = res.get("needs_agent_work")
            if naw:
                agent_units.extend(naw if isinstance(naw, list) else [naw])
        else:
            launched.append(unit["id"])
    return {
        "units": len(units),
        "launched": launched,
        "agent_units": agent_units,
    }


def bind_dbos_workflows(dbos: Any, steps: dict[str, Callable[..., Any]]) -> dict:
    """Decorate the pre/post/outer bodies as ``@DBOS.workflow`` after the
    CLI launched DBOS. ``steps`` is the bound step map from
    :func:`codeconv.durable.steps.bind_dbos_steps`. Returns
    ``{"pre": <wf>, "post": <wf>, "outer": <wf>}`` for the builder tool
    to drive. Idempotent (DBOS dedups by qualified name)."""

    def _pre(repo_root: str, members: Sequence[str], data_dir=None):
        return pre_agent_workflow(
            repo_root, members, data_dir=data_dir, steps=steps
        )

    def _post(repo_root: str, members: Sequence[str], data_dir=None):
        return post_agent_workflow(
            repo_root, members, data_dir=data_dir, steps=steps
        )

    def _outer(repo_root: str, units: Sequence[dict], data_dir=None):
        def _process(unit, repo_root, data_dir):
            from pathlib import Path

            from dbos import SetWorkflowID

            from codeconv.durable import (
                artifact_digest,
                combined_artifact_digest,
                post_file_workflow_id,
                post_scc_workflow_id,
                pre_file_workflow_id,
                pre_scc_workflow_id,
            )
            from codeconv.tools.convspec.artefact import artifact_path

            kind = unit.get("kind", "file")
            members = list(unit["members"])

            # --- PRE: deterministic, recovered if already done (R9). ---
            pre_id = (
                pre_scc_workflow_id(members)
                if kind == "scc"
                else pre_file_workflow_id(members[0])
            )
            with SetWorkflowID(pre_id):
                hp = dbos.start_workflow(pre_wf, repo_root, members, data_dir)
            pre_res = hp.get_result()
            launched = [pre_id]
            agent = list(
                pre_res.get("needs_agent_work") or []
                if isinstance(pre_res, dict)
                else []
            )

            # --- Artifact gate (read-only, deterministic given FS). ---
            rr = Path(repo_root)
            texts: dict[str, str] = {}
            for m in members:
                ap = artifact_path(rr, m)
                if ap.is_file():
                    texts[m] = ap.read_text(encoding="utf-8")
            if len(texts) != len(members):
                # Awaiting agent for ≥1 member — POST not launched
                # (SCC = one indivisible unit: ALL members' artifacts
                # required, FR-002). Surface the pre sentinel(s).
                return {
                    "unit_id": unit["id"],
                    "members": members,
                    "launched": launched,
                    "pre": pre_res,
                    "needs_agent_work": agent,
                }

            # --- POST: content-addressed by the artifact (FR-044). ---
            if kind == "scc":
                digest = combined_artifact_digest(
                    artifact_digest(texts[m]) for m in sorted(members)
                )
                post_id = post_scc_workflow_id(members, digest)
            else:
                digest = artifact_digest(texts[members[0]])
                post_id = post_file_workflow_id(members[0], digest)
            with SetWorkflowID(post_id):
                hq = dbos.start_workflow(post_wf, repo_root, members, data_dir)
            post_res = hq.get_result()
            launched.append(post_id)
            post_agent = list(
                post_res.get("needs_agent_work") or []
                if isinstance(post_res, dict)
                else []
            )
            return {
                "unit_id": unit["id"],
                "members": members,
                "launched": launched,
                "pre": pre_res,
                "post": post_res,
                # Only post_agent here: an invalid artifact re-surfaces;
                # a valid one ⇒ specced ⇒ [] (no double-count vs pre).
                "needs_agent_work": post_agent,
            }

        return outer_builder_workflow(
            repo_root, units, data_dir=data_dir, process_unit=_process
        )

    pre_wf = dbos.workflow()(_pre)
    post_wf = dbos.workflow()(_post)
    outer_wf = dbos.workflow()(_outer)
    return {"pre": pre_wf, "post": post_wf, "outer": outer_wf}


__all__ = [
    "GRAPH_ENTRY_STAGES",
    "PER_UNIT_STAGES",
    "POST_STAGES",
    "PRE_STAGES",
    "bind_dbos_workflows",
    "outer_builder_workflow",
    "plan_units",
    "post_agent_workflow",
    "pre_agent_workflow",
]
