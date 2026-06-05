"""FR-011 first-implementation verification spike (US4, SC-008).

A deterministic **reference harness** (Gabi-confirmed 2026-06-05) that proves
the resume + budget *contract* the marathon relies on, BEFORE relying on it:

- **cached-prefix resume** — re-running a multi-step run with an unchanged
  leading prefix returns those steps **cached**; the first changed/new step
  AND everything after it runs live (the Workflow tool's ``resumeFromRunId``
  cascade semantics). (US4-AS1)
- **budget observability** — ``spent``/``remaining`` are observable and
  consistent throughout the run. (US4-AS2)

It records the verification result durably as a ``verification_traces`` row
(``subject=workflow-spike``) (FR-011/SC-008).

This Python harness is a self-contained verification fixture — NOT the
marathon's orchestrator. The production orchestration composes the real
Claude Code Workflow tool at the buildkit-stage skill layer (model-driven),
where FR-009's non-reinvention guard applies; this harness only verifies the
contract that orchestration depends on.
"""

from __future__ import annotations

from typing import Any, Optional

from .orchestrate import Budget
from .trace import write_trace

# Illustrative per-executed-step token cost — large enough to make spend
# movement legible, small enough that the canonical spike stays well under a
# realistic ceiling.
_SPIKE_STEP_COST = 1000

# The canonical small multi-step run and its resume variant (a single changed
# step in the middle, to exercise BOTH the cached prefix and the cascade).
_STEPS_A = ["scaffold", "analyze", "plan", "emit"]
_STEPS_B = ["scaffold", "analyze", "plan_v2", "emit"]


class _CachedPrefixRunner:
    """Models the Workflow tool's ``resumeFromRunId``: the longest UNCHANGED
    prefix returns cached; the first changed/new step and EVERYTHING after it
    runs live (cascade — a later step with unchanged input still re-runs if a
    predecessor changed)."""

    def __init__(self, budget: Budget) -> None:
        self._cache: dict[int, tuple[Any, str]] = {}
        self._budget = budget
        self.executed: list[int] = []
        self.budget_trace: list[tuple[int, int]] = []  # (index, spent_after)

    def run(self, steps: list[str]) -> list[tuple[str, str]]:
        self.executed = []
        results: list[tuple[str, str]] = []
        diverged = False
        for i, inp in enumerate(steps):
            cached = self._cache.get(i)
            if not diverged and cached is not None and cached[0] == inp:
                results.append(("cached", cached[1]))
            else:
                diverged = True
                self._budget.add(_SPIKE_STEP_COST)
                result = f"r({inp})"
                self._cache[i] = (inp, result)
                self.executed.append(i)
                self.budget_trace.append((i, self._budget.spent()))
                results.append(("ran", result))
        return results


def _budget_observable(trace: list[tuple[int, int]], ceiling: Optional[int]) -> bool:
    """Spend was observable throughout iff it is strictly increasing, positive,
    and (when bounded) never crosses the ceiling."""
    spents = [s for _, s in trace]
    if not spents:
        return False
    monotonic = spents[0] > 0 and all(b > a for a, b in zip(spents, spents[1:]))
    within = ceiling is None or all(s <= ceiling for s in spents)
    return monotonic and within


def run_spike(
    store: Any, marathon_id: str, *, ceiling: Optional[int] = None
) -> dict[str, Any]:
    """Run the FR-011 spike and record its result durably.

    Returns ``{cached_prefix_ok, budget_observed_ok, first_reexecuted_step,
    recorded_trace_id}`` (contracts/cli.md ``verify-spike``)."""
    budget = Budget(ceiling)
    runner = _CachedPrefixRunner(budget)

    runner.run(_STEPS_A)  # run A — all steps live; populate the cache
    run_b = runner.run(_STEPS_B)  # run B (resume) — cached prefix + cascade

    cached_prefix = [i for i, (kind, _) in enumerate(run_b) if kind == "cached"]
    reexecuted = [i for i, (kind, _) in enumerate(run_b) if kind == "ran"]
    first_reexecuted_step = reexecuted[0] if reexecuted else None

    cached_prefix_ok = (
        first_reexecuted_step is not None
        and cached_prefix == list(range(first_reexecuted_step))
        and all(i >= first_reexecuted_step for i in reexecuted)
    )
    budget_observed_ok = _budget_observable(runner.budget_trace, ceiling)
    decision = "accept" if (cached_prefix_ok and budget_observed_ok) else "reject"

    trace = write_trace(
        store,
        marathon_id,
        subject="workflow-spike",
        experiment_input={
            "steps_a": _STEPS_A,
            "steps_b": _STEPS_B,
            "cached_prefix": cached_prefix,
            "reexecuted": reexecuted,
            "first_reexecuted_step": first_reexecuted_step,
            "spent_total": budget.spent(),
            "remaining": budget.remaining(),
            "budget_trace": runner.budget_trace,
        },
        metric_score=len(cached_prefix) / len(_STEPS_B),
        decision=decision,
    )

    return {
        "cached_prefix_ok": cached_prefix_ok,
        "budget_observed_ok": budget_observed_ok,
        "first_reexecuted_step": first_reexecuted_step,
        "recorded_trace_id": trace.id,
    }


__all__ = ["run_spike"]
