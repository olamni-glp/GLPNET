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
from . import render_run
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


# --------------------------------------------------------------------------- #
# Run layer (feature 023) — preview / run / explain / propose                  #
# Exit codes extend the 022 set (D10): 6 no-load-target · 7 no-goal · 8 backend #
# P1 · 9 not-implemented · 10 REPL-limitation · 11 drift-refused.              #
# --------------------------------------------------------------------------- #
_EXIT_NO_TARGET = 6
_EXIT_NO_GOAL = 7
_EXIT_BACKEND_P1 = 8
_EXIT_NOT_IMPLEMENTED = 9
_EXIT_REPL_LIMITATION = 10
_EXIT_DRIFT = 11


def _select_exercise(ctx: typer.Context, tutorial_id: str, exercise: str, corpus_opt):
    """Shared selector: (loaded corpus, Tutorial, Exercise, repo_root). Raises typer.Exit."""
    repo_root = _repo_root(ctx)
    try:
        root = resolve_corpus_root(repo_root, corpus_opt)
        loaded = load_corpus(root, repo_root)
    except CorpusUnreachableError as exc:
        typer.echo(f"error: {exc}", err=True)
        raise typer.Exit(_EXIT_CORPUS_UNREACHABLE)
    result = match_tutorial(tutorial_id, loaded.chapters)
    if result.kind == "none":
        typer.echo(f"error: no tutorial matches '{tutorial_id}'. available: "
                   f"{', '.join(result.candidates) or '(none)'}", err=True)
        raise typer.Exit(_EXIT_NO_MATCH)
    if result.kind == "ambiguous":
        typer.echo(f"error: '{tutorial_id}' is ambiguous; matches: "
                   f"{', '.join(result.candidates)} — disambiguate by id.", err=True)
        raise typer.Exit(_EXIT_AMBIGUOUS)
    tut = result.matched
    want = f"{int(exercise):02d}" if exercise.isdigit() else exercise
    ex = next((e for e in tut.exercises if e.number == want), None)
    if ex is None:
        # Use-case exercises (e.g. ch07) have NO .glp scripts, so 022's corpus
        # model skips them. Reconstruct a scripts-empty Exercise from the
        # filesystem so the resolver can classify it (D2 use-case path).
        import os
        from .corpus import Exercise
        ex_dir = root / tut.id / f"exercise-{want}"
        if ex_dir.is_dir() and (any(ex_dir.glob("*-tutorial.md")) or any(ex_dir.glob("*-repl-trace.md"))):
            rel = Path(os.path.relpath(ex_dir, repo_root)).as_posix()
            ex = Exercise(number=want, dir=rel, scripts=(), md_description=None)
        else:
            avail = ", ".join(e.number for e in tut.exercises) or "(none)"
            typer.echo(f"error: {tut.id} has no exercise '{want}' (not yet available). "
                       f"available: {avail}", err=True)
            raise typer.Exit(_EXIT_NOT_IMPLEMENTED)
    return loaded, tut, ex, repo_root


def _resolve(ctx, tutorial_id, exercise, corpus_opt, sibling_corpus, sibling_glp_root):
    from . import resolve as _rs
    loaded, tut, ex, repo_root = _select_exercise(ctx, tutorial_id, exercise, corpus_opt)
    sib_corpus = Path(sibling_corpus) if sibling_corpus else Path(_rs.DEFAULT_SIBLING_CORPUS)
    sib_root = Path(sibling_glp_root) if sibling_glp_root else Path(_rs.DEFAULT_SIBLING_GLP_ROOT)
    example = _rs.resolve_example(tut, ex, repo_root=repo_root,
                                  sibling_corpus=sib_corpus, sibling_glp_root=sib_root)
    return example, repo_root, sib_root


def _emit_warnings(example, quiet: bool) -> None:
    if quiet:
        return
    for w in example.warnings:
        typer.echo(f"warning: {w}", err=True)


