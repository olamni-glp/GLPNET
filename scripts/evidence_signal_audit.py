#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Evidence-signal audit — feature 108, the complement of feature 078.

078 governs signals whose declared job is to state a VERDICT. This audits the other class:
signals that state no verdict but that callers read as evidence anyway — a wait returning, an
idle predicate, a liveness flag, a process exit status, an emptiness.

    THE INVARIANT
        A signal a caller treats as evidence must not be observable in a state that reports
        completion before the work it reports has completed -- and must not report completion
        for work that does not survive the next restart.

Two independent sources must agree about what exists (FR-014b): a DECLARED manifest, which is
the denominator for coverage, and a deliberately crude mechanical SCAN. The scan is not trying
to be complete. Its job is to disagree with the manifest when one of them is wrong. A crude
scan with a cross-check has a measurable blind spot; a clever scan without one has an invisible
blind spot, and an invisible blind spot silently becomes the coverage claim.

This script is subject to the invariant it audits (FR-017). It emits a receipt, it reports the
regions it could not examine rather than dropping them from the denominator, and it never exits
0 while reporting a problem -- exiting 0 while refusing is measured instance 4.

Stdlib only, by design: an audit that cannot run because a dependency is missing is the failure
mode it exists to prevent.
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import os
import re
import sys
from datetime import datetime, timezone

# ---------------------------------------------------------------------------
# Exit codes -- the contract this tool must not itself violate.
# ---------------------------------------------------------------------------
EXIT_CLEAN = 0            # report generated AND clean
EXIT_FINDINGS = 1         # non-conforming or unproven surfaces present
EXIT_USAGE = 2            # usage / manifest refusal
EXIT_DISAGREEMENT = 3     # manifest and scan disagree (FR-014b)
EXIT_UNEXAMINED = 4       # a region we were asked to examine could not be read (FR-020)

KINDS = ("wait", "idle-predicate", "liveness-flag", "exit-status", "emptiness")
GOVERNED = ("FR-004", "FR-007", "FR-012")

# ---------------------------------------------------------------------------
# Scan patterns.
#
# Deliberately crude and deliberately over-broad: a false positive costs one manifest entry
# with an honest classification, while a false negative is an invisible hole in the coverage
# claim. When in doubt the pattern stays in.
# ---------------------------------------------------------------------------
PATTERNS: tuple[tuple[str, str, str], ...] = (
    # (kind, regex, human name)
    #
    # SCOPE RULE, from FR-002: a signal is in scope when a consumer DECIDES on it -- not when
    # the token merely appears. `echo "exit=$?"` reports; `if [ $? -eq 0 ]` decides. Capturing
    # `p.returncode` into a variable propagates; `if p.returncode == 0:` decides.
    #
    # The first version of these patterns matched every MENTION and produced 876 hits across
    # 1406 files, which is not an audit -- it is a list nobody can act on, and an audit nobody
    # runs is exactly the evidence failure this feature exists to name. They were narrowed to
    # DECISION SITES on the principle above, which is a scope decision, NOT a way to make the
    # number smaller. The patterns stay deliberately over-broad WITHIN that scope: a false
    # positive costs one honest manifest entry, a false negative is an invisible hole.
    ("wait", r"\bWaitForIdle\s*\(", "WaitForIdle()"),
    ("wait", r"\bWaitFor(?!Idle)[A-Z]\w*\s*\(", "WaitFor*()"),
    ("wait", r"\bwait_for_\w+\s*\(", "wait_for_*()"),
    ("wait", r"\bquiesce\w*\s*\(", "quiesce()"),
    ("idle-predicate", r"\b(?:IsIdle|is_idle)\b", "IsIdle"),
    ("liveness-flag", r"\bif\s+\w*_met\b|\bassert\s+\w*_met\b", "decision on a *_met flag"),
    ("liveness-flag", r"\b(?:IsHealthy|is_healthy)\s*\)?\s*(?:\)|\{|:)", "decision on IsHealthy"),
    ("exit-status", r"\breturncode\s*[!=]=", "decision on returncode"),
    ("exit-status", r"\bif\s+\w*\.returncode\b\s*[:)]", "truthiness of returncode"),
    ("exit-status", r"\bExitCode\s*[!=]=", "decision on ExitCode"),
    ("exit-status", r"\b(?:check_call|check_output)\s*\(", "check_call (raises on non-zero)"),
    ("exit-status", r"\$\?\s*(?:-eq|-ne|==|!=)", "decision on $?"),
    ("exit-status", r"\bif\s*\[\s*\$\?", "branch on $?"),
    ("emptiness", r"\blen\([^)]*\)\s*==\s*0\b", "len(...) == 0 as a verdict"),
    ("emptiness", r"\bCount\s*==\s*0\b", "Count == 0 as a verdict"),
)
COMPILED = tuple((kind, re.compile(rx), name) for kind, rx, name in PATTERNS)

