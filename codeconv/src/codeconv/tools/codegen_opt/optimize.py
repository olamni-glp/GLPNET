"""GEPA optimization driver + budget cap + instruction serialization.

Implements ``specs/019-codeconv-codegen/contracts/codegen_opt_cli.md`` +
``metric_contract.md`` § GEPA wiring. OFFLINE + non-durable.

**LM steps run IN CLAUDE — never an external API.** Generation and the
reflective instruction proposal are performed by Claude sub-agents (the
Agent tool, driven by the ``/codeconv-codegen-opt`` skill loop), the same
way ``/codeconv-codegen`` already produces every ``.cs``. There is **NO
``OPENAI_API_KEY``, NO litellm, NO openai** anywhere on this path — that
mandate (former contract text) is a deleted defect, not a constraint.

Design seams (so a run needs NO network and the mocked-LM test needs no
live SDK):
- ``generate_fn(instructions, example) -> csharp_text`` produces a
  candidate C# unit. It MUST be injected with a Claude-backed callable
  (the skill provides one by spawning a codegen sub-agent per example);
  there is no built-in default.
- ``build_fn(project) -> BuildResult`` is the hard build gate (default =
  ``buildgate.run_build``; ``run_test`` for Inc-2) — deterministic, no LM.
- ``propose_fn(instructions, reflections) -> new_instructions`` is GEPA's
  reflective instruction mutation, also Claude-backed and injected.

The **budget cap is HARD (SC-006)**: ``budget`` bounds the number of
metric-calls (each may run a ``dotnet build``); the driver stops at the
cap and returns the best-so-far instructions — a capped run still yields
a usable instruction set.
"""

from __future__ import annotations

import tempfile
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Optional

from codeconv.tools.codegen import artefact as _cg_artefact
from codeconv.tools.codegen.buildgate import BuildResult, run_build, run_test
from codeconv.tools.codegen.prompt import BASELINE_INSTRUCTIONS

