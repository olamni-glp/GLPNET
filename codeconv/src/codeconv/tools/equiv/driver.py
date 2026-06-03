"""Comprehensive equivalence sweep — run the goal-bearing corpus through the
dual-REPL oracle and tally equivalent / divergent / needs_agent_work / error,
plus the decision-2 outcome cross-check.

The CLI/agent layer (R12) — read-only: NO DB write, NO recorded-artifact write
(that is ``equiv capture`` + the durable step's job). This is the "combined
comprehensive test driver" (design-comprehensive-equiv-driver.md §5): it answers
"how behaviourally faithful is the converted C# across the whole corpus, and
where does it first diverge?".

Per-goal failures are RECORDED (never abort the sweep) so a single run surfaces
EVERY problem at once (DISCIPLINE §1.9) — but they are surfaced as typed
``error`` rows, never silently absorbed.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Optional

from codeconv.tools.equiv.capture import (
    CaptureResult,
    CaptureSpawn,
    GoalVerdict,
    ReplConfig,
    _default_spawn,
    compare_goal,
)
from codeconv.tools.equiv.goals import GoalEntry

EQUIVALENT = "equivalent"
DIVERGENT = "divergent"
NEEDS_AGENT_WORK = "needs_agent_work"
ERROR = "error"


@dataclass(frozen=True)
class SweepRow:
    goal: GoalEntry
    classification: str
    outcome_matches_expected: Optional[bool]
    verdict: Optional[GoalVerdict]
    error: Optional[str] = None


@dataclass(frozen=True)
class SweepReport:
    rows: tuple[SweepRow, ...]

    def count(self, classification: str) -> int:
        return sum(1 for r in self.rows if r.classification == classification)

    @property
    def total(self) -> int:
        return len(self.rows)

    @property
    def counts(self) -> dict[str, int]:
        return {
            EQUIVALENT: self.count(EQUIVALENT),
            DIVERGENT: self.count(DIVERGENT),
            NEEDS_AGENT_WORK: self.count(NEEDS_AGENT_WORK),
            ERROR: self.count(ERROR),
        }

    @property
    def outcome_mismatches(self) -> tuple[SweepRow, ...]:
        """Decision-2 violations: the golden re-capture did not reproduce the
        tutorial's approved outcome (goal-misextraction or golden drift)."""
        return tuple(r for r in self.rows if r.outcome_matches_expected is False)

    def rows_of(self, classification: str) -> tuple[SweepRow, ...]:
        return tuple(r for r in self.rows if r.classification == classification)


def _classify(gv: GoalVerdict) -> str:
    if gv.verdict is None:
        return NEEDS_AGENT_WORK
    return EQUIVALENT if gv.verdict.equivalent else DIVERGENT


def sweep(
    config: ReplConfig,
    goals: list[GoalEntry] | tuple[GoalEntry, ...],
    *,
    spawn: CaptureSpawn = _default_spawn,
    progress: Optional[Callable[[int, int, SweepRow], None]] = None,
) -> SweepReport:
    """Run every goal through ``compare_goal`` and tally. A per-goal exception is
    recorded as an ``error`` row (the sweep completes; nothing is hidden)."""
    rows: list[SweepRow] = []
    n = len(goals)
    for i, g in enumerate(goals):
        try:
            gv = compare_goal(config, g, spawn=spawn)
            row = SweepRow(
                goal=g,
                classification=_classify(gv),
                outcome_matches_expected=gv.outcome_matches_expected,
                verdict=gv,
            )
        except Exception as exc:  # surfaced as a typed error row, not swallowed
            row = SweepRow(
                goal=g,
                classification=ERROR,
                outcome_matches_expected=None,
                verdict=None,
                error=f"{type(exc).__name__}: {exc}",
            )
        rows.append(row)
        if progress is not None:
            progress(i + 1, n, row)
    return SweepReport(rows=tuple(rows))


__all__ = [
    "EQUIVALENT",
    "DIVERGENT",
    "NEEDS_AGENT_WORK",
    "ERROR",
    "SweepRow",
    "SweepReport",
    "sweep",
]
