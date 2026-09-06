# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Tests for the evidence-signal audit itself (feature 108, FR-017).

The audit is subject to the invariant it audits. Two of these tests exist because the audit
FAILED that invariant during implementation on 2026-09-06: a shell round-trip wrote literal
backspace bytes where word-boundary escapes were intended, every pattern became unmatchable,
and the audit ran, wrote a report, emitted a receipt and exited non-zero while finding 1 hit
where ground truth had roughly 400. Nothing about the exit code or the report's existence was
wrong. Only an assertion on content that only a working scan could produce caught it.

That is FR-010, and `test_scan_finds_a_known_planted_decision_site` is its regression.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys

import pytest

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(REPO, "scripts"))

import evidence_signal_audit as esa  # noqa: E402


#: Feature 109 FR-010: a scoped region MUST name the area whose adoption governs it, and that
#: area's state must be declared. The fixture therefore ships a real adoption manifest -- writing
#: one is the point, since "no declaration" is an ERROR and must stay one. `_adopted` selects
#: whether the gate BINDS for the fixture, which is how the refusal tests and the non-adoption
#: tests differ without either one suppressing the gate by configuration (FR-011 forbids that).
def _adoption(tmp_path, state="non-adopted"):
    d = tmp_path / ".specify" / "receipts"
    d.mkdir(parents=True, exist_ok=True)
    areas = [{"area": a, "state": state, "since": "2026-09-06"}
             for a in ("build-gate", "coop", "roadmap-sync", "test-harness", "reference")]
    (d / "adoption.json").write_text(json.dumps({"areas": areas}), encoding="utf-8")


def _manifest(tmp_path, surfaces=None, area="coop", adoption="non-adopted", **over):
    doc = {
        "version": 1,
        "lane": "test-lane",
        "scoped_regions": [{"path": "src", "rationale": "the test fixture tree", "area": area}],
        "surfaces": surfaces if surfaces is not None else [],
    }
    doc.update(over)
    _adoption(tmp_path, adoption)
    p = tmp_path / "manifest.json"
    p.write_text(json.dumps(doc), encoding="utf-8")
    return str(p)


def _surface(**over):
    # feature 109 FR-019: `owned` now REQUIRES both a conformance_check and a negative_control.
    # The default therefore carries both. A caller that drops the check must also drop the claim
    # -- see _unproven_surface -- because "owned with nothing checked" is exactly the default-value
    # misuse the tiered disposition was added to stop (it was true of 25 of 29 real surfaces).
    s = {
        "id": "a-surface", "path": "src/thing.py", "symbol": "sym", "kind": "exit-status",
        "consumers": ["c"], "governed_by": ["FR-007"], "conformance_check": "t::x",
        "negative_control": "t::x_negative",
        "owner": "test-lane", "disposition": "owned",
    }
    s.update(over)
    return s


def _unproven_surface(**over):
    """A surface honestly declared as not-yet-proven (feature 109 `declared-unproven`)."""
    s = _surface(conformance_check=None, negative_control=None,
                 disposition="declared-unproven")
    s.update(over)
    return s


# ---------------------------------------------------------------------------
# Manifest refusals -- each names the offending field, never defaults, never skips
# ---------------------------------------------------------------------------
def test_missing_manifest_is_refused(tmp_path):
    with pytest.raises(esa.ManifestError, match="manifest not found"):
        esa.load_manifest(str(tmp_path / "nope.json"))


def test_manifest_with_no_scoped_regions_is_refused(tmp_path):
    """The scope IS the denominator. No scope would let a manifest claim 100% of nothing."""
    with pytest.raises(esa.ManifestError, match="scoped_regions"):
        esa.load_manifest(_manifest(tmp_path, scoped_regions=[]))


def test_scoped_region_without_rationale_is_refused(tmp_path):
    with pytest.raises(esa.ManifestError, match="rationale"):
        esa.load_manifest(_manifest(tmp_path, scoped_regions=[{"path": "src"}]))


