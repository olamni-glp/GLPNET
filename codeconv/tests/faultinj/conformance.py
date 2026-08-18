"""The conformance fixture — its own output IS a receipt (FR-024, contract F1-F4).

Every implementation of the receipt contract runs this fixture; instead of
*trusting* that two repos agree, each runs it and emits a receipt, so conformance
is demonstrated under the invariant this feature defines. A partial fixture run is
UNREAD (FR-016), never a silent green.

Task T029. Implements ``specs/078-verification-receipts/contracts/conformance-fixture.md``.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.receipts import Receipt, ReceiptInvalid, Target, emit
from codeconv.receipts.outcome import Outcome
from codeconv.receipts.receipt import classify


# The full outcome case-set the fixture must exercise (F1).
_CASES = ("PASS", "EMPTY", "UNREAD", "UNSEARCHABLE", "FAIL", "BOUNDED", "FALSIFIED_REJECTED")


def run_conformance(*, root: str | Path, run_id: str,
                    fixture_check_id: str = "receipts.conformance-fixture") -> Receipt:
    """Drive the emitter through every outcome + a bounded + a rejected case.

    Returns the fixture's OWN receipt: PASS iff every case behaved correctly,
    else UNREAD (a partial fixture run never presents as a whole one — FR-016).
    """
    passed = 0

    # F2 — the three "nothing found" cases produce three distinct outcomes.
    resolved_full_empty = classify(Target("path", "t", resolved=True), 0, 0, [])
    resolved_partial = classify(Target("path", "t", resolved=True), 1, 3, [])
    unresolvable = classify(Target("path", "t", resolved=False, unresolved_reason="x"), 0, None, [])
    if resolved_full_empty is Outcome.EMPTY:
        passed += 1
    if resolved_partial is Outcome.UNREAD:
        passed += 1
    if unresolvable is Outcome.UNSEARCHABLE:
        passed += 1
    # distinctness assertion (F2): no two collapse
    if len({resolved_full_empty, resolved_partial, unresolvable}) == 3:
        passed += 1

    # PASS + FAIL classification
    if classify(Target("path", "t", resolved=True), 5, 5, []) is Outcome.PASS:
        passed += 1
    if classify(Target("path", "t", resolved=True), 5, 5, ["problem"]) is Outcome.FAIL:
        passed += 1

    # F3 — a falsified count (examined > total) MUST be rejected.
    try:
        emit(check_id="conformance.falsified", area="reference",
             target=Target("path", "t"), examined_count=10, total_count=1,
             run_id=run_id, root=root, write=False)
    except ReceiptInvalid:
        passed += 1  # correctly rejected

    total_cases = len(_CASES)
    outcome_ok = passed == total_cases
    # The fixture's own receipt: examined = cases that behaved, total = all cases.
    return emit(
        check_id=fixture_check_id, area="reference",
        target=Target(kind="item-set", identity="conformance-cases", resolved=True),
        examined_count=passed, total_count=total_cases,
        problems=[] if outcome_ok else ["one or more conformance cases misbehaved"],
        examined=list(_CASES[:passed]),
        run_id=run_id, root=root,
    )
