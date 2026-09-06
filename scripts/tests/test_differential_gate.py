# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Tests for the differential acceptance harness (feature 109 US1).

Every test here is paired with a POSITIVE control -- a near-identical declaration that differs
only in the property under test and that must produce the OTHER outcome. FR-023: this feature's
harness is subject to its own invariant, and a test that has never been shown capable of
returning the other answer has measured nothing.

The participants are real subprocesses (`sys.executable -c ...`), not stubs, because the failure
modes being tested -- a participant that cannot start, a participant that exits 0 with no output
-- are properties of *starting a process*, and a stub that returns a canned dict cannot exhibit
them.
"""

from __future__ import annotations

import json
import os
import sys

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.realpath(__file__))))

import differential_gate as dg  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.realpath(__file__))))


# ---------------------------------------------------------------------------
# Builders. A criterion is verbose by design (every normalisation must declare a rationale and a
# control), so these keep the tests about the property under test.
# ---------------------------------------------------------------------------
def _emitter(text: str) -> dict:
    """A participant that prints `text` and exits 0."""
    return {
        "name": "emitter",
        "why": "test participant",
        "command": [sys.executable, "-c", f"import sys; sys.stdout.write({text!r})"],
        "cwd": REPO,
    }


def _named(name: str, text: str) -> dict:
    p = _emitter(text)
    p["name"] = name
    return p


def _criterion(participants: list[dict], *, normalisations: list[dict] | None = None,
               neg_participant: str | None = None) -> dict:
    return {
        "id": "test-criterion",
        "title": "a criterion under test",
        "requirement": "feature 109 US1 self-test",
        "participants": participants,
        "script": "",
        "normalisations": normalisations if normalisations is not None else [],
        "negative_control": {
            "participant": neg_participant or participants[0]["name"],
            "rule": {"kind": "regex_sub", "pattern": "a", "replacement": "PERTURBED"},
            "why": "perturbing a participant's transcript must make the criterion diverge",
        },
    }


def _env() -> dict[str, str]:
    return dg.build_environment(REPO)


def _write(tmp_path, criteria: list[dict]) -> str:
    path = os.path.join(str(tmp_path), "criteria.json")
    with open(path, "w", encoding="utf-8") as fh:
        json.dump({"schema": "glpnet/differential-criteria/1", "criteria": criteria}, fh)
    return path


# ===========================================================================
# T050 -- FR-004: two empty transcripts are NOT agreement
# ===========================================================================
def test_two_empty_transcripts_are_not_measured():
    """The whole harness exists because an empty transcript diffs clean against another empty
    transcript. A comparator that reports AGREE here has answered 'were the outputs equal?' when
    the question was 'did the runtimes agree?'."""
    crit = _criterion([_named("a", ""), _named("b", "")])
    result = dg.evaluate(crit, _env())
    assert result["outcome"] == dg.NOT_MEASURED
    assert result["outcome"] != dg.AGREE
    assert "empty transcript" in result["reason"]
    assert result["not_measured_participant"] in ("a", "b")


def test_one_empty_transcript_is_also_not_measured():
    """Half-vacuous is still vacuous: an empty side cannot corroborate a non-empty one."""
    crit = _criterion([_named("a", "→ succeeds\n"), _named("b", "")])
    result = dg.evaluate(crit, _env())
    assert result["outcome"] == dg.NOT_MEASURED
    assert result["not_measured_participant"] == "b"


def test_control_two_non_empty_identical_transcripts_do_agree():
    """POSITIVE CONTROL for T050. Same shape, non-empty -- must reach AGREE. Without this, the
    NOT-MEASURED above would also be produced by a harness that never says AGREE at all."""
    # The transcripts must contain the character the default perturbation substitutes, or the
    # criterion's own negative control is inert and the harness correctly returns NOT-MEASURED
    # instead. That is the harness working; it is not the property under test here.
    crit = _criterion([_named("a", "Y = some(send(1, a))\n"),
                       _named("b", "Y = some(send(1, a))\n")])
    result = dg.evaluate(crit, _env())
    assert result["outcome"] == dg.AGREE


# ===========================================================================
# T051 -- FR-003: a participant that cannot start is named, with a reason
# ===========================================================================
def test_missing_participant_names_the_participant_and_the_reason():
    crit = _criterion([
        _named("present", "→ succeeds\n"),
        {"name": "absent", "why": "a participant that is not installed",
         "command": [os.path.join(REPO, "no", "such", "binary.exe")], "cwd": REPO},
    ])
    result = dg.evaluate(crit, _env())
    assert result["outcome"] == dg.NOT_MEASURED
    assert result["not_measured_participant"] == "absent"
    assert "absent" in result["reason"]
    assert "executable not found" in result["reason"]


def test_unresolvable_symbol_is_a_named_measurement_failure_not_a_crash():
    """A declaration may name a tool this host does not have. That must arrive as NOT-MEASURED
    with the symbol named -- not as a traceback, and not as a literal '${X}' handed to exec."""
    crit = _criterion([
        _named("present", "→ succeeds\n"),
        {"name": "symbolic", "why": "names a tool that does not resolve here",
         "command": ["${NO_SUCH_SYMBOL}"], "cwd": REPO},
    ])
    result = dg.evaluate(crit, _env())
    assert result["outcome"] == dg.NOT_MEASURED
    assert result["not_measured_participant"] == "symbolic"
    assert "NO_SUCH_SYMBOL" in result["reason"]


def test_not_measured_is_reported_not_skipped():
    """A skipped check disappears from the report; a NOT-MEASURED criterion is counted and makes
    the tool exit non-zero. FR-003 forbids the former."""
    crit = _criterion([_named("a", ""), _named("b", "")])
    result = dg.evaluate(crit, _env())
    rendered = dg.render({
        "schema": "glpnet/differential-report/1", "declaration": "x",
        "totals": {"declared": 1, "measured_agree": 0, "measured_diverge": 0, "not_measured": 1},
        "agreement_is_not_correctness": "-", "criteria": [result]})
    assert dg.NOT_MEASURED in rendered
    assert "not a skip" in rendered


# ===========================================================================
# T052 -- FR-005: a one-participant declaration is refused AT LOAD
# ===========================================================================
def test_one_participant_declaration_is_refused_at_load(tmp_path):
    path = _write(tmp_path, [_criterion([_named("only", "x")])])
    with pytest.raises(dg.DeclarationError) as exc:
        dg.load(path)
    assert "at least 2" in str(exc.value)
    assert "category error" in str(exc.value)


def test_zero_participant_declaration_is_refused_at_load(tmp_path):
    crit = _criterion([_named("a", "x"), _named("b", "x")])
    crit["participants"] = []
    crit["negative_control"]["participant"] = "a"
    path = _write(tmp_path, [crit])
    with pytest.raises(dg.DeclarationError):
        dg.load(path)


def test_control_two_participant_declaration_loads(tmp_path):
    """POSITIVE CONTROL for T052: the loader must accept the valid shape, or the refusals above
    would also be produced by a loader that refuses everything."""
    path = _write(tmp_path, [_criterion([_named("a", "x"), _named("b", "x")])])
    assert len(dg.load(path)) == 1


def test_negative_control_naming_an_unknown_participant_is_refused(tmp_path):
    """A criterion whose negative control perturbs a participant that does not exist would never
    perturb anything, and would therefore always 'pass' its own control."""
    crit = _criterion([_named("a", "x"), _named("b", "x")], neg_participant="ghost")
    path = _write(tmp_path, [crit])
    with pytest.raises(dg.DeclarationError) as exc:
        dg.load(path)
    assert "ghost" in str(exc.value)


def test_duplicate_criterion_id_is_refused(tmp_path):
    a = _criterion([_named("a", "x"), _named("b", "x")])
    b = _criterion([_named("a", "y"), _named("b", "y")])
    path = _write(tmp_path, [a, b])
    with pytest.raises(dg.DeclarationError) as exc:
        dg.load(path)
    assert "duplicate" in str(exc.value)


# ===========================================================================
# T053 -- FR-002: differing transcripts diverge, and the divergence is PRINTED
# ===========================================================================
def test_differing_transcripts_are_measured_diverge_and_the_diff_is_printed():
    crit = _criterion([_named("a", "Y = some(send(1, a))\n"),
                       _named("b", "Y = some(send(2, b))\n")])
    result = dg.evaluate(crit, _env())
    assert result["outcome"] == dg.DIVERGE
    assert "send(1, a)" in result["divergence"]
    assert "send(2, b)" in result["divergence"]
    rendered = dg.render({
        "schema": "glpnet/differential-report/1", "declaration": "x",
        "totals": {"declared": 1, "measured_agree": 0, "measured_diverge": 1, "not_measured": 0},
        "agreement_is_not_correctness": "-", "criteria": [result]})
    assert "divergence:" in rendered
    assert "send(2, b)" in rendered


def test_diverge_exit_code_is_not_zero(tmp_path):
    path = _write(tmp_path, [_criterion([_named("a", "one\n"), _named("b", "two\n")])])
    report, code = dg.run(REPO, path)
    assert report["totals"]["measured_diverge"] == 1
    assert code == dg.EXIT_DIVERGE != 0


def test_not_measured_exit_code_is_not_zero(tmp_path):
    """The measured failure mode this replaces: 'the tool did not run' read as 'nothing to
    report'. NOT-MEASURED must never be exit 0."""
    path = _write(tmp_path, [_criterion([_named("a", ""), _named("b", "")])])
    report, code = dg.run(REPO, path)
    assert report["totals"]["not_measured"] == 1
    assert code == dg.EXIT_NOT_MEASURED != 0


