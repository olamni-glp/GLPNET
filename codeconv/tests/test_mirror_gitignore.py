"""Unit tests for the internal gitignore matcher — Feature 016 / spec
Amendment 1 FR-043/FR-044.

Pure / no-bridge: `GitignoreSpec` is filesystem-free.
"""

from __future__ import annotations

from codeconv.tools.mirror.gitignore import GitignoreSpec


def _spec(*lines: str) -> GitignoreSpec:
    return GitignoreSpec.from_lines(list(lines))


def test_unanchored_name_matches_at_any_depth() -> None:
    s = _spec("build/")
    assert s.match("build", is_dir=True)
    assert s.match("lib/build", is_dir=True)
    assert s.match("a/b/build", is_dir=True)
    # dir-only: a FILE named build is NOT matched.
    assert not s.match("lib/build", is_dir=False)


def test_anchored_pattern_only_at_root() -> None:
    s = _spec("/build/")
    assert s.match("build", is_dir=True)
    assert not s.match("lib/build", is_dir=True)  # not anchored here


def test_double_star_spans_segments() -> None:
    s = _spec("lib/**/gen/")
    assert s.match("lib/gen", is_dir=True)
    assert s.match("lib/a/b/gen", is_dir=True)
    assert not s.match("other/gen", is_dir=True)


def test_single_star_within_segment_only() -> None:
    s = _spec("*.tmp")
    assert s.match("a.tmp", is_dir=False)
    assert s.match("lib/x.tmp", is_dir=False)
    # '*' does not cross '/': 'a/b.tmp' still matches by basename rule,
    # but 'a.tmp/c' (a dir segment) should not be matched as the file.
    assert not s.match("a.tmpx", is_dir=False)


def test_question_mark_single_char() -> None:
    s = _spec("v?.dart")
    assert s.match("v1.dart", is_dir=False)
    assert not s.match("v12.dart", is_dir=False)


def test_negation_last_match_wins() -> None:
    # Last matching rule decides (gitignore order).
    s = _spec("*.tmp", "!keep.tmp")
    assert s.match("a.tmp", is_dir=False)
    assert not s.match("keep.tmp", is_dir=False)  # re-included by !rule
    # Reversed order → the later *.tmp re-excludes keep.tmp.
    s2 = _spec("!keep.tmp", "*.tmp")
    assert s2.match("keep.tmp", is_dir=False)
    # Same with a dir rule pair.
    s3 = _spec("build/", "!build/")
    assert not s3.match("lib/build", is_dir=True)


def test_comments_and_blanks_ignored() -> None:
    s = _spec("", "# a comment", "  ", "build/")
    assert s.match("build", is_dir=True)
    assert not s.match("src", is_dir=True)


def test_empty_spec_is_falsey_and_matches_nothing() -> None:
    s = _spec("", "# only comments")
    assert not s
    assert not s.match("anything", is_dir=True)


def test_negative_control_unrelated_path() -> None:
    s = _spec("build/", "*.tmp")
    assert not s.match("lib/runtime/heap.dart", is_dir=False)
    assert not s.match("lib/runtime", is_dir=True)
