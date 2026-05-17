"""codeconv mirror tool — Feature 016 / spec Amendment 1 (US6).

Per ``specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_mirror_cli.md``
and ``specs/012-codeconv-runner/contracts/codeconv_tool_contract.md``:

- Exports ``app: typer.Typer`` (required by feature-012 auto-discovery).
- Exports ``register_workflows(dbos_app)`` (no-op; kept on the surface).

Generic re-expression of the removed ``D2NET-scaffold`` (NOT a revival):
reproduces spec ``001-d2net-scaffold`` via the workspace-bound language
pair's mirror hooks. Command tree:

- ``codeconv mirror [run]`` — produce the inventory subtree.

The slash skill (``.claude/skills/codeconv-mirror/SKILL.md``) forwards
args verbatim plus the ``--refresh`` destructive gate; the CLI is
authoritative.
"""

from __future__ import annotations

import json as _json
from pathlib import Path
from typing import Optional

import typer

from .workflow import register, run_mirror


app = typer.Typer(
    add_completion=False,
    help="Mirror the source-language tree into the inventory subtree "
    "(spec-001 behaviour via the workspace-bound language pair).",
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
    """When invoked with no subcommand, fall through to ``run``."""
    if ctx.invoked_subcommand is None:
        ctx.invoke(run)


@app.command("run")
def run(
    ctx: typer.Context,
    refresh: bool = typer.Option(
        False,
        "--refresh",
        help="Re-run against an existing output tree (spec-001 FR-011: "
        "rewrite .src/non-source; preserve every companion + tracker; "
        "stub newly-found source files). Destructive-adjacent; the "
        "/codeconv-mirror skill drives the confirmation.",
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress output."),
    json_out: bool = typer.Option(
        False, "--json", help="Emit a JSON summary."
    ),
) -> None:
    """Produce the inventory subtree for the workspace's pair."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    summary = run_mirror(
        repo_root=repo_root,
        refresh=refresh,
        data_dir=data_dir,
        quiet=quiet,
    )
    _emit(summary, json_out=json_out, quiet=quiet, header="mirror")


def _emit(
    summary: dict, *, json_out: bool, quiet: bool, header: str
) -> None:
    if json_out:
        typer.echo(
            _json.dumps(summary, indent=2, sort_keys=True, default=str)
        )
    elif not quiet:
        typer.echo(f"codeconv-mirror [{header}]")
        for key, value in summary.items():
            if isinstance(value, (list, dict)):
                continue
            typer.echo(f"  {key:>22}  {value}")
    # Process exit code MUST reflect summary["exit_code"] in ALL modes
    # (feature-012/015 Amendment-v2 carry-forward).
    err = summary.get("error")
    if err and not summary.get("ok"):
        typer.echo(str(err), err=True)
    code = int(summary.get("exit_code", 0) or 0)
    if code != 0:
        raise typer.Exit(code)


def register_workflows(dbos_app) -> None:
    """Register mirror workflows with DBOS at runner startup (no-op)."""
    register(dbos_app)


__all__ = ["app", "register_workflows"]