SCAN_SUFFIXES = (".py", ".cs", ".dart", ".sh", ".ps1")

# Regions never scanned. Each exclusion is a deliberate, stated decision, and every excluded
# region is REPORTED as unexamined rather than dropped (FR-020).
EXCLUDE_DIRS = (
    ".git", "node_modules", "bin", "obj", ".dart_tool", ".venv", "__pycache__",
    ".pgdb", ".pgdb.bridge.lock", "build", "dist", "_build", ".gleam",
)
EXCLUDE_GLOBS = ("*/archive/*", "*/wt-archive/*")


def _utcnow() -> str:
    return datetime.now(timezone.utc).isoformat()


# ---------------------------------------------------------------------------
# Manifest
# ---------------------------------------------------------------------------
class ManifestError(Exception):
    """A manifest we refuse. Always names the offending field -- never defaults, never skips."""


def load_manifest(path: str) -> dict:
    if not os.path.isfile(path):
        raise ManifestError(f"manifest not found: {path}")
    try:
        with open(path, "r", encoding="utf-8") as fh:
            raw = fh.read()
        doc = json.loads(raw)
    except (OSError, json.JSONDecodeError) as exc:
        raise ManifestError(f"manifest unreadable: {path}: {exc}") from exc
    validate_manifest(doc)
    doc["_sha256"] = hashlib.sha256(raw.encode("utf-8")).hexdigest()
    return doc


def validate_manifest(doc: object) -> None:
    if not isinstance(doc, dict):
        raise ManifestError("manifest: top level must be an object")
    if doc.get("version") != 1:
        raise ManifestError("manifest.version: must be 1")
    if not isinstance(doc.get("lane"), str) or not doc["lane"]:
        raise ManifestError("manifest.lane: must be a non-empty string")
    scoped = doc.get("scoped_regions")
    if not isinstance(scoped, list) or not scoped:
        raise ManifestError(
            "manifest.scoped_regions: at least one region required. The declared scope IS the "
            "SC-002 denominator; a manifest with no scope could claim 100% coverage of nothing.")
    for i, r in enumerate(scoped):
        if not isinstance(r, dict) or "path" not in r or "rationale" not in r:
            raise ManifestError(
                f"manifest.scoped_regions[{i}]: each region needs 'path' and 'rationale' -- an "
                "undocumented scope boundary is indistinguishable from an oversight.")
    surfaces = doc.get("surfaces")
    if not isinstance(surfaces, list):
        raise ManifestError("manifest.surfaces: must be an array")

    seen: set[str] = set()
    for idx, s in enumerate(surfaces):
        where = f"manifest.surfaces[{idx}]"
        if not isinstance(s, dict):
            raise ManifestError(f"{where}: must be an object")
        for field in ("id", "path", "symbol", "kind", "consumers",
                      "governed_by", "owner", "disposition"):
            if field not in s:
                raise ManifestError(f"{where}.{field}: required field is missing")
        if "conformance_check" not in s:
            raise ManifestError(f"{where}.conformance_check: required (use null if there is none)")

        sid = s["id"]
        if not isinstance(sid, str) or not re.fullmatch(r"[a-z0-9]+(-[a-z0-9]+)*", sid):
            raise ManifestError(f"{where}.id: must be kebab-case, got {sid!r}")
        if sid in seen:
            raise ManifestError(f"{where}.id: duplicate id {sid!r}")
        seen.add(sid)

        if "\\" in s["path"]:
            raise ManifestError(
                f"{where}.path: use forward slashes so the manifest is identical on every host")
        if s["kind"] not in KINDS:
            raise ManifestError(f"{where}.kind: must be one of {KINDS}, got {s['kind']!r}")
        if not isinstance(s["consumers"], list) or not s["consumers"]:
            raise ManifestError(
                f"{where}.consumers: at least one consumer required -- a surface nobody reads "
                "as evidence is not evidence-bearing and does not belong in the manifest (FR-002)")
        gov = s["governed_by"]
        if not isinstance(gov, list) or not gov or any(g not in GOVERNED for g in gov):
            raise ManifestError(f"{where}.governed_by: non-empty subset of {GOVERNED}")
        if s["disposition"] not in ("owned", "disclosed", "not-reproduced-on-this-build"):
            raise ManifestError(f"{where}.disposition: unrecognised {s['disposition']!r}")

        # FR-018a. A contention claim with no way to be wrong is worse than an absent entry,
        # so this is a REFUSAL, not an 'unproven' classification.
        #
        # It binds only when the entry CLAIMS to be proven. An entry with conformance_check=null
        # is declaring "this surface exists and is not yet proven", which is exactly the honest
        # state the quickstart tells people to start from -- refusing it would leave no legal way
        # to declare an unproven wait, and a rule whose only compliant answers are "lie" or
        # "leave it undeclared" manufactures the blind spot it was meant to close. Found by using
        # this tool on this repo, 2026-09-06.
        if "FR-004" in gov and s.get("conformance_check"):
            if not s.get("negative_control"):
                raise ManifestError(
                    f"{where}.negative_control: required whenever governed_by includes FR-004. "
                    "A contention property asserted with no demonstrated way to fail is not "
                    "evidence -- it is only evidence that a check ran (FR-018a).")
            if s.get("iterations") != 40:
                raise ManifestError(
                    f"{where}.iterations: must be 40 for an FR-004 surface (FR-018a), "
                    f"got {s.get('iterations')!r}")
            if not s.get("contention"):
                raise ManifestError(
                    f"{where}.contention: required alongside iterations, so a pass on an idle "
                    "host is not read as a pass under load (FR-018)")
        if s["disposition"] == "disclosed" and s["owner"] == doc["lane"]:
            raise ManifestError(
                f"{where}.owner: a 'disclosed' surface must name a lane other than {doc['lane']!r}")


