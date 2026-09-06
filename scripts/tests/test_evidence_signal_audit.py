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


def _manifest(tmp_path, surfaces=None, **over):
    doc = {
        "version": 1,
        "lane": "test-lane",
        "scoped_regions": [{"path": "src", "rationale": "the test fixture tree"}],
        "surfaces": surfaces if surfaces is not None else [],
    }
    doc.update(over)
    p = tmp_path / "manifest.json"
    p.write_text(json.dumps(doc), encoding="utf-8")
    return str(p)


def _surface(**over):
    s = {
        "id": "a-surface", "path": "src/thing.py", "symbol": "sym", "kind": "exit-status",
        "consumers": ["c"], "governed_by": ["FR-007"], "conformance_check": "t::x",
        "owner": "test-lane", "disposition": "owned",
    }
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
            _surface(kind="wait", governed_by=["FR-004"], conformance_check="t::x")]))


def test_an_unproven_fr004_surface_is_ACCEPTED(tmp_path):
    """The negative control for the rule above.

    Declaring "this wait exists and I have not proven it" must be legal, or the only compliant
    answers are to lie or to leave it undeclared -- which manufactures the blind spot the rule
    was meant to close. Found by using this tool on this repo, 2026-09-06.
    """
    doc = esa.load_manifest(_manifest(tmp_path, [
        _surface(kind="wait", governed_by=["FR-004"], conformance_check=None)]))
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
            _surface(disposition="disclosed", owner="test-lane")]))


# ---------------------------------------------------------------------------
# Classification -- absence of evidence is never a pass
# ---------------------------------------------------------------------------
def test_a_surface_with_no_conformance_check_is_unproven_never_conforming(tmp_path):
    v = esa.classify(_surface(conformance_check=None), str(tmp_path))
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
    v = esa.classify(_surface(disposition="disclosed", owner="another-lane"), str(tmp_path))
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
    mp = _manifest(tmp_path, [_surface(conformance_check=None)])
    assert _run(tmp_path, mp).returncode == esa.EXIT_FINDINGS

    # clean -- the cited check must EXIST, because the audit verifies it (FR-016). Citing a
    # test nobody wrote is a conformance claim with no evidence, and the audit says so.
    (tmp_path / "src" / "check_test.py").write_text(
        "def test_x():\n    pass\n", encoding="utf-8")
    mp = _manifest(tmp_path, [_surface(conformance_check="src/check_test.py::test_x")])
    r = _run(tmp_path, mp)
    assert r.returncode == esa.EXIT_CLEAN, r.stdout + r.stderr


def test_audit_never_exits_zero_while_reporting_a_problem(tmp_path):
    """Measured instance 4 was a tool exiting 0 while REFUSING. An audit for that class
    committing that class would be worthless, so this is checked directly rather than assumed."""
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "thing.py").write_text(PLANTED, encoding="utf-8")
    for surfaces in ([], [_surface(conformance_check=None)],
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
