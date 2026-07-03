"""T048 [US6] — the pure exclusion filter (filter.py; FR-028/R9)."""

from __future__ import annotations

from glp_quick.rcopy.filter import ExclusionFilter, FileItem, apply_filter


def F(rel, size=100, mtime=0, hidden=False, readonly=False):
    return FileItem(rel, size, mtime, hidden, readonly)


def test_size_min_max_excludes():
    kept, out = apply_filter([F("a", 10), F("b", 500), F("c", 100)],
                             ExclusionFilter(min_size=50, max_size=200))
    assert [f.rel for f in kept] == ["c"]
    assert {f.rel for f in out} == {"a", "b"}


def test_name_glob_excludes():
    kept, _ = apply_filter([F("keep.txt"), F("skip.tmp"), F("also.log")],
                           ExclusionFilter(name_globs=("*.tmp", "*.log")))
    assert [f.rel for f in kept] == ["keep.txt"]


def test_subdir_glob_excludes():
    kept, _ = apply_filter([F("src/a.py"), F("build/b.py"), F("c.py")],
                           ExclusionFilter(subdir_globs=("build",)))
    assert [f.rel for f in kept] == ["src/a.py", "c.py"]


def test_hidden_and_readonly_attributes_excluded():
    kept, _ = apply_filter([F("a", hidden=True), F("b", readonly=True), F("c")],
                           ExclusionFilter(exclude_hidden=True, exclude_readonly=True))
    assert [f.rel for f in kept] == ["c"]


def test_mtime_window_excludes():
    kept, _ = apply_filter([F("old", mtime=100), F("new", mtime=1000)],
                           ExclusionFilter(mtime_before=500))
    assert [f.rel for f in kept] == ["new"]


def test_empty_filter_keeps_all_and_reports_nothing_filtered():
    kept, out = apply_filter([F("a"), F("b")], ExclusionFilter())
    assert len(kept) == 2 and out == []
