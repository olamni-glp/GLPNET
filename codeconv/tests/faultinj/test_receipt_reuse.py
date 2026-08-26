"""Regression cover for the 2026-08-24 codexreview product findings.

Each test names the finding it closes. Every one of these passed silently before
the fix — they are the ways 078's own module could pass without proving it ran.
"""

from __future__ import annotations

import json
import os
from pathlib import Path

import pytest

from codeconv.receipts import (
    ReceiptInvalid, Skip, Target, UndeclaredRun, Verdict, VerdictRefused,
    declare_expected, emit, missing_checks, read,
)
from codeconv.receipts.outcome import Outcome


# --- consumer.py: a receipt binds to exactly ONE verdict (FR-002) -------------

def test_another_checks_pass_receipt_is_refused(tmp_path):
    root = tmp_path / "receipts"
    other = emit(check_id="other.check", area="reference", target=Target("path", "t"),
                 examined_count=3, total_count=3, run_id="r", root=root)
    assert other.outcome is Outcome.PASS
    with pytest.raises(VerdictRefused, match="exactly one verdict"):
        read(Verdict(check_id="mine.check", area="reference", run_id="r",
                     receipt_pointer=other.verdict_pointer))


def test_another_areas_pass_receipt_is_refused(tmp_path):
    root = tmp_path / "receipts"
    other = emit(check_id="same.check", area="reference", target=Target("path", "t"),
                 examined_count=3, total_count=3, run_id="r", root=root)
    with pytest.raises(VerdictRefused, match="exactly one verdict"):
        read(Verdict(check_id="same.check", area="build-gate", run_id="r",
                     receipt_pointer=other.verdict_pointer))


def test_a_prior_runs_pass_receipt_is_refused(tmp_path):
    root = tmp_path / "receipts"
    old = emit(check_id="same.check", area="reference", target=Target("path", "t"),
               examined_count=3, total_count=3, run_id="run-1", root=root)
    # Same check, same area, valid, conforming — just produced by a DIFFERENT run.
    with pytest.raises(VerdictRefused, match="not run 'run-2'"):
        read(Verdict(check_id="same.check", area="reference",
                     receipt_pointer=old.verdict_pointer, run_id="run-2"))
    reading = read(Verdict(check_id="same.check", area="reference",
                           receipt_pointer=old.verdict_pointer, run_id="run-1"))
    assert reading.successful is True


def test_a_malformed_shape_is_a_named_refusal_not_a_crash(tmp_path):
    root = tmp_path / "receipts"
    r = emit(check_id="c", area="reference", target=Target("path", "t"),
             examined_count=1, total_count=1, run_id="r", root=root)
    p = Path(r.verdict_pointer)
    data = json.loads(p.read_text(encoding="utf-8"))
    data["resolved_target"] = "a string where an object belongs"
    p.write_text(json.dumps(data), encoding="utf-8")
    # Must be the named UNREAD refusal (C1.2), never a TypeError escaping load().
    with pytest.raises(VerdictRefused, match="malformed/invalid receipt"):
        read(Verdict(check_id="c", area="reference", run_id="r", receipt_pointer=str(p)))


def test_non_adopted_area_keeps_its_verdict_behind_the_marker(tmp_path):
    """C1 — non-adoption marks a verdict, it does not DISABLE it."""
    root = tmp_path / "receipts"
    r = emit(check_id="c", area="reference", target=Target("path", "t"),
             examined_count=2, total_count=2, run_id="r", root=root)
    adoption = {"build-gate": "adopted", "coop": "adopted", "roadmap-sync": "adopted",
                "test-harness": "adopted", "reference": "non-adopted"}
    reading = read(Verdict(check_id="c", area="reference", run_id="r",
                           receipt_pointer=r.verdict_pointer), adoption=adoption)
    assert reading.non_adoption is True
    assert reading.successful is False          # never counts as a pass
    assert reading.receipt is not None          # but the verdict is NOT discarded
    assert reading.outcome is Outcome.PASS      # its real outcome survives the marker


# --- receipt.py: reconciliation and an earned PASS ---------------------------

