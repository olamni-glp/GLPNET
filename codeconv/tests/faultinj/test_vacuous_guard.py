"""Instance 12 — a preventive guard that passes cleanly on the failing case.

The spec calls this the sharpest statement of the whole problem: *a guard that
passes on the failing case is worse than no guard*, because it converts an
unknown risk into a false assurance. The original was a guard that checked a
condition which was already false, so it "passed" while the protected artifact
was destroyed anyway.

WHAT IS ACTUALLY INJECTED HERE. A guard is *vacuous* when the population it
examines is empty for a reason unrelated to the property it claims to protect.
The counts tell the two apart and nothing else does:

    examined 0 / total 0  -> EMPTY : the target genuinely contained nothing
    examined 0 / total 3  -> UNREAD: three things existed and none was looked at

A tally-based guard reports "0 problems found" for both. This test drives the
receipt path with a guard whose predicate can never fire, and asserts the
classification refuses to call it clean when there was something to examine —
and, as the negative control, that a genuinely empty population is still allowed
to be EMPTY. Without the negative control the assertion would also be satisfied
by a guard that simply never passes, which is a different useless guard.

Registers instance 12 with the SC-001 case-keyed registry (T029b).
"""

from __future__ import annotations

from codeconv.receipts import Outcome, Target, classify

from .instances import register


def _vacuous_guard(items: list[str]) -> list[str]:
    """A guard whose predicate is already false for every possible input.

    This is the defect, reproduced faithfully: it returns no problems, and it
    would return no problems no matter what it was handed.
    """
    return [x for x in items if x is None]  # nothing is None; the filter can never fire


def test_vacuous_guard_over_a_nonempty_target_is_unread_not_pass():
    items = ["a", "b", "c"]
    problems = _vacuous_guard(items)
    assert problems == [], "precondition: the guard reports clean, as the real one did"

    # The guard examined nothing, because its predicate cannot fire. Three items
    # existed. That is UNREAD, and UNREAD is not successful.
    outcome = classify(
        Target(kind="item-set", identity="three protected artifacts", resolved=True),
        examined_count=0,
        total_count=len(items),
        problems=problems,
    )
    assert outcome is Outcome.UNREAD
    assert not outcome.is_successful

    register(12, "test_vacuous_guard_over_a_nonempty_target_is_unread_not_pass: 0/3 -> UNREAD")


def test_negative_control_a_genuinely_empty_population_may_still_be_empty():
    """Without this, the test above would also pass for a guard that never passes."""
    outcome = classify(
        Target(kind="item-set", identity="no protected artifacts", resolved=True),
        examined_count=0,
        total_count=0,
        problems=[],
    )
    assert outcome is Outcome.EMPTY
    assert outcome.is_successful


def test_a_guard_that_really_examined_everything_may_pass():
    """The second negative control: full examination of a clean population is PASS."""
    outcome = classify(
        Target(kind="item-set", identity="three protected artifacts", resolved=True),
        examined_count=3,
        total_count=3,
        problems=[],
    )
    assert outcome is Outcome.PASS
    assert outcome.is_successful
