"""Run-layer tests (feature 023): outcome parsing, resolver shape-classification,
explain verdicts, CLI exit codes, and a gated real-backend run.

Hermetic units use the REAL vendored corpus (``tutorials/olamni/``) — resolution
is filesystem-read-only and does not need a REPL. The gated test drives the C#
backend and skips-with-report if it is absent (never silently passes).
"""

from __future__ import annotations

from pathlib import Path

import pytest
from typer.testing import CliRunner

from codeconv.cli import app
from codeconv.tutorials import outcome as oc
from codeconv.tutorials import explain as ex
from codeconv.tutorials import resolve as rs
from codeconv.tutorials.corpus import load_corpus, resolve_corpus_root, Exercise, Tutorial

REPO_ROOT = Path(__file__).resolve().parents[2]
CORPUS = REPO_ROOT / "tutorials" / "olamni"
CS_EXE = sorted((REPO_ROOT / "out" / "csharp" / "glp_repl" / "bin").rglob("glp_repl.exe"))
SIBLING_GLP = Path(rs.DEFAULT_SIBLING_GLP_ROOT)


def _resolve(chapter_id: str, number: str) -> rs.RunnableExample:
    loaded = load_corpus(resolve_corpus_root(REPO_ROOT, None), REPO_ROOT)
    tut = next(t for t in loaded.chapters if t.id == chapter_id)
    ex_obj = next((e for e in tut.exercises if e.number == number), None)
    if ex_obj is None:  # use-case (scripts-empty) — reconstruct from FS
        import os
        d = CORPUS / chapter_id / f"exercise-{number}"
        ex_obj = Exercise(number, Path(os.path.relpath(d, REPO_ROOT)).as_posix(), (), None)
    return rs.resolve_example(tut, ex_obj, repo_root=REPO_ROOT,
                             sibling_corpus=Path(rs.DEFAULT_SIBLING_CORPUS), sibling_glp_root=SIBLING_GLP)


# --------------------------------------------------------------------------- #
# outcome parsing (D7)                                                         #
# --------------------------------------------------------------------------- #
def test_parse_glued_output_segment_keeps_first_binding():
    # The C# REPL / ch03-style traces glue the first output to the GLP> prompt.
    text = "GLP> ✓ Loaded: x.glp\nGLP> A = [5, 4, 3, 2, 1]\nB = [3, 2, 1]\n→ succeeds\n"
    outs = oc.parse_outcome_segments(text)
    assert len(outs) == 1
    assert [(b.name, b.value) for b in outs[0].bindings] == [("A", "[5, 4, 3, 2, 1]"), ("B", "[3, 2, 1]")]
    assert outs[0].status == oc.Status.SUCCEEDS


def test_parse_goal_echo_segment_drops_goal():
    text = "GLP> merge([1,2,3],[a,b],Xs).\nXs = [1, a, 2, b, 3]\n→ succeeds\n"
    outs = oc.parse_outcome_segments(text)
    assert len(outs) == 1
    assert [(b.name, b.value) for b in outs[0].bindings] == [("Xs", "[1, a, 2, b, 3]")]


def test_freshvar_normalization_and_equality():
    a = oc.parse_outcome_segments("GLP> X = [m(X10) | X34]\n→ suspended\n")[0]
    b = oc.parse_outcome_segments("GLP> X = [m(X60) | X84]\n→ suspended\n")[0]
    assert oc.outcomes_equal(a, b)


def test_load_failure_kind_detected():
    out = oc.parse_outcome_block(["Error loading x.glp: Type checking failed:", "  bad"])
    assert out.kind == oc.GoldenKind.LOAD_FAILURE


def test_side_effect_kind_detected():
    out = oc.parse_outcome_block(["tagged(alice, cmd(connect(bob)))", "→ suspended"])
    assert out.kind == oc.GoldenKind.SIDE_EFFECT
    assert out.side_effects == ("tagged(alice, cmd(connect(bob)))",)


