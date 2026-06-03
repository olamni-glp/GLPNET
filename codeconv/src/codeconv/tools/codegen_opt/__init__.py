"""codeconv-codegen_opt tool — OFFLINE DSPy/GEPA codegen-prompt optimizer.

Per ``specs/019-codeconv-codegen/contracts/codegen_opt_cli.md``:

- Exports ``app: typer.Typer`` (auto-discovered as a ``codeconv``
  subcommand — invoked as ``codeconv codegen_opt …``; the contract's
  hyphenated ``codegen-opt`` is the conceptual name, Python package dirs
  cannot contain ``-``).
- **Deliberately exports NO ``register_workflows``** so it is NEVER
  registered as a durable DBOS stage (R3/R10 replay-safety). Its sole
  output into production is the optimized-prompt artifact.

**LM steps run IN CLAUDE — never an external API.** Generation + the
reflective proposal are Claude sub-agents driven by the
``/codeconv-codegen-opt`` skill loop; there is NO ``OPENAI_API_KEY``, NO
litellm, NO openai on this (or any) path. ``dspy`` (the program/signature
scaffold) is imported LAZILY inside the command bodies, so a bare
``codeconv`` invocation never pays the import cost.
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
    subsystem_prompt_path,
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
# artifact. Keeps a SINGLE writer (`export-prompt`) for the prompt files.
# Per-subsystem runs use `.candidate-<subsystem>.md` so subsystems do not
# clobber one another's scratch candidate.
_CANDIDATE_REL = ".codeconv/codegen-prompt/.candidate.md"


def _candidate_rel(subsystem: Optional[str]) -> str:
    if subsystem:
        return f".codeconv/codegen-prompt/.candidate-{subsystem}.md"
    return _CANDIDATE_REL


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
    subsystem: Optional[str] = typer.Option(
        None, "--subsystem",
        help="Optimize this subsystem's prompt (per-subsystem dataset + "
        "carry-forward seed). Omit for the legacy global prompt.",
    ),
    seed_prompt: Optional[Path] = typer.Option(
        None, "--seed-prompt",
        help="Carry-forward seed instructions file (e.g. _base.md or the "
        "prior subsystem's prompt). Default: baseline.",
    ),
    budget: int = typer.Option(20, "--budget", help="HARD max metric-calls (SC-006)."),
    model: str = typer.Option(
        "claude-in-session",
        "--model",
        help="LM label recorded in provenance (Claude sub-agents; no external API).",
    ),
    eval_size: int = typer.Option(10, "--eval-size", help="Held-out eval size (global only)."),
    seed: int = typer.Option(0, "--seed", help="Deterministic split seed (global only)."),
    increment: int = typer.Option(1, "--increment", help="1=no tests, 2=tests."),
    json_out: bool = typer.Option(False, "--json", help="Emit JSON summary."),
) -> None:
    """Run budget-capped GEPA; serialize the best-so-far to the scratch
    candidate (promote with ``export-prompt``).

    The LM steps (generate/reflect) run IN CLAUDE — a bare CLI invocation
    with no injected Claude-backed callable exits 2 with an actionable
    message pointing back to the ``/codeconv-codegen-opt`` skill loop
    (NEVER an external-API fallback)."""
    json_out = _ctx_flag(ctx, json_out)
    repo_root = _ctx_repo_root(ctx)
    from .optimize import run_optimize  # lazy (imports dspy on demand)

    seed_instructions = None
    if seed_prompt is not None:
        seed_instructions = _parse_prompt(
            Path(seed_prompt).read_text(encoding="utf-8")
        ).instructions

    try:
        result = run_optimize(
            repo_root, subsystem=subsystem, seed_instructions=seed_instructions,
            budget=budget, model=model,
            eval_size=eval_size, seed=seed, increment=increment,
        )
    except RuntimeError as exc:  # no injected Claude backend / optimizer extra
        summary = {"ok": False, "exit_code": 2, "error": str(exc)}
        _emit(summary, json_out=json_out, header="optimize")
        return

    cand = repo_root / _candidate_rel(subsystem)
    cand.parent.mkdir(parents=True, exist_ok=True)
    cand.write_text(
        _serialize_prompt(result.best_instructions, result.provenance()),
        encoding="utf-8",
    )
    summary = {
        "ok": True,
        "exit_code": 0,
        "subsystem": subsystem or "(global)",
        "metric_score": result.metric_score,
        "baseline_score": result.baseline_score,
        "improved": result.metric_score >= result.baseline_score,
        "budget": result.budget,
        "budget_used": result.budget_used,
        "candidate_path": str(cand),
        "next": (
            f"codeconv codegen_opt export-prompt --subsystem {subsystem}"
            if subsystem else "codeconv codegen_opt export-prompt"
        ),
    }
    _emit(summary, json_out=json_out, header="optimize")


@app.command("eval")
def eval_cmd(
    ctx: typer.Context,
    subsystem: Optional[str] = typer.Option(
        None, "--subsystem", help="Score on this subsystem's held-out split."
    ),
    prompt: Optional[Path] = typer.Option(
        None, "--prompt", help="Instruction set to score (default: production)."
    ),
    model: str = typer.Option("claude-in-session", "--model"),
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
        instructions = _load_prompt(repo_root, subsystem).instructions
    try:
        score = evaluate(
            repo_root, instructions, subsystem=subsystem, model=model,
            eval_size=eval_size, seed=seed, increment=increment,
        )
    except RuntimeError as exc:
        _emit({"ok": False, "exit_code": 2, "error": str(exc)},
              json_out=json_out, header="eval")
        return
    _emit({"ok": True, "exit_code": 0, "subsystem": subsystem or "(global)",
           "metric_score": score},
          json_out=json_out, header="eval")


@app.command("export-prompt")
def export_prompt(
    ctx: typer.Context,
    subsystem: Optional[str] = typer.Option(
        None, "--subsystem",
        help="Promote to <subsystem>.md (default: the global optimized.md).",
    ),
    out: Optional[Path] = typer.Option(
        None, "--out", help=f"Override the artifact path (default {OPTIMIZED_PROMPT_REL})."
    ),
    from_candidate: Optional[Path] = typer.Option(
        None, "--from",
        help="Source candidate (default the per-subsystem scratch candidate).",
    ),
    instructions_file: Optional[Path] = typer.Option(
        None, "--instructions-file",
        help="Promote these instructions directly (the skill-authored "
        "best-so-far prompt); provenance is taken from the flags below.",
    ),
    score: Optional[float] = typer.Option(None, "--score", help="metric_score (provenance)."),
    baseline_score: Optional[float] = typer.Option(None, "--baseline-score"),
    budget: Optional[int] = typer.Option(None, "--budget"),
    budget_used: Optional[int] = typer.Option(None, "--budget-used"),
    dataset_hash: Optional[str] = typer.Option(None, "--dataset-hash"),
    model: str = typer.Option("claude-in-session", "--model"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Serialize the best candidate + provenance to the PRODUCTION artifact.

    The single writer of the prompt files (``optimized.md`` or, with
    ``--subsystem``, ``<subsystem>.md``). Two source modes:
      • ``--from`` / default: promote a serialized scratch candidate;
      • ``--instructions-file``: promote skill-authored instructions, with
        provenance built from the ``--score``/``--budget``/… flags (the
        skill drives the GEPA loop, so it supplies the run provenance).
    Failure/empty ⇒ leave the prior artifact intact (never write a
    degraded prompt silently)."""
    json_out = _ctx_flag(ctx, json_out)
    repo_root = _ctx_repo_root(ctx)
    out_path = (
        Path(out) if out
        else (subsystem_prompt_path(repo_root, subsystem) if subsystem else None)
    )

    if instructions_file is not None:
        if not Path(instructions_file).is_file():
            _emit({"ok": False, "exit_code": 2,
                   "error": f"no instructions file at {instructions_file}"},
                  json_out=json_out, header="export-prompt")
            return
        art = _parse_prompt(Path(instructions_file).read_text(encoding="utf-8"))
        instructions = art.instructions
        prov: dict = dict(art.provenance)
        prov.setdefault("optimizer", "gepa")
        prov["model"] = model
        if score is not None:
            prov["metric_score"] = round(score, 6)
        if baseline_score is not None:
            prov["baseline_score"] = round(baseline_score, 6)
        if budget is not None:
            prov["budget"] = budget
        if budget_used is not None:
            prov["budget_used"] = budget_used
        if dataset_hash is not None:
            prov["dataset_hash"] = dataset_hash
        if subsystem:
            prov["subsystem"] = subsystem
    else:
        src = Path(from_candidate) if from_candidate else (repo_root / _candidate_rel(subsystem))
        if not src.is_file():
            _emit(
                {"ok": False, "exit_code": 2,
                 "error": f"no candidate to export at {src}; run `optimize` first"},
                json_out=json_out, header="export-prompt",
            )
            return
        art = _parse_prompt(src.read_text(encoding="utf-8"))
        instructions = art.instructions
        prov = dict(art.provenance)
        if subsystem:
            prov["subsystem"] = subsystem

    if not instructions.strip():
        _emit(
            {"ok": False, "exit_code": 2,
             "error": "empty instructions; refusing to export"},
            json_out=json_out, header="export-prompt",
        )
        return
    target = write_optimized(repo_root, instructions, prov, out_path=out_path)
    _emit(
        {"ok": True, "exit_code": 0, "subsystem": subsystem or "(global)",
         "written": str(target)},
        json_out=json_out, header="export-prompt",
    )


