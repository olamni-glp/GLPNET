"""SC-007 — deliberately weakening a guard makes the suite go red. T030.

Demonstrates the count guard is load-bearing: with it intact a falsified receipt is
rejected; monkeypatched to a no-op, the same falsified receipt slips through — which
is exactly the failure the real suite would catch.
"""

from __future__ import annotations

import pytest

from codeconv.receipts import ReceiptInvalid, Target, emit
from codeconv.receipts import receipt as receipt_mod


def test_intact_guard_rejects_falsified_receipt(tmp_path):
    with pytest.raises(ReceiptInvalid):
        emit(check_id="x", area="reference", target=Target("path", "/t"),
             examined_count=10, total_count=1, run_id="r", root=tmp_path, write=False)


def test_weakened_guard_lets_the_fault_through(monkeypatch, tmp_path):
    monkeypatch.setattr(receipt_mod, "validate", lambda receipt: None)
    slipped = emit(check_id="x", area="reference", target=Target("path", "/t"),
                   examined_count=10, total_count=1, run_id="r", root=tmp_path, write=False)
    # The fault the real guard catches now slips through — proving the guard matters.
    assert slipped.examined_count > slipped.total_count
