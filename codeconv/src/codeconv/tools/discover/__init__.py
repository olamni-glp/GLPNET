"""codeconv discover tool — Phase 6 / US4 / T073.

Per ``specs/012-codeconv-runner/contracts/codeconv_tool_contract.md``:

- Exports ``app: typer.Typer`` (required).
- Exports ``register_workflows(dbos_app)`` (used by the runner after
  ``dbos.launch()`` to register the durable workflow).

Adding flags to the ``run`` command is the only place the discover
tool's CLI surface lives — the slash skill (``.claude/skills/codeconv-
discover/SKILL.md``) forwards args verbatim.
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

import typer

from .workflow import register, run_discover


app = typer.Typer(
    add_completion=False,
    help="Walk glp_runtime_net/ and inventory .dart files into the codeconv schema + tombstones.",
    no_args_is_help=False,
)


@app.command("run")
def run(
    ctx: typer.Context,
    from_tombstones: bool = typer.Option(
        False,
        "--from-tombstones",
        help="Reconstruct inventory from .codeconv/tombstones/ only (no .dart parse). FR-022.",
    ),
    verify_tombstones: bool = typer.Option(
        False,
        "--verify-tombstones",
        help="Read-only source-truth audit (reads .dart; no bridge, no DB/tombstone writes). "
        "Mutually exclusive with --from-tombstones.",
    ),
    root: Optional[Path] = typer.Option(
        None,
        "--root",
        help="Override the discover subtree (default: <repo>/glp_runtime_net).",
    ),
    quiet: bool = typer.Option(
        False, "--quiet", help="Suppress per-file logging."
    ),
    json_out: bool = typer.Option(
        False, "--json", help="Emit JSON summary instead of human-readable."
    ),
    dry_run: bool = typer.Option(
        False, "--dry-run", help="Walk + parse but do not write DB or tombstones."
    ),
    no_orphan_revival: bool = typer.Option(
        False,
        "--no-orphan-revival",
        help="Skip the FR-025 orphan-revival step (testing only).",
    ),
) -> None:
    """Build / refresh the Dart inventory for ``glp_runtime_net/``."""
    repo_root = Path(ctx.obj["repo_root"]) if ctx.obj else Path.cwd()
    data_dir_override = ctx.obj.get("data_dir") if ctx.obj else None
    # CLI top-level may have set ``--quiet`` / ``--json`` globally; honour either.
    if ctx.obj:
        quiet = quiet or bool(ctx.obj.get("quiet"))
        json_out = json_out or bool(ctx.obj.get("json"))
    if from_tombstones and verify_tombstones:
        raise typer.BadParameter(
            "--from-tombstones and --verify-tombstones are mutually exclusive"
        )
    if verify_tombstones:
        mode = "verify_tombstones"
    elif from_tombstones:
        mode = "from_tombstones"
    else:
        mode = "normal"
    summary = run_discover(
        repo_root=repo_root,
        mode=mode,
        root=root,
        dry_run=dry_run,
        no_orphan_revival=no_orphan_revival,
        quiet=quiet,
        data_dir=data_dir_override,
    )
    _emit_summary(summary, json_out=json_out, quiet=quiet)


def _emit_summary(summary: dict, *, json_out: bool, quiet: bool) -> None:
    if json_out:
        import json as _json

        typer.echo(_json.dumps(summary, indent=2, default=str))
    elif quiet:
        pass
    elif summary.get("mode") == "verify_tombstones":
        typer.echo(
            f"codeconv-discover --verify-tombstones: "
            f"{summary.get('root', '?')} (root)"
        )
        typer.echo(f"  verified clean:          {summary.get('verified_clean', 0):>4}")
        typer.echo(f"  stale:                   {summary.get('stale', 0):>4}")
        typer.echo(f"  missing source:          {summary.get('missing_source', 0):>4}")
        typer.echo(
            f"  missing tombstone:       {summary.get('missing_tombstone', 0):>4}"
        )
        if summary.get("warnings"):
            typer.echo("  warnings:")
            for w in summary["warnings"]:
                typer.echo(f"    - {_format_warning(w)}")
    else:
        typer.echo(f"codeconv-discover: {summary.get('root', '?')} (root)")
        typer.echo(f"  walked:                  {summary['files_walked']:>4} files")
        typer.echo(
            f"  processed:               {summary['files_processed']:>4} files"
        )
        typer.echo(
            f"  skipped (idempotent):    {summary['files_skipped_idempotent']:>4} files"
        )
        typer.echo(f"  imports recorded:        {summary['imports']:>4} edges")
        typer.echo(
            f"  callers recorded:        {summary['callers']:>4} edges (mirror)"
        )
        typer.echo(
            f"  warnings:                {len(summary.get('warnings') or []):>4}"
        )
        typer.echo(f"  orphaned this run:       {summary.get('orphaned', 0):>4} files")
        typer.echo(f"  revived this run:        {summary.get('revived', 0):>4} files")
        if summary.get("warnings"):
            typer.echo("  warnings:")
            for w in summary["warnings"]:
                typer.echo(f"    - {_format_warning(w)}")

    # Amendment v2 / contract § Exit codes: the PROCESS exit code MUST
    # reflect summary["exit_code"] in ALL modes — including --json. The
    # JSON summary is still emitted on stdout above; an abort message (if
    # any) goes to stderr. A prior bug returned process-exit 0 here even
    # when the run aborted (contract violation).
    err = summary.get("error")
    if err:
        typer.echo(str(err), err=True)
    code = int(summary.get("exit_code", 0) or 0)
    if code != 0:
        raise typer.Exit(code)


def _format_warning(w: dict) -> str:
    kind = w.get("kind", "unknown")
    if kind == "duplicate_import":
        return f"duplicate import {w.get('import')!r} in {w.get('file')} (deduped)"
    if kind == "outside_caller":
        return (
            f"outside-subtree caller: {w.get('outside_file')} imports "
            f"{w.get('inside_file')} (NOT recorded as caller edge)"
        )
    if kind == "missing_tombstone":
        ref = w.get("referrer")
        if ref:
            return f"missing tombstone: {w.get('path')} referenced by {ref}"
        return f"missing tombstone: {w.get('path')}"
    if kind == "missing_source":
        return f"missing source: {w.get('path')}"
    if kind == "stale_tombstone":
        return (
            f"stale tombstone: {w.get('path')} differs from source "
            f"({', '.join(w.get('fields') or [])})"
        )
    if kind == "type_invalid_015_field":
        return (
            f"type-invalid feature-015 field {w.get('field')!r} in "
            f"{w.get('path')} (treated as null)"
        )
    if kind in ("malformed_field", "malformed_orphan_tombstone"):
        return f"{kind}: {w.get('path')} {w.get('field') or w.get('error') or ''}".rstrip()
    return str(w)


def register_workflows(dbos_app) -> None:
    """Register the discover workflow with DBOS at runner startup."""
    register(dbos_app)


__all__ = ["app", "register_workflows"]
