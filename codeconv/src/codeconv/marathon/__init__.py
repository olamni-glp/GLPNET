"""``codeconv marathon`` — durable, restart-safe stage harness (feature 024).

A statically-registered Typer sub-app (mirroring the bridge-free
``tutorials`` command in ``codeconv/cli.py``) — deliberately **NOT** wired
through ``runner.tool_registry()`` so the harness never appears as a
Dart→C# conversion tool in ``codeconv list`` (research.md D3).

The harness *composes* the existing ``codeconv`` substrate as libraries —
``bridge_client`` (PGLite bridge), ``db.engine`` (SQLAlchemy + DBOS), and
``durable`` (deterministic, replay-safe id derivation) — and adds only the
three things the Claude Code Workflow tool lacks (FR-010): cross-session
durable checkpointing, the per-stage approval gate, and a JSON-store
fallback.

Subcommands (contracts/cli.md): ``start``, ``resume``, ``gate``,
``verify-spike``, ``status``, ``rerun``, ``trace``, ``reconcile``,
``doctor``. Each honors the global ``--data-dir`` convention and reuses
the parent CLI's ``ctx.obj`` (repo_root / data_dir / json / quiet).
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

import typer

# Exit codes (contracts/cli.md): 0 success · 2 escalation/awaiting-Gabi
# (blocked, not an error) · 64 usage/filesystem guard · 70 internal failure.
EXIT_OK = 0
EXIT_ESCALATION = 2
EXIT_USAGE = 64
EXIT_INTERNAL = 70


marathon_app = typer.Typer(
    add_completion=False,
    no_args_is_help=True,
    help=(
        "Durable, restart-safe stage harness driving one long buildkit "
        "feature across many sessions (feature 024). See "
        "/marathon-stage-harness."
    ),
)


# --- context accessors (the parent callback stashes these on ctx.obj) -------


def _repo_root(ctx: typer.Context) -> Path:
    obj = getattr(ctx, "obj", None)
    if isinstance(obj, dict) and obj.get("repo_root"):
        return Path(obj["repo_root"])
    return Path.cwd()


def _data_dir(ctx: typer.Context) -> Optional[Path]:
    obj = getattr(ctx, "obj", None)
    if isinstance(obj, dict) and obj.get("data_dir"):
        return Path(obj["data_dir"])
    return None


def _ctx_flag(ctx: typer.Context, key: str) -> bool:
    obj = getattr(ctx, "obj", None)
    return bool(obj.get(key)) if isinstance(obj, dict) else False


def _resolve_marathon_id(store, feature: Optional[str]) -> Optional[str]:
    """Resolve the target marathon id: from ``--feature`` when given, else the
    sole known marathon (subcommands take ``[--feature]``; one marathon is the
    feature's scope). ``None`` when absent/ambiguous → the caller errors."""
    from .store import marathon_id_for

    if feature:
        return marathon_id_for(feature)
    ids = store.list_marathon_ids()
    return ids[0] if len(ids) == 1 else None


def _emit(ctx: typer.Context, payload: dict, *, json_out: bool) -> None:
    """Print a result either as JSON (``--json``, global or local) or as a
    compact key=value human line."""
    import json as _json

    if json_out or _ctx_flag(ctx, "json"):
        typer.echo(_json.dumps(payload, indent=2, default=str))
    else:
        for k, v in payload.items():
            typer.echo(f"{k}: {v}")


# ---------------------------------------------------------------------------
# start — create/re-attach the marathon + record the two standing grants (T009)
# ---------------------------------------------------------------------------


@marathon_app.command("start")
def cmd_start(
    ctx: typer.Context,
    feature: str = typer.Option(..., "--feature", help="Feature slug to drive."),
    branch: Optional[str] = typer.Option(None, "--branch", help="Feature branch."),
    budget: Optional[int] = typer.Option(
        None, "--budget", help="Token ceiling (omit = unbounded)."
    ),
    auto: bool = typer.Option(False, "--auto", help="Unattended auto-mode."),
    preauth_commit_push: bool = typer.Option(
        False, "--preauth-commit-push", help="Standing grant #1 (FR-014/D10)."
    ),
    preauth_workflow: bool = typer.Option(
        False, "--preauth-workflow", help="Standing grant #2 (FR-023/D10)."
    ),
    json_out: bool = typer.Option(False, "--json", help="Emit the structured model."),
) -> None:
    """Create (or idempotently re-attach to) the marathon and record the two
    standing preauthorizations (FR-014/023, D10)."""
    from .models import Marathon
    from .store import MarathonStore, marathon_id_for

    store = MarathonStore(_repo_root(ctx), data_dir=_data_dir(ctx))
    marathon_id = marathon_id_for(feature)
    try:
        m = store.upsert_marathon(
            Marathon(
                id=marathon_id,
                feature_slug=feature,
                feature_branch=branch or feature,
                budget_ceiling=budget,
                preauth_commit_push=preauth_commit_push,
                preauth_workflow_optin=preauth_workflow,
                auto_mode=auto,
            )
        )
    except Exception as exc:  # noqa: BLE001 — surface a clean internal error
        typer.echo(f"marathon start: {exc}", err=True)
        raise typer.Exit(EXIT_INTERNAL)

    _emit(
        ctx,
        {
            "marathon_id": m.id,
            "feature_slug": m.feature_slug,
            "auto_mode": m.auto_mode,
            "preauth_commit_push": m.preauth_commit_push,
            "preauth_workflow_optin": m.preauth_workflow_optin,
            "budget_ceiling": m.budget_ceiling,
        },
        json_out=json_out,
    )


# ---------------------------------------------------------------------------
# doctor — read-only diagnostics (T010)
# ---------------------------------------------------------------------------


@marathon_app.command("doctor")
def cmd_doctor(
    ctx: typer.Context,
    feature: Optional[str] = typer.Option(
        None, "--feature", help="Feature slug to diagnose (per-marathon stats)."
    ),
    json_out: bool = typer.Option(False, "--json", help="Emit the structured model."),
) -> None:
    """Diagnose bridge reachability, the active store, last checkpoint seq per
    store, open escalations, and budget headroom (contracts/cli.md). Exit 0."""
    from .escalation import open_escalations
    from .store import MarathonStore, marathon_id_for

    store = MarathonStore(_repo_root(ctx), data_dir=_data_dir(ctx))
    report: dict = {"active_store": store.active_store()}
    report["bridge_reachable"] = report["active_store"] == "primary"

    if feature:
        marathon_id = marathon_id_for(feature)
        report["marathon_id"] = marathon_id
        primary_cps = store._primary_checkpoints(marathon_id)
        fallback_cps = store._fallback_checkpoints(marathon_id)
        report["last_seq_primary"] = store._max_seq(primary_cps)
        report["last_seq_fallback"] = store._max_seq(fallback_cps)
        report["open_escalations"] = len(open_escalations(store, marathon_id))
        m = store.read_marathon(marathon_id)
        if m is not None and m.budget_ceiling is not None:
            report["budget_headroom"] = m.budget_ceiling - m.budget_spent
        else:
            report["budget_headroom"] = None

    _emit(ctx, report, json_out=json_out)
    raise typer.Exit(EXIT_OK)


# ---------------------------------------------------------------------------
# verify-spike — FR-011 substrate verification (T015, runs FIRST per FR-011)
# ---------------------------------------------------------------------------


@marathon_app.command("verify-spike")
def cmd_verify_spike(
    ctx: typer.Context,
    feature: Optional[str] = typer.Option(
        None, "--feature", help="Feature slug (defaults to the sole marathon)."
    ),
    json_out: bool = typer.Option(False, "--json", help="Emit the structured model."),
) -> None:
    """Run the FR-011 verification spike (cached-prefix resume + budget
    observability) and record a ``verification_traces`` row (SC-008)."""
    from .store import MarathonStore
    from .verify_spike import run_spike

    store = MarathonStore(_repo_root(ctx), data_dir=_data_dir(ctx))
    marathon_id = _resolve_marathon_id(store, feature)
    if marathon_id is None:
        typer.echo(
            "marathon verify-spike: specify --feature (no sole marathon found)",
            err=True,
        )
        raise typer.Exit(EXIT_USAGE)
    m = store.read_marathon(marathon_id)
    ceiling = m.budget_ceiling if m is not None else None
    try:
        res = run_spike(store, marathon_id, ceiling=ceiling)
    except Exception as exc:  # noqa: BLE001
        typer.echo(f"marathon verify-spike: {exc}", err=True)
        raise typer.Exit(EXIT_INTERNAL)
    _emit(ctx, res, json_out=json_out)
    raise typer.Exit(EXIT_OK if res.get("cached_prefix_ok") else EXIT_INTERNAL)


# ---------------------------------------------------------------------------
# gate — per-stage plan-approval gate (T027)
# ---------------------------------------------------------------------------


@marathon_app.command("gate")
def cmd_gate(
    ctx: typer.Context,
    block: str = typer.Option(..., "--block", help="Stage-block id."),
    approve: bool = typer.Option(False, "--approve", help="Record an approval."),
    change: bool = typer.Option(False, "--change", help="Record a change (needs --plan)."),
    plan: Optional[str] = typer.Option(None, "--plan", help="Plan ref (present/change)."),
    by: Optional[str] = typer.Option(None, "--by", help="Decider, e.g. gabi."),
    json_out: bool = typer.Option(False, "--json", help="Emit the structured model."),
) -> None:
    """Present / record the per-stage approval decision (FR-004/005, D6). With
    no flags, presents the plan + current state (no re-ask if already
    approved — SC-004)."""
    from .gate import present_gate, record_decision
    from .store import MarathonStore

    if approve and change:
        typer.echo("marathon gate: --approve and --change are mutually exclusive", err=True)
        raise typer.Exit(EXIT_USAGE)

    store = MarathonStore(_repo_root(ctx), data_dir=_data_dir(ctx))
    if approve:
        res = record_decision(store, block, outcome="approve", decided_by=by)
    elif change:
        if not plan:
            typer.echo("marathon gate: --change requires --plan <ref>", err=True)
            raise typer.Exit(EXIT_USAGE)
        record_decision(store, block, outcome="change", decided_by=by, plan_ref=plan)
        res = present_gate(store, block, plan_ref=plan)  # re-present the new plan
        res["state"] = "changed"
    else:
        res = present_gate(store, block, plan_ref=plan or f"plan:{block}")

    _emit(ctx, res, json_out=json_out)
    raise typer.Exit(EXIT_OK)


# ---------------------------------------------------------------------------
# resume — objective restart-safe resume (T022, the US1 keystone)
# ---------------------------------------------------------------------------


@marathon_app.command("resume")
def cmd_resume(
    ctx: typer.Context,
    feature: Optional[str] = typer.Option(
        None, "--feature", help="Feature slug (defaults to the sole marathon)."
    ),
    json_out: bool = typer.Option(False, "--json", help="Emit the structured model."),
) -> None:
    """Objectively locate the resume position from durable state and report it
    (FR-002/003/005/020/021). Never reads a conversation summary. On store
    divergence → exit 2 + escalation (D5)."""
    from .checkpoint import resume as _resume
    from .store import MarathonStore

    store = MarathonStore(_repo_root(ctx), data_dir=_data_dir(ctx))
    marathon_id = _resolve_marathon_id(store, feature)
    if marathon_id is None:
        typer.echo("marathon resume: specify --feature (no sole marathon found)", err=True)
        raise typer.Exit(EXIT_USAGE)

    report = _resume(store, marathon_id)
    payload = {"marathon_id": marathon_id, **report.as_dict()}
    _emit(ctx, payload, json_out=json_out)
    if report.diverged:
        raise typer.Exit(EXIT_ESCALATION)
    raise typer.Exit(EXIT_OK)


# ---------------------------------------------------------------------------
# status — standardized four-field report on the ~5-min cadence (T038)
# ---------------------------------------------------------------------------


@marathon_app.command("status")
def cmd_status(
    ctx: typer.Context,
    feature: Optional[str] = typer.Option(
        None, "--feature", help="Feature slug (defaults to the sole marathon)."
    ),
    emit: bool = typer.Option(
        False, "--emit", help="Persist a status_reports row (cadence driver)."
    ),
    json_out: bool = typer.Option(False, "--json", help="Emit the structured model."),
) -> None:
    """Show / emit the standardized four-field report — done / issues / tokens
    (spent + remaining) / to-do (FR-013, D8, SC-005). ``--emit`` persists a
    row; the ~5-min cadence driver (the skill) calls this at each checkpoint."""
    from .status import build_status, emit_status, status_to_dict
    from .store import MarathonStore

    store = MarathonStore(_repo_root(ctx), data_dir=_data_dir(ctx))
    marathon_id = _resolve_marathon_id(store, feature)
    if marathon_id is None:
        typer.echo("marathon status: specify --feature (no sole marathon found)", err=True)
        raise typer.Exit(EXIT_USAGE)

    report = emit_status(store, marathon_id) if emit else build_status(store, marathon_id)
    _emit(ctx, status_to_dict(report), json_out=json_out)
    raise typer.Exit(EXIT_OK)


# ---------------------------------------------------------------------------
# rerun — per-stage / per-subagent re-run on failure (T033)
# ---------------------------------------------------------------------------


@marathon_app.command("rerun")
def cmd_rerun(
    ctx: typer.Context,
    block: str = typer.Option(..., "--block", help="Stage-block id to re-run."),
    subagent: Optional[str] = typer.Option(
        None, "--subagent", help="Re-run only this subagent (isolated)."
    ),
    json_out: bool = typer.Option(False, "--json", help="Emit the structured model."),
) -> None:
    """Re-run a failed stage from its last checkpoint (``--block``), or a single
    failed subagent in isolation (``--block --subagent``). Succeeded units are
    never redone; failure history is retained append-only (FR-006/007/008)."""
    from .orchestrate import rerun_block, rerun_subagent
    from .store import MarathonStore

    store = MarathonStore(_repo_root(ctx), data_dir=_data_dir(ctx))
    if subagent is not None:
        res = rerun_subagent(store, block, subagent)
    else:
        res = rerun_block(store, block)
    res = {"block_id": block, **res}
    _emit(ctx, res, json_out=json_out)
    raise typer.Exit(EXIT_OK)


# ---------------------------------------------------------------------------
# trace — append-only verification-trace substrate (T045)
# ---------------------------------------------------------------------------


@marathon_app.command("trace")
def cmd_trace(
    ctx: typer.Context,
    subject: str = typer.Option(..., "--subject", help="Stage/primitive name."),
    input_: str = typer.Option(..., "--input", help="Experiment input as a JSON object."),
    score: Optional[float] = typer.Option(None, "--score", help="Metric score."),
    accept: bool = typer.Option(False, "--accept", help="Record an accept decision."),
    reject: bool = typer.Option(False, "--reject", help="Record a reject decision."),
    feature: Optional[str] = typer.Option(
        None, "--feature", help="Feature slug (defaults to the sole marathon)."
    ),
    json_out: bool = typer.Option(False, "--json", help="Emit the structured model."),
) -> None:
    """Append a verification-trace record (substrate only — no optimizer).
    Preserves ``(subject, refine_seq)`` order; never overwrites earlier
    iterations (FR-016/017)."""
    import json as _json

    from .store import MarathonStore
    from .trace import write_trace

    if accept == reject:
        typer.echo("marathon trace: pass exactly one of --accept / --reject", err=True)
        raise typer.Exit(EXIT_USAGE)
    try:
        experiment_input = _json.loads(input_)
        if not isinstance(experiment_input, dict):
            raise ValueError("--input must be a JSON object")
    except ValueError as exc:
        typer.echo(f"marathon trace: bad --input: {exc}", err=True)
        raise typer.Exit(EXIT_USAGE)

    store = MarathonStore(_repo_root(ctx), data_dir=_data_dir(ctx))
    marathon_id = _resolve_marathon_id(store, feature)
    if marathon_id is None:
        typer.echo("marathon trace: specify --feature (no sole marathon found)", err=True)
        raise typer.Exit(EXIT_USAGE)

    t = write_trace(
        store,
        marathon_id,
        subject=subject,
        experiment_input=experiment_input,
        metric_score=score,
        decision="accept" if accept else "reject",
    )
    _emit(
        ctx,
        {
            "id": t.id,
            "subject": t.subject,
            "refine_seq": t.refine_seq,
            "decision": t.decision,
        },
        json_out=json_out,
    )
    raise typer.Exit(EXIT_OK)


# ---------------------------------------------------------------------------
# reconcile — primary vs JSON fallback by sequence_no (T022)
# ---------------------------------------------------------------------------


@marathon_app.command("reconcile")
def cmd_reconcile(
    ctx: typer.Context,
    feature: Optional[str] = typer.Option(
        None, "--feature", help="Feature slug (defaults to the sole marathon)."
    ),
    json_out: bool = typer.Option(False, "--json", help="Emit the structured model."),
) -> None:
    """Compare primary (PGLite) and JSON-fallback stores by ``sequence_no``.
    Strictly-higher wins and fast-forwards the stale store; a true fork → exit
    2 + escalation, never a silent pick (FR-020/021, D5, SC-007)."""
    from .store import MarathonStore

    store = MarathonStore(_repo_root(ctx), data_dir=_data_dir(ctx))
    marathon_id = _resolve_marathon_id(store, feature)
    if marathon_id is None:
        typer.echo(
            "marathon reconcile: specify --feature (no sole marathon found)", err=True
        )
        raise typer.Exit(EXIT_USAGE)

    rec = store.reconcile(marathon_id)
    payload = {
        "marathon_id": marathon_id,
        "status": rec.status,
        "primary_seq": rec.primary_seq,
        "fallback_seq": rec.fallback_seq,
        "winner": rec.winner,
        "escalation_id": rec.escalation_id,
    }
    _emit(ctx, payload, json_out=json_out)
    if rec.status == "fork":
        raise typer.Exit(EXIT_ESCALATION)
    raise typer.Exit(EXIT_OK)


__all__ = [
    "EXIT_ESCALATION",
    "EXIT_INTERNAL",
    "EXIT_OK",
    "EXIT_USAGE",
    "marathon_app",
]
