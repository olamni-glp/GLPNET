"""US3.1 — a check pointed at a deliberately-removed target refuses (FR-014/015). T023."""

from __future__ import annotations

from codeconv.receipts.outcome import Outcome

from .reference_check import run_reference_check


def test_removed_target_is_unsearchable_not_clean(tmp_path):
    target = tmp_path / "t"
    target.mkdir()
    (target / "a.txt").write_text("x", encoding="utf-8")
    r = run_reference_check(root=tmp_path / "receipts", run_id="r", target_dir=target,
                            target_removed=True)
    assert r.outcome is Outcome.UNSEARCHABLE
    assert not r.outcome.is_successful, "a clean pass on a removed target must fail the suite (US3.1)"
