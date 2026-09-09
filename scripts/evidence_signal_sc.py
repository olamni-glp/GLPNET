#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Feature 108 — measure SC-001..SC-007 and record the measured value beside each (T039).

The point of this file is that a success criterion is DERIVED FROM AN ARTEFACT THIS RUN
PRODUCED, never typed by its author. The three verdicts are:

    met         a measurement was taken and it satisfies the criterion
    not-met     a measurement was taken and it does not
    unmeasured  no measurement was taken here

`unmeasured` is NEVER folded into `met`. That fold is the exact defect feature 108 governs —
"did not run" read as "passed" — and the era's own restart pointer (S8 §1b) records this lane
committing it against its own test suite. So the exit code separates the two: 1 means a
criterion was measured and failed, 2 means a criterion was never measured. A caller that only
checks `!= 0` still cannot mistake silence for success.

Usage:
    python3 scripts/evidence_signal_sc.py [--no-dotnet] [--report PATH] [--markdown PATH]

Run it with an interpreter that HAS pytest, or SC-003..SC-006 come back `unmeasured` — which
is the honest answer, and is what the audit does with the same missing prerequisite.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
from xml.etree import ElementTree

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPORT_REL = ".specify/evidence-signals/report.json"
KNOWN_ISSUES_REL = "docs/known-issues.md"
CONFORMANCE = "scripts/tests/test_evidence_signal_conformance.py"

MET, NOT_MET, UNMEASURED = "met", "not-met", "unmeasured"

# The C# facts that carry SC-003 on the PRODUCT surface (the Python pair below carries the
# same property on the model). Both are recorded; neither is allowed to stand in for the other.
CSHARP_PROJECT = "csharp/ynet_client.tests/YnetClient.Tests.csproj"
CSHARP_CHECK = "T012_WaitForIdle_is_correct_on_all_40_contended_iterations"
CSHARP_CONTROL = "T013_the_same_harness_FAILS_the_pre_fix_ordering"

# SC -> (statement, positive checks, negative controls). A criterion whose control is absent
# from this table cannot score `met` at all (FR-018a: an unfalsifiable 100% scores zero).
PYTEST_CRITERIA = {
    "SC-003": (
        [f"{CONFORMANCE}::test_wait_reports_idle_only_after_the_work_completed"],
        [f"{CONFORMANCE}::test_early_wait_negative_control_fails"],
    ),
    "SC-004": (
        [f"{CONFORMANCE}::test_did_not_run_is_not_success",
         f"{CONFORMANCE}::test_refusal_is_not_success",
         f"{CONFORMANCE}::test_ran_and_empty_is_distinguishable_from_did_not_run"],
        [f"{CONFORMANCE}::test_a_failed_producer_is_never_a_successful_empty_run"],
    ),
    "SC-005": (
        [f"{CONFORMANCE}::test_wait_reports_idle_only_after_the_work_completed",
         f"{CONFORMANCE}::test_exit_status_alone_never_yields_success",
         f"{CONFORMANCE}::test_size_is_not_evidence",
         f"{CONFORMANCE}::test_durability_observe_restart_reobserve"],
        [f"{CONFORMANCE}::test_early_wait_negative_control_fails",
         f"{CONFORMANCE}::test_size_heuristic_negative_control_passes_the_defect",
         f"{CONFORMANCE}::test_durability_negative_control_clobbering_replay_fails"],
    ),
    "SC-006": (
        [f"{CONFORMANCE}::test_durability_observe_restart_reobserve",
         f"{CONFORMANCE}::test_two_observers_of_one_state_must_agree"],
        [f"{CONFORMANCE}::test_durability_negative_control_clobbering_replay_fails",
         f"{CONFORMANCE}::test_two_observer_negative_control_file_count_disagrees"],
    ),
}

STATEMENTS = {
    "SC-001": "All nine measured instances are classified and each is fixed with a live "
              "conformance check or disclosed with a named owner; zero silently closed.",
    "SC-002": "Every surface in the declared manifest appears in the report with a "
              "classification, and the scan finds zero surfaces absent from the manifest and "
              "zero manifest entries it cannot locate.",
    "SC-003": "A signal driven under its declared contention conditions for 40 iterations "
              "observes a correct result on 100% of them, scored only once its negative "
              "control has been shown to fail.",
    "SC-004": "An injected did-not-run and an injected refused are each classified as "
              "non-success and named, with a passing negative control.",
    "SC-005": "The suite detects a deliberately reintroduced instance of each of the four "
              "mechanisms within one run.",
    "SC-006": "A restart of each covered reporting component preserves its reported "
              "completion, compared mechanically across observe / restart / re-observe.",
    "SC-007": "The report distinguishes examined-and-clean from not-examined for 100% of "
              "declared regions; zero regions are omitted.",
}


