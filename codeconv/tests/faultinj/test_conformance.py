"""FR-024 — the conformance fixture's own output is a valid receipt. T029."""

from __future__ import annotations

from codeconv.receipts import load
from codeconv.receipts.outcome import Outcome

from . import conformance
from .conformance import _CASES, run_conformance


def test_conformance_fixture_output_is_itself_a_valid_receipt(tmp_path):
    r = run_conformance(root=tmp_path / "receipts", run_id="r")
    assert r.outcome is Outcome.PASS, "every conformance case must behave (F1-F3)"
    reloaded = load(r.verdict_pointer)  # its output IS a receipt (FR-024)
    assert reloaded.check_id == "receipts.conformance-fixture"
    assert reloaded.examined_count == reloaded.total_count  # a full fixture run, not partial


def test_every_declared_case_is_named_in_the_fixture_receipt(tmp_path):
    """Coverage is case-keyed: the receipt NAMES the cases that ran (F1)."""
    r = run_conformance(root=tmp_path / "receipts", run_id="r")
    assert list(r.examined) == list(_CASES)
    assert "BOUNDED" in r.examined and "OVERRIDDEN" in r.examined


def test_an_unexercised_declared_case_is_unread_not_a_full_green(tmp_path, monkeypatch):
    """Regression (2026-08-24 review): the fixture reached full coverage while the
    declared BOUNDED case never ran. Dropping a runner must now report UNREAD."""
    monkeypatch.delitem(conformance._RUNNERS, "BOUNDED")
    r = run_conformance(root=tmp_path / "receipts", run_id="r")
    assert r.outcome is Outcome.UNREAD, "a partial fixture run is UNREAD, never green (FR-016)"
    assert r.examined_count == len(_CASES) - 1
    assert "BOUNDED" not in r.examined
