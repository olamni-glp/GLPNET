"""``codeconv tutorials`` Typer sub-app — read-only GLP tutorial browser.

PURE / BRIDGE-FREE (research D1). Wired into the codeconv CLI via a direct
``app.add_typer(tutorials_app, name="tutorials")`` in ``codeconv/cli.py`` — NOT
through ``runner.tool_registry()`` — so it never acquires the PGLite bridge,
starts DBOS, or spawns the REPL. The ``/glptutorial-list`` skill forwards
verbatim to ``codeconv tutorials list`` (FR-009).

Contract: ``specs/022-glptutorial-list/contracts/tutorials_cli.md``.
Exit codes (listing-specific): 0 ok · 3 no-match · 4 ambiguous · 5 corpus
unreachable.
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

import typer

from . import render
from .corpus import CorpusUnreachableError, load_corpus, resolve_corpus_root
from .match import match_tutorial

_EXIT_OK = 0
_EXIT_NO_MATCH = 3
_EXIT_AMBIGUOUS = 4
_EXIT_CORPUS_UNREACHABLE = 5

tutorials_app = typer.Typer(
    add_completion=False,
    no_args_is_help=True,
    help="Read-only GLP tutorial browser (bridge-free). See /glptutorial-list.",
)


def _ctx_flag(ctx: typer.Context, key: str) -> bool:
    """Read a global flag (``--json``/``--quiet``) stashed by the parent callback."""
    obj = getattr(ctx, "obj", None)
    return bool(obj.get(key)) if isinstance(obj, dict) else False


def _repo_root(ctx: typer.Context) -> Path:
    obj = getattr(ctx, "obj", None)
    if isinstance(obj, dict) and obj.get("repo_root"):
        return Path(obj["repo_root"])
    return Path.cwd()


@tutorials_app.command("list")
def cmd_list(
    ctx: typer.Context,
    tutorial: Optional[str] = typer.Argument(
        None,
        help="Chapter identifier (id/prefix/title, e.g. ch03, 3, core). "
        "Omit to list the whole catalog.",
    ),
    corpus: Optional[Path] = typer.Option(
        None, "--corpus", help="Override the vendored corpus root (default tutorials/olamni)."
    ),
    json_out: bool = typer.Option(False, "--json", help="Emit the structured model."),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress non-error warnings."),
) -> None:
    """List GLP tutorials grouped by chapter → exercise → script (read-only)."""
    repo_root = _repo_root(ctx)
    json_mode = json_out or _ctx_flag(ctx, "json")
    quiet_mode = quiet or _ctx_flag(ctx, "quiet")

    try:
        root = resolve_corpus_root(repo_root, corpus)
        loaded = load_corpus(root, repo_root)
    except CorpusUnreachableError as exc:
        typer.echo(f"error: {exc}", err=True)
        raise typer.Exit(_EXIT_CORPUS_UNREACHABLE)

    chapters = loaded.chapters
    if tutorial:
        result = match_tutorial(tutorial, loaded.chapters)
        if result.kind == "none":
            available = ", ".join(result.candidates) or "(none)"
            typer.echo(
                f"error: no tutorial matches '{tutorial}'. available: {available}",
                err=True,
            )
            raise typer.Exit(_EXIT_NO_MATCH)
        if result.kind == "ambiguous":
            typer.echo(
                f"error: '{tutorial}' is ambiguous; matches: "
                f"{', '.join(result.candidates)} — disambiguate by id.",
                err=True,
            )
            raise typer.Exit(_EXIT_AMBIGUOUS)
        chapters = [result.matched]  # type: ignore[list-item]

    if json_mode:
        # JSON is self-contained (warnings live in the payload) — keep stdout
        # machine-clean and do not also echo warnings to stderr.
        typer.echo(render.render_json(loaded, chapters))
        raise typer.Exit(_EXIT_OK)

    if not quiet_mode:
        for warning in loaded.warnings:
            typer.echo(f"warning: {warning}", err=True)
    text = render.render_human(chapters)
    if text:
        typer.echo(text)
    raise typer.Exit(_EXIT_OK)


@tutorials_app.command("sync")
def cmd_sync(
    ctx: typer.Context,
    check: bool = typer.Option(
        False, "--check", help="Verify the vendored tree vs the manifest; non-zero on drift."
    ),
    source: Optional[Path] = typer.Option(
        None, "--source", help="Sibling tutorial corpus to vendor from (build-time only)."
    ),
    corpus: Optional[Path] = typer.Option(
        None, "--corpus", help="Vendored destination root (default tutorials/olamni)."
    ),
) -> None:
    """Re-vendor the sibling corpus / verify the snapshot (build-time, D3)."""
    from . import sync as _sync  # local import: not on the list path

    repo_root = _repo_root(ctx)
    quiet_mode = _ctx_flag(ctx, "quiet")

    if check:
        result = _sync.check(repo_root, source=source, dest=corpus)
        if result.ok:
            if not quiet_mode:
                typer.echo(f"sync --check: OK ({result.dest})")
            raise typer.Exit(_EXIT_OK)
        for rel in result.missing:
            typer.echo(f"drift: missing from vendored tree: {rel}", err=True)
        for rel in result.tampered:
            typer.echo(f"drift: vendored content differs from manifest: {rel}", err=True)
        for rel in result.sibling_drift:
            typer.echo(f"drift: vendored content differs from sibling: {rel}", err=True)
        raise typer.Exit(1)

    try:
        result = _sync.sync(repo_root, source=source, dest=corpus)
    except FileNotFoundError as exc:
        typer.echo(f"error: {exc}", err=True)
        raise typer.Exit(_EXIT_CORPUS_UNREACHABLE)
    if not quiet_mode:
        typer.echo(
            f"vendored {result.files} files → {result.dest} "
            f"(source ref: {result.source_ref or 'n/a'})"
        )
    raise typer.Exit(_EXIT_OK)


@tutorials_app.command("run")
def cmd_run(ctx: typer.Context) -> None:
    """Reserved for the companion /glptutorial-run feature (not implemented here)."""
    typer.echo(
        "error: 'tutorials run' is reserved for the companion /glptutorial-run "
        "feature; this build only lists (read-only, FR-010).",
        err=True,
    )
    raise typer.Exit(64)


__all__ = ["tutorials_app"]
