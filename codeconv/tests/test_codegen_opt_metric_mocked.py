"""T029 — GEPA beats baseline w/ a MOCKED LM + fixture build (SC-003/SC-006).

No real LM, no real ``dotnet`` (FR-004): inject ``generate_fn``
(mock codegen), ``build_fn`` (mock build gate), and ``propose_fn``
(deterministic reflective improvement). The baseline instructions
produce a non-compiling candidate (score 0.0); the proposed instructions
produce a compiling one (score 1.0 pre-review). Asserts the optimized
score ≥ baseline AND the HARD budget cap is honored.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.codegen.buildgate import BuildResult
from codeconv.tools.codegen_opt.metric import composite_score
from codeconv.tools.codegen_opt.optimize import BudgetCounter, run_optimize


# ---- pure metric math (also exercised by T033) --------------------------


def test_composite_build_fail_is_zero() -> None:
    assert composite_score(build_passed=False, human_review=5, increment=1) == 0.0


def test_composite_inc1_prereview_is_build_gate() -> None:
    # Pre-review Inc-1: a compiling candidate scores 1.0 (build gate).
    assert composite_score(build_passed=True, increment=1) == 1.0


def test_composite_inc1_with_human() -> None:
    assert composite_score(build_passed=True, human_review=5, increment=1) == 1.0
    assert composite_score(build_passed=True, human_review=1, increment=1) == 0.0
    assert composite_score(build_passed=True, human_review=3, increment=1) == 0.5


def test_composite_inc2_weighting() -> None:
    # 0.6·tpr + 0.4·norm(human); tpr=1.0, human=5 ⇒ 1.0
    assert composite_score(
        build_passed=True, test_pass_rate=1.0, human_review=5, increment=2
    ) == 1.0
    # tpr=0.5, human=3 ⇒ 0.6·0.5 + 0.4·0.5 = 0.5
    assert composite_score(
        build_passed=True, test_pass_rate=0.5, human_review=3, increment=2
    ) == 0.5


# ---- mocked GEPA optimization -------------------------------------------


def _make_examples(repo_root: Path, n: int = 2) -> None:
    """Write n plan+convspec artifact pairs so build_examples() finds them."""
    for i in range(n):
        rel = f"lib/f{i}.dart"
        plan = repo_root / ".codeconv" / "conversion-plans" / (rel + ".md")
        spec = repo_root / ".codeconv" / "conversion-specs" / (rel + ".md")
        plan.parent.mkdir(parents=True, exist_ok=True)
        spec.parent.mkdir(parents=True, exist_ok=True)
        plan.write_text(
            f"---\npath: {rel}\ntarget_code_unit: F{i}\n---\n## plan\n",
            encoding="utf-8",
        )
        spec.write_text("# spec\n", encoding="utf-8")


_OPT_MARKER = "OPTIMIZED"


def _mock_generate(instructions: str, ex) -> str:
    """Baseline ⇒ broken C#; optimized instructions ⇒ clean C#."""
    cls = ex.expected_units[0] if ex.expected_units else "Gen"
    if _OPT_MARKER in instructions:
        return f"namespace Demo;\npublic class {cls} {{ public int V; }}\n"
    return f"namespace Demo;\npublic class {cls} {{ BROKEN }}\n"


def _mock_build(project: Path) -> BuildResult:
    text = (project.parent / "Generated.cs").read_text(encoding="utf-8")
    if "BROKEN" in text:
        return BuildResult(status="fail", errors=("CS1519: invalid token 'BROKEN'",))
    return BuildResult(status="pass")


def _mock_propose(instructions: str, reflections: list[str]) -> str:
    return _OPT_MARKER + " " + instructions


def test_gepa_beats_baseline_mocked(tmp_path: Path) -> None:
    _make_examples(tmp_path, n=2)
    result = run_optimize(
        tmp_path,
        budget=100,
        eval_size=2,
        increment=1,
        generate_fn=_mock_generate,
        build_fn=_mock_build,
        propose_fn=_mock_propose,
        max_rounds=3,
    )
    assert result.baseline_score == 0.0  # baseline = broken builds
    assert result.metric_score >= result.baseline_score
    assert result.metric_score == 1.0  # optimized = compiling builds
    assert _OPT_MARKER in result.best_instructions


def test_budget_cap_is_hard(tmp_path: Path) -> None:
    _make_examples(tmp_path, n=4)
    # Budget 3 < (baseline 4 + any round) ⇒ stops mid-baseline.
    result = run_optimize(
        tmp_path,
        budget=3,
        eval_size=4,
        increment=1,
        generate_fn=_mock_generate,
        build_fn=_mock_build,
        propose_fn=_mock_propose,
        max_rounds=10,
    )
    assert result.budget_used <= 3, result.budget_used


def test_budget_counter_stops_at_cap() -> None:
    c = BudgetCounter(2)
    assert c.spend() is True
    assert c.spend() is True
    assert c.spend() is False
    assert c.used == 2
