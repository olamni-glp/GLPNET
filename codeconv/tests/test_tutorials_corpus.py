"""Corpus discovery tests — US1 (T007/T008/T009).

Pure filesystem walk against the shaped fixture corpus; no bridge, no DBOS,
no REPL. Covers full-catalog coverage + deterministic order (SC-002), the
empty-chapter indicator (FR-008), and non-standard-dir skipping (FR-011).
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tutorials import corpus as C

REPO_ROOT = Path(__file__).resolve().parents[2]
FIXTURE = Path(__file__).resolve().parent / "fixtures" / "tutorials_corpus"

# Every recognized .glp under the fixture (the SC-002 coverage target).
ALL_SCRIPTS = {
    "ch-01-ex-01-hello.glp",
    "ch-01-ex-02-producer.glp",
    "ch-01-ex-02-consumer.glp",
    "ch-02-ex-01-corrected.glp",
    "ch-02-ex-01-failing.glp",
    "ch-02-ex-03-nodesc.glp",
    "ch-07-ex-01-modules.glp",
}


def _load() -> C.Corpus:
    return C.load_corpus(FIXTURE, repo_root=REPO_ROOT)


# --- T007: full-catalog discovery, 100% coverage, deterministic order ------ #
def test_all_chapters_discovered_sorted_by_id() -> None:
    corpus = _load()
    assert [c.id for c in corpus.chapters] == ["ch01", "ch02", "ch07", "ch08"]


def test_every_script_appears_once_full_coverage() -> None:
    corpus = _load()
    names = [
        s.name
        for c in corpus.chapters
        for e in c.exercises
        for s in e.scripts
    ]
    assert sorted(names) == sorted(ALL_SCRIPTS)  # 100% coverage, no dupes (SC-002)


def test_exercises_sorted_by_number_scripts_by_name() -> None:
    corpus = _load()
    ch02 = next(c for c in corpus.chapters if c.id == "ch02")
    assert [e.number for e in ch02.exercises] == ["01", "03"]  # ex-09 dropped (no glp)
    ex01 = ch02.exercises[0]
    assert [s.name for s in ex01.scripts] == [
        "ch-02-ex-01-corrected.glp",
        "ch-02-ex-01-failing.glp",
    ]


def test_walk_is_deterministic_and_idempotent() -> None:
    first, second = _load(), _load()
    assert [c.id for c in first.chapters] == [c.id for c in second.chapters]
    assert first.warnings == second.warnings


def test_duplicate_exercise_number_grouped_under_owning_chapter() -> None:
    corpus = _load()
    owners = {
        c.id for c in corpus.chapters for e in c.exercises if e.number == "01"
    }
    # exercise-01 exists under ch01, ch02 and ch07 — each kept under its chapter.
    assert {"ch01", "ch02", "ch07"} <= owners


# --- T008: empty (no-exercise) chapter included with explicit indicator ---- #
def test_empty_chapter_present_and_marked() -> None:
    corpus = _load()
    ch08 = next(c for c in corpus.chapters if c.id == "ch08")
    assert ch08.is_empty is True
    assert ch08.exercises == ()
    assert ch08.title == "Planned Fixture"  # still titled, not omitted


# --- T009: non-standard dir skipped with a warning, listing not aborted ---- #
def test_non_standard_dir_warned_not_absorbed() -> None:
    corpus = _load()
    assert "skipped non-standard dir: ch01/spec-rev-eng-input" in corpus.warnings
    assert "skipped non-standard dir: ch08/spec-rev-eng-input" in corpus.warnings


def test_exercise_without_scripts_warned_not_rendered() -> None:
    corpus = _load()
    assert "skipped exercise with no scripts: ch02/exercise-09" in corpus.warnings
    ch02 = next(c for c in corpus.chapters if c.id == "ch02")
    assert "09" not in [e.number for e in ch02.exercises]


def test_listing_not_aborted_by_non_standard_content() -> None:
    corpus = _load()
    # Despite the non-standard dirs/exercise, the full catalog is intact.
    assert len(corpus.chapters) == 4
    ch01 = next(c for c in corpus.chapters if c.id == "ch01")
    assert len(ch01.exercises) == 2


def test_corpus_root_relative_posix() -> None:
    corpus = _load()
    assert corpus.root_rel == "codeconv/tests/fixtures/tutorials_corpus"
    assert "\\" not in corpus.root_rel
