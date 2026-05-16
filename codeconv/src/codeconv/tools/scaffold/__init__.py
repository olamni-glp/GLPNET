"""codeconv scaffold tool — Feature 016 / US2.

Per ``specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_scaffold_cli.md``
and ``specs/012-codeconv-runner/contracts/codeconv_tool_contract.md``:

- Exports ``app: typer.Typer`` (required by feature-012 auto-discovery).
- Exports ``register_workflows(dbos_app)`` (no-op; kept on the surface).

De-brand of ``D2NET-scaffold``. Command tree:

- ``codeconv scaffold [run]`` — produce the target tree.

The slash skill (``.claude/skills/codeconv-scaffold/SKILL.md``) forwards
args verbatim plus the destructive gate; the CLI is authoritative.
"""

from __future__ import annotations

import json as _json
from pathlib import Path
from typing import Optional

import typer

from .workflow import register, run_scaffold


app = typer.Typer(
    add_completion=False,
    help="Mirror the in-scope source tree into the target with the "
    "selected pair's extension + workdir convention; record target_path.",
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
    """When invoked with no subcommand, fall through to ``run``."""
    if ctx.invoked_subcommand is None:
        ctx.invoke(run)


@app.command("run")
def run(
    ctx: typer.Context,
    source_lang: Optional[str] = typer.Option(
        None,
        "--source-lang",
        help="Per-invocation source-lang override (must match the "
        "workspace pair; a mismatch is refused — SC-008).",
    ),
    target_lang: Optional[str] = typer.Option(
        None,
        "--target-lang",
        help="Per-invocation target-lang override (must match the "
        "workspace pair; a mismatch is refused — SC-008).",
    ),
    force_delete_target: bool = typer.Option(
        False,
        "--force-delete-target",
        help="Destructive: overwrite a non-empty target. Requires "
        "explicit confirmation (skill-driven).",
    ),
    no_tombstone_update: bool = typer.Option(
        False,
        "--no-tombstone-update",
        help="Skip writing target_path into tombstones (testing only).",
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress output."),
    json_out: bool = typer.Option(
        False, "--json", help="Emit a JSON summary."
    ),
) -> None:
    """Produce the target tree for the workspace's Dart→C# pair."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    summary = run_scaffold(
        repo_root=repo_root,
        source_lang=source_lang,
        target_lang=target_lang,
        force_delete_target=force_delete_target,
        no_tombstone_update=no_tombstone_update,
        data_dir=data_dir,
        quiet=quiet,
    )
    _emit(summary, json_out=json_out, quiet=quiet, header="scaffold")


def _emit(
    summary: dict, *, json_out: bool, quiet: bool, header: str
) -> None:
    if json_out:
        typer.echo(_json.dumps(summary, indent=2, sort_keys=True, default=str))
    elif not quiet:
        typer.echo(f"codeconv-scaffold [{header}]")
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
    """Register scaffold workflows with DBOS at runner startup (no-op)."""
    register(dbos_app)


__all__ = ["app", "register_workflows"]