def test_examined_plus_skipped_may_not_exceed_total(tmp_path):
    """FR-010 — 5 examined + 1 skipped from a 5-item target is six outcomes from five."""
    with pytest.raises(ReceiptInvalid, match="self-inconsistent"):
        emit(check_id="c", area="reference", target=Target("path", "t"),
             examined_count=5, total_count=5, skipped=[Skip("f", "no reader")],
             run_id="r", root=tmp_path, write=False)
    # The honest form reconciles: 4 examined + 1 skipped = 5.
    ok = emit(check_id="c", area="reference", target=Target("path", "t"),
              examined_count=4, total_count=5, skipped=[Skip("f", "no reader")],
              run_id="r", root=tmp_path, write=False)
    assert ok.outcome is Outcome.UNREAD


def test_pass_with_an_unresolved_target_is_rejected(tmp_path):
    from codeconv.receipts.receipt import Receipt, Truncation, validate
    forged = Receipt(
        schema_version="buildkit-draft-0", contract_version="buildkit-draft-0",
        check_id="c", area="reference", run_id="r",
        resolved_target=Target("path", "t", resolved=False, unresolved_reason="absent"),
        outcome=Outcome.PASS, examined_count=0, total_count=None,
        skipped=[], skipped_total=0, examined=[], truncated=Truncation(),
        ran_at="2026-08-25T00:00:00+00:00", verdict_pointer="p",
    )
    with pytest.raises(ReceiptInvalid, match="PASS requires"):
        validate(forged)


# --- manifest.py: the expected set and what "ran" means ----------------------

def test_an_empty_or_foreign_expected_set_refuses(tmp_path):
    from codeconv.receipts import paths
    root = tmp_path / "receipts"
    path = paths.expected_set_path(root, "r")
    path.parent.mkdir(parents=True, exist_ok=True)

    path.write_text("{}", encoding="utf-8")
    with pytest.raises(UndeclaredRun):
        missing_checks(root, "r")

    path.write_text(json.dumps({"run_id": "r", "expected_checks": []}), encoding="utf-8")
    with pytest.raises(UndeclaredRun, match="declares no checks"):
        missing_checks(root, "r")

    path.write_text(json.dumps({"run_id": "OTHER", "expected_checks": ["c"]}), encoding="utf-8")
    with pytest.raises(UndeclaredRun, match="declares run 'OTHER'"):
        missing_checks(root, "r")


def test_a_correctly_named_file_is_not_proof_the_check_ran(tmp_path):
    """FR-001/013 — reconciliation must LOAD the sidecar, not trust its filename."""
    from codeconv.receipts import paths
    root = tmp_path / "receipts"
    declare_expected(root, "r", ["ghost.check"])
    forged = paths.receipt_path(root, "reference", "r", "ghost.check")
    forged.parent.mkdir(parents=True, exist_ok=True)
    forged.write_text("not even json", encoding="utf-8")
    assert missing_checks(root, "r") == ["ghost.check"]


def test_another_runs_receipt_in_this_runs_dir_is_not_proof(tmp_path):
    from codeconv.receipts import paths
    root = tmp_path / "receipts"
    declare_expected(root, "r", ["c"])
    real = emit(check_id="c", area="reference", target=Target("path", "t"),
                examined_count=1, total_count=1, run_id="OTHER-RUN", root=root)
    # Move the foreign receipt into this run's dir under the right name.
    dest = paths.receipt_path(root, "reference", "r", "c")
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(Path(real.verdict_pointer).read_text(encoding="utf-8"), encoding="utf-8")
    assert missing_checks(root, "r") == ["c"]


# --- the four HIGH findings from re-review 20260826T084941Z --------------------

def test_a_receipt_backed_verdict_must_declare_its_run(tmp_path):
    """HIGH-1 — an OPTIONAL run binding is not a binding."""
    r = emit(check_id="c", area="reference", target=Target("path", "t"),
             examined_count=1, total_count=1, run_id="run-1", root=tmp_path / "receipts")
    with pytest.raises(VerdictRefused, match="must declare its run_id"):
        read(Verdict(check_id="c", area="reference", receipt_pointer=r.verdict_pointer))