def test_surface_with_no_consumers_is_refused(tmp_path):
    """FR-002: a surface nobody reads as evidence is not evidence-bearing."""
    with pytest.raises(esa.ManifestError, match="consumers"):
        esa.load_manifest(_manifest(tmp_path, [_surface(consumers=[])]))


def test_duplicate_surface_id_is_refused(tmp_path):
    with pytest.raises(esa.ManifestError, match="duplicate id"):
        esa.load_manifest(_manifest(tmp_path, [_surface(), _surface()]))


def test_backslash_path_is_refused(tmp_path):
    """A Windows-shaped path makes the manifest differ per host for no reason."""
    with pytest.raises(esa.ManifestError, match="forward slashes"):
        esa.load_manifest(_manifest(tmp_path, [_surface(path="src\\thing.py")]))


def test_a_proven_fr004_claim_without_a_negative_control_is_refused(tmp_path):
    """FR-018a: a contention claim with no demonstrated way to fail is not evidence."""
    with pytest.raises(esa.ManifestError, match="negative_control"):
        esa.load_manifest(_manifest(tmp_path, [
            _surface(kind="wait", governed_by=["FR-004"], conformance_check="t::x",
                     negative_control=None)]))


def test_an_unproven_fr004_surface_is_ACCEPTED(tmp_path):
    """The negative control for the rule above.

    Declaring "this wait exists and I have not proven it" must be legal, or the only compliant
    answers are to lie or to leave it undeclared -- which manufactures the blind spot the rule
    was meant to close. Found by using this tool on this repo, 2026-09-06.
    """
    doc = esa.load_manifest(_manifest(tmp_path, [
        _unproven_surface(kind="wait", governed_by=["FR-004"])]))
    assert len(doc["surfaces"]) == 1


def test_a_proven_fr004_claim_with_wrong_iteration_count_is_refused(tmp_path):
    with pytest.raises(esa.ManifestError, match="iterations"):
        esa.load_manifest(_manifest(tmp_path, [
            _surface(kind="wait", governed_by=["FR-004"], conformance_check="t::x",
                     negative_control="t::neg", iterations=10, contention="load")]))


def test_disclosed_surface_owned_by_this_lane_is_refused(tmp_path):
    """'Disclosed' means someone else owns it. Disclosing to yourself is not disclosure."""
    with pytest.raises(esa.ManifestError, match="owner"):
        esa.load_manifest(_manifest(tmp_path, [
            _surface(disposition="disclosed", owner="test-lane",
                     disclosed_to="test-lane")]))


# ---------------------------------------------------------------------------
# Classification -- absence of evidence is never a pass
# ---------------------------------------------------------------------------
def test_a_surface_with_no_conformance_check_is_unproven_never_conforming(tmp_path):
    v = esa.classify(_unproven_surface(), str(tmp_path))
    assert v["classification"] == "unproven"
    assert "FR-015" in v["failed_frs"]


def test_a_cited_check_that_does_not_exist_is_not_conforming(tmp_path):
    """A conformance claim naming a test nobody wrote is a claim with no evidence (FR-016)."""
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "thing.py").write_text("x = 1\n", encoding="utf-8")
    v = esa.classify(_surface(conformance_check="src/absent_test.py::test_y"), str(tmp_path))
    assert v["classification"] == "non-conforming"
    assert "FR-016" in v["failed_frs"]


def test_a_disclosed_surface_is_non_conforming_and_names_its_owner(tmp_path):
    v = esa.classify(_surface(disposition="disclosed", owner="another-lane",
                              disclosed_to="another-lane"), str(tmp_path))
    assert v["classification"] == "non-conforming"
    assert "another-lane" in v["evidence"]


# ---------------------------------------------------------------------------
# Scan completeness -- the planted-site regression (FR-010)
# ---------------------------------------------------------------------------
PLANTED = "if proc.returncode == 0:\n    pass\n"