# ---------------------------------------------------------------------------
# Scan
# ---------------------------------------------------------------------------
def _excluded(rel: str) -> bool:
    parts = rel.split("/")
    if any(p in EXCLUDE_DIRS for p in parts):
        return True
    return any(fnmatch.fnmatch("/" + rel, g) for g in EXCLUDE_GLOBS)


def _in_scope(rel: str, scoped: list[dict]) -> bool:
    return any(rel == r["path"] or rel.startswith(r["path"].rstrip("/") + "/") for r in scoped)


def scan(repo: str, scoped: list[dict]) -> tuple[list[dict], list[str], list[dict]]:
    """Return (hits, regions_examined, regions_unexamined).

    A file outside the declared scope is recorded in regions_unexamined with the reason
    'out-of-declared-scope'. It is REPORTED, never omitted (FR-020): an unexamined region that
    vanishes from the denominator is how a coverage claim becomes a lie.
    """
    hits: list[dict] = []
    examined: list[str] = []
    unexamined: list[dict] = []

    for dirpath, dirnames, filenames in os.walk(repo):
        rel_dir = os.path.relpath(dirpath, repo).replace(os.sep, "/")
        if rel_dir == ".":
            rel_dir = ""
        dirnames[:] = [d for d in dirnames if d not in EXCLUDE_DIRS]
        for fn in filenames:
            if not fn.endswith(SCAN_SUFFIXES):
                continue
            rel = f"{rel_dir}/{fn}" if rel_dir else fn
            if _excluded(rel):
                continue
            if not _in_scope(rel, scoped):
                unexamined.append({"path": rel, "reason": "out-of-declared-scope"})
                continue
            full = os.path.join(dirpath, fn)
            try:
                with open(full, "r", encoding="utf-8", errors="strict") as fh:
                    text = fh.read()
            except (OSError, UnicodeDecodeError) as exc:
                # Reported, never omitted (FR-020).
                unexamined.append({"path": rel, "reason": f"{type(exc).__name__}: {exc}"})
                continue
            examined.append(rel)
            for kind, rx, name in COMPILED:
                for m in rx.finditer(text):
                    line = text.count("\n", 0, m.start()) + 1
                    hits.append({"path": rel, "line": line, "symbol": name, "kind": kind})
    return hits, examined, unexamined


