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
from typing import Callable, Optional

from codeconv.tools.codegen.buildgate import BuildResult, run_build, run_test


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


__all__ = [
    "W_HUMAN",
    "W_TESTS",
    "MetricResult",
    "composite_score",
    "norm_human",
    "score_candidate",
]
