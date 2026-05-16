"""codeconv-planagents tool — orchestrated per-tombstone conversion-plan
generation (deterministic state engine half).

Per ``specs/017-conversion-plan-agents/contracts/planagents_cli.md``:

- Exports ``app: typer.Typer`` (required by feature 012 FR-006
  auto-discovery — no ``runner.py``/``cli.py`` edit needed).
- Exports ``register_workflows(dbos_app)`` (no-op stub; reserved for a
  future DBOS-managed workflow).

Subcommand surface (FR-001/FR-005/FR-013/FR-016):

- ``status`` (default — bare ``codeconv planagents`` invokes it).
- ``next [--limit 7]``
- ``plan-started <path>``
- ``plan-completed <path>``
- ``aggregate-escalations``
- ``stamp-tombstones``
- ``rebuild-plans-from-tombstones``

This Python CLI owns ALL deterministic state. It spawns NO LLM agents —
the ``/codeconv-planagents`` skill carries the ≤7 planning sub-agent +
separate research sub-agent orchestration loop (R1; the justified
deviation recorded in plan Complexity Tracking). Top-level flags
(``--repo-root``, ``--data-dir``, ``--quiet``, ``--json``) propagate
from the ``codeconv`` console script via ``typer.Context`` (feature
012 FR-019).
"""

from __future__ import annotations

import json as _json
from pathlib import Path
from typing import Optional

import typer

from .workflow import (
    register,
    run_aggregate_escalations,
    run_next,
    run_plan_completed,
    run_plan_started,
    run_rebuild_plans_from_tombstones,
    run_stamp_tombstones,
    run_status,
)


app = typer.Typer(
    add_completion=False,
    help=(
        "Orchestrate per-tombstone Dart→C#/.NET conversion-plan "
        "generation (plan-readiness oracle + dart_plans lifecycle)."
    ),
    invoke_without_command=True,
    no_args_is_help=False,
)


def _ctx_repo_root(ctx: typer.Context) -> Path:
    return Path(ctx.obj["repo_root"]) if ctx.obj else Path.cwd()


def _ctx_data_dir(ctx: typer.Context) -> Optional[Path]:
    return ctx.obj.get("data_dir") if ctx.obj else None


def _ctx_flags(
    ctx: typer.Context, quiet: bool, json_out: bool
) -> tuple[bool, bool]:
    if ctx.obj:
        quiet = quiet or bool(ctx.obj.get("quiet"))
        json_out = json_out or bool(ctx.obj.get("json"))
    return quiet, json_out


@app.callback(invoke_without_command=True)
def _default(ctx: typer.Context) -> None:
    """Bare ``codeconv planagents`` ⇒ ``status`` (FR-018; spawns nothing)."""
    if ctx.invoked_subcommand is None:
        ctx.invoke(status)


@app.command("status")
def status(
    ctx: typer.Context,
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Readiness view; no agents, no writes (FR-018)."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_status(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        quiet=quiet,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet,
                  header="status")