# ===========================================================================
# T054 -- FR-006: every normalisation carries an EXECUTED negative control
# ===========================================================================
_GOOD_RULE = {
    "id": "strip-prompt", "kind": "strip_line_prefix", "prefix": "GLP> ",
    "rationale": "the prompt is line discipline, not a result",
    "negative_control": {"a": "GLP> Y = 1", "b": "GLP> Y = 2",
                         "why": "a differing binding after the prompt must survive"},
}

# A rule that erases everything. This is the failure FR-006 exists to catch: a normaliser is a
# CLAIM about what is irrelevant, and an over-broad claim silently converts every divergence into
# agreement.
_ERASING_RULE = {
    "id": "erase-everything", "kind": "regex_sub", "pattern": ".*", "replacement": "",
    "rationale": "deliberately over-broad, for the test",
    "negative_control": {"a": "Y = 1", "b": "Y = 2",
                         "why": "two real, differing bindings"},
}


def test_a_normalisation_that_erases_its_control_makes_the_criterion_not_measured():
    crit = _criterion([_named("a", "Y = 1\n"), _named("b", "Y = 2\n")],
                      normalisations=[_ERASING_RULE])
    result = dg.evaluate(crit, _env())
    assert result["outcome"] == dg.NOT_MEASURED
    assert "erased its own negative control" in result["reason"]
    assert result["normalisation_controls"][0]["executed"] is True
    assert result["normalisation_controls"][0]["passed"] is False


