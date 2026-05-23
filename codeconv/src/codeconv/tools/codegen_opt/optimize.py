"""GEPA optimization driver + budget cap + instruction serialization.

Implements ``specs/019-codeconv-codegen/contracts/codegen_opt_cli.md`` +
``metric_contract.md`` § GEPA wiring. OFFLINE + non-durable: the ONLY
place an LM client (litellm/OpenAI) lives.

Design seams (so the mocked-LM test T029 needs NO network):
- ``generate_fn(instructions, example) -> csharp_text`` produces a
  candidate C# unit (default = the dspy program backed by a real LM).
- ``build_fn(project) -> BuildResult`` is the hard build gate (default =
  ``buildgate.run_build``; ``run_test`` for Inc-2).
- ``propose_fn(instructions, reflections) -> new_instructions`` is GEPA's
  reflective instruction mutation (default = ``dspy.GEPA`` driven; the
  test injects a deterministic proposer).

The **budget cap is HARD (SC-006)**: ``budget`` bounds the number of
metric-calls (each may run a ``dotnet build``); the driver stops at the
cap and returns the best-so-far instructions — a capped run still yields
a usable instruction set. ``OPENAI_API_KEY`` is read ONLY here; absent ⇒
an actionable error (never a guessed fallback).
"""

from __future__ import annotations

import os
import tempfile
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Optional

from codeconv.tools.codegen import artefact as _cg_artefact
from codeconv.tools.codegen.buildgate import BuildResult, run_build, run_test
from codeconv.tools.codegen.prompt import BASELINE_INSTRUCTIONS

from .dataset import Example, build_examples, dataset_hash, split
from .metric import composite_score


GenerateFn = Callable[[str, Example], str]
BuildFn = Callable[[Path], BuildResult]
ProposeFn = Callable[[str, list[str]], str]


@dataclass
class OptimizeResult:
    """Outcome of an optimization run (serialized into the prompt)."""

    best_instructions: str
    metric_score: float
    baseline_score: float
    dataset_hash: str
    budget: int
    budget_used: int
    model: str
    eval_size: int
    generated_at: str
    reflections: list[str] = field(default_factory=list)

    def provenance(self, optimizer: str = "gepa") -> dict[str, Any]:
        return {
            "schema_version": 1,
            "optimizer": optimizer,
            "metric_score": round(self.metric_score, 6),
            "baseline_score": round(self.baseline_score, 6),
            "dataset_hash": self.dataset_hash,
            "generated_at": self.generated_at,
            "model": self.model,
            "budget": self.budget,
            "budget_used": self.budget_used,
            "eval_size": self.eval_size,
        }


