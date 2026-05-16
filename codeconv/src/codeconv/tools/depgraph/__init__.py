"""codeconv-depgraph tool — topological ordering + conversion-readiness oracle.

Per ``specs/015-codeconv-depgraph/contracts/depgraph_cli.md``:

- Exports ``app: typer.Typer`` (required by feature 012 FR-006 auto-discovery).
- Exports ``register_workflows(dbos_app)`` (currently a no-op; reserved for
  future DBOS-managed workflow registration).

Subcommand surface:

- ``compute`` (default — no-args ``codeconv depgraph`` invokes it).
- ``mark-started <path>``
- ``mark-completed <path>``
- ``stamp-tombstones``
- ``rebuild-conversions-from-tombstones``

Flags propagate from the top-level ``codeconv`` console script via the
Typer context (``--repo-root``, ``--data-dir``, ``--quiet``, ``--json``).
"""

from __future__ import annotations

import json as _json
from pathlib import Path
from typing import Optional

import typer

from .workflow import (
    register,
    run_compute,
    run_mark_completed,
    run_mark_started,
    run_rebuild_conversions_from_tombstones,
    run_stamp_tombstones,
)


app = typer.Typer(
    add_completion=False,
    help="Compute the Dart dependency graph + conversion-readiness oracle.",
    invoke_without_command=True,
    no_args_is_help=False,
)


def _ctx_repo_root(ctx: typer.Context) -> Path:
    return Path(ctx.obj["repo_root"]) if ctx.obj else Path.cwd()


def _ctx_data_dir(ctx: typer.Context) -> Optional[Path]:
    return ctx.obj.get("data_dir") if ctx.obj else None


def _ctx_flags(ctx: typer.Context, quiet: bool, json_out: bool) -> tuple[bool, bool]:
    if ctx.obj:
        quiet = quiet or bool(ctx.obj.get("quiet"))
        json_out = json_out or bool(ctx.obj.get("json"))
    return quiet, json_out


@app.callback(invoke_without_command=True)
def _default(ctx: typer.Context) -> None:
    """When invoked with no subcommand, fall through to ``compute``.

    Typer dispatches to the named subcommand if one is given; this callback
    only runs (and only invokes ``compute`` itself) when none was provided.
    """
    if ctx.invoked_subcommand is None:
        ctx.invoke(compute)


@app.command("compute")
def compute(
    ctx: typer.Context,
    json_out_path: Optional[Path] = typer.Option(
        None,
        "--json-out",
        help="Override JSON artefact path (default: <repo>/.codeconv/depgraph.json).",
    ),
    dry_run: bool = typer.Option(
        False, "--dry-run", help="Compute without writing DB or JSON artefact."
    ),
    quiet: bool = typer.Option(
        False, "--quiet", help="Suppress per-file logging."
    ),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Compute the depgraph and write ``.codeconv/depgraph.json`` + DB rows."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_compute(
        repo_root=repo_root,
        data_dir=data_dir,
        json_out=json_out_path,
        dry_run=dry_run,
        quiet=quiet,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet, header="compute")


@app.command("mark-started")
def mark_started(
    ctx: typer.Context,
    path: str = typer.Argument(..., help="Subtree-relative POSIX path to a .dart file."),
    sha256: Optional[str] = typer.Option(
        None,
        "--sha256",
        help="Override the recorded sha256-at-start (default: codeconv.dart_files.sha256).",
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
    """Record that conversion of ``<path>`` has started."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_mark_started(
        repo_root=repo_root,
        data_dir=data_dir,
        path=path,
        sha256=sha256,
        no_tombstone_update=no_tombstone_update,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet, header="mark-started")


@app.command("mark-completed")
def mark_completed(
    ctx: typer.Context,
    path: str = typer.Argument(..., help="Subtree-relative POSIX path to a .dart file."),
    target: Optional[str] = typer.Option(
        None, "--target", help="Path of the produced C# / .NET artefact."
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
    """Record that conversion of ``<path>`` has completed."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_mark_completed(
        repo_root=repo_root,
        data_dir=data_dir,
        path=path,
        target=target,
        no_tombstone_update=no_tombstone_update,
    )
    _emit_summary(summary, json_summary=json_summary, quiet=quiet, header="mark-completed")


@app.command("stamp-tombstones")
def stamp_tombstones(
    ctx: typer.Context,
    dry_run: bool = typer.Option(
        False, "--dry-run", help="Compute would-be tombstone updates; write nothing."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress per-file logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Embed depgraph + conversion state into every tombstone's YAML frontmatter."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_stamp_tombstones(
        repo_root=repo_root,
        data_dir=data_dir,
        dry_run=dry_run,
        quiet=quiet,
    )
    _emit_summary(
        summary,
        json_summary=json_summary,
        quiet=quiet,
        header="stamp-tombstones",
    )


@app.command("rebuild-conversions-from-tombstones")
def rebuild_conversions_from_tombstones(
    ctx: typer.Context,
    dry_run: bool = typer.Option(
        False, "--dry-run", help="Compute would-be DB writes; write nothing."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress per-file logging."),
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Rebuild ``codeconv.dart_conversions`` from tombstone YAML frontmatter."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_summary = _ctx_flags(ctx, quiet, json_summary)
    summary = run_rebuild_conversions_from_tombstones(
        repo_root=repo_root,
        data_dir=data_dir,
        dry_run=dry_run,
        quiet=quiet,
    )
    _emit_summary(
        summary,
        json_summary=json_summary,
        quiet=quiet,
        header="rebuild-conversions-from-tombstones",
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
        return
    if quiet:
        return
    typer.echo(f"codeconv-depgraph [{header}]")
    for key, value in summary.items():
        if isinstance(value, (list, dict)):
            continue
        typer.echo(f"  {key:>32}  {value}")


def register_workflows(dbos_app) -> None:
    """Register depgraph workflows with DBOS at runner startup.

    Currently a no-op; depgraph's writes are short-lived single transactions,
    not durable workflows. Kept on the public surface so the feature-012
    tool contract (``codeconv_tool_contract.md``) is satisfied and a future
    polish step can wire up DBOS without an API change.
    """
    register(dbos_app)


__all__ = ["app", "register_workflows"]
