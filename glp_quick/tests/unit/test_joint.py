"""T028 [US4] — joint live-edit pinpoint changes (joint.py; FR-012/013/014)."""

from __future__ import annotations

from glp_quick.terminal import joint
from glp_quick.terminal.pages import Page

GRID = "line one\nline two\nline three"


def _page(joint_on=True):
    return Page("P", owner="me", text=GRID, joint=joint_on)


def test_pinpoint_rejected_when_joint_off():
    pg = _page(joint_on=False)
    r = joint.apply_pinpoint(pg, 0, 0, 1, 4, "XXXX", "transient")
    assert not r.ok and r.reason == "joint_off"
    assert pg.text == GRID  # unchanged


def test_pinpoint_applied_when_joint_on_saves_original():
    pg = _page()
    r = joint.apply_pinpoint(pg, 0, 0, 1, 4, "XXXX", "permanent")
    assert r.ok
    assert pg.text.split("\n")[0] == "XXXX one"
    assert pg.saved_regions[(0, 0, 1, 4)]["original"] == "line"  # original recoverable (FR-013)


def test_transient_pinpoint_dismiss_restores_original():
    pg = _page()
    joint.apply_pinpoint(pg, 1, 0, 1, 4, "NOTE", "transient")
    assert pg.text.split("\n")[1] == "NOTE two"
    r = joint.undo_pin(pg)
    assert r.ok
    assert pg.text == GRID  # restored to the saved original
    assert (1, 0, 1, 4) not in pg.saved_regions


def test_permanent_pinpoint_persists_on_dismiss():
    pg = _page()
    joint.apply_pinpoint(pg, 0, 0, 1, 4, "PERM", "permanent")
    r = joint.undo_pin(pg)
    assert not r.ok and r.reason == "no_transient"       # nothing transient to dismiss
    assert pg.text.split("\n")[0] == "PERM one"          # overwrite remains
    assert pg.saved_regions[(0, 0, 1, 4)]["original"] == "line"  # still recoverable


def test_overlap_last_writer_wins_original_recoverable():
    pg = _page()
    joint.apply_pinpoint(pg, 0, 0, 1, 4, "AAAA", "permanent")
    joint.apply_pinpoint(pg, 0, 0, 1, 4, "BBBB", "permanent")  # same region, later write
    assert pg.text.split("\n")[0] == "BBBB one"                # last write wins
    assert pg.saved_regions[(0, 0, 1, 4)]["original"] == "line"  # first (true) original preserved


def test_out_of_bounds_rejected():
    pg = _page()
    assert joint.apply_pinpoint(pg, 5, 0, 1, 4, "X", "transient").reason == "out_of_bounds"  # row past end
    assert joint.apply_pinpoint(pg, 0, 50, 1, 4, "X", "transient").reason == "out_of_bounds"  # col past width
    assert joint.apply_pinpoint(pg, 0, 0, 0, 4, "X", "transient").reason == "out_of_bounds"   # zero height
    assert pg.text == GRID


def test_closed_page_rejected():
    assert joint.apply_pinpoint(None, 0, 0, 1, 1, "X", "transient").reason == "closed"
    assert joint.undo_pin(None).reason == "closed"


def test_undo_dismisses_most_recent_transient_only():
    pg = _page()
    joint.apply_pinpoint(pg, 0, 0, 1, 4, "AAAA", "transient")
    joint.apply_pinpoint(pg, 1, 0, 1, 4, "BBBB", "transient")
    joint.undo_pin(pg)  # dismisses row 1 (most recent) first
    assert pg.text.split("\n")[1] == "line two" and pg.text.split("\n")[0] == "AAAA one"
    joint.undo_pin(pg)  # then row 0
    assert pg.text == GRID


def test_block_dims():
    assert joint.block_dims("abc") == (1, 3)
    assert joint.block_dims("ab\ncdef\ng") == (3, 4)
