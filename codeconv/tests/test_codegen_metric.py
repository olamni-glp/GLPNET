"""T033 — composite-metric + promotion-gate math (pure).

Covers metric_contract.md: the build hard-gate floor (0.0), the
0.6/0.4 Inc-2 weighting, ``norm(1..5)=(s-1)/4``, and the batch promotion
gate (100% build AND median human ≥ 4/5). No bridge.
"""

from __future__ import annotations

import pytest

from codeconv.tools.codegen.review import PROMOTION_MIN_MEDIAN, promotion_gate
from codeconv.tools.codegen_opt.metric import (
    composite_score,
    norm_human,
)


def test_norm_human() -> None:
    assert norm_human(1) == 0.0
    assert norm_human(3) == 0.5
    assert norm_human(5) == 1.0
    assert norm_human(None) is None
    with pytest.raises(ValueError):
        norm_human(6)


def test_build_fail_is_hard_floor() -> None:
    assert composite_score(build_passed=False, human_review=5, increment=2,
                           test_pass_rate=1.0) == 0.0


def test_inc1_prereview_build_gate() -> None:
    assert composite_score(build_passed=True, increment=1) == 1.0


def test_inc1_human_only() -> None:
    assert composite_score(build_passed=True, human_review=4, increment=1) == 0.75


def test_inc2_weighting() -> None:
    # 0.6*tpr + 0.4*norm(human)
    assert composite_score(
        build_passed=True, test_pass_rate=1.0, human_review=5, increment=2
    ) == 1.0
    assert composite_score(
        build_passed=True, test_pass_rate=0.0, human_review=5, increment=2
    ) == pytest.approx(0.4)
    assert composite_score(
        build_passed=True, test_pass_rate=1.0, human_review=1, increment=2
    ) == pytest.approx(0.6)


# ---- promotion gate ------------------------------------------------------


def test_gate_passes_all_built_median_4() -> None:
    rows = [
        ("a.dart", "pass", 5),
        ("b.dart", "pass", 4),
        ("c.dart", "pass", 4),
    ]
    g = promotion_gate(rows)
    assert g.passed is True
    assert g.median_human == 4.0
    assert g.blockers == ()


def test_gate_fails_on_unbuilt_file() -> None:
    rows = [("a.dart", "pass", 5), ("b.dart", "fail", 5)]
    g = promotion_gate(rows)
    assert g.passed is False
    assert g.all_built is False
    assert any("b.dart" in b for b in g.blockers)


def test_gate_fails_on_low_median() -> None:
    rows = [("a.dart", "pass", 3), ("b.dart", "pass", 3), ("c.dart", "pass", 5)]
    g = promotion_gate(rows)
    assert g.median_human == 3.0
    assert g.passed is False
    assert any("median" in b for b in g.blockers)


def test_gate_fails_when_no_reviews() -> None:
    rows = [("a.dart", "pass", None), ("b.dart", "pass", None)]
    g = promotion_gate(rows)
    assert g.passed is False
    assert any("no human reviews" in b for b in g.blockers)


def test_gate_empty_batch() -> None:
    g = promotion_gate([])
    assert g.passed is False
    assert PROMOTION_MIN_MEDIAN == 4.0