# --------------------------------------------------------------------------- #
# resolver shape-classification (D2-D5) — the conformity mechanism            #
# --------------------------------------------------------------------------- #
def test_resolve_section_single_ch01():
    ex_ = _resolve("ch01", "01")
    assert ex_.shape == rs.Shape.SECTION_SINGLE
    assert ex_.supported and len(ex_.load_targets) == 1
    assert ex_.load_targets[0].kind == rs.LoadKind.SINGLE_FILE
    assert any("merge([1,2,3],[a,b],Xs)." == g.text for g in ex_.goals)
    # golden positionally aligned, first goal -> Xs binding
    assert ex_.golden[0] is not None and ex_.golden[0].status == oc.Status.SUCCEEDS


def test_resolve_section_multi_compose_ch03():
    ex_ = _resolve("ch03", "01")
    assert ex_.shape == rs.Shape.SECTION_MULTI_COMPOSE
    assert len(ex_.load_targets) == 2
    assert ex_.goals[0].text.startswith("producer(A, 5)")
    # glued-output golden keeps the A binding
    names = [b.name for b in ex_.golden[0].bindings]
    assert "A" in names and "Sum" in names


def test_resolve_use_case_project_ch07():
    ex_ = _resolve("ch07", "01")
    assert ex_.shape == rs.Shape.USE_CASE_PROJECT
    assert ex_.load_targets[0].kind == rs.LoadKind.PROJECT_DIR
    assert ex_.load_targets[0].exec_path.replace("\\", "/").endswith("programs/cssg_modules")
    primary = ex_.primary_goal
    assert primary is not None and primary.text.lower().startswith("fplay1")
    assert primary.needs_limit == 1000000


def test_resolve_stub_chapter_not_implemented():
    # ch08 is a stub (no exercises with runnable content).
    import os
    d = CORPUS / "ch08" / "exercise-01"
    loaded = load_corpus(resolve_corpus_root(REPO_ROOT, None), REPO_ROOT)
    tut = next(t for t in loaded.chapters if t.id == "ch08")
    ex_obj = Exercise("01", "tutorials/olamni/ch08/exercise-01", (), None)
    ex_ = rs.resolve_example(tut, ex_obj, repo_root=REPO_ROOT,
                             sibling_corpus=Path(rs.DEFAULT_SIBLING_CORPUS), sibling_glp_root=SIBLING_GLP)
    assert ex_.shape == rs.Shape.NOT_IMPLEMENTED and not ex_.supported


# --------------------------------------------------------------------------- #
# explain verdicts (D8)                                                        #
# --------------------------------------------------------------------------- #
def _outcome(bindings, status):
    return oc.Outcome(tuple(oc.Binding(n, v) for n, v in bindings), status)


def test_explain_match_and_suspended_valid():
    o = _outcome([("Xs", "[]")], oc.Status.SUSPENDED)
    v = ex.explain_goal("g.", o, o)
    assert v.kind == ex.VerdictKind.MATCH and "suspended" in v.explanation


def test_explain_difference_always_surfaced():
    a = _outcome([("Xs", "[1]")], oc.Status.SUCCEEDS)
    g = _outcome([("Xs", "[2]")], oc.Status.SUCCEEDS)
    v = ex.explain_goal("g.", a, g)
    assert v.kind == ex.VerdictKind.DIFFERENCE and v.diffs


def test_explain_no_golden():
    a = _outcome([("Xs", "[1]")], oc.Status.SUCCEEDS)
    v = ex.explain_goal("g.", a, None)
    assert v.kind == ex.VerdictKind.NO_GOLDEN


# --------------------------------------------------------------------------- #
# CLI exit codes (D10)                                                         #
# --------------------------------------------------------------------------- #
def test_cli_preview_no_execution(monkeypatch):
    # preview must not launch a backend; patch run_example to detect any call.
    import codeconv.tutorials.backends as be
    called = {"n": 0}
    monkeypatch.setattr(be, "run_example", lambda *a, **k: called.__setitem__("n", called["n"] + 1))
    res = CliRunner().invoke(app, ["tutorials", "preview", "ch01", "01"])
    assert res.exit_code == 0 and called["n"] == 0


def test_cli_run_not_implemented_exit9():
    res = CliRunner().invoke(app, ["tutorials", "run", "ch08", "01"])
    assert res.exit_code == 9