def test_the_erasing_rule_would_otherwise_have_produced_a_false_agree():
    """This is the point of the previous test, made explicit: without the control, that same rule
    turns two genuinely different transcripts into agreement. The control is the only thing
    standing between the harness and a confident false green."""
    assert dg.apply_rules([_ERASING_RULE], "Y = 1\n") == dg.apply_rules([_ERASING_RULE], "Y = 2\n")


def test_control_a_sound_normalisation_passes_its_control_and_still_compares():
    """POSITIVE CONTROL for T054."""
    crit = _criterion([_named("a", "GLP> Y = 1\n"), _named("b", "GLP> Y = 2\n")],
                      normalisations=[_GOOD_RULE])
    result = dg.evaluate(crit, _env())
    assert result["normalisation_controls"][0]["passed"] is True
    assert result["outcome"] == dg.DIVERGE   # the rule did NOT hide the real difference


def test_a_normalisation_without_a_negative_control_is_refused_at_load(tmp_path):
    rule = {k: v for k, v in _GOOD_RULE.items() if k != "negative_control"}
    crit = _criterion([_named("a", "x"), _named("b", "x")], normalisations=[rule])
    path = _write(tmp_path, [crit])
    with pytest.raises(dg.DeclarationError) as exc:
        dg.load(path)
    assert "negative_control" in str(exc.value)


