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
    # A genuinely successful receipt that EXAMINED the item it claims.
    emit(check_id="bash.skip-guard", area="test-harness",
         target=Target(kind="path", identity="test/run_all_tests.sh", resolved=True),
         examined_count=1, total_count=1, examined=["instance:5"], run_id="rb", root=root)
    # An UNREAD receipt claiming instance 7 must register NOTHING.
    emit(check_id="bash.corpus-scope", area="test-harness",
         target=Target(kind="path", identity="test/run_all_tests.sh", resolved=True),
         examined_count=1, total_count=4, examined=["instance:7"], run_id="rb", root=root)

    reg = Registry()
    added = absorb_receipts(root, "rb", registry=reg)
    assert added == [5], "only the successful receipt may register"
    assert 7 not in reg.registered


def test_a_tampered_empty_receipt_cannot_claim_every_instance(tmp_path):
    """KILLS the 2026-09-01 adversarial finding, and it is the sharpest one.

    A hand-written EMPTY receipt is 'successful' (EMPTY.is_successful is True) and
    examined NOTHING. Under the first implementation it could list all thirteen
    `instance:` entries and register every one of them — turning SC-001 green from
    a file anybody could write. A receipt that examined nothing cannot have
    demonstrated an injection, so `examined_count >= len(claims)` refuses it.
    """
    root = tmp_path / "receipts"
    emit(check_id="tampered", area="test-harness",
         target=Target(kind="path", identity="x", resolved=True),
         examined_count=0, total_count=0, run_id="rt", root=root)
    from codeconv.receipts import paths
    p = paths.receipt_path(root, "test-harness", "rt", "tampered")
    data = json.loads(p.read_text(encoding="utf-8"))
    assert data["outcome"] == "EMPTY"          # precondition: it IS 'successful'
    data["examined"] = [f"instance:{n}" for n in range(1, 14)]
    p.write_text(json.dumps(data), encoding="utf-8")

    reg = Registry()
    assert absorb_receipts(root, "rt", registry=reg) == []
    assert reg.examined == []
    r = sc001_receipt(run_id="rt2", root=tmp_path, registry=reg, write=False)
    assert r.outcome is Outcome.UNREAD, "a tampered receipt must not reach PASS"


def test_absorb_refuses_another_runs_receipt_and_a_filename_mismatch(tmp_path):
    """KILLS: trusting a file's LOCATION or NAME instead of its content."""
    root = tmp_path / "receipts"
    from codeconv.receipts import paths
    # (a) a receipt whose own run_id is a DIFFERENT run, sitting in this run's dir
    emit(check_id="other-run", area="test-harness",
         target=Target(kind="path", identity="x", resolved=True),
         examined_count=1, total_count=1, examined=["instance:2"],
         run_id="elsewhere", root=root)
    src = paths.receipt_path(root, "test-harness", "elsewhere", "other-run")
    dst = paths.receipt_path(root, "test-harness", "here", "other-run")
    dst.parent.mkdir(parents=True, exist_ok=True)
    dst.write_text(src.read_text(encoding="utf-8"), encoding="utf-8")

    # (b) a valid receipt for THIS run, renamed so the filename lies about it
    emit(check_id="real-name", area="test-harness",
         target=Target(kind="path", identity="x", resolved=True),
         examined_count=1, total_count=1, examined=["instance:6"],
         run_id="here", root=root)
    real = paths.receipt_path(root, "test-harness", "here", "real-name")
    liar = paths.receipt_path(root, "test-harness", "here", "different-name")
    liar.write_text(real.read_text(encoding="utf-8"), encoding="utf-8")
    real.unlink()

    reg = Registry()
    assert absorb_receipts(root, "here", registry=reg) == []
    assert reg.examined == []


def test_absorb_ignores_an_undeclared_or_malformed_claim(tmp_path):
    """KILLS: a receipt inflating coverage with instance:99 or a non-numeric claim."""
    root = tmp_path / "receipts"
    emit(check_id="bash.bogus", area="test-harness",
         target=Target(kind="path", identity="x", resolved=True),
         examined_count=4, total_count=4,
         examined=["instance:99", "instance:abc", "not-an-instance", "instance:6"],
         run_id="rc", root=root)

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