def test_cli_unknown_backend_rejected():
    res = CliRunner().invoke(app, ["tutorials", "run", "ch01", "01", "--backend", "nope"])
    assert res.exit_code == 2


def test_cli_propose_apply_refused_without_approval():
    res = CliRunner().invoke(app, ["tutorials", "propose", "--apply"])
    assert res.exit_code == 2


def test_cli_propose_flags_spec_violation_and_stale_golden():
    res = CliRunner().invoke(app, ["tutorials", "propose", "--json"])
    assert res.exit_code == 0
    assert "spec-violation-ch04-ex07" in res.stdout
    assert "stale-golden-ch04-ex08" in res.stdout


# --------------------------------------------------------------------------- #
# Gated real-backend run (skip-with-report, never silent)                      #
# --------------------------------------------------------------------------- #
@pytest.mark.skipif(not CS_EXE, reason="C# REPL build absent (out/csharp/.../glp_repl.exe)")
@pytest.mark.skipif(not (SIBLING_GLP / "olamni" / "tutorial" / "ch01").is_dir(),
                    reason="sibling GLP corpus absent")
def test_real_backend_ch01_matches_golden():
    import codeconv.tutorials.backends as be
    ex_ = _resolve("ch01", "01")
    result = be.run_example(ex_, backend=be.BackendKind.CSHARP, repo_root=REPO_ROOT,
                            sibling_glp_root=SIBLING_GLP)
    assert not result.p1, result.p1_notice
    verdicts = ex.explain_run(result.goal_outcomes, ex_.golden)
    assert verdicts and all(v.kind == ex.VerdictKind.MATCH for v in verdicts), \
        [(v.goal, v.kind.value, v.explanation) for v in verdicts]


def _gated_run_all_match(chapter, number, *, timeout=180):
    import codeconv.tutorials.backends as be
    ex_ = _resolve(chapter, number)
    result = be.run_example(ex_, backend=be.BackendKind.CSHARP, repo_root=REPO_ROOT,
                            sibling_glp_root=SIBLING_GLP, timeout=timeout)
    assert not result.p1, result.p1_notice
    verdicts = ex.explain_run(result.goal_outcomes, ex_.golden)
    assert verdicts and all(v.kind == ex.VerdictKind.MATCH for v in verdicts), \
        [(v.goal, v.kind.value, v.explanation) for v in verdicts]


@pytest.mark.skipif(not CS_EXE, reason="C# REPL build absent")
@pytest.mark.skipif(not (SIBLING_GLP / "olamni" / "tutorial" / "ch03").is_dir(),
                    reason="sibling GLP corpus absent")
def test_real_backend_ch03_multi_compose_matches_golden():
    # Section multi-compose: two .glp loaded in ONE session + a composed goal.
    _gated_run_all_match("ch03", "01")


@pytest.mark.skipif(not CS_EXE, reason="C# REPL build absent")
@pytest.mark.skipif(not (SIBLING_GLP / "programs" / "cssg_modules").is_dir(),
                    reason="sibling cssg_modules project absent")
def test_real_backend_ch07_use_case_matches_golden():
    # US2 — the unification: the SAME run path executes the ch07 project + fplay1.
    _gated_run_all_match("ch07", "01")


# --------------------------------------------------------------------------- #
# US5 — backend choice + C# P1 policy + flagged Dart fallback                  #
# --------------------------------------------------------------------------- #
import sys
import codeconv.tutorials.backends as be


def test_resolve_backend_kinds():
    cs = be.resolve_backend(be.BackendKind.CSHARP, repo_root=REPO_ROOT, sibling_glp_root=SIBLING_GLP)
    assert cs.kind == be.BackendKind.CSHARP  # available iff build present
    dart = be.resolve_backend(be.BackendKind.DART, repo_root=REPO_ROOT, sibling_glp_root=SIBLING_GLP)
    assert dart.kind == be.BackendKind.DART


