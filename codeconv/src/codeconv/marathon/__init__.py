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


__all__ = [
    "EXIT_ESCALATION",
    "EXIT_INTERNAL",
    "EXIT_OK",
    "EXIT_USAGE",
    "marathon_app",
]
