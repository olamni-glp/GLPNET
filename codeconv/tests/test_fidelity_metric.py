"""T012 — tiered fidelity scorer boundaries (SC-004, FR-013).

PURE, no bridge / no runtime. Asserts the verbatim tiers of
``tools/equiv/fidelity.py`` (contracts/fidelity_metric.md):
non-compile→0.0; compile-no-evidence→0.25; partial→(0.5,1.0);
frac==1.0→exactly 1.0; and that a compiling + back-tested + captured +
human-approved-but-NOT-trace-equivalent file scores strictly < 1.0.
"""

from __future__ import annotations

from codeconv.tools.equiv.fidelity import NEXTBELOW_1, FidelityInputs, score


def _inp(**kw) -> FidelityInputs:
    base = dict(
        builds=True,
        back_tested=True,
        trace_captured=True,
        in_scope_sources=10,
        trace_equivalent_sources=10,
    )
    base.update(kw)
    return FidelityInputs(**base)


def test_non_compile_is_hard_floor_zero() -> None:
    # builds=False dominates regardless of every other input.
    assert score(_inp(builds=False)) == 0.0
    assert (
        score(
            FidelityInputs(
                builds=False,
                back_tested=True,
                trace_captured=True,
                in_scope_sources=10,
                trace_equivalent_sources=10,
            )
        )
        == 0.0
    )


def test_compile_no_evidence_is_flat_quarter() -> None:
    # Compiles but lacks back-test AND/OR captured trace ⇒ flat 0.25,
    # independent of frac.
    assert score(_inp(back_tested=False, trace_captured=False)) == 0.25
    assert score(_inp(back_tested=True, trace_captured=False)) == 0.25
    assert score(_inp(back_tested=False, trace_captured=True)) == 0.25
    # even with frac=1.0, missing evidence keeps it at 0.25
    assert score(_inp(back_tested=False)) == 0.25


def test_partial_high_band_strictly_between_half_and_one() -> None:
    s = score(_inp(in_scope_sources=5, trace_equivalent_sources=3))  # frac=0.6
    assert s == 0.5 + 0.5 * 0.6
    assert 0.5 < s < 1.0


def test_frac_one_snaps_to_exactly_one() -> None:
    assert score(_inp(in_scope_sources=7, trace_equivalent_sources=7)) == 1.0


def test_compiling_backtested_human_ok_but_not_equivalent_is_below_one() -> None:
    # The SC-004 obligation: nothing short of full trace-equivalence reaches
    # 1.0. A file that compiles, back-tests pass, trace captured, and is even
    # human-approved (not an input — approval can't lift the score) but has
    # one non-equivalent source scores strictly < 1.0.
    s = score(_inp(in_scope_sources=1000, trace_equivalent_sources=999))
    assert s < 1.0
    assert s <= NEXTBELOW_1


def test_high_band_is_monotonic_in_frac() -> None:
    prev = -1.0
    for eq in range(0, 10):  # frac 0.0 .. 0.9, all < 1.0
        s = score(_inp(in_scope_sources=10, trace_equivalent_sources=eq))
        assert s > prev
        prev = s
    assert score(_inp(in_scope_sources=10, trace_equivalent_sources=10)) == 1.0


def test_clamp_never_reads_as_one_below_full() -> None:
    # Even an arbitrarily-close-to-1 frac stays strictly below 1.0.
    s = score(_inp(in_scope_sources=10**9, trace_equivalent_sources=10**9 - 1))
    assert s < 1.0
