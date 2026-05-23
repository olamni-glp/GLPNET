"""codeconv-codegen_opt tool — OFFLINE DSPy/GEPA codegen-prompt optimizer.

Per ``specs/019-codeconv-codegen/contracts/codegen_opt_cli.md``:

- Exports ``app: typer.Typer`` (auto-discovered as a ``codeconv``
  subcommand — invoked as ``codeconv codegen_opt …``; the contract's
  hyphenated ``codegen-opt`` is the conceptual name, Python package dirs
  cannot contain ``-``).
- **Deliberately exports NO ``register_workflows``** so it is NEVER
  registered as a durable DBOS stage (R3/R10 replay-safety). It is the
  ONLY place an LM client + ``OPENAI_API_KEY`` live; its sole output into
  production is the optimized-prompt artifact.

dspy/litellm/openai are imported LAZILY inside the command bodies, so a
bare ``codeconv`` invocation (which imports every tool package for
registration) never pays the optimizer import cost or emits litellm's
import-time warnings.
"""

from __future__ import annotations

import json as _json
from pathlib import Path
from typing import Optional

import typer

from codeconv.tools.codegen.prompt import (
    OPTIMIZED_PROMPT_REL,
    optimized_prompt_path,
    parse as _parse_prompt,
    serialize as _serialize_prompt,
    write_optimized,
)


app = typer.Typer(
    add_completion=False,
    help=(
        "OFFLINE GEPA/DSPy optimizer for the codegen prompt (the only "
        "LM-backed, non-durable codeconv tool). Output = the "
        "optimized-prompt artifact."
    ),
    invoke_without_command=True,
    no_args_is_help=False,
)

# Scratch candidate (dot-prefixed ⇒ gitignored): `optimize` writes its
# best-so-far here; `export-prompt` promotes it to the production
# artifact. Keeps a SINGLE writer (`export-prompt`) for `optimized.md`.
_CANDIDATE_REL = ".codeconv/codegen-prompt/.candidate.md"


def _ctx_repo_root(ctx: typer.Context) -> Path:
    return Path(ctx.obj["repo_root"]) if ctx.obj else Path.cwd()


def _ctx_flag(ctx: typer.Context, json_out: bool) -> bool:
    return json_out or bool(ctx.obj.get("json")) if ctx.obj else json_out


@app.callback(invoke_without_command=True)
def _default(ctx: typer.Context) -> None:
    """Bare ``codeconv codegen_opt`` ⇒ ``show`` (read-only)."""
    if ctx.invoked_subcommand is None:
        ctx.invoke(show)


@app.command("optimize")
def optimize(
    ctx: typer.Context,
    budget: int = typer.Option(20, "--budget", help="HARD max metric-calls (SC-006)."),
    model: str = typer.Option(
        "openai/gpt-5.1", "--model", help="litellm model id (reasoning model)."
    ),
    eval_size: int = typer.Option(10, "--eval-size", help="Held-out eval size."),
    seed: int = typer.Option(0, "--seed", help="Deterministic split seed."),
    increment: int = typer.Option(1, "--increment", help="1=no tests, 2=tests."),
    json_out: bool = typer.Option(False, "--json", help="Emit JSON summary."),
) -> None:
    """Run budget-capped GEPA; serialize the best-so-far to the scratch
    candidate (promote with ``export-prompt``)."""
    json_out = _ctx_flag(ctx, json_out)
    repo_root = _ctx_repo_root(ctx)
    from .optimize import run_optimize  # lazy (imports dspy on demand)

    try:
        result = run_optimize(
            repo_root, budget=budget, model=model,
            eval_size=eval_size, seed=seed, increment=increment,
        )
    except RuntimeError as exc:  # missing API key / optimizer extra
        summary = {"ok": False, "exit_code": 2, "error": str(exc)}
        _emit(summary, json_out=json_out, header="optimize")
        return

    cand = repo_root / _CANDIDATE_REL
    cand.parent.mkdir(parents=True, exist_ok=True)
    cand.write_text(
        _serialize_prompt(result.best_instructions, result.provenance()),
        encoding="utf-8",
    )
    summary = {
        "ok": True,
        "exit_code": 0,
        "metric_score": result.metric_score,
        "baseline_score": result.baseline_score,
        "improved": result.metric_score >= result.baseline_score,
        "budget": result.budget,
        "budget_used": result.budget_used,
        "candidate_path": str(cand),
        "next": "codeconv codegen_opt export-prompt",
    }
    _emit(summary, json_out=json_out, header="optimize")


