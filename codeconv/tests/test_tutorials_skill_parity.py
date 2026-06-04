"""Skill ↔ CLI equivalence — FR-009 (T028).

The `/glptutorial-list` skill is a thin front-end that forwards verbatim to
`codeconv tutorials list` and relays output unchanged. This test pins (a) that
the skill doc documents exactly that forwarding contract, and (b) that the CLI's
``--json`` output is a faithful, transformation-free serialization of the single
engine model — so both entry points produce equivalent listings.
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
SKILL_MD = REPO_ROOT / ".claude" / "skills" / "glptutorial-list" / "SKILL.md"

runner = CliRunner()


def test_skill_doc_forwards_verbatim_to_cli() -> None:
    assert SKILL_MD.is_file(), "the /glptutorial-list skill must exist (T027)"
    text = SKILL_MD.read_text(encoding="utf-8")
    assert "codeconv tutorials list" in text
    assert "verbatim" in text
    # Read-only scope is documented (FR-010).
    assert "read-only" in text.lower() or "never run" in text.lower()


def test_cli_json_is_pure_serialization_of_engine_model() -> None:
    """The CLI adds no transformation: its --json equals build_payload directly."""
    res = runner.invoke(tutorials_app, ["list", "--corpus", str(FIXTURE), "--json"])
    assert res.exit_code == 0
    cli_payload = json.loads(res.stdout)

    corpus = C.load_corpus(FIXTURE, repo_root=REPO_ROOT)
    engine_payload = render.build_payload(corpus, corpus.chapters)
    assert cli_payload == engine_payload


def test_cli_json_is_deterministic_across_invocations() -> None:
    a = runner.invoke(tutorials_app, ["list", "--corpus", str(FIXTURE), "--json"])
    b = runner.invoke(tutorials_app, ["list", "--corpus", str(FIXTURE), "--json"])
    assert a.stdout == b.stdout
