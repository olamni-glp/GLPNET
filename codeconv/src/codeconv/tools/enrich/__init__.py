"""codeconv-enrich tool — fill BLANK tombstone purpose/key_idea (feature 035).

Per ``specs/035-semantic-tombstone-enrichment/contracts/enrich_cli.md``:

- Exports ``app: typer.Typer`` (auto-discovered by ``tool_registry()``
  via ``pkgutil.iter_modules`` — NO edit to ``runner.py``/``cli.py``;
  FR-016 zero-edit registry).
- Exports a no-op ``register_workflows(dbos_app)`` so the feature-012 tool
  contract is satisfied; enrich's writes are short per-file transactions,
  not durable DBOS workflows.

**LM inference runs IN CLAUDE — never an external API.** A bare ``codeconv
enrich run`` (no injected Claude-backed ``infer_fn``) exits 2 with the
"drive me through the /codeconv-enrich skill" message — NEVER an external-API
fallback (Constitution V / FR-003 / SC-004). The seam lives in
``seam.py``; the candidate scan + infer + write logic in ``workflow.py``
(imported lazily inside the command body so a bare ``codeconv`` invocation
pays no import cost).
"""

from __future__ import annotations

import json as _json
from pathlib import Path
from typing import Optional

import typer


app = typer.Typer(
    add_completion=False,
    help="Enrich blank tombstone purpose/key_idea via the Claude seam.",
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
    path: list[str] = typer.Option(
        [],
        "--path",
        help="Scope to candidates under this path/prefix (repeatable). "
        "Default: all blank candidates excl. .orphaned/ (FR-012/013).",
    ),
    dry_run: bool = typer.Option(
        False, "--dry-run", help="Compute candidates/inferences; write nothing."
    ),
    quiet: bool = typer.Option(
        False, "--quiet", help="Suppress progress logging."
    ),
    json_out: bool = typer.Option(
        False, "--json", help="Emit the run-summary JSON to stdout."
    ),
) -> None:
    """Fill blank tombstone ``purpose``/``key_idea`` via the Claude seam.

    The inference runs IN CLAUDE — a bare CLI invocation with no injected
    Claude-backed ``infer_fn`` exits 2 with an actionable message pointing
    back to the ``/codeconv-enrich`` skill loop (NEVER an external-API
    fallback). The skill drives enrichment by calling ``run_enrich`` with a
    Claude-backed ``infer_fn`` (one sub-agent per file)."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    from .workflow import run_enrich  # lazy

    try:
        summary = run_enrich(
            repo_root,
            infer_fn=None,  # bare CLI: no Claude seam → _require_fn raises → exit 2
            paths=list(path) or None,
            dry_run=dry_run,
            quiet=quiet,
            data_dir=data_dir,
        )
    except RuntimeError as exc:  # no injected Claude backend (seam.py:_require_fn)
        _emit(
            {"ok": False, "tool": "enrich", "exit_code": 2, "error": str(exc)},
            json_out=json_out,
            quiet=quiet,
        )
        return
    _emit(summary, json_out=json_out, quiet=quiet)


def _emit(summary: dict, *, json_out: bool, quiet: bool) -> None:
    if json_out:
        typer.echo(_json.dumps(summary, indent=2, sort_keys=True, default=str))
    elif not quiet:
        typer.echo("codeconv-enrich [run]")
        for key, value in summary.items():
            if isinstance(value, (list, dict)):
                continue
            typer.echo(f"  {key:>24}  {value}")
    # The PROCESS exit code MUST reflect summary["exit_code"] in ALL modes
    # (including --json), mirroring discover/depgraph. A run with per-file
    # failures still exits 0 (failures are in the report, FR-010); only a
    # missing seam (2) or a pre-flight failure (non-zero) is a process error.
    err = summary.get("error")
    if err and not json_out:
        typer.echo(str(err), err=True)
    code = int(summary.get("exit_code", 0) or 0)
    if code != 0:
        raise typer.Exit(code)


def register_workflows(dbos_app) -> None:
    """No-op: enrich performs short per-file transactions, not durable
    DBOS workflows. Kept on the public surface so the feature-012 tool
    contract (``codeconv_tool_contract.md``) is satisfied and the runner's
    auto-discovery (FR-016) finds the tool with zero registry edits."""
    return None


__all__ = ["app", "register_workflows"]
