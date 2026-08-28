"""US3.2 — a suppressed output block is UNREAD, not 0-findings (instance 2). T024."""

from __future__ import annotations

from codeconv.receipts.outcome import Outcome

from .reference_check import run_reference_check


def test_suppressed_output_block_is_unread_not_zero(tmp_path):
    target = tmp_path / "t"
    target.mkdir()
    for n in ("a", "b", "c"):
        (target / n).write_text("x", encoding="utf-8")
    r = run_reference_check(root=tmp_path / "receipts", run_id="r", target_dir=target,
                            suppress_output=True)
    assert r.outcome is Outcome.UNREAD          # never read as "0 findings"
    assert r.total_count == 3 and r.examined_count == 0
    assert not r.outcome.is_successful