# ---------------------------------------------------------------------------
# Classification
# ---------------------------------------------------------------------------
def classify(surface: dict, repo: str) -> dict:
    """Classify one declared surface. Absence of evidence is UNPROVEN, never conforming."""
    failed: list[str] = []
    check = surface.get("conformance_check")

    if not check:
        # FR-015 -- absence is not a pass.
        return {
            "id": surface["id"],
            "classification": "unproven",
            "failed_frs": sorted(set(surface["governed_by"]) | {"FR-015"}),
            "consumers": surface["consumers"],
            "evidence": None,
        }

    if surface["disposition"] == "disclosed":
        # The check exists and is EXPECTED to fail: it is the disclosure mechanism for a defect
        # this lane does not own (Constitution II -- report, never patch another lane's tree).
        return {
            "id": surface["id"],
            "classification": "non-conforming",
            "failed_frs": sorted(set(surface["governed_by"])),
            "consumers": surface["consumers"],
            "evidence": f"{check} (disclosed; owner={surface['owner']})",
        }

    src = os.path.join(repo, surface["path"].replace("/", os.sep))
    if not os.path.exists(src):
        failed.append("FR-014b")

    # A cited check that does not exist is a claim with no evidence behind it -- which is the
    # defect this feature governs, committed by its own audit. Verify the file AND the symbol.
    # (Added 2026-09-06 after noticing classify() would report `conforming` for a surface whose
    # conformance_check named a test nobody had written.)
    for field in ("conformance_check", "negative_control"):
        ref = surface.get(field)
        if not ref:
            continue
        ref_path, _, sym = ref.partition("::")
        full = os.path.join(repo, ref_path.replace("/", os.sep))
        if not os.path.isfile(full):
            failed.append("FR-016")
            continue
        if sym:
            try:
                with open(full, "r", encoding="utf-8", errors="replace") as fh:
                    if sym not in fh.read():
                        failed.append("FR-016")
            except OSError:
                failed.append("FR-016")

    failed = sorted(set(failed))
    return {
        "id": surface["id"],
        "classification": "non-conforming" if failed else "conforming",
        "failed_frs": failed,
        "consumers": surface["consumers"],
        "evidence": check,
    }


def cross_check(manifest: dict, hits: list[dict], repo: str) -> tuple[list[dict], list[str]]:
    """Both directions of disagreement are errors (FR-014b)."""
    # Matched on (path, kind), NOT on path alone. Declaring one surface per FILE would let a
    # single entry silence every other kind of signal in that file -- a denominator that shrinks
    # when you look at it. One entry per (file, kind) is the finest granularity the crude scan
    # can honestly support.
    declared_paths = {(s["path"], s["kind"]) for s in manifest["surfaces"]}

    scan_only = [h for h in hits if (h["path"], h["kind"]) not in declared_paths]
    manifest_only = [
        s["id"] for s in manifest["surfaces"]
        if not os.path.exists(os.path.join(repo, s["path"].replace("/", os.sep)))
    ]
    return scan_only, manifest_only


# ---------------------------------------------------------------------------
# Receipt (FR-017) -- this audit is subject to the invariant it audits.
# ---------------------------------------------------------------------------
def write_receipt(path: str, repo_resolved: str, manifest_sha: str,
                  examined: int, skipped: list[dict], outcome: str) -> str:
    receipt = {
        "check": "evidence-signal-audit",
        "feature": "108-evidence-signal-ordering",
        "resolved_target": repo_resolved,   # as RESOLVED, not as requested (078 FR-003)
        "manifest_sha256": manifest_sha,
        "items_examined": examined,
        "items_skipped": len(skipped),
        "skipped_reasons": sorted({s["reason"].split(":")[0] for s in skipped}),
        "outcome": outcome,
        "ran_utc": _utcnow(),
    }
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(receipt, fh, indent=2)
        fh.write("\n")
    return path