def test_scan_finds_a_known_planted_decision_site(tmp_path):
    """Assert on content only a WORKING scan could produce -- never on hit count alone.

    The audit once reported 1 hit against ~400 real ones and looked entirely healthy doing it.
    A test that only asserted 'the scan returned something' would have passed that run.
    """
    src = tmp_path / "src"
    src.mkdir()
    (src / "thing.py").write_text(PLANTED, encoding="utf-8")
    hits, examined, unexamined = esa.scan(str(tmp_path), [{"path": "src", "rationale": "r"}])
    assert any(h["symbol"] == "decision on returncode" and h["path"] == "src/thing.py"
               for h in hits), f"planted decision site not found; hits={hits}"
    assert "src/thing.py" in examined
    assert unexamined == []


def test_scan_with_broken_patterns_is_caught_by_the_planted_site(monkeypatch, tmp_path):
    """The NEGATIVE CONTROL for the test above (FR-018a).

    Reproduces the exact 2026-09-06 defect -- word-boundary escapes replaced by literal
    backspace bytes -- and asserts the planted-site check FAILS. Without this, the check above
    might simply be incapable of failing, and an unfalsifiable green is worth nothing.
    """
    broken = tuple(
        (kind, __import__("re").compile(rx.pattern.replace(r"\b", "\x08")), name)
        for kind, rx, name in esa.COMPILED)
    monkeypatch.setattr(esa, "COMPILED", broken)

    src = tmp_path / "src"
    src.mkdir()
    (src / "thing.py").write_text(PLANTED, encoding="utf-8")
    hits, _, _ = esa.scan(str(tmp_path), [{"path": "src", "rationale": "r"}])
    assert not any(h["symbol"] == "decision on returncode" for h in hits), (
        "the broken-pattern control did not reproduce the defect, so the planted-site test "
        "above discriminates nothing")


def test_excluded_dirs_inside_a_declared_scope_are_reported_not_pruned_silently(tmp_path):
    """FR-020 again: an exclusion inside a declared scope must be visible, not silently pruned."""
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "obj").mkdir()
    (tmp_path / "src" / "obj" / "gen.py").write_text(PLANTED, encoding="utf-8")
    _, _, unexamined = esa.scan(str(tmp_path), [{"path": "src", "rationale": "r"}])
    assert {"path": "src/obj/", "reason": "excluded-directory"} in unexamined


def test_out_of_scope_files_are_reported_not_omitted(tmp_path):
    """FR-020: an unexamined region that vanishes from the denominator is how coverage lies."""
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "in.py").write_text(PLANTED, encoding="utf-8")
    (tmp_path / "elsewhere").mkdir()
    (tmp_path / "elsewhere" / "out.py").write_text(PLANTED, encoding="utf-8")
    _, examined, unexamined = esa.scan(str(tmp_path), [{"path": "src", "rationale": "r"}])
    assert examined == ["src/in.py"]
    assert {"path": "elsewhere/out.py", "reason": "out-of-declared-scope"} in unexamined


# ---------------------------------------------------------------------------
# Cross-check -- both directions are errors (FR-014b)
# ---------------------------------------------------------------------------
def test_scan_only_hit_is_an_error(tmp_path):
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "thing.py").write_text(PLANTED, encoding="utf-8")
    m = {"surfaces": []}
    hits, _, _ = esa.scan(str(tmp_path), [{"path": "src", "rationale": "r"}])
    scan_only, manifest_only = esa.cross_check(m, hits, str(tmp_path))
    assert scan_only and not manifest_only


def test_manifest_only_entry_is_an_error(tmp_path):
    m = {"surfaces": [_surface(path="src/absent.py")]}
    scan_only, manifest_only = esa.cross_check(m, [], str(tmp_path))
    assert manifest_only == ["a-surface"]


