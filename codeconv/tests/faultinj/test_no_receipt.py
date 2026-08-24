"""US3.3 — a consumer fed a verdict with no receipt refuses it (FR-008). T025."""

from __future__ import annotations

import pytest

from codeconv.receipts import Verdict, VerdictRefused, read


def test_verdict_with_no_receipt_is_refused():
    with pytest.raises(VerdictRefused):
        read(Verdict(check_id="x", area="reference", receipt_pointer=None))


def test_verdict_pointing_at_absent_file_is_refused(tmp_path):
    with pytest.raises(VerdictRefused):
        read(Verdict(check_id="x", area="reference",
                     receipt_pointer=str(tmp_path / "nope.receipt.json")))
