"""The SC-001 registry is itself subject to FR-016 (T029b).

These tests exist because the 2026-08-24 adversarial review found that the
guard-weakening mutation test stayed GREEN under a no-op validator — the inverse
of SC-007. A coverage mechanism that cannot detect its own neutering is the very
thing it is supposed to prevent, so each assertion below is paired with the
mutation it kills, named in the docstring.

Every test builds its OWN :class:`Registry` rather than touching the module-level
one. A test that mutated the shared registry would make coverage depend on test
ordering, which is a second way to manufacture a green.
"""

from __future__ import annotations

import json

import pytest

from codeconv.receipts import Outcome, emit, Target

from .instances import (
    BUILDKIT,
    BY_NUMBER,
    DENOMINATOR,
    GLPNET,
    INSTANCES,
    Registry,
    UndeclaredInstance,
    absorb_receipts,
    declared,
    numbers,
    report,
    sc001_receipt,
)


def test_all_thirteen_instances_are_declared():
    """KILLS: truncating the table. SC-001's denominator is derived, not literal."""
    assert DENOMINATOR == 13
    assert numbers(INSTANCES) == list(range(1, 14))
    assert len(BY_NUMBER) == 13


def test_ownership_split_is_explicit_and_totals_thirteen():
    """KILLS: quietly reclassifying a buildkit instance as glpnet to lift coverage."""
    glpnet = numbers(declared(GLPNET))
    buildkit = numbers(declared(BUILDKIT))
    assert glpnet == [2, 5, 6, 7, 9, 12]
    assert buildkit == [1, 3, 4, 8, 10, 11, 13]
    assert len(glpnet) + len(buildkit) == DENOMINATOR
    # Every instance names a surface a reader can go and act on.
    assert all(i.surface.strip() for i in INSTANCES)


def test_empty_registry_is_unread_never_pass(tmp_path):
    """KILLS: reporting an unexercised denominator as a vacuous pass."""
    reg = Registry()
    r = sc001_receipt(run_id="r0", root=tmp_path, registry=reg, write=False)
    assert r.outcome is Outcome.UNREAD
    assert not r.outcome.is_successful
    assert r.examined_count == 0 and r.total_count == DENOMINATOR


def test_partial_coverage_is_unread_and_names_what_it_examined(tmp_path):
    """KILLS: an anonymous tally. The receipt carries NAMES, not just a count."""
    reg = Registry()
    reg.register(2, "unit")
    reg.register(9, "unit")
    r = sc001_receipt(run_id="r1", root=tmp_path, registry=reg, write=False)
    assert r.outcome is Outcome.UNREAD
    assert r.examined_count == 2 and r.total_count == DENOMINATOR
    assert r.examined == ["instance:2", "instance:9"]
    # The eleven unread are NOT recorded as skips: a skip means "deliberately not
    # examined and that is fine", which is precisely what they are not.
    assert r.skipped == [] and r.skipped_total == 0


def test_full_coverage_is_the_only_route_to_pass(tmp_path):
    """KILLS: a PASS branch reachable below 13/13."""
    reg = Registry()
    for n in BY_NUMBER:
        reg.register(n, "unit")
    r = sc001_receipt(run_id="r2", root=tmp_path, registry=reg, write=False)
    assert r.outcome is Outcome.PASS
    assert r.examined_count == DENOMINATOR == r.total_count


def test_one_missing_instance_drops_the_whole_receipt_off_pass(tmp_path):
    """KILLS: an off-by-one that lets 12/13 read as complete."""
    for missing in (1, 7, 13):
        reg = Registry()
        for n in BY_NUMBER:
            if n != missing:
                reg.register(n, "unit")
        r = sc001_receipt(run_id=f"r-miss-{missing}", root=tmp_path, registry=reg, write=False)
        assert r.outcome is Outcome.UNREAD, f"12/13 with {missing} missing must not pass"
        assert reg.unread == [missing]


def test_registering_an_undeclared_number_is_loud():
    """KILLS: inflating coverage with a typo or an invented instance."""
    reg = Registry()
    with pytest.raises(UndeclaredInstance):
        reg.register(14, "unit")
    with pytest.raises(UndeclaredInstance):
        reg.register(0, "unit")
    assert reg.examined == []


