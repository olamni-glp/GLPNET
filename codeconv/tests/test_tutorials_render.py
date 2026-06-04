"""Rendering tests — US1 (T011): human + --json output shape, ordering, exits.

Covers FR-005 (grouped/indented human listing + empty indicator), FR-008,
FR-009 (JSON model), and the contract exit code 0 for full-catalog listing.
"""

from __future__ import annotations

import json
from pathlib import Path

from typer.testing import CliRunner

from codeconv.tutorials import corpus as C
from codeconv.tutorials import render
from codeconv.tutorials.cli import tutorials_app

REPO_ROOT = Path(__file__).resolve().parents[2]
FIXTURE = Path(__file__).resolve().parent / "fixtures" / "tutorials_corpus"

runner = CliRunner()


def _corpus() -> C.Corpus:
    return C.load_corpus(FIXTURE, repo_root=REPO_ROOT)


# --- human-readable -------------------------------------------------------- #
def test_human_grouped_indented_with_titles() -> None:
    text = render.render_human(_corpus().chapters)
    assert "ch01 — Introduction Fixture" in text
    assert "  exercise-01" in text
    assert "    ch-01-ex-01-hello.glp" in text  # 4-space script indent


def test_human_empty_chapter_indicator() -> None:
    text = render.render_human(_corpus().chapters)
    lines = text.splitlines()
    idx = lines.index("ch08 — Planned Fixture")
    assert lines[idx + 1].strip() == "(no scripts)"


def test_human_bare_id_when_no_title() -> None:
    text = render.render_human(_corpus().chapters)
    assert "\nch07\n" in text + "\n"  # ch07 has no title source → bare id


def test_human_descriptions_present() -> None:
    text = render.render_human(_corpus().chapters)
    assert "— Hello world single-script intro" in text
    assert "— (no description)" in text


# --- JSON ------------------------------------------------------------------ #
def test_json_shape_and_keys() -> None:
    corpus = _corpus()
    payload = json.loads(render.render_json(corpus, corpus.chapters))
    assert set(payload) == {"corpus_root", "chapters", "warnings"}
    chapter = payload["chapters"][0]
    assert set(chapter) == {"id", "title", "is_empty", "exercises"}
    script = chapter["exercises"][0]["scripts"][0]
    assert set(script) == {"name", "path", "description", "description_source"}
    assert script["description_source"] in {"exercise_md", "glp_header", "none"}


def test_json_paths_are_posix() -> None:
    corpus = _corpus()
    payload = json.loads(render.render_json(corpus, corpus.chapters))
    for chapter in payload["chapters"]:
        for ex in chapter["exercises"]:
            for s in ex["scripts"]:
                assert "\\" not in s["path"]
                assert s["path"].endswith(".glp")


def test_json_warnings_included() -> None:
    corpus = _corpus()
    payload = json.loads(render.render_json(corpus, corpus.chapters))
    assert any("spec-rev-eng-input" in w for w in payload["warnings"])


def test_render_is_deterministic() -> None:
    a = render.render_json(_corpus(), _corpus().chapters)
    b = render.render_json(_corpus(), _corpus().chapters)
    assert a == b


# --- CLI exit code --------------------------------------------------------- #
def test_full_catalog_exit_zero() -> None:
    res = runner.invoke(tutorials_app, ["list", "--corpus", str(FIXTURE)])
    assert res.exit_code == 0
    assert "ch01" in res.stdout and "ch08" in res.stdout


def test_warnings_go_to_stderr_and_quiet_suppresses() -> None:
    res = runner.invoke(tutorials_app, ["list", "--corpus", str(FIXTURE)])
    assert "warning: skipped non-standard dir" in res.stderr
    quiet = runner.invoke(tutorials_app, ["list", "--corpus", str(FIXTURE), "--quiet"])
    assert quiet.stderr.strip() == ""
    assert quiet.exit_code == 0
