"""SC-007 — deliberately weakening a guard makes the suite go RED. T030.

SC-007 is a statement about the *suite*, not about the emitter: weaken one guard
and the acceptance suite must go red. So the demonstration must run the suite's
own assertion against the weakened emitter and confirm that assertion FAILS.

The earlier form of this test asserted the opposite — that the falsified receipt
slipped through — which left it GREEN under a no-op validator, the exact inverse
of SC-007 (2026-08-24 adversarial review). Regression-guarded below.
"""

from __future__ import annotations

import pytest

from codeconv.receipts import ReceiptInvalid, Target, emit
from codeconv.receipts import receipt as receipt_mod
from codeconv.receipts.outcome import Outcome

from .conformance import run_conformance


def _acceptance_assertion(tmp_path) -> None:
    """The assertion the real suite makes: a falsified receipt MUST be rejected.

    Intact, this passes. It is called directly by the intact-guard test and again
    under a weakened guard, where it must fail.
    """
    with pytest.raises(ReceiptInvalid):
        emit(check_id="x", area="reference", target=Target("path", "/t"),
             examined_count=10, total_count=1, run_id="r", root=tmp_path, write=False)


def test_intact_guard_rejects_falsified_receipt(tmp_path):
    _acceptance_assertion(tmp_path)


def test_weakened_guard_makes_the_suite_go_red(tmp_path, monkeypatch):
    """Weaken the count guard to a no-op: the suite's assertion must now FAIL."""
    monkeypatch.setattr(receipt_mod, "validate", lambda receipt: None)
    with pytest.raises(pytest.fail.Exception):
        _acceptance_assertion(tmp_path)


def test_weakened_guard_also_makes_the_conformance_fixture_go_red(tmp_path, monkeypatch):
    """The same weakening must break the fixture's F3 assertion, not slip past it."""
    monkeypatch.setattr(receipt_mod, "validate", lambda receipt: None)
    r = run_conformance(root=tmp_path / "receipts", run_id="r")
    assert r.outcome is not Outcome.PASS, (
        "a no-op validator must not leave the conformance fixture PASSing (SC-007)"
    )