@app.command("eval")
def eval_cmd(
    ctx: typer.Context,
    prompt: Optional[Path] = typer.Option(
        None, "--prompt", help="Instruction set to score (default: production)."
    ),
    model: str = typer.Option("openai/gpt-5.1", "--model"),
    eval_size: int = typer.Option(10, "--eval-size"),
    seed: int = typer.Option(0, "--seed"),
    increment: int = typer.Option(1, "--increment"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Score a given (or the production) instruction set on the eval set."""
    json_out = _ctx_flag(ctx, json_out)
    repo_root = _ctx_repo_root(ctx)
    from codeconv.tools.codegen.prompt import load as _load_prompt
    from .optimize import evaluate  # lazy

    if prompt is not None:
        instructions = _parse_prompt(Path(prompt).read_text(encoding="utf-8")).instructions
    else:
        instructions = _load_prompt(repo_root).instructions
    try:
        score = evaluate(
            repo_root, instructions, model=model,
            eval_size=eval_size, seed=seed, increment=increment,
        )
    except RuntimeError as exc:
        _emit({"ok": False, "exit_code": 2, "error": str(exc)},
              json_out=json_out, header="eval")
        return
    _emit({"ok": True, "exit_code": 0, "metric_score": score},
          json_out=json_out, header="eval")


@app.command("export-prompt")
def export_prompt(
    ctx: typer.Context,
    out: Optional[Path] = typer.Option(
        None, "--out", help=f"Override the artifact path (default {OPTIMIZED_PROMPT_REL})."
    ),
    from_candidate: Optional[Path] = typer.Option(
        None, "--from", help="Source candidate (default the scratch candidate)."
    ),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Serialize the best candidate + provenance to the PRODUCTION artifact.

    The single writer of ``optimized.md``. Failure/empty ⇒ leave the
    prior artifact intact (never write a degraded prompt silently)."""
    json_out = _ctx_flag(ctx, json_out)
    repo_root = _ctx_repo_root(ctx)
    src = Path(from_candidate) if from_candidate else (repo_root / _CANDIDATE_REL)
    if not src.is_file():
        _emit(
            {
                "ok": False,
                "exit_code": 2,
                "error": f"no candidate to export at {src}; run `optimize` first",
            },
            json_out=json_out, header="export-prompt",
        )
        return
    art = _parse_prompt(src.read_text(encoding="utf-8"))
    if not art.instructions.strip():
        _emit(
            {"ok": False, "exit_code": 2,
             "error": "candidate has empty instructions; refusing to export"},
            json_out=json_out, header="export-prompt",
        )
        return
    target = write_optimized(
        repo_root, art.instructions, art.provenance, out_path=out
    )
    _emit(
        {"ok": True, "exit_code": 0, "written": str(target)},
        json_out=json_out, header="export-prompt",
    )


@app.command("show")
def show(
    ctx: typer.Context,
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Print the production artifact's provenance (or that it's absent)."""
    json_out = _ctx_flag(ctx, json_out)
    repo_root = _ctx_repo_root(ctx)
    path = optimized_prompt_path(repo_root)
    if not path.is_file():
        _emit(
            {"ok": True, "exit_code": 0, "exists": False,
             "message": "no optimized prompt; production uses the baseline"},
            json_out=json_out, header="show",
        )
        return
    art = _parse_prompt(path.read_text(encoding="utf-8"))
    summary = {"ok": True, "exit_code": 0, "exists": True, "path": str(path)}
    summary.update({f"prov_{k}": v for k, v in art.provenance.items()})
    _emit(summary, json_out=json_out, header="show")


def _emit(summary: dict, *, json_out: bool, header: str) -> None:
    if json_out:
        typer.echo(_json.dumps(summary, indent=2, sort_keys=True, default=str))
    else:
        typer.echo(f"codeconv-codegen_opt [{header}]")
        for key, value in summary.items():
            if isinstance(value, (list, dict)):
                continue
            typer.echo(f"  {key:>20}  {value}")
    code = int(summary.get("exit_code", 0) or 0)
    if code != 0:
        raise typer.Exit(code)


# NOTE: intentionally NO `register_workflows` — codegen_opt is offline +
# non-durable and must never become a DBOS stage (R3/R10).
__all__ = ["app"]