@app.command("show")
def show(
    ctx: typer.Context,
    subsystem: Optional[str] = typer.Option(
        None, "--subsystem", help="Show this subsystem's prompt provenance."
    ),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Print the production artifact's provenance (or that it's absent)."""
    json_out = _ctx_flag(ctx, json_out)
    repo_root = _ctx_repo_root(ctx)
    path = (
        subsystem_prompt_path(repo_root, subsystem) if subsystem
        else optimized_prompt_path(repo_root)
    )
    if not path.is_file():
        _emit(
            {"ok": True, "exit_code": 0, "exists": False,
             "subsystem": subsystem or "(global)",
             "message": "no optimized prompt; production uses the baseline"},
            json_out=json_out, header="show",
        )
        return
    art = _parse_prompt(path.read_text(encoding="utf-8"))
    summary = {"ok": True, "exit_code": 0, "exists": True,
               "subsystem": subsystem or "(global)", "path": str(path)}
    summary.update({f"prov_{k}": v for k, v in art.provenance.items()})
    _emit(summary, json_out=json_out, header="show")


@app.command("dataset")
def dataset_cmd(
    ctx: typer.Context,
    subsystem: str = typer.Option(
        ..., "--subsystem", help="Subsystem whose train/held-out split to print."
    ),
    held_out_frac: float = typer.Option(
        0.30, "--held-out-frac", help="Held-out fraction (default 0.30)."
    ),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Print a subsystem's deterministic train/held-out dataset split.

    Read-only, deterministic, LM-free (T032). The
    ``/codeconv-codegen-opt`` skill loop consumes this to drive per-example
    generation: ``train`` is what GEPA reflects on, ``held_out`` is the
    fixed SC-003 eval split."""
    json_out = _ctx_flag(ctx, json_out)
    repo_root = _ctx_repo_root(ctx)
    from .dataset import build_subsystem_examples, subsystem_split  # lazy, pure

    examples = build_subsystem_examples(repo_root, subsystem)
    train, held = subsystem_split(examples, held_out_frac=held_out_frac)
    summary = {
        "ok": True,
        "exit_code": 0 if examples else 3,
        "subsystem": subsystem,
        "n_total": len(examples),
        "n_train": len(train),
        "n_held_out": len(held),
        "train": [e.rel_path for e in train],
        "held_out": [e.rel_path for e in held],
        "train_units": {e.rel_path: list(e.expected_units) for e in train},
        "held_out_units": {e.rel_path: list(e.expected_units) for e in held},
    }
    if not examples:
        summary["message"] = (
            f"no plan+convspec examples classify to subsystem {subsystem!r}; "
            "check the manifest path_prefixes and that plans/convspecs exist"
        )
    _emit(summary, json_out=json_out, header="dataset")