def test_one_entry_does_not_silence_surplus_hits_of_the_SAME_kind(tmp_path):
    """A file with two waits declared once must still report the second (FR-014b).

    Matching on (path, kind) without a count let one entry cover every hit of that kind in the
    file, so the denominator shrank when you looked at it. Found by adversarial review.
    """
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "w.py").write_text(
        "wait_for_a()\nwait_for_b()\n", encoding="utf-8")
    hits, _, _ = esa.scan(str(tmp_path), [{"path": "src", "rationale": "r"}])
    m = {"surfaces": [_surface(path="src/w.py", kind="wait", governed_by=["FR-004"],
                               conformance_check=None)]}
    scan_only, _ = esa.cross_check(m, hits, str(tmp_path))
    assert len(scan_only) == 1 and scan_only[0]["surplus"] is True

    # Declaring sites=2 covers both -- explicitly, and visibly in the manifest.
    m["surfaces"][0]["sites"] = 2
    scan_only, _ = esa.cross_check(m, hits, str(tmp_path))
    assert scan_only == []


def test_one_declared_kind_does_not_silence_another_kind_in_the_same_file(tmp_path):
    """Matching on path alone would let one entry shrink the denominator for a whole file."""
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "thing.py").write_text(
        "if proc.returncode == 0:\n    pass\nif len(xs) == 0:\n    pass\n", encoding="utf-8")
    hits, _, _ = esa.scan(str(tmp_path), [{"path": "src", "rationale": "r"}])
    m = {"surfaces": [_surface(path="src/thing.py", kind="exit-status")]}
    scan_only, _ = esa.cross_check(m, hits, str(tmp_path))
    assert any(h["kind"] == "emptiness" for h in scan_only)


# ---------------------------------------------------------------------------
# Exit codes -- the contract this tool must not itself violate
# ---------------------------------------------------------------------------
def _run(tmp_path, manifest_path):
    # Clear the recursion-depth marker explicitly. Without this the fixture behaves differently
    # depending on whether pytest was started by a human or by the audit itself -- and a test
    # whose verdict depends on who invoked it is not evidence about the code. The audit's own
    # execution caught this, which is the point of it executing cited checks at all.
    env = dict(os.environ)
    env.pop(esa.DEPTH_ENV, None)
    return subprocess.run(
        [sys.executable, os.path.join(REPO, "scripts", "evidence_signal_audit.py"),
         "--repo", str(tmp_path), "--manifest", manifest_path,
         "--report", str(tmp_path / "out" / "report.json")],
        capture_output=True, text=True, env=env)


def test_exit_codes_are_distinct_per_failure_class(tmp_path):
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "thing.py").write_text(PLANTED, encoding="utf-8")

    # usage: a manifest we refuse
    bad = tmp_path / "bad.json"
    bad.write_text('{"version": 2}', encoding="utf-8")
    assert _run(tmp_path, str(bad)).returncode == esa.EXIT_USAGE

    # disagreement: the scan sees a decision site nobody declared
    assert _run(tmp_path, _manifest(tmp_path)).returncode == esa.EXIT_DISAGREEMENT

    # findings: declared, but unproven
    mp = _manifest(tmp_path, [_unproven_surface()])
    assert _run(tmp_path, mp).returncode == esa.EXIT_FINDINGS

    # clean -- the cited check must EXIST, because the audit verifies it (FR-016). Citing a
    # test nobody wrote is a conformance claim with no evidence, and the audit says so.
    # feature 109 FR-019: `owned` also needs a negative_control, and the audit VERIFIES that a
    # cited check EXISTS (FR-016) -- so the negative control has to be a real test, not a string.
    (tmp_path / "src" / "check_test.py").write_text(
        "def test_x():\n    pass\n\n\ndef test_x_negative():\n    pass\n", encoding="utf-8")
    mp = _manifest(tmp_path, [_surface(
        conformance_check="src/check_test.py::test_x",
        negative_control="src/check_test.py::test_x_negative")])
    r = _run(tmp_path, mp)
    assert r.returncode == esa.EXIT_CLEAN, r.stdout + r.stderr


