"""T031 — fidelity-based GEPA metric (SC-004, contracts/gepa_optimizer.md).

The GEPA metric's SCALAR is ``tools/equiv/fidelity.py:score`` — the SAME
function object the production gate uses (asserted by import identity, SC-004);
its FEEDBACK is the textual ``DivergenceRecord``. Mocked oracle + LM — no real
REPL, no real ``dotnet``, no real GEPA (contracts/gepa_optimizer.md § Tests).
``optimize.score_instructions`` takes the fidelity path when an oracle is
injected and the build-only ``composite_score`` path otherwise.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import pytest

from codeconv.tools.codegen.buildgate import BuildResult
from codeconv.tools.codegen_opt import metric as metric_mod
from codeconv.tools.codegen_opt.dataset import build_examples
from codeconv.tools.codegen_opt.metric import (
    CandidateEvaluation,
    OracleOutcome,
    fidelity_metric_result,
    make_gepa_metric,
    render_divergence,
)
from codeconv.tools.codegen_opt.optimize import score_instructions
from codeconv.tools.equiv import fidelity as fidelity_mod
from codeconv.tools.equiv.fidelity import FidelityInputs
from codeconv.tools.equiv.relation import DivergenceRecord, Verdict
from codeconv.tools.equiv.trace import Event, EventKind


_HAS_DSPY = importlib.util.find_spec("dspy") is not None


# ---- verdict fixtures ----------------------------------------------------


def _equiv() -> Verdict:
    return Verdict(equivalent=True, divergence=None)


def _diverge(kind: str = "UNIFY", pos: int = 3) -> Verdict:
    exp = Event(seq=pos, kind=EventKind.UNIFY, payload={"outcome": "success", "vars": ["v_0"]})
    act = Event(seq=pos, kind=EventKind.UNIFY, payload={"outcome": "suspend", "vars": ["v_0"]})
    return Verdict(
        equivalent=False,
        divergence=DivergenceRecord(
            event_kind=kind, causal_position=pos, expected=exp, actual=act, spine_pc=None
        ),
    )


# ---- SC-004 import identity ----------------------------------------------


def test_gepa_metric_uses_the_same_fidelity_score_object() -> None:
    # The GEPA metric must NOT re-implement the tiers — it imports the SAME
    # function the gate uses (SC-004 import identity).
    assert metric_mod.fidelity_score is fidelity_mod.score


def test_fidelity_metric_result_scalar_equals_gate_scorer() -> None:
    verdicts = [_equiv(), _equiv(), _diverge()]  # frac = 2/3
    s, _ = fidelity_metric_result(
        builds=True, back_tested=True, trace_captured=True, source_verdicts=verdicts
    )
    expected = fidelity_mod.score(
        FidelityInputs(
            builds=True, back_tested=True, trace_captured=True,
            in_scope_sources=3, trace_equivalent_sources=2,
        )
    )
    assert s == expected
    assert 0.5 < s < 1.0


# ---- tiers + feedback ----------------------------------------------------


def test_all_equivalent_is_one_with_positive_feedback() -> None:
    s, fb = fidelity_metric_result(
        builds=True, back_tested=True, trace_captured=True,
        source_verdicts=[_equiv(), _equiv()],
    )
    assert s == 1.0
    assert fb == "all sources equivalent"


def test_non_equivalent_feedback_is_the_divergence_text() -> None:
    s, fb = fidelity_metric_result(
        builds=True, back_tested=True, trace_captured=True,
        source_verdicts=[_equiv(), _diverge(kind="UNIFY", pos=7)],
    )
    assert s < 1.0
    assert "trace divergence" in fb
    assert "UNIFY" in fb


def test_non_compile_is_zero_with_build_error_feedback() -> None:
    s, fb = fidelity_metric_result(
        builds=False, back_tested=False, trace_captured=False,
        source_verdicts=[], build_feedback="CS1002: ; expected",
    )
    assert s == 0.0
    assert "CS1002" in fb


def test_compiles_no_evidence_is_quarter() -> None:
    s, fb = fidelity_metric_result(
        builds=True, back_tested=False, trace_captured=False, source_verdicts=[],
    )
    assert s == 0.25
    assert "no in-scope" in fb


def test_compiles_evidence_incomplete_keeps_quarter() -> None:
    # in-scope sources exist but capture/back-test not done ⇒ 0.25 flat band.
    s, fb = fidelity_metric_result(
        builds=True, back_tested=False, trace_captured=True,
        source_verdicts=[_equiv()],
    )
    assert s == 0.25
    assert "incomplete" in fb


def test_render_divergence_contains_key_fields() -> None:
    div = _diverge(kind="WRITER_BIND", pos=5).divergence
    assert div is not None
    txt = render_divergence(div)
    assert "WRITER_BIND" in txt
    assert "position 5" in txt


# ---- dspy.GEPA-facing metric ---------------------------------------------


@pytest.mark.skipif(not _HAS_DSPY, reason="dspy (optimizer extra) not installed")
def test_make_gepa_metric_returns_prediction() -> None:
    def _evaluate(gold, pred) -> CandidateEvaluation:
        return CandidateEvaluation(
            builds=True, back_tested=True, trace_captured=True,
            source_verdicts=(_equiv(), _diverge()),
        )

    metric = make_gepa_metric(_evaluate)
    pred = metric(gold=object(), pred=object())
    assert isinstance(pred.score, float)
    assert 0.0 <= pred.score <= 1.0
    assert isinstance(pred.feedback, str) and pred.feedback
    # score is exactly the fidelity scorer's value (frac 1/2 ⇒ high band).
    assert pred.score == fidelity_mod.score(
        FidelityInputs(
            builds=True, back_tested=True, trace_captured=True,
            in_scope_sources=2, trace_equivalent_sources=1,
        )
    )
    assert "trace divergence" in pred.feedback


# ---- optimize wiring: oracle path vs build-only fallback -----------------


def _make_examples(repo_root: Path, n: int = 2) -> None:
    for i in range(n):
        rel = f"lib/f{i}.dart"
        plan = repo_root / ".codeconv" / "conversion-plans" / (rel + ".md")
        spec = repo_root / ".codeconv" / "conversion-specs" / (rel + ".md")
        plan.parent.mkdir(parents=True, exist_ok=True)
        spec.parent.mkdir(parents=True, exist_ok=True)
        plan.write_text(
            f"---\npath: {rel}\ntarget_code_unit: F{i}\n---\n## plan\n", encoding="utf-8"
        )
        spec.write_text("# spec\n", encoding="utf-8")


def _gen_clean(instructions: str, ex) -> str:
    cls = ex.expected_units[0] if ex.expected_units else "Gen"
    return f"namespace Demo;\npublic class {cls} {{ public int V; }}\n"


def _build_pass(project: Path) -> BuildResult:
    return BuildResult(status="pass")


def test_score_instructions_oracle_all_equivalent_is_one(tmp_path: Path) -> None:
    _make_examples(tmp_path, n=2)
    examples = build_examples(tmp_path)

    def _oracle(ex, cs_text) -> OracleOutcome:
        return OracleOutcome(back_tested=True, trace_captured=True,
                             source_verdicts=(_equiv(),))

    mean, refl = score_instructions(
        tmp_path, "instr", examples,
        generate_fn=_gen_clean, build_fn=_build_pass, oracle_fn=_oracle,
    )
    assert mean == 1.0
    # The fidelity path emits an equivalence reflection even for a clean build
    # (the build-only path emits NONE for a compiling candidate).
    assert refl and all("all sources equivalent" in r for r in refl)


def test_score_instructions_oracle_partial_is_below_one(tmp_path: Path) -> None:
    _make_examples(tmp_path, n=1)
    examples = build_examples(tmp_path)

    def _oracle(ex, cs_text) -> OracleOutcome:
        return OracleOutcome(back_tested=True, trace_captured=True,
                             source_verdicts=(_equiv(), _diverge()))

    mean, refl = score_instructions(
        tmp_path, "instr", examples,
        generate_fn=_gen_clean, build_fn=_build_pass, oracle_fn=_oracle,
    )
    # A compiling candidate that is NOT fully trace-equivalent scores in the
    # high band but strictly below 1.0 — impossible under the build-only path.
    assert 0.5 < mean < 1.0
    assert any("trace divergence" in r for r in refl)


def test_score_instructions_build_only_without_oracle(tmp_path: Path) -> None:
    _make_examples(tmp_path, n=1)
    examples = build_examples(tmp_path)
    mean, refl = score_instructions(
        tmp_path, "instr", examples,
        generate_fn=_gen_clean, build_fn=_build_pass,  # no oracle_fn
    )
    assert mean == 1.0  # build-only Inc-1: compiling ⇒ 1.0
    assert refl == []  # compiling ⇒ no build-failure reflection (build-only path)