def test_registration_without_evidence_is_refused():
    """KILLS: register(n) degenerating into a bare counter increment."""
    reg = Registry()
    for bad in ("", "   ", None):
        with pytest.raises((ValueError, TypeError)):
            reg.register(2, bad)  # type: ignore[arg-type]
    assert reg.examined == []


def test_unread_are_attributed_to_a_named_owner():
    """KILLS: an UNREAD reading that says a number but not who must act."""
    reg = Registry()
    reg.register(2, "unit")
    by_owner = reg.unread_by_owner()
    assert set(by_owner) == {GLPNET, BUILDKIT}
    assert by_owner[BUILDKIT] == [1, 3, 4, 8, 10, 11, 13]
    text = report(reg)
    assert "UNREAD" in text and "owner=buildkit" in text
    assert "COMPLETE" not in text


def test_absorb_registers_only_from_a_successful_receipt(tmp_path):
    """KILLS: counting a UNREAD/FAIL receipt as proof — instance 5 by another route."""
    root = tmp_path / "receipts"
    # A successful (EMPTY) receipt from the bash emitter claiming instance 5.
    emit(check_id="bash.skip-guard", area="test-harness",
         target=Target(kind="path", identity="test/run_all_tests.sh", resolved=True),
         examined_count=0, total_count=0, examined=[], run_id="rb", root=root)
    # Patch the enumeration in place — emit() caps/validates, the claim rides in
    # `examined`, which is what a real bash emitter writes.
    from codeconv.receipts import paths
    p = paths.receipt_path(root, "test-harness", "rb", "bash.skip-guard")
    data = json.loads(p.read_text(encoding="utf-8"))
    data["examined"] = ["instance:5"]
    p.write_text(json.dumps(data), encoding="utf-8")

    # An UNREAD receipt claiming instance 7 must register NOTHING.
    emit(check_id="bash.corpus-scope", area="test-harness",
         target=Target(kind="path", identity="test/run_all_tests.sh", resolved=True),
         examined_count=1, total_count=4, examined=[], run_id="rb", root=root)
    p2 = paths.receipt_path(root, "test-harness", "rb", "bash.corpus-scope")
    d2 = json.loads(p2.read_text(encoding="utf-8"))
    assert d2["outcome"] == "UNREAD"
    d2["examined"] = ["instance:7"]
    p2.write_text(json.dumps(d2), encoding="utf-8")

    reg = Registry()
    added = absorb_receipts(root, "rb", registry=reg)
    assert added == [5], "only the successful receipt may register"
    assert 7 not in reg.registered


def test_absorb_ignores_an_undeclared_or_malformed_claim(tmp_path):
    """KILLS: a receipt inflating coverage with instance:99 or a non-numeric claim."""
    root = tmp_path / "receipts"
    emit(check_id="bash.bogus", area="test-harness",
         target=Target(kind="path", identity="x", resolved=True),
         examined_count=0, total_count=0, run_id="rc", root=root)
    from codeconv.receipts import paths
    p = paths.receipt_path(root, "test-harness", "rc", "bash.bogus")
    data = json.loads(p.read_text(encoding="utf-8"))
    data["examined"] = ["instance:99", "instance:abc", "not-an-instance", "instance:6"]
    p.write_text(json.dumps(data), encoding="utf-8")

    reg = Registry()
    assert absorb_receipts(root, "rc", registry=reg) == [6]
    assert reg.examined == [6]


def test_written_receipt_round_trips_and_stays_unread(tmp_path):
    """KILLS: a write path that loses the denominator or the names."""
    reg = Registry()
    reg.register(2, "unit")
    r = sc001_receipt(run_id="rw", root=tmp_path, registry=reg, write=True)
    data = json.loads(open(r.verdict_pointer, encoding="utf-8").read())
    assert data["outcome"] == "UNREAD"
    assert data["total_count"] == DENOMINATOR
    assert data["examined"] == ["instance:2"]
