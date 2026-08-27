"""FR-012 — an override is recorded, scoped and EXPIRING; incomplete ones rejected. T022."""

from __future__ import annotations

from datetime import datetime, timezone

import pytest

from codeconv.receipts import OverrideInvalid, applies, record


def test_override_requires_ack_rationale_and_expiry():
    with pytest.raises(OverrideInvalid):  # no acknowledgement
        record(area="test-harness", check="T", reason="r", briefing="b",
               rationale="why", acknowledged=False, expiry="2026-09-01T00:00:00Z")
    with pytest.raises(OverrideInvalid):  # empty rationale (SC-006)
        record(area="test-harness", check="T", reason="r", briefing="b",
               rationale="   ", acknowledged=True, expiry="2026-09-01T00:00:00Z")
    with pytest.raises(OverrideInvalid):  # no expiry — no indefinite override
        record(area="test-harness", check="T", reason="r", briefing="b",
               rationale="why", acknowledged=True, expiry="")


def test_override_applies_only_within_scope_and_before_expiry():
    ov = record(area="test-harness", check="section.T", reason="glpquick.pfx absent",
                briefing="skip Section T on this host", rationale="tracked in #NNN",
                acknowledged=True, expiry="2026-09-01T00:00:00Z")
    now = datetime(2026, 8, 20, tzinfo=timezone.utc)
    assert applies(ov, "test-harness", "section.T", "glpquick.pfx absent", now=now)
    assert not applies(ov, "test-harness", "section.OTHER", "glpquick.pfx absent", now=now)
    assert not applies(ov, "other-area", "section.T", "glpquick.pfx absent", now=now)
    assert not applies(ov, "test-harness", "section.T", "glpquick.pfx absent",
                       now=datetime(2026, 9, 2, tzinfo=timezone.utc))          # expired


def test_override_does_not_authorise_a_different_refusal_from_the_same_check():
    """FR-012 — scope is area+check+REASON. Matching area+check alone would let one
    recorded override authorise every OTHER refusal that check can raise until expiry."""
    ov = record(area="test-harness", check="section.T", reason="glpquick.pfx absent",
                briefing="skip Section T on this host", rationale="tracked in #NNN",
                acknowledged=True, expiry="2026-09-01T00:00:00Z")
    now = datetime(2026, 8, 20, tzinfo=timezone.utc)
    assert applies(ov, "test-harness", "section.T", "glpquick.pfx absent", now=now)
    assert not applies(ov, "test-harness", "section.T", "the runner crashed", now=now)
    assert not applies(ov, "test-harness", "section.T", "", now=now)