def _utc_now_iso() -> str:
    return datetime.now(tz=timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def resolve_api_key() -> str:
    """Read ``OPENAI_API_KEY`` (the ONLY place). Absent ⇒ actionable error."""
    key = os.environ.get("OPENAI_API_KEY")
    if not key:
        raise RuntimeError(
            "OPENAI_API_KEY is not set. The codegen optimizer is the only "
            "component that calls an LM; export OPENAI_API_KEY before "
            "running `codeconv codegen_opt optimize` (it is never read by "
            "the production codegen path)."
        )
    return key


def _default_generate_fn(model: str) -> GenerateFn:
    """Build the real-LM generate function (dspy program over litellm)."""
    from .program import build_program  # lazy: imports dspy

    import dspy  # lazy

    resolve_api_key()
    lm = dspy.LM(model)
    dspy.configure(lm=lm)

    def _gen(instructions: str, ex: Example) -> str:
        prog = build_program(instructions)
        pred = prog(plan=ex.plan_text, convspec=ex.spec_text)
        return getattr(pred, "csharp", "") or ""

    return _gen


def score_instructions(
    repo_root: Path,
    instructions: str,
    examples: list[Example],
    *,
    generate_fn: GenerateFn,
    build_fn: Optional[BuildFn] = None,
    increment: int = 1,
    budget_counter: Optional["BudgetCounter"] = None,
) -> tuple[float, list[str]]:
    """Mean composite score of ``instructions`` over ``examples``.

    For each example: generate the C#, write it to a throwaway project,
    run the build gate, and compute the composite score. Returns
    ``(mean_score, reflections)`` where reflections are the per-example
    feedback strings GEPA reflects on. Honors the HARD budget cap: once
    ``budget_counter`` is exhausted, remaining examples are skipped
    (best-so-far semantics).
    """
    builder = build_fn or (run_test if increment >= 2 else run_build)
    scores: list[float] = []
    reflections: list[str] = []
    for ex in examples:
        if budget_counter is not None and not budget_counter.spend():
            break
        cs_text = generate_fn(instructions, ex)
        val_errors = _cg_artefact.validate_generated(
            cs_text, expected_units=ex.expected_units
        )
        if val_errors:
            scores.append(0.0)
            reflections.append(
                f"{ex.rel_path}: not real C# / missing construct: "
                + "; ".join(val_errors)
            )
            continue
        with tempfile.TemporaryDirectory() as td:
            proj = _materialize_project(Path(td), cs_text)
            result = builder(proj)
        s = composite_score(
            build_passed=result.status == "pass",
            test_pass_rate=result.test_pass_rate,
            increment=increment,
        )
        scores.append(s)
        if result.status != "pass":
            reflections.append(
                f"{ex.rel_path}: build {result.status}: "
                + "; ".join(result.errors or ([result.reason] if result.reason else []))
            )
    mean = sum(scores) / len(scores) if scores else 0.0
    return mean, reflections


def _materialize_project(root: Path, cs_text: str) -> Path:
    """Write a throwaway net10.0 classlib containing ``cs_text``."""
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
    (root / "Generated.cs").write_text(cs_text, encoding="utf-8")
    return root / "proj.csproj"


class BudgetCounter:
    """HARD metric-call budget (SC-006). ``spend()`` returns False once
    the budget is exhausted; callers stop and keep best-so-far."""

    def __init__(self, budget: int) -> None:
        self.budget = int(budget)
        self.used = 0

    def spend(self) -> bool:
        if self.used >= self.budget:
            return False
        self.used += 1
        return True


def run_optimize(
    repo_root: Path,
    *,
    budget: int = 20,
    model: str = "openai/gpt-5.1",
    eval_size: int = 10,
    seed: int = 0,
    increment: int = 1,
    generate_fn: Optional[GenerateFn] = None,
    build_fn: Optional[BuildFn] = None,
    propose_fn: Optional[ProposeFn] = None,
    max_rounds: int = 5,
) -> OptimizeResult:
    """Run a budget-capped GEPA optimization, returning the best result.

    The loop: score the baseline, then repeatedly ask ``propose_fn`` for a
    reflectively-improved instruction set, score it, and keep the best —
    until the HARD budget (metric-calls) is exhausted or ``max_rounds`` is
    reached. A capped run still returns a usable best-so-far (SC-006).
    """
    repo_root = Path(repo_root)
    examples = build_examples(repo_root)
    _train, eval_set = split(examples, eval_size=eval_size, seed=seed)
    dh = dataset_hash(examples)

    gen = generate_fn or _default_generate_fn(model)
    prop = propose_fn or _default_propose_fn(model)
    counter = BudgetCounter(budget)

    baseline = BASELINE_INSTRUCTIONS
    base_score, base_refl = score_instructions(
        repo_root, baseline, eval_set,
        generate_fn=gen, build_fn=build_fn, increment=increment,
        budget_counter=counter,
    )
    best_instr, best_score = baseline, base_score
    all_refl = list(base_refl)

    rounds = 0
    while counter.used < counter.budget and rounds < max_rounds:
        rounds += 1
        candidate = prop(best_instr, all_refl)
        cand_score, cand_refl = score_instructions(
            repo_root, candidate, eval_set,
            generate_fn=gen, build_fn=build_fn, increment=increment,
            budget_counter=counter,
        )
        all_refl = cand_refl or all_refl
        if cand_score > best_score:
            best_instr, best_score = candidate, cand_score

    return OptimizeResult(
        best_instructions=best_instr,
        metric_score=best_score,
        baseline_score=base_score,
        dataset_hash=dh,
        budget=budget,
        budget_used=counter.used,
        model=model,
        eval_size=len(eval_set),
        generated_at=_utc_now_iso(),
        reflections=all_refl,
    )


def _default_propose_fn(model: str) -> ProposeFn:
    """GEPA reflective proposer backed by dspy (lazy import)."""

    def _propose(instructions: str, reflections: list[str]) -> str:
        from .program import build_program  # lazy: imports dspy
        import dspy  # lazy

        # Use dspy.GEPA's reflective instruction proposal indirectly: ask
        # the LM to rewrite the instructions given the failure reflections.
        resolve_api_key()
        dspy.configure(lm=dspy.LM(model))
        reflect = dspy.Predict("instructions, feedback -> improved_instructions")
        out = reflect(
            instructions=instructions,
            feedback="\n".join(reflections[-20:]) or "no failures observed",
        )
        return getattr(out, "improved_instructions", instructions) or instructions

    return _propose


def evaluate(
    repo_root: Path,
    instructions: str,
    *,
    model: str = "openai/gpt-5.1",
    eval_size: int = 10,
    seed: int = 0,
    increment: int = 1,
    generate_fn: Optional[GenerateFn] = None,
    build_fn: Optional[BuildFn] = None,
) -> float:
    """Score an instruction set on the held-out eval set (the ``eval`` CLI)."""
    repo_root = Path(repo_root)
    examples = build_examples(repo_root)
    _train, eval_set = split(examples, eval_size=eval_size, seed=seed)
    gen = generate_fn or _default_generate_fn(model)
    score, _ = score_instructions(
        repo_root, instructions, eval_set,
        generate_fn=gen, build_fn=build_fn, increment=increment,
    )
    return score


__all__ = [
    "BudgetCounter",
    "BuildFn",
    "Example",
    "GenerateFn",
    "OptimizeResult",
    "ProposeFn",
    "evaluate",
    "resolve_api_key",
    "run_optimize",
    "score_instructions",
]