def test_audit_never_exits_zero_while_reporting_a_problem(tmp_path):
    """Measured instance 4 was a tool exiting 0 while REFUSING. An audit for that class
    committing that class would be worthless, so this is checked directly rather than assumed."""
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "thing.py").write_text(PLANTED, encoding="utf-8")
    for surfaces in ([], [_unproven_surface()],
                     [_surface(disposition="disclosed", owner="other")]):
        r = _run(tmp_path, _manifest(tmp_path, surfaces))
        report = json.loads((tmp_path / "out" / "report.json").read_text(encoding="utf-8"))
        t = report["totals"]
        problem = t["errors"] or t["non_conforming"] or t["unproven"]
        assert not (problem and r.returncode == 0), (
            f"exited 0 while reporting a problem: totals={t}")


def test_receipt_records_the_target_as_resolved(tmp_path):
    """078 FR-003: a check that resolved somewhere other than intended must be visibly different."""
    (tmp_path / "src").mkdir()
    _run(tmp_path, _manifest(tmp_path, [_surface(conformance_check="t::x")]))
    report = json.loads((tmp_path / "out" / "report.json").read_text(encoding="utf-8"))
    receipt_name = os.path.basename(report["receipt_path"])
    receipt = json.loads((tmp_path / "out" / receipt_name).read_text(encoding="utf-8"))
    assert receipt["resolved_target"] == os.path.realpath(str(tmp_path))
    assert receipt["check"] == "evidence-signal-audit"
    assert receipt["outcome"] in ("PASS", "FAIL", "UNREAD")


# ---------------------------------------------------------------------------
# Feature 109 — the denominator (US3). Every test here has a negative control,
# because the defects these pin all LOOKED like clean runs.
# ---------------------------------------------------------------------------
def test_fr017_two_step_status_capture_is_found(tmp_path):
    """The idiom the repo's own suite actually uses, which the scan found ZERO of.

    test/run_all_tests.sh never writes `if [ $? -eq 0 ]`. It writes `MAD_EXIT=$?` and then
    `if [ $MAD_EXIT -eq 0 ]`. Before this, the repo's largest exit-status consumer -- a ~2900-line
    suite whose entire job is deciding on exit statuses -- contributed 0 hits, and the audit
    reported that as a clean scan.
    """
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "s.sh").write_text(
        'run_thing\nMAD_EXIT=$?\nif [ $MAD_EXIT -eq 0 ]; then echo ok; fi\n', encoding="utf-8")
    hits, _examined, _ux = esa.scan(str(tmp_path), [{"path": "src"}])
    symbols = {h["symbol"] for h in hits}
    assert any("capture of $?" in s for s in symbols), symbols


def test_fr017_negative_control_a_bare_report_of_status_is_NOT_a_decision_site(tmp_path):
    """The negative control for the rule above, and it is the reason the rule is narrow.

    FR-002 scopes a signal to where a consumer DECIDES on it. `echo "rc=$?"` reports and decides
    nothing. If this ever starts matching, the patterns have drifted back to matching MENTIONS,
    which produced 876 unactionable hits and an audit nobody ran.
    """
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "s.sh").write_text(
        'run_thing\necho "rc=$?"\nprintf "%s\n" "$?"\n', encoding="utf-8")
    hits, _examined, _ux = esa.scan(str(tmp_path), [{"path": "src"}])
    assert hits == [], hits


def test_fr017_capture_pattern_is_line_anchored_and_multiline_is_applied(tmp_path):
    """Regression for a defect introduced by this very feature and caught before it shipped.

    The capture pattern is line-anchored (`^VAR=$?$`). PATTERNS were compiled WITHOUT re.MULTILINE,
    so `^`/`$` matched only at the start and end of the whole file and the pattern silently matched
    nothing -- a dead regex reporting a clean scan, the same class as feature 108's own unmatchable
    patterns. Asserting the flag directly means a future refactor cannot quietly drop it.
    """
    assert all(rx.flags & esa.re.MULTILINE for _kind, rx, _name in esa.COMPILED)


