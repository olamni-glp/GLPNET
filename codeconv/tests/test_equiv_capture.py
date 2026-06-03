"""Live differential capture orchestration (capture.py).

Hermetic core: the injectable ``CaptureSpawn`` is fed the committed
known-equivalent append fixtures (no live REPL) to exercise outcome parsing,
the decision-2 cross-check, the equivalence verdict, and the needs_agent_work /
divergence paths. A final OPT-IN test runs both real REPLs IF present.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from codeconv.tools.equiv.capture import (
    CaptureResult,
    GoalVerdict,
    RawCapture,
    ReplConfig,
    capture_pair,
    compare_goal,
    default_config,
    parse_repl_outcome,
)
from codeconv.tools.equiv.goals import GoalEntry

_FIX = Path(__file__).resolve().parent / "fixtures" / "equiv"
_DART = (_FIX / "append_dart.txt").read_text(encoding="utf-8")
_CSHARP = (_FIX / "append_csharp.txt").read_text(encoding="utf-8")

# The committed fixtures are the known-equivalent pair for this goal.
_GOAL = GoalEntry(
    source="ch02/exercise-01/ch-02-ex-01-glp-append.glp",
    goal="append([a],[c],Zs).",
    expected_status="succeeds",
    expected_bindings=(("Zs", "[a, c]"),),
    origin="tutorial:ch02/exercise-01",
)

_CFG = ReplConfig(
    repo_root=Path("/repo"),
    tutorial_root=Path("/tut"),
    dart_repl=Path("/repo/dart.exe"),
    csharp_repl=Path("/repo/cs.exe"),
)


def _spawn_equiv(config: ReplConfig, source_abs: str, goal: str) -> RawCapture:
    return RawCapture(
        dart_stdout=_DART,
        candidate_wire=_CSHARP,
        candidate_stdout="GLP> Zs = [a, c]\n→ succeeds\n",
        candidate_ran=True,
    )


# ---- outcome parsing ------------------------------------------------------


def test_parse_repl_outcome_ignores_debug_and_reduction_lines() -> None:
    status, binds = parse_repl_outcome(_DART)
    assert status == "succeeds"
    assert binds == (("Zs", "[a, c]"),)  # NOT the `append(...) :- true` reduction


def test_parse_repl_outcome_multi_binding_suspended() -> None:
    out = "GLP> Out = [a, b]\nRest = <unbound>\n→ suspended\n"
    status, binds = parse_repl_outcome(out)
    assert status == "suspended"
    assert binds == (("Out", "[a, b]"), ("Rest", "<unbound>"))


# ---- capture_pair / compare_goal on the equivalent fixture pair -----------


def test_capture_pair_collects_both_traces() -> None:
    cap = capture_pair(_CFG, _GOAL.source, _GOAL.goal, spawn=_spawn_equiv)
    assert cap.candidate_trace.startswith("EV 0 BYTECODE_OP")
    assert "Zs = [a, c]" in cap.golden_trace
    assert cap.golden_status == "succeeds"
    assert cap.golden_bindings == (("Zs", "[a, c]"),)
    assert cap.have_both


def test_compare_goal_equivalent_and_outcome_matches() -> None:
    gv = compare_goal(_CFG, _GOAL, spawn=_spawn_equiv)
    assert gv.equivalent
    assert gv.outcome_matches_expected is True
    assert not gv.needs_agent_work


def test_outcome_mismatch_is_flagged_even_when_equivalent() -> None:
    # Decision 2: a golden that does not reproduce the tutorial's approved
    # outcome is surfaced — independent of the trace verdict.
    wrong = GoalEntry(
        source=_GOAL.source, goal=_GOAL.goal, expected_status="succeeds",
        expected_bindings=(("Zs", "[WRONG]"),), origin=_GOAL.origin,
    )
    gv = compare_goal(_CFG, wrong, spawn=_spawn_equiv)
    assert gv.outcome_matches_expected is False


def test_candidate_absent_is_needs_agent_work() -> None:
    def _spawn_no_candidate(config: ReplConfig, source_abs: str, goal: str) -> RawCapture:
        return RawCapture(_DART, "", "", candidate_ran=False, reason="no GLP_EQUIV_TRACE")

    gv = compare_goal(_CFG, _GOAL, spawn=_spawn_no_candidate)
    assert gv.needs_agent_work and gv.verdict is None
    assert not gv.equivalent
    assert gv.capture.reason == "no GLP_EQUIV_TRACE"
    # the decision-2 cross-check still runs off the golden:
    assert gv.outcome_matches_expected is True


def test_tampered_candidate_spine_is_divergent() -> None:
    def _spawn_tampered(config: ReplConfig, source_abs: str, goal: str) -> RawCapture:
        return RawCapture(
            dart_stdout=_DART,
            candidate_wire=_CSHARP.replace("opcode=Commit", "opcode=Push", 1),
            candidate_stdout="GLP> Zs = [a, c]\n→ succeeds\n",
            candidate_ran=True,
        )

    gv = compare_goal(_CFG, _GOAL, spawn=_spawn_tampered)
    assert gv.verdict is not None and not gv.equivalent  # positional spine mismatch
    assert gv.verdict.divergence is not None


# ---- opt-in: real dual-REPL capture (skipped if either exe is absent) -----


_REPO = Path(__file__).resolve().parents[2]
_TUT = Path("D:/bstdev/research/glp/GLP/olamni/tutorial")
_LIVE_CFG = default_config(_REPO, _TUT)
_LIVE_READY = (
    _LIVE_CFG.dart_repl.is_file()
    and _LIVE_CFG.csharp_repl.is_file()
    and (_TUT / "ch02/exercise-01/ch-02-ex-01-glp-append.glp").is_file()
)


@pytest.mark.skipif(not _LIVE_READY, reason="dual REPL exes / tutorial source not present")
def test_live_capture_append_runs_and_cross_checks() -> None:
    live_goal = GoalEntry(
        source="ch02/exercise-01/ch-02-ex-01-glp-append.glp",
        goal="append([1,2,3], [a,b,c], Zs).",
        expected_status="succeeds",
        expected_bindings=(("Zs", "[1, 2, 3, a, b, c]"),),
        origin="tutorial:ch02/exercise-01",
    )
    gv = compare_goal(_LIVE_CFG, live_goal)
    # The mechanics must work and the golden must reproduce the tutorial outcome
    # (decision 2). Whether the verdict is equivalent or divergent is REAL oracle
    # data (a divergence here is an FR-017 finding to report, not a test bug).
    assert gv.capture.have_both, gv.capture.reason
    assert gv.outcome_matches_expected is True, (
        gv.capture.golden_status, gv.capture.golden_bindings
    )
    assert gv.verdict is not None