# ---------------------------------------------------------------------------
# Driver
# ---------------------------------------------------------------------------
def audit(repo: str, manifest_path: str, report_path: str) -> tuple[dict, int]:
    manifest = load_manifest(manifest_path)
    hits, examined, unexamined = scan(repo, manifest["scoped_regions"])
    scan_only, manifest_only = cross_check(manifest, hits, repo)
    verdicts = [classify(s, repo) for s in manifest["surfaces"]]

    totals = {
        "conforming": sum(1 for v in verdicts if v["classification"] == "conforming"),
        "non_conforming": sum(1 for v in verdicts if v["classification"] == "non-conforming"),
        "unproven": sum(1 for v in verdicts if v["classification"] == "unproven"),
        "errors": len(scan_only) + len(manifest_only),
    }

    # Out-of-declared-scope is a stated boundary, not a read failure. A region we FAILED to
    # read is a different thing and still trips EXIT_UNEXAMINED. Both stay in the report.
    unread = [u for u in unexamined if u["reason"] != "out-of-declared-scope"]
    if totals["errors"]:
        code, outcome = EXIT_DISAGREEMENT, "FAIL"
    elif unread:
        code, outcome = EXIT_UNEXAMINED, "UNREAD"
    elif totals["non_conforming"] or totals["unproven"]:
        code, outcome = EXIT_FINDINGS, "FAIL"
    else:
        code, outcome = EXIT_CLEAN, "PASS"

    receipt_path = os.path.join(
        os.path.dirname(report_path),
        f"receipt-{datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')}.json")
    write_receipt(receipt_path, os.path.realpath(repo), manifest["_sha256"],
                  len(examined), unexamined, outcome)

    report = {
        "generated_utc": _utcnow(),
        "manifest_sha256": manifest["_sha256"],
        "surfaces": verdicts,
        "scan_only": scan_only,
        "manifest_only": manifest_only,
        "regions_examined": examined,
        "regions_unexamined": unexamined,
        "totals": totals,
        "receipt_path": os.path.relpath(receipt_path, repo).replace(os.sep, "/"),
    }
    os.makedirs(os.path.dirname(report_path), exist_ok=True)
    with open(report_path, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2)
        fh.write("\n")
    return report, code


def render(report: dict) -> str:
    t = report["totals"]
    out = [
        "evidence-signal audit (feature 108)",
        f"  manifest sha256   {report['manifest_sha256'][:16]}...",
        f"  regions examined  {len(report['regions_examined'])}",
        f"  out of scope      {sum(1 for u in report['regions_unexamined'] if u['reason'] == 'out-of-declared-scope')}",
        f"  regions UNREAD    {sum(1 for u in report['regions_unexamined'] if u['reason'] != 'out-of-declared-scope')}",
        f"  conforming        {t['conforming']}",
        f"  non-conforming    {t['non_conforming']}",
        f"  unproven          {t['unproven']}",
        f"  errors            {t['errors']}",
        f"  receipt           {report['receipt_path']}",
    ]
    for v in report["surfaces"]:
        if v["classification"] != "conforming":
            frs = ",".join(v["failed_frs"]) or "-"
            out.append(f"  [{v['classification']:>14}] {v['id']}  fails={frs}  "
                       f"consumers={len(v['consumers'])}")
    if report["scan_only"]:
        out.append(f"  ERROR: {len(report['scan_only'])} scan hit(s) absent from the manifest "
                   "-- undeclared evidence-bearing signals (FR-014b)")
        for h in report["scan_only"][:10]:
            out.append(f"    {h['path']}:{h['line']}  {h['symbol']} [{h['kind']}]")
        if len(report["scan_only"]) > 10:
            out.append(f"    ... and {len(report['scan_only']) - 10} more")
    for mid in report["manifest_only"]:
        out.append(f"  ERROR: manifest entry {mid!r} names a path the scan cannot locate (FR-014b)")
    for r in report["regions_unexamined"]:
        if r["reason"] != "out-of-declared-scope":
            out.append(f"  UNREAD: {r['path']}  ({r['reason']})")
    return "\n".join(out)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="Evidence-signal audit (feature 108)")
    default_repo = os.path.dirname(os.path.dirname(os.path.realpath(__file__)))
    ap.add_argument("--repo", default=default_repo,
                    help="repo root (defaults to this script's own repo, never the CWD)")
    ap.add_argument("--manifest", default=None)
    ap.add_argument("--report", default=None)
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args(argv)

    repo = os.path.realpath(args.repo)
    manifest_path = args.manifest or os.path.join(
        repo, ".specify", "evidence-signals", "manifest.json")
    report_path = args.report or os.path.join(
        repo, ".specify", "evidence-signals", "report.json")

    if not sys.stdout.isatty():
        # Measured: piping replaces $? with the pipe's status, which is how callers lose an
        # exit code. The canonical YNET client prints the same warning for the same reason.
        print("evidence-signal-audit: not a terminal -- if you piped this, $? is the PIPE's "
              "status, not mine. Run bare and check $? separately.", file=sys.stderr)

    try:
        report, code = audit(repo, manifest_path, report_path)
    except ManifestError as exc:
        print(f"evidence-signal-audit: REFUSED: {exc}", file=sys.stderr)
        return EXIT_USAGE

    print(json.dumps(report, indent=2) if args.json else render(report))
    return code


if __name__ == "__main__":
    sys.exit(main())
