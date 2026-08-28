"""The conformance fixture — its own output IS a receipt (FR-024, contract F1-F4).

Every implementation of the receipt contract runs this fixture; instead of
*trusting* that two repos agree, each runs it and emits a receipt, so conformance
is demonstrated under the invariant this feature defines. A partial fixture run is
UNREAD (FR-016), never a silent green.

Coverage is **case-keyed, never counted**: a declared case contributes to
``examined_count`` only by running its own runner and registering itself. An
anonymous tally cannot reach full coverage while a declared case sits unexercised
— the defect this fixture exists to make impossible, found inside the fixture
itself by the 2026-08-24 adversarial review.

Task T029. Implements ``specs/078-verification-receipts/contracts/conformance-fixture.md``.
"""

from __future__ import annotations

from pathlib import Path
from typing import Callable

from codeconv.receipts import Receipt, ReceiptInvalid, Target, emit
from codeconv.receipts import bind
from codeconv.receipts.outcome import Outcome
from codeconv.receipts.override import record as record_override
from codeconv.receipts.receipt import classify


# The full outcome case-set the fixture must exercise (contract F1.1): each
# terminal outcome, plus a bounded/truncated case and an overridden case.
_CASES = ("PASS", "EMPTY", "UNREAD", "UNSEARCHABLE", "FAIL", "BOUNDED", "OVERRIDDEN")

# A case runner returns None when the case behaved, else the problem to report.
CaseRunner = Callable[["str | Path", str], "str | None"]


def _case_pass(root: str | Path, run_id: str) -> str | None:
    r = emit(check_id="conformance.pass", area="reference",
             target=Target("path", "t", resolved=True),
             examined_count=5, total_count=5, run_id=run_id, root=root, write=False)
    return None if r.outcome is Outcome.PASS else f"PASS case classified {r.outcome.value}"


def _case_empty(root: str | Path, run_id: str) -> str | None:
    r = emit(check_id="conformance.empty", area="reference",
             target=Target("path", "t", resolved=True),
             examined_count=0, total_count=0, run_id=run_id, root=root, write=False)
    return None if r.outcome is Outcome.EMPTY else f"EMPTY case classified {r.outcome.value}"


def _case_unread(root: str | Path, run_id: str) -> str | None:
    r = emit(check_id="conformance.unread", area="reference",
             target=Target("path", "t", resolved=True),
             examined_count=1, total_count=3, run_id=run_id, root=root, write=False)
    if r.outcome is not Outcome.UNREAD:
        return f"UNREAD case classified {r.outcome.value}"
    # F2 — UNREAD must state what it did not examine, not just that it fell short.
    if r.total_count is None or r.total_count - r.examined_count != 2:
        return "UNREAD case did not state its unexamined count (F2)"
    return None


def _case_unsearchable(root: str | Path, run_id: str) -> str | None:
    r = emit(check_id="conformance.unsearchable", area="reference",
             target=Target("path", "t", resolved=False, unresolved_reason="target absent"),
             examined_count=0, total_count=None, run_id=run_id, root=root, write=False)
    if r.outcome is not Outcome.UNSEARCHABLE:
        return f"UNSEARCHABLE case classified {r.outcome.value}"
    if not r.resolved_target.unresolved_reason:
        return "UNSEARCHABLE case carried no unresolved_reason (F2, FR-011)"
    return None


def _case_fail(root: str | Path, run_id: str) -> str | None:
    r = emit(check_id="conformance.fail", area="reference",
             target=Target("path", "t", resolved=True),
             examined_count=5, total_count=5, problems=["a problem"],
             run_id=run_id, root=root, write=False)
    return None if r.outcome is Outcome.FAIL else f"FAIL case classified {r.outcome.value}"


def _case_bounded(root: str | Path, run_id: str) -> str | None:
    """A bounded/truncated case: the enumeration is capped, the TOTAL survives (FR-005)."""
    overflow = 7
    items = [f"item-{i}" for i in range(bind.MAX_ENUM + overflow)]
    r = emit(check_id="conformance.bounded", area="reference",
             target=Target("path", "t", resolved=True),
             examined_count=len(items), total_count=len(items), examined=items,
             run_id=run_id, root=root, write=False)
    if not r.truncated.enumerations:
        return "BOUNDED case did not record that its enumeration was truncated (FR-005)"
    if r.truncated.dropped != overflow:
        return f"BOUNDED case recorded {r.truncated.dropped} dropped, expected {overflow}"
    if len(r.examined) != bind.MAX_ENUM:
        return f"BOUNDED case enumerated {len(r.examined)}, expected the cap {bind.MAX_ENUM}"
    if r.examined_count != len(items) or r.total_count != len(items):
        return "BOUNDED case lost its totals while capping the enumeration (FR-005)"
    if r.outcome is not Outcome.PASS:
        return f"BOUNDED case classified {r.outcome.value}, expected PASS"
    return None