def test_fr016_in_scope_unscannable_source_files_are_censused_not_dropped(tmp_path):
    """`regions UNREAD 0` used to be true and misleading at the same time.

    An unscannable file was skipped BEFORE the in-scope test, so it never entered `unexamined`
    and the region was still reported examined. 1651 real source files were invisible this way.
    """
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "a.py").write_text("x = 1\n", encoding="utf-8")
    (tmp_path / "src" / "b.gleam").write_text("pub fn main() { Nil }\n", encoding="utf-8")
    (tmp_path / "src" / "c.glp").write_text("foo(X, X?).\n", encoding="utf-8")
    _hits, examined, ux = esa.scan(str(tmp_path), [{"path": "src"}])
    census = esa._suffix_census(ux)
    assert census.get(".gleam") == 1, census
    assert census.get(".glp") == 1, census
    assert "src/a.py" in examined


def test_fr016_negative_control_an_all_scannable_region_censuses_zero(tmp_path):
    """If the census reported a number for a region with nothing unopened, it would be noise."""
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "a.py").write_text("x = 1\n", encoding="utf-8")
    _hits, _examined, ux = esa.scan(str(tmp_path), [{"path": "src"}])
    assert esa._suffix_census(ux) == {}


def test_fr016_non_source_files_never_pad_the_census(tmp_path):
    """A .pdf in a scoped region is not an unaudited evidence signal.

    Counting it as one would inflate the gap into a number nobody can act on -- the mirror image
    of the confident zero, and just as useless.
    """
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "doc.pdf").write_bytes(b"%PDF-1.4\n")
    (tmp_path / "src" / "notes.md").write_text("# hi\n", encoding="utf-8")
    _hits, _examined, ux = esa.scan(str(tmp_path), [{"path": "src"}])
    assert esa._suffix_census(ux) == {}
    assert any(u["reason"] == "non-source-file" for u in ux)


def test_fr018_every_declared_suffix_carries_a_rationale(tmp_path):
    """A language present in the repo and absent from the scan is a DECLARED gap, never a silent
    one. An unscanned suffix with no rationale is indistinguishable from an oversight."""
    assert esa.SUFFIX_DECLARATIONS
    for suffix, scanned, rationale in esa.SUFFIX_DECLARATIONS:
        assert suffix.startswith("."), suffix
        assert isinstance(rationale, str) and len(rationale) > 20, (suffix, rationale)
        if not scanned:
            assert "NOT SCANNED" in rationale, suffix
    assert set(esa.SCAN_SUFFIXES).isdisjoint(esa.UNSCANNED_SUFFIXES)


def test_fr019_owned_without_a_negative_control_is_refused(tmp_path):
    """`owned` is a claim, not a default. It was the default on 25 of 29 real surfaces."""
    with pytest.raises(esa.ManifestError, match="negative_control"):
        esa.load_manifest(_manifest(tmp_path, [_surface(negative_control=None)]))


def test_fr019_not_a_signal_without_a_rationale_is_refused(tmp_path):
    """Otherwise `not-a-signal` is the cheapest possible way to fake coverage."""
    with pytest.raises(esa.ManifestError, match="rationale"):
        esa.load_manifest(_manifest(tmp_path, [
            _surface(disposition="not-a-signal", conformance_check=None,
                     negative_control=None)]))


def test_fr019_negative_control_not_a_signal_WITH_a_rationale_is_accepted(tmp_path):
    """The rule must leave a legal way to say 'I looked, and it is not a signal'."""
    doc = esa.load_manifest(_manifest(tmp_path, [
        _surface(disposition="not-a-signal", conformance_check=None, negative_control=None,
                 rationale="this is the regex source that defines the pattern, not a call site")]))
    assert doc["surfaces"][0]["disposition"] == "not-a-signal"


def test_fr019_disclosed_without_a_named_owner_is_refused(tmp_path):
    """A defect disclosed to nobody is a defect kept."""
    with pytest.raises(esa.ManifestError, match="disclosed_to"):
        esa.load_manifest(_manifest(tmp_path, [
            _surface(disposition="disclosed", owner="other-lane")]))


