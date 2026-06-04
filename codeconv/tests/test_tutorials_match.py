"""Identifier matching tests — US2 (T017/T018), D5 / FR-002 / SC-003.

Exercises every matching tier (exact id, zero-pad, prefix, title substring) and
the no-match (exit 3) / ambiguous (exit 4) reporting via the CLI.
"""

from __future__ import annotations

from pathlib import Path

from typer.testing import CliRunner

from codeconv.tutorials import corpus as C
from codeconv.tutorials.cli import tutorials_app
from codeconv.tutorials.match import match_tutorial

REPO_ROOT = Path(__file__).resolve().parents[2]
FIXTURE = Path(__file__).resolve().parent / "fixtures" / "tutorials_corpus"

runner = CliRunner()


def _chapters():
    return C.load_corpus(FIXTURE, repo_root=REPO_ROOT).chapters


# --- T017: matching variants each return only the matched chapter ---------- #
def test_exact_id() -> None:
    r = match_tutorial("ch01", _chapters())
    assert r.kind == "one" and r.matched.id == "ch01"


def test_zero_pad_normalized_bare_number() -> None:
    r = match_tutorial("1", _chapters())
    assert r.kind == "one" and r.matched.id == "ch01"


def test_zero_pad_normalized_ch_prefix() -> None:
    r = match_tutorial("ch7", _chapters())
    assert r.kind == "one" and r.matched.id == "ch07"


def test_case_insensitive_title_substring() -> None:
    r = match_tutorial("TYPES", _chapters())
    assert r.kind == "one" and r.matched.id == "ch02"


def test_match_filters_to_single_chapter_via_cli() -> None:
    res = runner.invoke(tutorials_app, ["list", "ch07", "--corpus", str(FIXTURE)])
    assert res.exit_code == 0
    assert "ch07" in res.stdout
    assert "ch01" not in res.stdout and "ch02" not in res.stdout


# --- T018: unknown → exit 3 + available ids; ambiguous → exit 4 + candidates #
def test_unknown_identifier_no_match() -> None:
    r = match_tutorial("zzz", _chapters())
    assert r.kind == "none"
    assert set(r.candidates) == {"ch01", "ch02", "ch07", "ch08"}


def test_unknown_identifier_cli_exit_3_lists_ids() -> None:
    res = runner.invoke(tutorials_app, ["list", "zzz", "--corpus", str(FIXTURE)])
    assert res.exit_code == 3
    assert "no tutorial matches 'zzz'" in res.stderr
    for cid in ("ch01", "ch02", "ch07", "ch08"):
        assert cid in res.stderr


def test_ambiguous_identifier() -> None:
    r = match_tutorial("ch0", _chapters())  # prefix of ch01/ch02/ch07/ch08
    assert r.kind == "ambiguous"
    assert len(r.candidates) >= 2


def test_ambiguous_identifier_cli_exit_4_lists_candidates() -> None:
    res = runner.invoke(tutorials_app, ["list", "ch0", "--corpus", str(FIXTURE)])
    assert res.exit_code == 4
    assert "ambiguous" in res.stderr
    assert "ch01" in res.stderr and "ch08" in res.stderr
