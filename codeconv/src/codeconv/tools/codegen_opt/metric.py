"""Composite feedback metric for the offline GEPA optimizer.

Implements ``specs/019-codeconv-codegen/contracts/metric_contract.md``.
The SAME metric is computed by the production gate
(``tools/codegen/workflow`` + ``review``) and here for GEPA, so the two
agree by construction — both put the build through ``buildgate.py``.

Per file:
- **Build gate (hard)**: build fail ⇒ score = **0.0** (floor); no
  partial credit for non-compiling output.
- **Compiling**: ``score = 0.6 · test_pass_rate + 0.4 · norm(human)``
  where ``norm(1..5) = (s-1)/4``.
  - **Increment 1** (no tests in scope): ``score = norm(human)``.
  - **Increment 2** (tests converted): full ``0.6/0.4`` weighting.

The pure ``composite_score`` (no I/O) is unit-tested (T033). The
GEPA-facing ``metric`` runs a real ``dotnet build`` via ``buildgate``
(each metric-call may build — hence the HARD budget cap in
``optimize.py``). This module imports ``buildgate`` (deterministic, no
LM) but is itself only ever imported by the OFFLINE optimizer — never by
the production codegen path.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Optional, Sequence

from codeconv.tools.codegen.buildgate import BuildResult, run_build, run_test
from codeconv.tools.equiv.fidelity import FidelityInputs, score as fidelity_score
from codeconv.tools.equiv.relation import DivergenceRecord, Verdict
from codeconv.tools.equiv.verdict import divergence_to_dict


# Metric weights (Increment 2). Increment 1 uses the human term alone.
W_TESTS = 0.6
W_HUMAN = 0.4


def norm_human(score: Optional[int]) -> Optional[float]:
    """Normalise a 1–5 human review score to [0,1]: ``(s-1)/4``.

    ``None`` (not reviewed) ⇒ ``None`` (the caller decides how to treat
    an unreviewed candidate; the optimizer omits the human term when
    optimizing pre-review).
    """
    if score is None:
        return None
    s = int(score)
    if not 1 <= s <= 5:
        raise ValueError(f"human review score must be 1..5, got {s}")
    return (s - 1) / 4.0


def composite_score(
    *,
    build_passed: bool,
    test_pass_rate: Optional[float] = None,
    human_review: Optional[int] = None,
    increment: int = 1,
) -> float:
    """Pure composite metric (metric_contract.md).

    - ``build_passed`` False ⇒ 0.0 (hard floor).
    - Increment 1: ``norm(human)`` (0.0 if unreviewed — no signal yet).
    - Increment 2: ``0.6·test_pass_rate + 0.4·norm(human)`` (each missing
      term contributes 0.0).
    """
    if not build_passed:
        return 0.0
    nh = norm_human(human_review)
    if increment <= 1:
        # Increment 1: the human review is the quality signal
        # (metric_contract.md Inc-1 = norm(human)). PRE-review (no
        # recorded human score) the human term is "omitted when
        # optimizing pre-review" (metric_contract.md § GEPA wiring) —
        # leaving the build hard-gate as the SOLE available signal, so a
        # compiling candidate scores 1.0 (else the optimizer would have
        # no gradient pre-review). [interpretation flagged to Gabi.]
        return nh if nh is not None else 1.0
    # Increment 2: tests are in scope. With a human score, the full
    # 0.6/0.4 weighting; pre-review, the test pass-rate is the signal.
    tpr = test_pass_rate if test_pass_rate is not None else 0.0
    if nh is None:
        return tpr
    return W_TESTS * tpr + W_HUMAN * nh


@dataclass(frozen=True)
class MetricResult:
    """Outcome of scoring one candidate (GEPA reflective signal)."""

    score: float
    build_status: str
    feedback: str
    test_pass_rate: Optional[float] = None


def score_candidate(
    project: Path,
    *,
    human_review: Optional[int] = None,
    increment: int = 1,
    build_fn: Optional[Callable[[Path], BuildResult]] = None,
    human_note: Optional[str] = None,
) -> MetricResult:
    """Build (Inc-2: test) ``project`` and compute the composite score.

    ``build_fn`` is injectable so the mocked-LM test (T029) can score
    without a live SDK. The returned ``feedback`` string is the reflective
    signal GEPA uses (parsed compiler errors + any human note).
    """
    builder = build_fn or (run_test if increment >= 2 else run_build)
    result = builder(project)
    passed = result.status == "pass"
    score = composite_score(
        build_passed=passed,
        test_pass_rate=result.test_pass_rate,
        human_review=human_review,
        increment=increment,
    )
    parts: list[str] = []
    if not passed:
        if result.errors:
            parts.append("Build errors: " + "; ".join(result.errors))
        elif result.reason:
            parts.append(f"Build not run: {result.reason}")
        else:
            parts.append("Build failed.")
    else:
        parts.append("Build passed.")
    if human_note:
        parts.append(f"Reviewer: {human_note}")
    return MetricResult(
        score=score,
        build_status=result.status,
        feedback=" ".join(parts),
        test_pass_rate=result.test_pass_rate,
    )


# --------------------------------------------------------------------------- #
# T031 — fidelity-based GEPA metric (SC-004, contracts/gepa_optimizer.md       #
# § Module+metric + contracts/fidelity_metric.md § GEPA wiring).               #
#                                                                              #
# The GEPA metric's SCALAR is `tools/equiv/fidelity.py:score` — the SAME       #
# function object the production gate uses (imported, NOT re-implemented), so  #
# the two agree by construction (SC-004, asserted by import identity). Its     #
# FEEDBACK is the textual trace-divergence GEPA reflects on. This is the       #
# genuine optimization gradient ABOVE the build-only ceiling: a                #
# compiling-but-not-equivalent candidate sits in 0.25..<1.0 by `frac`          #
# (trace-equivalent sources / in-scope sources), monotonically — the whole     #
# point of the fidelity re-run (handoff anti-drift D). The build-only          #
# `composite_score` above stays for the pre-REPL `score` CLI fallback.         #
# --------------------------------------------------------------------------- #


def render_divergence(div: DivergenceRecord) -> str:
    """Compact one-line text of a DivergenceRecord (GEPA reflective signal).

    Reuses ``verdict.divergence_to_dict`` (the SAME representation persisted as
    ``dart_equivalence.divergence``) so the gate evidence and the GEPA feedback
    are one thing rendered, not two."""
    d = divergence_to_dict(div)
    pc = d.get("spine_pc")
    where = f"position {d['causal_position']}"
    if pc is not None:
        where += f", spine_pc {pc}"
    return (
        f"trace divergence at {d['event_kind']} ({where}): "
        f"expected={d['expected']} actual={d['actual']}"
    )


@dataclass(frozen=True)
class OracleOutcome:
    """Per-candidate trace-equivalence evidence the oracle produces.

    ``source_verdicts`` is one ``Verdict`` per in-scope corpus source (the
    capture-both-REPLs + compare result). ``back_tested``/``trace_captured``
    are set by the caller that ran the oracle (it knows whether back-tests
    exist and whether capture succeeded) — they are NOT inferred here."""

    back_tested: bool
    trace_captured: bool
    source_verdicts: tuple[Verdict, ...] = ()


@dataclass(frozen=True)
class CandidateEvaluation:
    """Full per-candidate evaluation the GEPA metric scores: the build result
    PLUS the oracle's trace evidence. ``make_gepa_metric``'s injected
    ``evaluate_fn`` returns this; ``fidelity_metric_result`` scores it."""

    builds: bool
    back_tested: bool
    trace_captured: bool
    source_verdicts: tuple[Verdict, ...] = ()
    build_feedback: str = ""


def fidelity_metric_result(
    *,
    builds: bool,
    back_tested: bool,
    trace_captured: bool,
    source_verdicts: Sequence[Verdict],
    build_feedback: str = "",
) -> tuple[float, str]:
    """Fidelity score + reflective feedback for one candidate (T031).

    The scalar is ``fidelity_score(FidelityInputs(...))`` — the production
    gate's scorer, imported (SC-004), NEVER a re-implementation. The feedback:
      - the build error, if it does not compile (score 0.0);
      - else the first non-equivalent source's ``DivergenceRecord`` as text;
      - else ``"all sources equivalent"`` (every in-scope source matched);
      - else a compiles-no-evidence note (no captured trace evidence yet).
    """
    in_scope = len(source_verdicts)
    equivalent = sum(1 for v in source_verdicts if v.equivalent)
    s = fidelity_score(
        FidelityInputs(
            builds=builds,
            back_tested=back_tested,
            trace_captured=trace_captured,
            in_scope_sources=in_scope,
            trace_equivalent_sources=equivalent,
        )
    )
    if not builds:
        return s, (build_feedback.strip() or "Build failed.")
    first_div = next(
        (
            v.divergence
            for v in source_verdicts
            if not v.equivalent and v.divergence is not None
        ),
        None,
    )
    if first_div is not None:
        return s, render_divergence(first_div)
    nonequiv = in_scope - equivalent
    if nonequiv:  # non-equivalent but no divergence record attached (defensive)
        return s, f"{nonequiv}/{in_scope} sources not trace-equivalent"
    if not in_scope:
        return s, "compiles; no in-scope trace-equivalence evidence yet"
    if not (back_tested and trace_captured):
        return s, "compiles; trace evidence incomplete (not back-tested/captured)"
    return s, "all sources equivalent"


def _require_dspy_for_metric() -> Any:
    """Lazy dspy import (mirrors ``program.py:_require_dspy``). Keeps metric.py
    importable without the optimizer extra — only ``make_gepa_metric`` needs it."""
    try:
        import dspy

        return dspy
    except ImportError as exc:  # pragma: no cover - env guard
        raise RuntimeError(
            "make_gepa_metric requires the optimizer extra: "
            "pip install -e 'codeconv[opt]' (dspy/gepa — no openai/litellm; "
            "the LM is Claude sub-agents)."
        ) from exc


def make_gepa_metric(
    evaluate_fn: Callable[[Any, Any], CandidateEvaluation],
) -> Callable[..., Any]:
    """Build the ``dspy.GEPA``-facing metric callable.

    ``dspy`` is imported LAZILY here (this module stays importable WITHOUT
    dspy for the registry/CLI path). The returned callable matches the
    dspy.GEPA metric signature — ``metric(gold, pred, trace=None,
    pred_name=None, pred_trace=None) -> dspy.Prediction(score, feedback)``.
    ``evaluate_fn`` performs the (build + oracle) evaluation of ``pred`` and
    is INJECTED — a Claude sub-agent backend, never an external API (NO-API
    hard rule, handoff anti-drift E)."""
    dspy = _require_dspy_for_metric()

    def metric(
        gold: Any,
        pred: Any,
        trace: Any = None,
        pred_name: Any = None,
        pred_trace: Any = None,
    ) -> Any:
        ev = evaluate_fn(gold, pred)
        score_value, feedback = fidelity_metric_result(
            builds=ev.builds,
            back_tested=ev.back_tested,
            trace_captured=ev.trace_captured,
            source_verdicts=ev.source_verdicts,
            build_feedback=ev.build_feedback,
        )
        return dspy.Prediction(score=score_value, feedback=feedback)

    return metric


__all__ = [
    "W_HUMAN",
    "W_TESTS",
    "CandidateEvaluation",
    "FidelityInputs",
    "MetricResult",
    "OracleOutcome",
    "composite_score",
    "fidelity_metric_result",
    "fidelity_score",
    "make_gepa_metric",
    "norm_human",
    "render_divergence",
    "score_candidate",
]
