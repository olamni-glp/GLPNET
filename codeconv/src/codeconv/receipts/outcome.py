"""The five-valued outcome classification and the non-collapse rules (FR-006/007).

Exactly one of PASS / EMPTY / UNREAD / UNSEARCHABLE / FAIL is attached to every
verdict. Only PASS and EMPTY are successful; UNREAD and UNSEARCHABLE MUST NEVER
be reported as, aggregated into, or rendered like success (FR-007). The three
"nothing found" cases never collapse (US2): EMPTY = examined-in-full-nothing-there,
UNREAD = existed-but-not-examined, UNSEARCHABLE = could-not-examine.

Implements ``specs/078-verification-receipts/data-model.md`` (Outcome) and
research decision D4.
"""

from __future__ import annotations

from enum import Enum
from typing import Iterable


class Outcome(str, Enum):
    PASS = "PASS"
    EMPTY = "EMPTY"
    UNREAD = "UNREAD"
    UNSEARCHABLE = "UNSEARCHABLE"
    FAIL = "FAIL"

    @property
    def is_successful(self) -> bool:
        """Only PASS and EMPTY are successful (FR-007)."""
        return self in (Outcome.PASS, Outcome.EMPTY)


# Worst-wins ordering for aggregate propagation (FR-009, contract C2):
#   PASS ≈ EMPTY  <  UNREAD  <  UNSEARCHABLE  <  FAIL
_RANK: dict[Outcome, int] = {
    Outcome.PASS: 0,
    Outcome.EMPTY: 0,
    Outcome.UNREAD: 1,
    Outcome.UNSEARCHABLE: 2,
    Outcome.FAIL: 3,
}


def worst(outcomes: Iterable[Outcome]) -> Outcome:
    """The aggregate outcome of a parent over its children (FR-009).

    Any UNREAD/UNSEARCHABLE/FAIL child forbids a clean parent. An empty set of
    children is a vacuous PASS. Ties at the successful rank prefer PASS when any
    child actually examined something, else EMPTY.
    """
    outcomes = list(outcomes)
    if not outcomes:
        return Outcome.PASS
    top = max(outcomes, key=lambda o: _RANK[o])
    if _RANK[top] == 0:  # every child is successful
        return Outcome.PASS if Outcome.PASS in outcomes else Outcome.EMPTY
    return top