def _case_overridden(root: str | Path, run_id: str) -> str | None:
    """An overridden case: the override stays VISIBLE in the receipt (FR-012)."""
    override = record_override(
        area="reference", check="conformance.overridden",
        reason="conformance fixture exercises the recorded-override case",
        briefing="contract F1 requires the fixture to drive an overridden case",
        rationale="demonstrates an override remains visible in the emitted receipt",
        acknowledged=True, expiry="2099-01-01T00:00:00+00:00",
    )
    r = emit(check_id="conformance.overridden", area="reference",
             target=Target("path", "t", resolved=True),
             examined_count=1, total_count=1, override=override.to_json(),
             run_id=run_id, root=root, write=False)
    if r.override is None:
        return "OVERRIDDEN case did not keep the override visible in the receipt (FR-012)"
    if not r.override.get("acknowledged"):
        return "OVERRIDDEN case recorded an override without acknowledgement (FR-012)"
    if not r.override.get("expiry"):
        return "OVERRIDDEN case recorded an override without a mandatory expiry (FR-012)"
    return None


# Every declared case must map to a runner here. A declared case with NO runner is
# never exercised and therefore never counted — the fixture reports UNREAD (FR-016)
# instead of a full-coverage green.
_RUNNERS: dict[str, CaseRunner] = {
    "PASS": _case_pass,
    "EMPTY": _case_empty,
    "UNREAD": _case_unread,
    "UNSEARCHABLE": _case_unsearchable,
    "FAIL": _case_fail,
    "BOUNDED": _case_bounded,
    "OVERRIDDEN": _case_overridden,
}


def run_conformance(*, root: str | Path, run_id: str,
                    fixture_check_id: str = "receipts.conformance-fixture") -> Receipt:
    """Drive the emitter through every declared case, then the F2/F3 assertions.

    Returns the fixture's OWN receipt. ``examined`` names the cases that actually
    ran, so ``examined_count`` cannot exceed what was exercised; a case that did
    not run leaves the receipt UNREAD (a partial fixture run never presents as a
    whole one — FR-016), and a case that ran but misbehaved makes it FAIL.
    """
    exercised: list[str] = []
    problems: list[str] = []

    for case in _CASES:
        runner = _RUNNERS.get(case)
        if runner is None:
            # Declared but unimplemented: NOT exercised, so NOT counted (FR-016).
            problems.append(f"declared case {case} has no runner — it was never exercised")
            continue
        try:
            failure = runner(root, run_id)
        except Exception as exc:  # a raising case still ran — it ran and misbehaved
            failure = f"{case} case raised {type(exc).__name__}: {exc}"
        exercised.append(case)
        if failure:
            problems.append(failure)

    # F2 — the three "nothing found" cases produce three DISTINCT outcomes (US2).
    nothing_found = {
        classify(Target("path", "t", resolved=True), 0, 0, []),
        classify(Target("path", "t", resolved=True), 1, 3, []),
        classify(Target("path", "t", resolved=False, unresolved_reason="x"), 0, None, []),
    }
    if len(nothing_found) != 3:
        problems.append("two of the three 'nothing found' cases collapsed (F2)")

    # F3 — a falsified count (examined > total) MUST be rejected (FR-010).
    try:
        emit(check_id="conformance.falsified", area="reference",
             target=Target("path", "t"), examined_count=10, total_count=1,
             run_id=run_id, root=root, write=False)
    except ReceiptInvalid:
        pass  # correctly rejected
    else:
        problems.append("a falsified count (examined > total) was NOT rejected (F3, FR-010)")

    # The fixture's own receipt: examined = the cases that RAN, total = all declared.
    return emit(
        check_id=fixture_check_id, area="reference",
        target=Target(kind="item-set", identity="conformance-cases", resolved=True),
        examined_count=len(exercised), total_count=len(_CASES),
        problems=problems,
        examined=exercised,
        run_id=run_id, root=root,
    )
