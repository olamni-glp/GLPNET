"""Instance 6 at the PRODUCTION gate, not just in the abstract (T054).

``test_compile_only_gate.py`` proves the classification rule. This proves the
real ``codeconv.tools.codegen.buildgate`` obeys it — that the gate which actually
decides promotions cannot report a compile-only run as clean.

Each test names the mutation it kills, for the same reason as
``test_instance_registry.py``: the 2026-08-24 review found a mutation test that
stayed green under a no-op validator.
"""

from __future__ import annotations

from codeconv.receipts import Outcome
from codeconv.tools.codegen.buildgate import (
    BUILD_FAIL,
    BUILD_PASS,
    DIM_BEHAVES,
    DIM_COMPILES,
    GATE_DIMENSIONS,
    NOT_BUILT,
    BuildResult,
    emit_gate_receipt,
    gate_counts,
)


def _emit(result: BuildResult, tmp_path, run_id="g"):
    return emit_gate_receipt(result, project="X.csproj", run_id=run_id,
                             root=tmp_path, write=False)


def test_promotability_has_two_dimensions():
    """KILLS: silently redefining the denominator to whatever was examined."""
    assert GATE_DIMENSIONS == (DIM_COMPILES, DIM_BEHAVES)


def test_compile_only_green_build_is_unread_not_pass(tmp_path):
    """KILLS: instance 6 — a compile-only gate promoting a wrong artifact."""
    r = _emit(BuildResult(status=BUILD_PASS, dimensions_examined=(DIM_COMPILES,)), tmp_path)
    assert r.outcome is Outcome.UNREAD
    assert not r.outcome.is_successful
    assert (r.examined_count, r.total_count) == (1, 2)
    assert r.examined == [DIM_COMPILES]


def test_a_test_run_that_really_ran_tests_may_pass(tmp_path):
    """KILLS: a gate that can never report success — the useless opposite."""
    r = _emit(BuildResult(status=BUILD_PASS, test_pass_rate=1.0,
                          dimensions_examined=(DIM_COMPILES, DIM_BEHAVES)), tmp_path)
    assert r.outcome is Outcome.PASS
    assert (r.examined_count, r.total_count) == (2, 2)


def test_zero_tests_is_not_evidence_of_behaviour(tmp_path):
    """KILLS: a vacuous green — 0 tests passing is instance 12, not a pass."""
    r = _emit(BuildResult(status=BUILD_PASS, test_pass_rate=None,
                          dimensions_examined=(DIM_COMPILES,)), tmp_path)
    assert r.outcome is Outcome.UNREAD
    assert not r.outcome.is_successful


def test_compiler_errors_are_fail_not_unread(tmp_path):
    """KILLS: a real defect being downgraded to 'we did not look'."""
    r = _emit(BuildResult(status=BUILD_FAIL, errors=("CS0246: type not found",),
                          dimensions_examined=(DIM_COMPILES,)), tmp_path)
    assert r.outcome is Outcome.FAIL
    assert not r.outcome.is_successful


def test_missing_sdk_is_unsearchable_and_names_the_reason(tmp_path):
    """KILLS: an environment outage recorded as a clean gate, or as a code FAIL."""
    r = _emit(BuildResult(status=NOT_BUILT, reason="dotnet SDK not on PATH"), tmp_path)
    assert r.outcome is Outcome.UNSEARCHABLE
    assert not r.outcome.is_successful
    assert "dotnet SDK" in (r.resolved_target.unresolved_reason or "")


def test_gate_counts_are_derived_not_asserted():
    """KILLS: a hardcoded (1, 2) that goes on agreeing after the model changes."""
    assert gate_counts(BuildResult(status=BUILD_PASS,
                                   dimensions_examined=(DIM_COMPILES,))) == (1, 2)
    assert gate_counts(BuildResult(status=BUILD_PASS,
                                   dimensions_examined=GATE_DIMENSIONS)) == (2, 2)
    assert gate_counts(BuildResult(status=NOT_BUILT)) == (0, 2)


def test_the_status_word_alone_never_decides_the_outcome(tmp_path):
    """KILLS: reintroducing `if result.status == BUILD_PASS: outcome = PASS`.

    Two results share status='pass' and MUST classify differently. If this ever
    passes with both PASS, the gate is reading the status word again and instance
    6 is back.
    """
    a = _emit(BuildResult(status=BUILD_PASS, dimensions_examined=(DIM_COMPILES,)),
              tmp_path, run_id="a")
    b = _emit(BuildResult(status=BUILD_PASS, dimensions_examined=GATE_DIMENSIONS),
              tmp_path, run_id="b")
    assert a.outcome is Outcome.UNREAD and b.outcome is Outcome.PASS
    assert a.outcome is not b.outcome