@app.command("score")
def score_cmd(
    ctx: typer.Context,
    file: Path = typer.Option(
        ..., "--file", help="Candidate .cs file to build-gate score."
    ),
    expected_units: Optional[str] = typer.Option(
        None, "--expected-units",
        help="Comma-separated top-level constructs the .cs must declare.",
    ),
    dep: list[Path] = typer.Option(
        [], "--dep",
        help="Dependency .cs source(s) to include in the throwaway build "
        "(repeatable) so the candidate compiles in context, offline.",
    ),
    increment: int = typer.Option(1, "--increment", help="1=build, 2=test."),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Build-gate score one candidate .cs (the Python scorer the skill calls).

    Materializes a throwaway net10.0 classlib containing the candidate
    (plus any ``--dep`` sources), runs the SAME ``dotnet build`` gate as
    production (``buildgate.py``), and prints the composite score +
    reflective feedback. Deterministic, OFFLINE, LM-free (T035): the GEPA
    loop's generator (a Claude sub-agent) writes the .cs; THIS scores it.
    A non-compiling candidate scores 0.0 (hard floor)."""
    json_out = _ctx_flag(ctx, json_out)
    import tempfile

    from codeconv.tools.codegen import artefact as _cg_artefact
    from codeconv.tools.codegen.buildgate import run_build, run_test
    from .metric import composite_score

    cs_path = Path(file)
    if not cs_path.is_file():
        _emit({"ok": False, "exit_code": 64, "error": f"no .cs at {cs_path}"},
              json_out=json_out, header="score")
        return
    cs_text = cs_path.read_text(encoding="utf-8")
    units = tuple(u for u in (expected_units or "").replace(",", " ").split() if u)
    val_errors = _cg_artefact.validate_generated(cs_text, expected_units=units)
    if val_errors:
        _emit(
            {"ok": True, "exit_code": 0, "score": 0.0, "build_status": "invalid",
             "feedback": "not real C# / missing construct: " + "; ".join(val_errors)},
            json_out=json_out, header="score",
        )
        return
    builder = run_test if increment >= 2 else run_build
    with tempfile.TemporaryDirectory() as td:
        root = Path(td)
        (root / "proj.csproj").write_text(
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
            "  <PropertyGroup>\n"
            "    <TargetFramework>net10.0</TargetFramework>\n"
            "    <Nullable>enable</Nullable>\n"
            "    <ImplicitUsings>enable</ImplicitUsings>\n"
            "  </PropertyGroup>\n"
            "</Project>\n",
            encoding="utf-8",
        )
        (root / "Candidate.cs").write_text(cs_text, encoding="utf-8")
        for i, d in enumerate(dep):
            dp = Path(d)
            if dp.is_file():
                (root / f"Dep{i}.cs").write_text(
                    dp.read_text(encoding="utf-8"), encoding="utf-8"
                )
        result = builder(root / "proj.csproj")
    s = composite_score(
        build_passed=result.status == "pass",
        test_pass_rate=result.test_pass_rate,
        increment=increment,
    )
    feedback = (
        "Build passed." if result.status == "pass"
        else "Build " + result.status + ": "
        + "; ".join(result.errors or ([result.reason] if result.reason else []))
    )
    _emit(
        {"ok": True, "exit_code": 0, "score": s, "build_status": result.status,
         "feedback": feedback, "errors": list(result.errors)},
        json_out=json_out, header="score",
    )


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