@app.command("next")
def next_cmd(
    ctx: typer.Context,
    limit: int = typer.Option(
        7, "--limit", help="Soft cap on tombstones (SCC units never split)."
    ),
    replan: Optional[str] = typer.Option(
        None,
        "--replan",
        help="Force re-selection of stale/named files (FR-015).",
    ),
    json_out_path: Optional[Path] = typer.Option(
        None, "--json-out", help="Override the JSON destination."
    ),
    dry_run: bool = typer.Option(
        False, "--dry-run", help="Compute only; write nothing."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Emit the next plan-ready batch (read-only — writes nothing)."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_next(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        limit=limit,
        replan=replan,
        json_out_path=json_out_path,
        dry_run=dry_run,
        quiet=quiet,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet,
                  header="next")


@app.command("plan-started")
def plan_started(
    ctx: typer.Context,
    path: str = typer.Argument(
        ..., help="Subtree-relative POSIX path to a .dart file."
    ),
    sha256: Optional[str] = typer.Option(
        None,
        "--sha256",
        help="Override recorded sha256-at-plan-start (default dart_files).",
    ),
    replan: bool = typer.Option(
        False,
        "--replan",
        help="UPDATE an existing (possibly completed) row in place (R9).",
    ),
    no_tombstone_update: bool = typer.Option(
        False,
        "--no-tombstone-update",
        help="Skip the tombstone YAML update (testing only).",
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Record that a planning sub-agent was dispatched for ``<path>``."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_plan_started(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        path=path,
        sha256=sha256,
        replan=replan,
        no_tombstone_update=no_tombstone_update,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet,
                  header="plan-started")


@app.command("plan-completed")
def plan_completed(
    ctx: typer.Context,
    path: str = typer.Argument(
        ..., help="Subtree-relative POSIX path to a .dart file."
    ),
    plan_path: Optional[str] = typer.Option(
        None, "--plan-path", help="Relative path of the produced artefact."
    ),
    escalations: int = typer.Option(
        0,
        "--escalations",
        help="Open escalation count (> 0 ⇒ conversion-blocked, FR-017).",
    ),
    no_tombstone_update: bool = typer.Option(
        False,
        "--no-tombstone-update",
        help="Skip the tombstone YAML update (testing only).",
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Record that the conversion plan for ``<path>`` is complete."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_plan_completed(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        path=path,
        plan_path=plan_path,
        escalations=escalations,
        no_tombstone_update=no_tombstone_update,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet,
                  header="plan-completed")


@app.command("aggregate-escalations")
def aggregate_escalations(
    ctx: typer.Context,
    report_out: Optional[Path] = typer.Option(
        None,
        "--report-out",
        help="Override the report path "
        "(default .codeconv/conversion-plans/_escalations-report.md).",
    ),
    dry_run: bool = typer.Option(
        False, "--dry-run", help="Compute only; write nothing."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Aggregate every open escalation into one engineer report (FR-016)."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_aggregate_escalations(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        report_out=report_out,
        dry_run=dry_run,
        quiet=quiet,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet,
                  header="aggregate-escalations")


@app.command("stamp-tombstones")
def stamp_tombstones(
    ctx: typer.Context,
    dry_run: bool = typer.Option(
        False, "--dry-run", help="Compute would-be writes; write nothing."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Embed the four plan-state keys into every tombstone (FR-013)."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_stamp_tombstones(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        dry_run=dry_run,
        quiet=quiet,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet,
                  header="stamp-tombstones")


@app.command("rebuild-plans-from-tombstones")
def rebuild_plans_from_tombstones(
    ctx: typer.Context,
    dry_run: bool = typer.Option(
        False, "--dry-run", help="Compute would-be DB writes; write nothing."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Rebuild ``codeconv.dart_plans`` from tombstone YAML (DB-wipe)."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_rebuild_plans_from_tombstones(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        dry_run=dry_run,
        quiet=quiet,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet,
                  header="rebuild-plans-from-tombstones")


def _emit_summary(
    summary: dict,
    *,
    json_summary: bool,
    quiet: bool,
    header: str,
) -> None:
    if json_summary:
        typer.echo(_json.dumps(summary, indent=2, sort_keys=True, default=str))
    elif not quiet:
        typer.echo(f"codeconv-planagents [{header}]")
        for key, value in summary.items():
            if isinstance(value, (list, dict)):
                continue
            typer.echo(f"  {key:>32}  {value}")
    # Contract (planagents_cli.md § status step 2 / feature-015 carry-
    # forward): the PROCESS exit code MUST reflect summary["exit_code"]
    # in ALL modes — including --json. The in-body "exit_code" field does
    # NOT substitute for the process exit status.
    code = int(summary.get("exit_code", 0) or 0)
    if code != 0:
        raise typer.Exit(code)


def register_workflows(dbos_app) -> None:
    """Register planagents workflows with DBOS at runner startup.

    No-op today (planagents' writes are short-lived single
    transactions, not durable workflows). Kept on the public surface so
    the feature-012 tool contract is satisfied (mirrors feature-015).
    """
    register(dbos_app)


__all__ = ["app", "register_workflows"]
