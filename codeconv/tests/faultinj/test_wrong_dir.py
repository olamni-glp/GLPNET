"""US3.4 — a check run from the wrong location is caught before any verdict (instance 9). T026."""

from __future__ import annotations

from codeconv.receipts.outcome import Outcome

from .instances import register
from .reference_check import run_reference_check


def test_wrong_working_location_detected_as_target_mismatch(tmp_path):
    actual = tmp_path / "wrong"
    actual.mkdir()
    (actual / "a").write_text("x", encoding="utf-8")
    r = run_reference_check(root=tmp_path / "receipts", run_id="r", target_dir=actual,
                            intended_identity=str(tmp_path / "intended"))
    assert r.outcome is Outcome.UNSEARCHABLE
    assert "mismatch" in (r.resolved_target.unresolved_reason or "")
    assert not r.outcome.is_successful
    register(9, "test_wrong_working_location_detected_as_target_mismatch: UNSEARCHABLE on mismatch")