def test_loaded_empty_with_nonzero_counts_is_rejected(tmp_path):
    """HIGH-2 — EMPTY means the target contained NOTHING; 5/5 is not empty."""
    from codeconv.receipts.receipt import Receipt, Truncation, validate
    forged = Receipt(
        schema_version="buildkit-draft-0", contract_version="buildkit-draft-0",
        check_id="c", area="reference", run_id="r",
        resolved_target=Target("path", "t", resolved=True),
        outcome=Outcome.EMPTY, examined_count=5, total_count=5,
        skipped=[], skipped_total=0, examined=[], truncated=Truncation(),
        ran_at="2026-08-26T00:00:00+00:00", verdict_pointer="p",
    )
    with pytest.raises(ReceiptInvalid, match="examined==total==0"):
        validate(forged)


def test_negative_counts_are_rejected(tmp_path):
    """HIGH-3 — a negative count is impossible, not merely small."""
    with pytest.raises(ReceiptInvalid, match="negative count"):
        emit(check_id="c", area="reference", target=Target("path", "t"),
             examined_count=-1, total_count=5, run_id="r", root=tmp_path, write=False)


def test_receipt_paths_cannot_escape_the_root(tmp_path):
    """HIGH-4 — an attacker-chosen receipt location is not evidence."""
    from codeconv.receipts.paths import UnsafeReceiptPath, receipt_path
    for bad in ("../../etc", "..", "a/b"):
        with pytest.raises(UnsafeReceiptPath):
            receipt_path(tmp_path, bad, "run", "check")
    with pytest.raises(UnsafeReceiptPath):
        receipt_path(tmp_path, "reference", "run", "../../../evil")
    ok = receipt_path(tmp_path, "reference", "run", "check")
    assert tmp_path.resolve() in ok.resolve().parents


def test_a_non_string_run_id_is_refused_not_coerced(tmp_path):
    """Re-review 20260826T102453Z HIGH — str(0)=='0' would reopen prior-run reuse."""
    from codeconv.receipts.paths import UnsafeReceiptPath
    with pytest.raises(UnsafeReceiptPath, match="must be a string"):
        emit(check_id="c", area="reference", target=Target("path", "t"),
             examined_count=1, total_count=1, run_id=0, root=tmp_path / "receipts")
    r = emit(check_id="c", area="reference", target=Target("path", "t"),
             examined_count=1, total_count=1, run_id="run-1", root=tmp_path / "receipts")
    # A non-string on the CONSUMER side must not satisfy the run binding either.
    with pytest.raises(VerdictRefused, match="must declare its run_id"):
        read(Verdict(check_id="c", area="reference", run_id=0,
                     receipt_pointer=r.verdict_pointer))


# --- paths.py: the _confine containment backstop (session-9 follow-up) --------
#
# `_safe_component` blocks every escape reachable from this module's own public
# callers, so `_confine`'s containment check cannot be exercised through
# `receipt_path` / `expected_set_path` — which is exactly why it survived
# mutation testing on 2026-08-26 and was published as NOT-VERIFIED. A backstop
# whose contract is never asserted is indistinguishable from a no-op, and this
# feature exists to stop unverified claims. So it is tested at its OWN boundary.

def test_confine_refuses_a_component_that_escapes_the_root(tmp_path):
    """The backstop's contract: parts that climb out of the root are refused."""
    from codeconv.receipts.paths import UnsafeReceiptPath, _confine
    for parts in ((os.pardir,), (os.pardir, os.pardir, "evil"), ("a", os.pardir, os.pardir)):
        with pytest.raises(UnsafeReceiptPath, match="escapes the receipts root"):
            _confine(tmp_path, *parts)


def test_confine_admits_paths_beneath_the_root(tmp_path):
    """Positive control: containment must not reject legitimate descendants."""
    from codeconv.receipts.paths import _confine
    kept = _confine(tmp_path, "area", "run", "check.receipt.json")
    assert tmp_path.resolve() in kept.resolve().parents


def test_confine_admits_the_root_itself(tmp_path):
    """The root is contained BY the root — it is not in its own `.parents`."""
    from codeconv.receipts.paths import _confine
    assert _confine(tmp_path).resolve() == tmp_path.resolve()


def test_confine_refuses_a_path_it_cannot_resolve(tmp_path, monkeypatch):
    """Unresolvable ⇒ containment is unproven, and unproven must not pass."""
    from codeconv.receipts import paths as paths_mod

    def boom(self, *a, **kw):
        raise OSError("resolution refused")

    monkeypatch.setattr(Path, "resolve", boom)
    with pytest.raises(paths_mod.UnsafeReceiptPath, match="could not be resolved"):
        paths_mod._confine(tmp_path, "area")