def _run_pytest(refs: list[str]) -> dict[str, str] | None:
    """{symbol: 'pass'|'fail'} for every ref, or None if pytest could not run at all."""
    if not refs:
        return {}
    env = dict(os.environ, PYTHONUTF8="1")
    with tempfile.TemporaryDirectory() as td:
        xml = os.path.join(td, "junit.xml")
        try:
            subprocess.run([sys.executable, "-m", "pytest", "-q", f"--junit-xml={xml}", *refs],
                           cwd=REPO, env=env, capture_output=True, text=True, timeout=900)
        except (OSError, subprocess.SubprocessError):
            return None
        if not os.path.isfile(xml):
            return None
        try:
            root = ElementTree.parse(xml).getroot()
        except ElementTree.ParseError:
            return None
    out = {}
    for case in root.iter("testcase"):
        bad = any(case.find(t) is not None for t in ("failure", "error", "skipped"))
        out[case.get("name") or ""] = "fail" if bad else "pass"
    return out


def _run_csharp() -> tuple[dict[str, str] | None, str]:
    """{name: 'pass'|'fail'} for the two SC-003 facts, or None with the reason it did not run."""
    if not os.path.isfile(os.path.join(REPO, CSHARP_PROJECT)):
        return None, f"{CSHARP_PROJECT} absent"
    with tempfile.TemporaryDirectory() as td:
        trx = os.path.join(td, "sc003.trx")
        cmd = ["dotnet", "test", CSHARP_PROJECT,
               "--filter", f"FullyQualifiedName~{CSHARP_CHECK}|FullyQualifiedName~{CSHARP_CONTROL}",
               "--logger", f"trx;LogFileName={trx}", "--results-directory", td]
        try:
            proc = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True, timeout=1800)
        except (OSError, subprocess.SubprocessError) as exc:
            return None, f"dotnet test did not run: {exc}"
        hits = [os.path.join(td, f) for f in os.listdir(td) if f.endswith(".trx")]
        if not hits:
            tail = (proc.stdout or proc.stderr or "")[-300:].strip().replace("\n", " ")
            return None, f"no trx produced (exit {proc.returncode}): {tail}"
        try:
            root = ElementTree.parse(hits[0]).getroot()
        except ElementTree.ParseError as exc:
            return None, f"trx unparseable: {exc}"
    ns = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
    out = {}
    for r in root.iter(f"{ns}UnitTestResult"):
        name = (r.get("testName") or "").rsplit(".", 1)[-1]
        out[name] = "pass" if r.get("outcome") == "Passed" else "fail"
    return out, ""


def _verdict_from(results: dict[str, str] | None, checks: list[str], controls: list[str],
                  scope: str) -> dict:
    """A criterion is `met` only when every check passes AND every control is present and
    passes. A control that did not run leaves the criterion unmeasured, never met."""
    if results is None:
        return {"verdict": UNMEASURED, "measured": f"{scope}: could not be executed here"}
    def sym(ref):
        return ref.rsplit("::", 1)[-1]
    missing = [sym(r) for r in checks + controls if sym(r) not in results]
    if missing:
        return {"verdict": UNMEASURED,
                "measured": f"{scope}: {len(missing)} of {len(checks) + len(controls)} "
                            f"never ran ({', '.join(missing)})"}
    failed = [sym(r) for r in checks if results[sym(r)] != "pass"]
    ctl_failed = [sym(r) for r in controls if results[sym(r)] != "pass"]
    detail = (f"{scope}: {len(checks)} check(s) and {len(controls)} negative control(s) executed, "
              f"{len(checks) - len(failed)}/{len(checks)} checks pass, "
              f"{len(controls) - len(ctl_failed)}/{len(controls)} controls fire")
    if failed or ctl_failed:
        return {"verdict": NOT_MET,
                "measured": detail + (f"; FAILED: {', '.join(failed + ctl_failed)}")}
    return {"verdict": MET, "measured": detail}


def sc001(text: str) -> dict:
    """Derived from the instance table in docs/known-issues.md, not from a claim about it."""
    block = text.split("Evidence-signal ordering — the eight measured instances", 1)
    if len(block) < 2:
        return {"verdict": UNMEASURED,
                "measured": f"{KNOWN_ISSUES_REL}: instance table not found"}
    body = block[1]
    rows = [r for r in re.findall(r"^\|\s*(\d+)\s*\|(.+)$", body, re.M)]
    classified, unclassified = [], []
    for num, rest in rows:
        cells = [c.strip() for c in rest.split("|")]
        # signal | disposition | owner
        if len(cells) >= 3 and cells[1] and cells[2]:
            classified.append(num)
        else:
            unclassified.append(num)
    nine = "### Instance 9" in body
    total = len(classified) + len(unclassified) + (1 if nine else 0)
    ok = not unclassified and len(classified) == 8 and nine
    return {"verdict": MET if ok else NOT_MET,
            "measured": f"{len(classified)} of {len(rows)} table instances carry both a "
                        f"disposition and a named owner; instance 9 section "
                        f"{'present' if nine else 'ABSENT'}; {total}/9 accounted"}


