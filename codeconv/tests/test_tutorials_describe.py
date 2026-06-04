"""Description extraction tests — US3 (T021/T022/T023), D7 / FR-004 / SC-004.

Covers the precedence (exercise_md → glp_header → none), correct
``description_source`` tagging, the ``(no description)`` indicator, and the
SC-004 ≥95%-meaningful target.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tutorials import corpus as C
from codeconv.tutorials import describe

REPO_ROOT = Path(__file__).resolve().parents[2]
FIXTURE = Path(__file__).resolve().parent / "fixtures" / "tutorials_corpus"


def _scripts() -> list[C.Script]:
    corpus = C.load_corpus(FIXTURE, repo_root=REPO_ROOT)
    return [s for c in corpus.chapters for e in c.exercises for s in e.scripts]


def _by_name(name: str) -> C.Script:
    return next(s for s in _scripts() if s.name == name)


# --- T021: precedence + source tagging ------------------------------------- #
def test_single_script_prefers_exercise_md() -> None:
    s = _by_name("ch-01-ex-01-hello.glp")
    assert s.description_source is C.DescriptionSource.EXERCISE_MD
    assert s.description == "Hello world single-script intro"


def test_multi_script_disambiguates_via_glp_header() -> None:
    prod = _by_name("ch-01-ex-02-producer.glp")
    cons = _by_name("ch-01-ex-02-consumer.glp")
    assert prod.description_source is C.DescriptionSource.GLP_HEADER
    assert cons.description_source is C.DescriptionSource.GLP_HEADER
    assert prod.description != cons.description  # genuinely disambiguated


def test_resolve_precedence_unit() -> None:
    assert describe.resolve_script_description("MD", "GLP", multi_script=False) == (
        "MD",
        "exercise_md",
    )
    assert describe.resolve_script_description("MD", "GLP", multi_script=True) == (
        "GLP",
        "glp_header",
    )
    assert describe.resolve_script_description(None, "GLP", multi_script=False) == (
        "GLP",
        "glp_header",
    )
    assert describe.resolve_script_description("MD", None, multi_script=True) == (
        "MD",
        "exercise_md",
    )
    assert describe.resolve_script_description(None, None, multi_script=False) == (
        describe.NO_DESCRIPTION,
        "none",
    )


def test_h1_tail_extraction() -> None:
    assert describe.chapter_title_from_guide("# Chapter 3 — GLP Core\n") == "GLP Core"
    assert (
        describe.extract_exercise_md_description("# Exercise 1 — A pipeline demo\n")
        == "A pipeline demo"
    )


def test_glp_header_skips_filename_banner() -> None:
    text = "% ch-09-ex-01-thing.glp\n%\n%% The informative line.\nfoo(a).\n"
    assert describe.extract_glp_header(text) == "The informative line."
    # A file whose only comment is the banner yields nothing.
    assert describe.extract_glp_header("% ch-09-ex-01-thing.glp\nfoo(a).\n") is None


def test_overview_titles_parsed_from_table() -> None:
    titles = describe.parse_overview_titles(
        "# T\n\n| # | Chapter | x | y |\n|---|---|---|---|\n"
        "| 3 | GLP Core | a | b |\n| 8 | Social Graph | a | b |\n"
    )
    assert titles == {"ch03": "GLP Core", "ch08": "Social Graph"}


# --- T022: a script with no derivable description shows the indicator ------ #
def test_no_description_script_marked_not_omitted() -> None:
    s = _by_name("ch-02-ex-03-nodesc.glp")  # raises if omitted
    assert s.description == describe.NO_DESCRIPTION
    assert s.description_source is C.DescriptionSource.NONE


# --- T023: ≥95% of scripts with available text get a meaningful line ------- #
def test_sc004_meaningful_description_coverage() -> None:
    scripts = _scripts()
    with_text = [s for s in scripts if s.description_source is not C.DescriptionSource.NONE]

    def meaningful(s: C.Script) -> bool:
        d = s.description
        return bool(d) and d != describe.NO_DESCRIPTION and d != s.name and "\n" not in d

    good = [s for s in with_text if meaningful(s)]
    assert with_text, "fixture must contain described scripts"
    assert len(good) / len(with_text) >= 0.95
    # Concrete, non-tautological anchors: the six describable fixture scripts.
    expected_meaningful = {
        "ch-01-ex-01-hello.glp",
        "ch-01-ex-02-producer.glp",
        "ch-01-ex-02-consumer.glp",
        "ch-02-ex-01-corrected.glp",
        "ch-02-ex-01-failing.glp",
        "ch-07-ex-01-modules.glp",
    }
    assert expected_meaningful <= {s.name for s in good}
