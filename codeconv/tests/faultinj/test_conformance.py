"""FR-024 — the conformance fixture's own output is a valid receipt. T029."""

from __future__ import annotations

from codeconv.receipts import load
from codeconv.receipts.outcome import Outcome

from .conformance import run_conformance


def test_conformance_fixture_output_is_itself_a_valid_receipt(tmp_path):
    r = run_conformance(root=tmp_path / "receipts", run_id="r")
    assert r.outcome is Outcome.PASS, "every conformance case must behave (F1-F3)"
    reloaded = load(r.verdict_pointer)  # its output IS a receipt (FR-024)
    assert reloaded.check_id == "receipts.conformance-fixture"
    assert reloaded.examined_count == reloaded.total_count  # a full fixture run, not partial