def _fake_resolve_cs_down(monkeypatch, dart_ok=True):
    def fake(kind, *, repo_root, sibling_glp_root):
        if kind == be.BackendKind.CSHARP:
            return be.Backend(kind, False, [], None, "simulated C# unavailable")
        inv = [sys.executable, "-c", "print('GLP> Xs = [1]'); print('→ succeeds')"] if dart_ok else []
        return be.Backend(kind, dart_ok, inv, None, None if dart_ok else "no dart")
    monkeypatch.setattr(be, "resolve_backend", fake)


def test_csharp_p1_is_loud_exit8(monkeypatch):
    _fake_resolve_cs_down(monkeypatch, dart_ok=True)
    res = CliRunner().invoke(app, ["tutorials", "run", "ch01", "01", "--skip-drift-check"])
    assert res.exit_code == 8  # C# P1, no fallback requested → loud exit 8


def test_dart_fallback_carries_p1_notice(monkeypatch):
    _fake_resolve_cs_down(monkeypatch, dart_ok=True)
    ex_ = _resolve("ch01", "01")
    result = be.run_example(ex_, backend=be.BackendKind.CSHARP, repo_root=REPO_ROOT,
                            sibling_glp_root=SIBLING_GLP, allow_dart_fallback=True)
    assert result.backend_used == be.BackendKind.DART
    assert result.p1 and result.p1_notice and "C# P1" in result.p1_notice  # never masked


# --------------------------------------------------------------------------- #
# Exit codes 6 / 11 (D10, FR-016)                                             #
# --------------------------------------------------------------------------- #
def test_exit6_missing_exec_path(tmp_path):
    # Point the section exec root at an empty dir → the .glp won't exist → exit 6.
    res = CliRunner().invoke(app, ["tutorials", "run", "ch01", "01",
                                   "--sibling-corpus", str(tmp_path), "--skip-drift-check"])
    assert res.exit_code == 6


def test_exit11_drift_refused(monkeypatch):
    from codeconv.tutorials import sync as _sync
    monkeypatch.setattr(_sync, "check", lambda *a, **k: _sync.CheckResult(
        ok=False, dest=REPO_ROOT, sibling_drift=["ch01/exercise-01/x.glp"]))
    # exec_path must exist for the drift check to be reached → needs the sibling.
    if not (SIBLING_GLP / "olamni" / "tutorial" / "ch01" / "exercise-01").is_dir():
        pytest.skip("sibling GLP corpus absent")
    res = CliRunner().invoke(app, ["tutorials", "run", "ch01", "01"])
    assert res.exit_code == 11


# --------------------------------------------------------------------------- #
# JSON schema (D10) + skill≡CLI parity (FR-014)                               #
# --------------------------------------------------------------------------- #
def test_preview_json_schema():
    import json
    res = CliRunner().invoke(app, ["tutorials", "preview", "ch01", "01", "--json"])
    assert res.exit_code == 0
    m = json.loads(res.stdout)
    assert m["chapter"] == "ch01" and m["shape"] == "section_single"
    assert m["load_target"]["kind"] == "single_file"
    assert m["goals"] and m["goals"][0]["expected"]["status"] == "succeeds"


def test_propose_json_schema():
    import json
    res = CliRunner().invoke(app, ["tutorials", "propose", "--json"])
    m = json.loads(res.stdout)
    kinds = {p["kind"] for p in m["proposals"]}
    assert {"drift_gap", "stale_artefact", "layout_normalise"} <= kinds


def test_skill_cli_parity_preview(tmp_path):
    """The skill forwards verbatim to the CLI → identical output. Compare a real
    subprocess (the skill's actual invocation) to the in-process CliRunner."""
    import json
    import subprocess
    py = REPO_ROOT / "codeconv" / ".venv" / "Scripts" / "python.exe"
    if not py.is_file():
        py = REPO_ROOT / "codeconv" / ".venv" / "bin" / "python"
    if not py.is_file():
        pytest.skip("codeconv venv absent")
    proc = subprocess.run([str(py), "-m", "codeconv.cli", "tutorials", "preview", "ch01", "01", "--json"],
                          capture_output=True, text=True, cwd=str(REPO_ROOT))
    cli = CliRunner().invoke(app, ["tutorials", "preview", "ch01", "01", "--json"])
    assert proc.returncode == cli.exit_code == 0
    assert json.loads(proc.stdout) == json.loads(cli.stdout)
