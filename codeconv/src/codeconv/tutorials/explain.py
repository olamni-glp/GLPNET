"""Compare the actual outcome to the golden and explain it (research D8).

PURE / BRIDGE-FREE (D1). No bridge/DBOS/LM — the explanation is assembled from
the verdict + the tutorial ``.md`` prose, never LM-generated (022 discipline).
Guarded by ``test_tutorials_no_bridge.py``.

A difference is ALWAYS surfaced (never a silent pass, FR-009/010). A
``→ suspended`` outcome is a *valid* outcome wherever the golden documents it
(it compares equal to a suspended golden — not a failure).
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum

from . import outcome as _oc
from .backends import BackendKind


class VerdictKind(str, Enum):
    MATCH = "match"
    DIFFERENCE = "difference"
    NO_GOLDEN = "no_golden"


@dataclass(frozen=True)
class Diff:
    field: str  # "status" | binding name | "side_effects"
    actual: str
    golden: str


@dataclass(frozen=True)
class Verdict:
    goal: str
    kind: VerdictKind
    diffs: tuple[Diff, ...]
    explanation: str
    actual: _oc.Outcome | None
    golden: _oc.Outcome | None


def _diff_outcomes(actual: _oc.Outcome, golden: _oc.Outcome) -> list[Diff]:
    diffs: list[Diff] = []
    if actual.status != golden.status:
        diffs.append(Diff("status",
                          actual.status.value if actual.status else "(none)",
                          golden.status.value if golden.status else "(none)"))
    a = {b.name: _oc.normalize_freshvars(b.value) for b in actual.bindings}
    g = {b.name: _oc.normalize_freshvars(b.value) for b in golden.bindings}
    for name in sorted(set(a) | set(g)):
        if a.get(name) != g.get(name):
            diffs.append(Diff(name, a.get(name, "(absent)"), g.get(name, "(absent)")))
    if golden.kind == _oc.GoldenKind.SIDE_EFFECT:
        a_s = [_oc.normalize_freshvars(s) for s in actual.side_effects]
        g_s = [_oc.normalize_freshvars(s) for s in golden.side_effects]
        if a_s != g_s:
            diffs.append(Diff("side_effects", f"{len(a_s)} lines", f"{len(g_s)} lines"))
    return diffs


def explain_goal(goal: str, actual: _oc.Outcome, golden: _oc.Outcome | None, *, guide_text: str = "") -> Verdict:
    """Verdict for one goal: MATCH / DIFFERENCE / NO_GOLDEN, referencing the guide."""
    key = goal.strip().rstrip(".").strip()
    if golden is None:
        return Verdict(goal, VerdictKind.NO_GOLDEN, (),
                       "No golden block documents this goal; reporting the actual outcome only.",
                       actual, None)
    if _oc.outcomes_equal(actual, golden):
        note = ""
        if golden.status == _oc.Status.SUSPENDED:
            note = " (→ suspended is the documented, expected steady-state outcome here)"
        return Verdict(goal, VerdictKind.MATCH, (),
                       f"Actual outcome matches the tutorial golden{note}.", actual, golden)
    diffs = tuple(_diff_outcomes(actual, golden))
    detail = "; ".join(f"{d.field}: actual={d.actual!r} golden={d.golden!r}" for d in diffs)
    return Verdict(goal, VerdictKind.DIFFERENCE, diffs,
                   f"Actual outcome DIFFERS from the tutorial golden — {detail}.", actual, golden)


def explain_run(goal_outcomes, golden, *, guide_text: str = "") -> list[Verdict]:
    """Verdicts for a whole run. ``golden`` is positionally aligned to the
    executed goals (a tuple of ``Outcome | None``); index i pairs with the i-th
    ``(goal, actual)`` in ``goal_outcomes``."""
    verdicts: list[Verdict] = []
    for i, (goal, actual) in enumerate(goal_outcomes):
        g_outcome = golden[i] if (golden is not None and i < len(golden)) else None
        verdicts.append(explain_goal(goal, actual, g_outcome, guide_text=guide_text))
    return verdicts


__all__ = ["VerdictKind", "Diff", "Verdict", "explain_goal", "explain_run"]