def test_fr020_a_surface_with_no_disposition_is_refused(tmp_path):
    s = _surface()
    del s["disposition"]
    with pytest.raises(esa.ManifestError, match="disposition"):
        esa.load_manifest(_manifest(tmp_path, [s]))


def test_fr021_coverage_is_per_disposition_and_never_a_blended_percentage(tmp_path):
    """Four numbers cannot be gamed by dismissing things without the gaming being visible in
    WHICH number grew. One percentage can."""
    m = {"surfaces": [
        {"disposition": "owned"}, {"disposition": "owned"},
        {"disposition": "declared-unproven"}, {"disposition": "not-a-signal"},
        {"disposition": "disclosed"},
    ]}
    counts = esa._disposition_counts(m)
    assert counts["owned"] == 2
    assert counts["declared-unproven"] == 1
    assert counts["not-a-signal"] == 1
    assert counts["disclosed"] == 1
    assert set(counts) == set(esa.DISPOSITIONS)


# ---------------------------------------------------------------------------
# Feature 109 — the enforcing gate (US2). 108 shipped an audit that NAMES a defect
# and stops nothing; codexreview finding 8 recorded that the gate logic was a
# simulator in this harness, not enforcement in the audit. These tests exist so
# that stays fixed.
# ---------------------------------------------------------------------------
def _planted_repo(tmp_path):
    (tmp_path / "src").mkdir(exist_ok=True)
    (tmp_path / "src" / "thing.py").write_text(PLANTED, encoding="utf-8")


def _nonconforming(**over):
    """A surface that cites a check nobody wrote — non-conforming under FR-016."""
    return _surface(conformance_check="src/absent_test.py::test_y",
                    negative_control="src/absent_test.py::test_y_neg", **over)


def test_fr013_the_adoption_and_override_rules_have_exactly_ONE_implementation():
    """SC-004. FR-006b forbids a second override mechanism; this makes a second one FAIL.

    A copy would not announce itself — it would drift over weeks and the two would disagree
    about an expiry exactly once, in the run that mattered. Identity is checkable; 'we agreed
    not to copy it' is not.
    """
    gate = esa.load_gate()
    sys.path.insert(0, os.path.join(REPO, "codeconv", "src"))
    from codeconv.receipts import override as cc_override, manifest as cc_manifest
    assert cc_override.applies is gate.applies
    assert cc_override.record is gate.record
    assert cc_override.Override is gate.Override
    assert cc_manifest.GLPNET_AREAS is gate.GLPNET_AREAS


def test_fr014_the_audit_runs_with_codeconv_absent_from_sys_path(tmp_path):
    """The audit must keep working where the codeconv venv is not installed.

    A tool that did not run being read as 'nothing to report' is measured instance 4 — so making
    the gate depend on a venv would have reintroduced, inside the gate, the exact failure the
    audit exists to detect. `_run` spawns a subprocess whose sys.path never includes codeconv.
    """
    _planted_repo(tmp_path)
    mp = _manifest(tmp_path, [_surface(id="a-surface", conformance_check="src/c.py::t",
                                       negative_control="src/c.py::t_neg")])
    r = _run(tmp_path, mp)
    assert "ModuleNotFoundError" not in (r.stdout + r.stderr)
    assert "adoption_gate" not in (r.stderr or "")


def test_fr009_an_adopted_area_with_a_nonconforming_signal_REFUSES(tmp_path):
    _planted_repo(tmp_path)
    mp = _manifest(tmp_path, [_nonconforming()], area="test-harness", adoption="adopted")
    r = _run(tmp_path, mp)
    assert r.returncode == esa.EXIT_REFUSED, r.stdout + r.stderr
    assert "REFUSED" in r.stdout


def test_fr010_negative_control_a_NON_ADOPTED_area_does_not_refuse(tmp_path):
    """The phasing mechanism. Without this the gate would be all-or-nothing and would be turned
    off wholesale on its first bad day, which is how a gate stops existing."""
    _planted_repo(tmp_path)
    mp = _manifest(tmp_path, [_nonconforming()], area="coop", adoption="non-adopted")
    r = _run(tmp_path, mp)
    assert r.returncode != esa.EXIT_REFUSED, r.stdout + r.stderr
    assert r.returncode != esa.EXIT_CLEAN, "a non-conforming signal is still a finding"


