"""US1 — a check proves it ran (FR-001/002/003/008/011). Task T013.

A green must be earned: the receipt names the resolved target and a real
examined-count; an unresolvable target is never clean; a verdict without a receipt
is refused; a zero is explicit and attributed.
"""

from __future__ import annotations

import pytest

from codeconv.receipts import Target, Verdict, VerdictRefused, emit, load, read
from codeconv.receipts.outcome import Outcome

RUN = "run-us1"


def test_clean_full_exam_names_target_and_count(tmp_path):
    r = emit(check_id="c1", area="reference",
             target=Target("path", "/x/target", requested="/x/target"),
             examined_count=5, total_count=5, run_id=RUN, root=tmp_path)
    assert r.outcome is Outcome.PASS
    assert r.resolved_target.identity == "/x/target"
    assert r.examined_count == 5
    reloaded = load(r.verdict_pointer)  # the receipt is a real sidecar file (FR-022)
    assert reloaded.check_id == "c1" and reloaded.examined_count == 5


def test_unresolvable_target_is_not_clean(tmp_path):
    r = emit(check_id="c2", area="reference",
             target=Target("path", "/x/missing", resolved=False, unresolved_reason="absent"),
             examined_count=0, total_count=None, run_id=RUN, root=tmp_path)
    assert r.outcome is Outcome.UNSEARCHABLE
    assert not r.outcome.is_successful


def test_verdict_without_receipt_is_refused():
    with pytest.raises(VerdictRefused):
        read(Verdict(check_id="c3", area="reference", receipt_pointer=None))


def test_zero_examined_is_explicit_empty_not_bare_clean(tmp_path):
    r = emit(check_id="c4", area="reference", target=Target("path", "/x/empty"),
             examined_count=0, total_count=0, run_id=RUN, root=tmp_path)
    assert r.outcome is Outcome.EMPTY      # attributed, never a bare "clean"/"0 findings"
    assert r.examined_count == 0 and r.total_count == 0
    assert r.outcome.is_successful         # legitimate emptiness remains a pass