@tutorials_app.command("preview")
def cmd_preview(
    ctx: typer.Context,
    chapter: str = typer.Argument(..., help="Chapter id/prefix/title (e.g. ch01, 1, core)."),
    exercise: str = typer.Argument(..., help="Exercise number (e.g. 01, 1)."),
    corpus: Optional[Path] = typer.Option(None, "--corpus"),
    sibling_corpus: Optional[Path] = typer.Option(None, "--sibling-corpus"),
    sibling_glp_root: Optional[Path] = typer.Option(None, "--sibling-glp-root"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Show the goal(s) + expected outcome from the tutorial docs — NO execution (FR-005)."""
    example, _, _ = _resolve(ctx, chapter, exercise, corpus, sibling_corpus, sibling_glp_root)
    json_mode = json_out or _ctx_flag(ctx, "json")
    if json_mode:
        typer.echo(render_run.preview_json(example))
        raise typer.Exit(_EXIT_OK)
    typer.echo(render_run.preview_human(example))
    raise typer.Exit(_EXIT_OK)


@tutorials_app.command("run")
def cmd_run(
    ctx: typer.Context,
    chapter: str = typer.Argument(...),
    exercise: str = typer.Argument(...),
    goal: list[str] = typer.Option(None, "--goal", help="Run a chosen/extra goal; repeatable."),
    backend: str = typer.Option("cs", "--backend", help="cs (mandated default) | dart."),
    limit: Optional[int] = typer.Option(None, "--limit", help="Reduction limit (:limit N)."),
    timeout: int = typer.Option(120, "--timeout", help="Per-run timeout (s) → bounds a hang to a P1."),
    corpus: Optional[Path] = typer.Option(None, "--corpus"),
    sibling_corpus: Optional[Path] = typer.Option(None, "--sibling-corpus"),
    sibling_glp_root: Optional[Path] = typer.Option(None, "--sibling-glp-root"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Load + run the example on the selected backend; report the actual outcome (FR-006/008)."""
    _run_or_explain(ctx, chapter, exercise, goal, backend, limit, timeout,
                    corpus, sibling_corpus, sibling_glp_root,
                    json_out or _ctx_flag(ctx, "json"), explain=False)


@tutorials_app.command("explain")
def cmd_explain(
    ctx: typer.Context,
    chapter: str = typer.Argument(...),
    exercise: str = typer.Argument(...),
    goal: list[str] = typer.Option(None, "--goal"),
    backend: str = typer.Option("cs", "--backend"),
    limit: Optional[int] = typer.Option(None, "--limit"),
    timeout: int = typer.Option(120, "--timeout"),
    corpus: Optional[Path] = typer.Option(None, "--corpus"),
    sibling_corpus: Optional[Path] = typer.Option(None, "--sibling-corpus"),
    sibling_glp_root: Optional[Path] = typer.Option(None, "--sibling-glp-root"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Run + compare to the golden + explain, referencing the tutorial .md (FR-009/010)."""
    _run_or_explain(ctx, chapter, exercise, goal, backend, limit, timeout,
                    corpus, sibling_corpus, sibling_glp_root,
                    json_out or _ctx_flag(ctx, "json"), explain=True)


def _run_or_explain(ctx, chapter, exercise, goal, backend, limit, timeout,
                    corpus, sibling_corpus, sibling_glp_root, json_mode, *, explain: bool):
    from . import backends as _be
    from . import explain as _ex
    from . import resolve as _rs

    example, repo_root, sib_root = _resolve(ctx, chapter, exercise, corpus, sibling_corpus, sibling_glp_root)
    quiet = _ctx_flag(ctx, "quiet")
    _emit_warnings(example, quiet)

    if not example.supported or example.shape == _rs.Shape.NOT_IMPLEMENTED:
        typer.echo(f"error: {example.chapter_id}/{example.exercise_number} not runnable: "
                   f"{example.unsupported_reason}", err=True)
        raise typer.Exit(_EXIT_NOT_IMPLEMENTED)
    if not example.load_targets:
        typer.echo(f"error: {example.chapter_id}/{example.exercise_number} has no resolvable "
                   "load target.", err=True)
        raise typer.Exit(_EXIT_NO_TARGET)

    # User-supplied goals override the guide goals (FR-004).
    goals = None
    if goal:
        goals = [_rs.Goal(text=g, is_primary=(i == 0), needs_limit=limit,
                          source=_rs.GoalSource.USER_SUPPLIED) for i, g in enumerate(goal)]
    elif not example.goals:
        typer.echo(f"error: no goal resolvable for {example.chapter_id}/{example.exercise_number}; "
                   "supply one with --goal \"<text>\".", err=True)
        raise typer.Exit(_EXIT_NO_GOAL)

    try:
        bk = _be.BackendKind(backend)
    except ValueError:
        typer.echo(f"error: unknown backend '{backend}' (use cs|dart).", err=True)
        raise typer.Exit(2)

    result = _be.run_example(example, backend=bk, repo_root=repo_root, sibling_glp_root=sib_root,
                             goals=goals, limit_override=limit, timeout=timeout)

    # Golden is positionally aligned to the GUIDE goals; user-supplied goals
    # have no golden to compare against.
    cmp_golden = example.golden if goals is None else ()
    verdicts = _ex.explain_run(result.goal_outcomes, cmp_golden, guide_text=example.guide_text)

    if json_mode:
        typer.echo(render_run.run_json(example, result, verdicts, explain=explain))
    else:
        typer.echo(render_run.run_human(example, result, verdicts, explain=explain))

    if result.p1 and not result.goal_outcomes:
        typer.echo(f"error: P1 — {result.p1_notice}", err=True)
        raise typer.Exit(_EXIT_BACKEND_P1)
    if result.error and not result.goal_outcomes:
        typer.echo(f"error: backend produced no outcome: {result.error}", err=True)
        raise typer.Exit(_EXIT_BACKEND_P1)
    raise typer.Exit(_EXIT_OK)


@tutorials_app.command("propose")
def cmd_propose(
    ctx: typer.Context,
    chapter: Optional[str] = typer.Argument(None, help="Limit to one chapter (optional)."),
    apply: bool = typer.Option(False, "--apply", help="Apply a proposal (requires --approve + --rationale)."),
    approve: Optional[str] = typer.Option(None, "--approve", help="Exercise to approve applying."),
    rationale: Optional[str] = typer.Option(None, "--rationale", help="Recorded improvement rationale."),
    corpus: Optional[Path] = typer.Option(None, "--corpus"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Read-only normalization report (D9); approval-gated --apply (FR-013/019)."""
    from . import propose as _pr
    repo_root = _repo_root(ctx)
    try:
        root = resolve_corpus_root(repo_root, corpus)
        loaded = load_corpus(root, repo_root)
    except CorpusUnreachableError as exc:
        typer.echo(f"error: {exc}", err=True)
        raise typer.Exit(_EXIT_CORPUS_UNREACHABLE)
    proposals = _pr.generate_proposals(loaded, repo_root=repo_root)
    if chapter:
        res = match_tutorial(chapter, loaded.chapters)
        if res.kind == "one":
            proposals = [p for p in proposals if p.chapter_id == res.matched.id]
    if apply:
        try:
            _pr.apply_proposal(proposals[0] if proposals else None, approve=approve, rationale=rationale)
        except _pr.ApplyRefused as exc:
            typer.echo(f"error: {exc}", err=True)
            raise typer.Exit(2)
    json_mode = json_out or _ctx_flag(ctx, "json")
    if json_mode:
        typer.echo(render_run.proposals_json(proposals))
    else:
        typer.echo(render_run.proposals_human(proposals))
    raise typer.Exit(_EXIT_OK)


__all__ = ["tutorials_app"]