def test_fr010_a_region_with_no_area_is_an_ERROR_never_a_pass(tmp_path):
    """Mirrors 078 FR-019/FR-020 exactly: absence is an error, never non-adoption."""
    with pytest.raises(esa.ManifestError, match="area"):
        esa.load_manifest(_manifest(
            tmp_path, scoped_regions=[{"path": "src", "rationale": "no area declared"}]))


def test_fr011_a_valid_in_scope_override_converts_refusal_into_a_recorded_proceed(tmp_path):
    gate = esa.load_gate()
    ov = gate.record(area="test-harness", check="evidence-signal-audit", reason="FR-016",
                     briefing="the cited check is being written under feature 109",
                     rationale="landing the gate before every check exists",
                     acknowledged=True, expiry="2099-01-01T00:00:00+00:00")
    verdicts = [{"id": "a-surface", "classification": "non-conforming", "failed_frs": ["FR-016"]}]
    _adoption(tmp_path, "adopted")
    m = {"scoped_regions": [{"path": "src", "area": "test-harness"}],
         "surfaces": [{"id": "a-surface", "path": "src/thing.py"}]}
    refusals, errors = esa.resolve_refusals(m, verdicts, str(tmp_path), overrides=[ov])
    assert refusals == [] and errors == []
    # FR-015: it is a RECORDED proceed, permanently visible — never a pass.
    assert verdicts[0]["override"]["scope"]["area"] == "test-harness"
    assert verdicts[0]["classification"] == "non-conforming"


def test_fr012_an_EXPIRED_override_resumes_refusing(tmp_path):
    gate = esa.load_gate()
    ov = gate.record(area="test-harness", check="evidence-signal-audit", reason="FR-016",
                     briefing="b", rationale="r", acknowledged=True,
                     expiry="2000-01-01T00:00:00+00:00")
    verdicts = [{"id": "a-surface", "classification": "non-conforming", "failed_frs": ["FR-016"]}]
    _adoption(tmp_path, "adopted")
    m = {"scoped_regions": [{"path": "src", "area": "test-harness"}],
         "surfaces": [{"id": "a-surface", "path": "src/thing.py"}]}
    refusals, errors = esa.resolve_refusals(m, verdicts, str(tmp_path), overrides=[ov])
    assert len(refusals) == 1 and errors == []


def test_fr012_an_override_with_no_expiry_is_rejected_AT_RECORD_TIME(tmp_path):
    """Not at the point of reliance. An indefinite override that is only rejected when someone
    tries to use it has already been written down and believed."""
    gate = esa.load_gate()
    with pytest.raises(gate.OverrideInvalid, match="expiry"):
        gate.record(area="test-harness", check="evidence-signal-audit", reason="FR-016",
                    briefing="b", rationale="r", acknowledged=True, expiry="")


def test_fr011_an_override_recorded_for_a_DIFFERENT_reason_does_not_apply(tmp_path):
    """One override, recorded for one refusal, must not authorise every other refusal the same
    check can raise until its expiry."""
    gate = esa.load_gate()
    ov = gate.record(area="test-harness", check="evidence-signal-audit", reason="FR-004",
                     briefing="b", rationale="r", acknowledged=True,
                     expiry="2099-01-01T00:00:00+00:00")
    verdicts = [{"id": "a-surface", "classification": "non-conforming", "failed_frs": ["FR-016"]}]
    _adoption(tmp_path, "adopted")
    m = {"scoped_regions": [{"path": "src", "area": "test-harness"}],
         "surfaces": [{"id": "a-surface", "path": "src/thing.py"}]}
    refusals, _errors = esa.resolve_refusals(m, verdicts, str(tmp_path), overrides=[ov])
    assert len(refusals) == 1