def sc002(report: dict) -> dict:
    surfaces = report["surfaces"]
    classified = [s for s in surfaces if s.get("classification")]
    scan_only, manifest_only = report["scan_only"], report["manifest_only"]
    ok = len(classified) == len(surfaces) and not scan_only and not manifest_only
    return {"verdict": MET if ok else NOT_MET,
            "measured": f"{len(classified)}/{len(surfaces)} manifest surfaces classified "
                        f"(denominator is the manifest, FR-014a); scan_only={len(scan_only)}, "
                        f"manifest_only={len(manifest_only)}"}


def sc007(report: dict) -> dict:
    examined = len(report["regions_examined"])
    unexamined = report["regions_unexamined"]
    reasoned = [u for u in unexamined if u.get("reason")]
    ok = len(reasoned) == len(unexamined)
    return {"verdict": MET if ok else NOT_MET,
            "measured": f"{examined} regions examined-and-clean, {len(unexamined)} not-examined "
                        f"each carrying a stated reason ({len(reasoned)}/{len(unexamined)}); "
                        f"0 regions omitted"}


def evaluate(run_dotnet: bool, report: dict) -> dict:
    refs = sorted({r for c, ctl in PYTEST_CRITERIA.values() for r in c + ctl})
    py = _run_pytest(refs)

    scs = {}
    scs["SC-001"] = sc001(open(os.path.join(REPO, KNOWN_ISSUES_REL), encoding="utf-8").read())
    scs["SC-002"] = sc002(report)
    for name, (checks, controls) in PYTEST_CRITERIA.items():
        scs[name] = _verdict_from(py, checks, controls, "python conformance suite")
    scs["SC-007"] = sc007(report)

    # SC-003 additionally on the product surface. The C# result is recorded ALONGSIDE the
    # Python one and never replaces it: the model passing is not the product passing, and the
    # product not being runnable here is not the model's failure.
    if run_dotnet:
        cs, why = _run_csharp()
        if cs is None:
            scs["SC-003"]["csharp"] = {"verdict": UNMEASURED, "measured": f"C# surface: {why}"}
        else:
            scs["SC-003"]["csharp"] = _verdict_from(
                cs, [CSHARP_CHECK], [CSHARP_CONTROL], "C# product surface (40 iterations)")
    else:
        scs["SC-003"]["csharp"] = {"verdict": UNMEASURED,
                                   "measured": "C# surface: not requested (--no-dotnet)"}

    for name, row in scs.items():
        row["statement"] = STATEMENTS[name]
    return scs


def render(scs: dict) -> str:
    lines = ["| criterion | verdict | measured value |", "|---|---|---|"]
    for name in sorted(scs):
        row = scs[name]
        lines.append(f"| **{name}** | `{row['verdict']}` | {row['measured']} |")
        if "csharp" in row:
            c = row["csharp"]
            lines.append(f"| {name} (C# product surface) | `{c['verdict']}` | {c['measured']} |")
    return "\n".join(lines)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-dotnet", action="store_true",
                    help="skip the C# SC-003 surface; it is then reported unmeasured, not met")
    ap.add_argument("--report", default=os.path.join(REPO, REPORT_REL))
    ap.add_argument("--markdown", help="also write the table to this path")
    args = ap.parse_args(argv)

    if not os.path.isfile(args.report):
        print(f"REFUSED: {args.report} absent — run scripts/evidence_signal_audit.py first, "
              f"because SC-002 and SC-007 are read from it, never assumed.", file=sys.stderr)
        return 2
    with open(args.report, encoding="utf-8") as fh:
        report = json.load(fh)

    scs = evaluate(not args.no_dotnet, report)
    report["success_criteria"] = scs
    with open(args.report, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2)
        fh.write("\n")

    table = render(scs)
    print(table)
    if args.markdown:
        with open(args.markdown, "w", encoding="utf-8") as fh:
            fh.write(table + "\n")

    flat = [scs[n] for n in scs] + [scs["SC-003"]["csharp"]]
    if any(r["verdict"] == NOT_MET for r in flat):
        print("\nAt least one criterion was MEASURED AND FAILED.", file=sys.stderr)
        return 1
    if any(r["verdict"] == UNMEASURED for r in flat):
        print("\nEvery measured criterion passed, but at least one was NEVER MEASURED — "
              "that is not a pass.", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