from .dataset import (
    DEFAULT_HELD_OUT_FRAC,
    Example,
    build_examples,
    build_subsystem_examples,
    dataset_hash,
    split,
    subsystem_split,
)
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
    subsystem: Optional[str] = None
    reflections: list[str] = field(default_factory=list)

    def provenance(self, optimizer: str = "gepa") -> dict[str, Any]:
        prov = {
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
        if self.subsystem:
            prov["subsystem"] = self.subsystem
        return prov


def _utc_now_iso() -> str:
    return datetime.now(tz=timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _require_fn(fn: Optional[Callable], name: str) -> Callable:
    """Require a Claude-backed LM callable — there is no API default.

    GEPA's generation and reflective proposal run IN CLAUDE (sub-agents
    via the Agent tool / the ``/codeconv-codegen-opt`` skill loop), never
    via an external LM API. The callable MUST be injected by the caller;
    a bare in-process run with no injected ``fn`` is a usage error (NOT a
    silent OpenAI fallback).
    """
    if fn is None:
        raise RuntimeError(
            f"{name} was not provided. The codegen optimizer runs its LM "
            "steps in Claude (sub-agents) — there is NO external-API "
            "default (no OPENAI_API_KEY / litellm / openai). Drive the "
            "optimizer through the /codeconv-codegen-opt skill loop, which "
            f"injects a Claude-backed {name}."
        )
    return fn


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

    For each example: generate the C# (via the injected Claude-backed
    ``generate_fn``), write it to a throwaway project, run the build gate,
    and compute the composite score. Returns ``(mean_score, reflections)``
    where reflections are the per-example feedback strings GEPA reflects
    on. Honors the HARD budget cap: once ``budget_counter`` is exhausted,
    remaining examples are skipped (best-so-far semantics).
    """
    gen = _require_fn(generate_fn, "generate_fn")
    builder = build_fn or (run_test if increment >= 2 else run_build)
    scores: list[float] = []
    reflections: list[str] = []
    for ex in examples:
        if budget_counter is not None and not budget_counter.spend():
            break
        cs_text = gen(instructions, ex)
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


def _resolve_dataset(
    repo_root: Path,
    *,
    subsystem: Optional[str],
    eval_size: int,
    seed: int,
    held_out_frac: float,
) -> tuple[list[Example], list[Example]]:
    """Return ``(all_examples, eval_set)`` for a run.

    Per-subsystem (``subsystem`` given): the subsystem's own examples,
    split train(~70%)/held-out(~30%) by the deterministic manifest scheme
    (gepa_optimizer.md § Dataset split). Global (``subsystem is None``):
    the legacy eval_size-cut split over all examples (019 behaviour, used
    by the mocked-LM test).
    """
    if subsystem:
        examples = build_subsystem_examples(repo_root, subsystem)
        _train, eval_set = subsystem_split(examples, held_out_frac=held_out_frac)
        return examples, eval_set
    examples = build_examples(repo_root)
    _train, eval_set = split(examples, eval_size=eval_size, seed=seed)
    return examples, eval_set


def run_optimize(
    repo_root: Path,
    *,
    subsystem: Optional[str] = None,
    seed_instructions: Optional[str] = None,
    budget: int = 20,
    model: str = "claude-in-session",
    eval_size: int = 10,
    seed: int = 0,
    increment: int = 1,
    held_out_frac: float = DEFAULT_HELD_OUT_FRAC,
    generate_fn: Optional[GenerateFn] = None,
    build_fn: Optional[BuildFn] = None,
    propose_fn: Optional[ProposeFn] = None,
    max_rounds: int = 5,
) -> OptimizeResult:
    """Run a budget-capped GEPA optimization, returning the best result.

    The loop: score the seed, then repeatedly ask ``propose_fn`` for a
    reflectively-improved instruction set, score it, and keep the best —
    until the HARD budget (metric-calls) is exhausted or ``max_rounds`` is
    reached. A capped run still returns a usable best-so-far (SC-006).

    ``subsystem`` selects the per-subsystem dataset (FR-011) and is recorded
    in provenance. ``seed_instructions`` is the carry-forward seed — the
    prior subsystem's frozen prompt or the shared ``_base.md`` (curriculum
    transfer, decision 6/8); defaults to ``BASELINE_INSTRUCTIONS``.

    ``generate_fn`` and ``propose_fn`` are Claude-backed and MUST be
    injected (the ``/codeconv-codegen-opt`` skill provides them by
    spawning sub-agents). There is no external-API default.
    """
    repo_root = Path(repo_root)
    examples, eval_set = _resolve_dataset(
        repo_root, subsystem=subsystem, eval_size=eval_size,
        seed=seed, held_out_frac=held_out_frac,
    )
    dh = dataset_hash(examples)

    gen = _require_fn(generate_fn, "generate_fn")
    prop = _require_fn(propose_fn, "propose_fn")
    counter = BudgetCounter(budget)

    baseline = seed_instructions or BASELINE_INSTRUCTIONS
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
        subsystem=subsystem,
        reflections=all_refl,
    )


def evaluate(
    repo_root: Path,
    instructions: str,
    *,
    subsystem: Optional[str] = None,
    model: str = "claude-in-session",
    eval_size: int = 10,
    seed: int = 0,
    increment: int = 1,
    held_out_frac: float = DEFAULT_HELD_OUT_FRAC,
    generate_fn: Optional[GenerateFn] = None,
    build_fn: Optional[BuildFn] = None,
) -> float:
    """Score an instruction set on the held-out eval set (the ``eval`` CLI).

    ``subsystem`` selects the per-subsystem held-out split; ``None`` uses
    the global eval_size-cut split (019). ``generate_fn`` is Claude-backed
    and MUST be injected (no API default).
    """
    repo_root = Path(repo_root)
    _examples, eval_set = _resolve_dataset(
        repo_root, subsystem=subsystem, eval_size=eval_size,
        seed=seed, held_out_frac=held_out_frac,
    )
    gen = _require_fn(generate_fn, "generate_fn")
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
    "run_optimize",
    "score_instructions",
]
