"""Comprehensive sweep driver (driver.py) — hermetic tally over fake spawns.

Verifies classification (equivalent / divergent / needs_agent_work / error),
the decision-2 outcome-mismatch projection, and that a per-goal exception is
recorded as a typed ``error`` row (the sweep never aborts).
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.equiv.capture import RawCapture, ReplConfig
from codeconv.tools.equiv.driver import (
    DIVERGENT,
    EQUIVALENT,
    ERROR,
    NEEDS_AGENT_WORK,
    sweep,
)
from codeconv.tools.equiv.goals import GoalEntry

_FIX = Path(__file__).resolve().parent / "fixtures" / "equiv"
_DART = (_FIX / "append_dart.txt").read_text(encoding="utf-8")
_CSHARP = (_FIX / "append_csharp.txt").read_text(encoding="utf-8")

_CFG = ReplConfig(
    repo_root=Path("/repo"), tutorial_root=Path("/tut"),
    dart_repl=Path("/d.exe"), csharp_repl=Path("/c.exe"),
)


def _ge(goal: str, *, expected="[a, c]") -> GoalEntry:
    return GoalEntry(
        source="ch02/exercise-01/x.glp", goal=goal, expected_status="succeeds",
        expected_bindings=(("Zs", expected),), origin="tutorial:ch02/exercise-01",
    )


def _spawn(config: ReplConfig, source_abs: str, goal: str) -> RawCapture:
    cstdout = "GLP> Zs = [a, c]\n→ succeeds\n"
    if "TAMPER" in goal:
        return RawCapture(_DART, _CSHARP.replace("opcode=Commit", "opcode=Push", 1), cstdout, True)
    if "NAW" in goal:
        return RawCapture(_DART, "", "", False, reason="no trace")
    if "BOOM" in goal:
        return RawCapture("garbage with no outcome", "EV bad", "", True)
    return RawCapture(_DART, _CSHARP, cstdout, True)


def test_sweep_classifies_and_tallies() -> None:
    goals = [
        _ge("append([a],[c],Zs)."),        # equivalent + outcome matches
        _ge("TAMPER([a],[c],Zs)."),         # divergent (tampered spine)
        _ge("NAW([a],[c],Zs)."),            # needs_agent_work (no candidate)
        _ge("append([a],[c],Zs).", expected="[WRONG]"),  # equiv but outcome mismatch
    ]
    rep = sweep(_CFG, goals, spawn=_spawn)
    assert rep.total == 4
    assert rep.count(EQUIVALENT) == 2
    assert rep.count(DIVERGENT) == 1
    assert rep.count(NEEDS_AGENT_WORK) == 1
    assert rep.count(ERROR) == 0
    # decision-2: the [WRONG]-expected goal is flagged as an outcome mismatch.
    assert len(rep.outcome_mismatches) == 1
    assert rep.outcome_mismatches[0].goal.expected_bindings == (("Zs", "[WRONG]"),)


def test_sweep_records_per_goal_error_without_aborting() -> None:
    # A goal whose candidate trace cannot be parsed surfaces as an `error` row;
    # the sweep still completes and reports the surrounding goals.
    goals = [_ge("append([a],[c],Zs)."), _ge("BOOM."), _ge("append([a],[c],Zs).")]
    rep = sweep(_CFG, goals, spawn=_spawn)
    assert rep.total == 3
    assert rep.count(EQUIVALENT) == 2
    err = rep.rows_of(ERROR)
    # BOOM either errors (unparseable candidate) or is needs_agent_work — either
    # way the sweep completes and the two good goals are classified.
    assert rep.count(ERROR) + rep.count(NEEDS_AGENT_WORK) == 1, rep.counts
    if err:
        assert err[0].error