def test_a_negative_control_whose_inputs_already_agree_is_refused_at_load(tmp_path):
    """A control whose two inputs are identical passes every rule, including one that erases
    everything. It is a control in name only."""
    rule = dict(_GOOD_RULE)
    rule["negative_control"] = {"a": "same", "b": "same", "why": "vacuous"}
    crit = _criterion([_named("a", "x"), _named("b", "x")], normalisations=[rule])
    path = _write(tmp_path, [crit])
    with pytest.raises(dg.DeclarationError) as exc:
        dg.load(path)
    assert "identical" in str(exc.value)


# ===========================================================================
# FR-007 / SC-002 -- the criterion's own negative control, EXECUTED every run
# ===========================================================================
def test_a_criterion_whose_negative_control_does_not_diverge_is_not_measured():
    """If perturbing a participant does not change the outcome, the comparator did not
    discriminate on this run, so this run's AGREE proves nothing -- exactly as a missing
    participant proves nothing."""
    crit = _criterion([_named("a", "zzz\n"), _named("b", "zzz\n")])
    # The perturbation substitutes 'a', which does not occur in the transcripts, so it changes
    # nothing -- an inert control.
    result = dg.evaluate(crit, _env())
    assert result["outcome"] == dg.NOT_MEASURED
    assert result["negative_control"]["executed"] is True
    assert result["negative_control"]["passed"] is False
    assert "did not diverge" in result["reason"]


def test_control_an_effective_negative_control_is_recorded_as_executed_and_passed():
    """POSITIVE CONTROL: the same machinery must be able to say 'passed', or the test above is
    satisfied by a harness that always says NOT-MEASURED."""
    crit = _criterion([_named("a", "aaa\n"), _named("b", "aaa\n")])
    result = dg.evaluate(crit, _env())
    assert result["outcome"] == dg.AGREE
    assert result["negative_control"] == {
        **result["negative_control"], "executed": True, "passed": True}


def test_agree_is_never_reached_without_an_executed_negative_control():
    """The structural claim behind SC-002, asserted rather than assumed: across the outcomes this
    suite produces, no MEASURED-AGREE exists whose control was not executed and did not pass."""
    cases = [
        _criterion([_named("a", "aaa\n"), _named("b", "aaa\n")]),   # AGREE
        _criterion([_named("a", "aaa\n"), _named("b", "bbb\n")]),   # DIVERGE
        _criterion([_named("a", ""), _named("b", "")]),             # NOT-MEASURED
        _criterion([_named("a", "zzz\n"), _named("b", "zzz\n")]),   # inert control
    ]
    for crit in cases:
        r = dg.evaluate(crit, _env())
        if r["outcome"] == dg.AGREE:
            assert r["negative_control"]["executed"] and r["negative_control"]["passed"]


# ===========================================================================
# FR-008 -- agreement is not correctness, and the artefact says so
# ===========================================================================
def test_the_report_states_that_agreement_is_not_correctness(tmp_path):
    path = _write(tmp_path, [_criterion([_named("a", "aaa\n"), _named("b", "aaa\n")])])
    report, _ = dg.run(REPO, path)
    assert "broken identically also agree" in report["agreement_is_not_correctness"]
    assert "broken identically also agree" in dg.render(report)


# ===========================================================================
# The SHIPPED declaration is itself under test
# ===========================================================================
def test_the_shipped_declaration_loads():
    """A declaration that only the author ever loaded is a claim. This makes the shipped file's
    validity a suite obligation."""
    path = os.path.join(REPO, ".specify", "differential", "criteria.json")
    criteria = dg.load(path)
    assert criteria, "the shipped declaration must contain at least one criterion"
    for crit in criteria:
        assert len(crit["participants"]) >= 2
        assert crit["normalisations"], (
            f"{crit['id']}: a criterion with no declared normalisation compares raw transcripts, "
            "which is a claim in itself and must be made explicitly")


def test_every_shipped_normalisation_control_actually_passes():
    """T054 applied to the file we ship, not only to fixtures: run each declared rule against its
    own control here, so a rule that erases real divergences fails the suite rather than quietly
    degrading a criterion to NOT-MEASURED at some later run."""
    path = os.path.join(REPO, ".specify", "differential", "criteria.json")
    for crit in dg.load(path):
        for res in dg.check_normalisation_controls(crit["normalisations"]):
            assert res["passed"], f"{crit['id']}/{res['normalisation']}: {res['detail']}"
