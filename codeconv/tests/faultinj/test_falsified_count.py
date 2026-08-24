"""US3.5 — an examined-count falsified to exceed the target is detected (FR-010). T027."""

from __future__ import annotations

import pytest

from codeconv.receipts import ReceiptInvalid

from .reference_check import run_reference_check


def test_falsified_examined_count_is_rejected(tmp_path):
    target = tmp_path / "t"
    target.mkdir()
    (target / "a").write_text("x", encoding="utf-8")
    with pytest.raises(ReceiptInvalid):
        run_reference_check(root=tmp_path / "receipts", run_id="r", target_dir=target,
                            falsify_count=True)
