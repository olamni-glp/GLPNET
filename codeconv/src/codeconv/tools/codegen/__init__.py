"""codeconv-codegen tool — deterministic Dart→C#/.NET code-generation
state engine (the production half).

Per ``specs/019-codeconv-codegen/contracts/codegen_cli.md``:

- Exports ``app: typer.Typer`` (feature 012 FR-006 auto-discovery — no
  ``runner.py``/``cli.py`` edit needed).
- Exports ``register_workflows(dbos_app)`` (delegates to the centralised
  durable activation; the ``codegen`` DBOS stage is added in T038).

Subcommand surface (codegen_cli.md):

- ``status`` (default — bare ``codeconv codegen`` invokes it).
- ``next [--limit 7]``
- ``ingest <path> [--respec]``
- ``retry <path>``
- ``aggregate-escalations``
- ``record-review`` / ``promote-batch`` (US3 — added in T032).

This Python CLI owns ALL deterministic codegen state and spawns NO LLM
agents (the ``/codeconv-codegen`` skill carries the codegen sub-agent +
human-review orchestration — the justified deviation). Top-level flags
(``--repo-root``, ``--data-dir``, ``--quiet``, ``--json``) propagate from
the ``codeconv`` console script via ``typer.Context``.
"""

from __future__ import annotations

import json as _json
from pathlib import Path
from typing import Optional

import typer

from .review import run_promote_batch, run_record_review
from .workflow import (
    register,
    run_aggregate_escalations,
    run_codegen_ingest,
    run_next,
    run_retry,
    run_status,
)


app = typer.Typer(
    add_completion=False,
    help=(
        "Drive deterministic Dart→C#/.NET code generation "
        "(codegen-readiness oracle + dart_codegen lifecycle + build gate)."
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
    """Bare ``codeconv codegen`` ⇒ ``status`` (spawns nothing)."""
    if ctx.invoked_subcommand is None:
        ctx.invoke(status)


@app.command("status")
def status(
    ctx: typer.Context,
    include_tests: bool = typer.Option(
        False, "--include-tests", help="Include test-tree files (Increment 2)."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Readiness + lifecycle view; no agents, no writes."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_status(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        include_tests=include_tests,
        quiet=quiet,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet, header="status")


@app.command("next")
def next_cmd(
    ctx: typer.Context,
    limit: int = typer.Option(
        7, "--limit", help="Soft cap on files (SCC units never split)."
    ),
    include_tests: bool = typer.Option(
        False, "--include-tests", help="Include test-tree files (Increment 2)."
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
    """Emit the next codegen-ready batch (read-only — writes nothing)."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_next(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        limit=limit,
        include_tests=include_tests,
        json_out_path=json_out_path,
        dry_run=dry_run,
        quiet=quiet,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet, header="next")


@app.command("ingest")
def ingest(
    ctx: typer.Context,
    path: str = typer.Argument(
        ..., help="Subtree-relative POSIX path to a .dart file."
    ),
    respec: bool = typer.Option(
        False,
        "--respec",
        help="Re-open a completed row on source-sha drift (FR-008/FR-019).",
    ),
    increment: int = typer.Option(
        1, "--increment", help="1 = dotnet build gate; 2 = dotnet test gate."
    ),
    batch_id: Optional[str] = typer.Option(
        None, "--batch-id", help="Tag this file into a promotion batch."
    ),
    no_tombstone_update: bool = typer.Option(
        False, "--no-tombstone-update", help="Skip tombstone YAML (testing)."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Validate + build-gate the produced .cs; two-phase dart_codegen write.

    Returns (in the summary ``outcome``) ``built｜needs_agent_work｜
    escalated``. Escalations are reported (not a nonzero exit) but mark
    the file conversion-blocked.
    """
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_codegen_ingest(
        _ctx_repo_root(ctx),
        rel_path=path,
        data_dir=_ctx_data_dir(ctx),
        respec=respec,
        increment=increment,
        batch_id=batch_id,
        no_tombstone_update=no_tombstone_update,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet, header="ingest")


@app.command("retry")
def retry(
    ctx: typer.Context,
    path: str = typer.Argument(
        ..., help="Subtree-relative POSIX path to a .dart file."
    ),
    no_tombstone_update: bool = typer.Option(
        False, "--no-tombstone-update", help="Skip tombstone YAML (testing)."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Re-open a file (clear build/escalation state) for re-generation."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_retry(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        path=path,
        no_tombstone_update=no_tombstone_update,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet, header="retry")


@app.command("record-review")
def record_review(
    ctx: typer.Context,
    batch_id: str = typer.Argument(..., help="Promotion-batch id."),
    file: str = typer.Option(..., "--file", help="Reviewed file's rel path."),
    score: int = typer.Option(..., "--score", help="Human review score 1–5."),
    note: Optional[str] = typer.Option(
        None, "--note", help="Free-text review note (feeds the offline optimizer)."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(False, "--json", help="Emit JSON summary."),
) -> None:
    """Record one sampled human review (US3 / FR-006)."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_record_review(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        batch_id=batch_id,
        file_path=file,
        score=score,
        note=note,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet,
                  header="record-review")


@app.command("promote-batch")
def promote_batch(
    ctx: typer.Context,
    batch_id: str = typer.Argument(..., help="Promotion-batch id."),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(False, "--json", help="Emit JSON summary."),
) -> None:
    """Apply the promotion gate (100% build + human median ≥4/5); set
    ``promoted`` on pass, else report blockers (US3 / FR-006)."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_promote_batch(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        batch_id=batch_id,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet,
                  header="promote-batch")


@app.command("aggregate-escalations")
def aggregate_escalations(
    ctx: typer.Context,
    report_out: Optional[Path] = typer.Option(
        None,
        "--report-out",
        help="Override the report path "
        "(default .codeconv/conversion-code/_escalations-report.md).",
    ),
    dry_run: bool = typer.Option(
        False, "--dry-run", help="Compute only; write nothing."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Aggregate every open codegen escalation into one engineer report."""
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_aggregate_escalations(
        repo_root=_ctx_repo_root(ctx),
        data_dir=_ctx_data_dir(ctx),
        report_out=report_out,
        dry_run=dry_run,
        quiet=quiet,
    )
    _emit_summary(
        summary, json_summary=json_summary, quiet=quiet,
        header="aggregate-escalations",
    )


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
        typer.echo(f"codeconv-codegen [{header}]")
        for key, value in summary.items():
            if isinstance(value, (list, dict)):
                continue
            typer.echo(f"  {key:>28}  {value}")
    # The PROCESS exit code MUST reflect summary["exit_code"] in ALL modes
    # (incl. --json) — the in-body field does NOT substitute for it.
    code = int(summary.get("exit_code", 0) or 0)
    if code != 0:
        raise typer.Exit(code)


def register_workflows(dbos_app) -> None:
    """Register codegen workflows with DBOS at runner startup.

    Delegates to the centralised durable activation (idempotent). The
    ``codegen`` DBOS stage itself is registered in
    :mod:`codeconv.durable.steps` (T038); kept on the public surface so
    the feature-012 tool contract is satisfied.
    """
    register(dbos_app)


__all__ = ["app", "register_workflows"]
