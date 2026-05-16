"""codeconv init tool — Feature 016 / US1 + US4.

Per ``specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_init_cli.md``
and ``specs/012-codeconv-runner/contracts/codeconv_tool_contract.md``:

- Exports ``app: typer.Typer`` (required by feature-012 auto-discovery).
- Exports ``register_workflows(dbos_app)`` (no-op; kept on the surface).

De-brand of ``D2NET-init``. Command tree (contract § Command tree):

- ``codeconv init [run]``                 — configure + delegate inventory
- ``codeconv init add-exclude <path>``    — add a manual exclusion
- ``codeconv init remove-exclude <path>`` — remove an exclusion
- ``codeconv init list``                  — list in-scope inventoried files
- ``codeconv init inspect``               — workspace introspection

The slash skill (``.claude/skills/codeconv-init/SKILL.md``) forwards args
verbatim; the CLI is authoritative.
"""

from __future__ import annotations

import json as _json
from pathlib import Path
from typing import List, Optional

import typer

from .workflow import (
    register,
    run_add_exclude,
    run_init,
    run_inspect,
    run_list,
    run_remove_exclude,
)


app = typer.Typer(
    add_completion=False,
    help="Configure a Dart→C# conversion workspace (pair, paths, "
    "exclusions, phase tracking) and delegate the inventory to discover.",
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


@app.command("run")
def run(
    ctx: typer.Context,
    source: str = typer.Option(
        "glp_runtime_net", "--source", help="Source subtree (repo-relative)."
    ),
    target: Optional[str] = typer.Option(
        None, "--target", help="Target tree root (repo-relative)."
    ),
    source_lang: str = typer.Option(
        "dart", "--source-lang", help="Source language id."
    ),
    target_lang: str = typer.Option(
        "csharp", "--target-lang", help="Target language id."
    ),
    exclude: Optional[List[str]] = typer.Option(
        None, "--exclude", help="Manual directory exclusion (repeatable)."
    ),
    accept_suggested_exclusions: bool = typer.Option(
        False,
        "--accept-suggested-exclusions",
        help="Accept the pair's tool-exclusion recommendations non-interactively.",
    ),
    non_interactive: bool = typer.Option(
        False,
        "--non-interactive",
        help="No prompts; requires --accept-suggested-exclusions or --exclude.",
    ),
    rebuild: bool = typer.Option(
        False,
        "--rebuild",
        help="Destructive re-init (discards existing workspace state). "
        "Requires --confirm-rebuild.",
    ),
    confirm_rebuild: bool = typer.Option(
        False,
        "--confirm-rebuild",
        help="Confirmation token for --rebuild (skill-driven).",
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress output."),
    json_out: bool = typer.Option(
        False, "--json", help="Emit a JSON summary."
    ),
) -> None:
    """Configure the workspace and delegate the inventory to discover."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    summary = run_init(
        repo_root=repo_root,
        source=source,
        target=target,
        source_lang=source_lang,
        target_lang=target_lang,
        exclude=list(exclude or []),
        accept_suggested_exclusions=accept_suggested_exclusions,
        non_interactive=non_interactive,
        rebuild=rebuild,
        confirm_rebuild=confirm_rebuild,
        data_dir=data_dir,
        quiet=quiet,
    )
    _emit(summary, json_out=json_out, quiet=quiet, header="init")


@app.command("add-exclude")
def add_exclude(
    ctx: typer.Context,
    path: str = typer.Argument(..., help="Repo/source-relative directory."),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress output."),
    json_out: bool = typer.Option(False, "--json", help="Emit JSON."),
) -> None:
    """Add a manual exclusion and re-sync the inventory."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    summary = run_add_exclude(
        repo_root=repo_root, path=path, data_dir=data_dir, quiet=quiet
    )
    _emit(summary, json_out=json_out, quiet=quiet, header="add-exclude")


@app.command("remove-exclude")
def remove_exclude(
    ctx: typer.Context,
    path: str = typer.Argument(..., help="Repo/source-relative directory."),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress output."),
    json_out: bool = typer.Option(False, "--json", help="Emit JSON."),
) -> None:
    """Remove an exclusion and re-sync the inventory."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    summary = run_remove_exclude(
        repo_root=repo_root, path=path, data_dir=data_dir, quiet=quiet
    )
    _emit(summary, json_out=json_out, quiet=quiet, header="remove-exclude")


@app.command("list")
def list_files(
    ctx: typer.Context,
    quiet: bool = typer.Option(False, "--quiet", help="Suppress output."),
    json_out: bool = typer.Option(False, "--json", help="Emit JSON."),
) -> None:
    """List the in-scope inventoried files."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    summary = run_list(
        repo_root=repo_root, data_dir=data_dir, quiet=quiet
    )
    _emit(summary, json_out=json_out, quiet=quiet, header="list")


@app.command("inspect")
def inspect(
    ctx: typer.Context,
    exclusions: bool = typer.Option(
        False, "--exclusions", help="Show only the exclusion set."
    ),
    current_phase: bool = typer.Option(
        False, "--current-phase", help="Show only the phase state."
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress output."),
    json_out: bool = typer.Option(False, "--json", help="Emit JSON."),
) -> None:
    """Workspace introspection (settings / exclusions / phases)."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    summary = run_inspect(
        repo_root=repo_root,
        exclusions=exclusions,
        current_phase=current_phase,
        data_dir=data_dir,
        quiet=quiet,
    )
    _emit(summary, json_out=json_out, quiet=quiet, header="inspect")


def _emit(
    summary: dict, *, json_out: bool, quiet: bool, header: str
) -> None:
    if json_out:
        typer.echo(_json.dumps(summary, indent=2, sort_keys=True, default=str))
    elif not quiet:
        typer.echo(f"codeconv-init [{header}]")
        for key, value in summary.items():
            if isinstance(value, (list, dict)):
                continue
            typer.echo(f"  {key:>22}  {value}")
    # Process exit code MUST reflect summary["exit_code"] in ALL modes
    # (feature-012/015 Amendment-v2 carry-forward). An error message (if
    # any) goes to stderr.
    err = summary.get("error")
    if err and not summary.get("ok"):
        typer.echo(str(err), err=True)
    code = int(summary.get("exit_code", 0) or 0)
    if code != 0:
        raise typer.Exit(code)


def register_workflows(dbos_app) -> None:
    """Register init workflows with DBOS at runner startup (no-op)."""
    register(dbos_app)


__all__ = ["app", "register_workflows"]
