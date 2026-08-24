"""US2 — EMPTY, UNREAD and UNSEARCHABLE never collapse (FR-006/007/009). Task T018."""

from __future__ import annotations

from codeconv.receipts import Skip, Target, aggregate, emit
from codeconv.receipts.outcome import Outcome

RUN = "run-us2"


def test_three_nothing_found_are_distinct_and_only_empty_passes(tmp_path):
    empty = emit(check_id="e", area="reference", target=Target("path", "/e"),
                 examined_count=0, total_count=0, run_id=RUN, root=tmp_path)
    unread = emit(check_id="u", area="reference", target=Target("path", "/u"),
                  examined_count=2, total_count=9, run_id=RUN, root=tmp_path)
    uns = emit(check_id="s", area="reference",
               target=Target("path", "/s", resolved=False, unresolved_reason="gone"),
               examined_count=0, total_count=None, run_id=RUN, root=tmp_path)
    assert {empty.outcome, unread.outcome, uns.outcome} == {
        Outcome.EMPTY, Outcome.UNREAD, Outcome.UNSEARCHABLE}
    assert empty.outcome.is_successful
    assert not unread.outcome.is_successful
    assert not uns.outcome.is_successful


def test_unread_states_how_many_left_unexamined(tmp_path):
    r = emit(check_id="u2", area="reference", target=Target("path", "/u"),
             examined_count=3, total_count=10, run_id=RUN, root=tmp_path)
    assert r.outcome is Outcome.UNREAD
    assert r.total_count - r.examined_count == 7


def test_skipped_items_counted_and_not_a_clean_pass_on_their_behalf(tmp_path):
    # one item skipped (not examined) → examined < total → UNREAD (instance 5)
    r = emit(check_id="sk", area="reference", target=Target("path", "/t"),
             examined_count=4, total_count=5,
             skipped=[Skip("linkX", "unsupported platform")], run_id=RUN, root=tmp_path)
    assert r.skipped_total == 1 and r.skipped[0].reason == "unsupported platform"
    assert r.outcome is Outcome.UNREAD
    assert not r.outcome.is_successful


def test_partial_run_states_both_portions(tmp_path):
    r = emit(check_id="pr", area="reference", target=Target("path", "/t"),
             examined_count=6, total_count=20, run_id=RUN, root=tmp_path)
    assert r.examined_count == 6 and r.total_count == 20  # partial never presents as whole


def test_aggregate_propagation_worst_wins():
    assert aggregate([Outcome.PASS, Outcome.EMPTY]) is Outcome.PASS
    assert aggregate([Outcome.EMPTY, Outcome.EMPTY]) is Outcome.EMPTY
    assert aggregate([Outcome.PASS, Outcome.UNREAD]) is Outcome.UNREAD
    assert aggregate([Outcome.EMPTY, Outcome.UNSEARCHABLE]) is Outcome.UNSEARCHABLE
    assert aggregate([Outcome.PASS, Outcome.FAIL, Outcome.UNREAD]) is Outcome.FAIL
