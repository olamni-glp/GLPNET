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
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from xml.etree import ElementTree

# ---------------------------------------------------------------------------
# Exit codes -- the contract this tool must not itself violate.
# ---------------------------------------------------------------------------
EXIT_CLEAN = 0            # report generated AND clean
EXIT_FINDINGS = 1         # non-conforming or unproven surfaces present
EXIT_USAGE = 2            # usage / manifest refusal
EXIT_DISAGREEMENT = 3     # manifest and scan disagree (FR-014b)
EXIT_UNEXAMINED = 4       # a region we were asked to examine could not be read (FR-020)
EXIT_REFUSED = 5          # an ADOPTED area holds a non-conforming signal (109 FR-009)

KINDS = ("wait", "idle-predicate", "liveness-flag", "exit-status", "emptiness")

# FR-019 (feature 109), engineer ruling Q-olg17-03: the tiered disposition. Every declared site
# carries exactly one, and the required fields differ per tier -- see validate_manifest. Widening
# the audit to a region this lane does not own is only honest if "I looked and it is not a signal"
# and "it is a signal and someone else owns it" are both sayable, and both reviewable.
#
# `declared-unproven` was added when the per-tier rule was first ENFORCED and immediately found
# that 25 of the 29 existing surfaces claimed `owned` while carrying NO conformance_check and NO
# negative_control. `owned` had become the default value rather than a claim. The two ways out
# were to fabricate 25 checks -- which is the placeholder coverage engineer ruling Q-olg17-03
# exists to prevent -- or to give the honest state a name. This is the name: "this IS a signal,
# this lane DOES own it, and it is NOT yet proven". It is an extension of Q-olg17-03's three
# tiers, made under that ruling's stated principle that the burden is proportional to standing,
# and it is recorded as an extension rather than presented as part of the ruling.
DISPOSITIONS = ("owned", "declared-unproven", "not-a-signal", "disclosed",
                "not-reproduced-on-this-build")

# A nested invocation must not execute cited checks: this audit's own tests spawn it as a
# subprocess, and executing checks from inside one of those would re-enter pytest unbounded.
DEPTH_ENV = "EVIDENCE_SIGNAL_AUDIT_DEPTH"
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
    # FR-017 (feature 109). The two patterns above matched ZERO of the six-plus real decision
    # sites in test/run_all_tests.sh, which is the repo's largest exit-status consumer -- a
    # ~2900-line suite whose entire job is deciding on exit statuses. It does not write
    # `if [ $? -eq 0 ]`. It writes the TWO-STEP form:
    #
    #     MAD_EXIT=$?                    <- capture
    #     if [ $MAD_EXIT -eq 0 ]; then   <- decide
    #
    # The capture is the scope-defining event: FR-002 says a signal is in scope when a consumer
    # DECIDES on it, and a capture into a named variable exists in order to be decided on later
    # (`echo "rc=$?"` reports and is deliberately NOT matched -- it neither captures nor decides).
    # Matching the capture rather than the branch also means the site is found once, at the point
    # the status becomes evidence, instead of once per branch that reads it.
    ("exit-status", r"^\s*(?:local\s+|declare\s+)?[A-Za-z_]\w*=\$\?\s*(?:#.*)?$",
     "capture of $? into a variable (two-step decision)"),
    ("exit-status", r"\bif\s*\[\s*\"?\$\{?[A-Za-z_]\w*(?:_(?:EXIT|RC|STATUS|CODE))\}?\"?\s*(?:-eq|-ne)",
     "branch on a captured status variable"),
    ("exit-status", r"\$LASTEXITCODE\s*(?:-eq|-ne|==|!=)", "decision on $LASTEXITCODE"),
    ("emptiness", r"\blen\([^)]*\)\s*==\s*0\b", "len(...) == 0 as a verdict"),
    ("emptiness", r"\bCount\s*==\s*0\b", "Count == 0 as a verdict"),
)
# re.MULTILINE, and it is load-bearing rather than cosmetic. The FR-017 capture pattern is
# line-anchored (`^VAR=$?$`) so that it matches a whole-line capture and not a `$?` buried in a
# larger expression. Without MULTILINE, `^` and `$` match only at the start and end of the WHOLE
# FILE, so that pattern silently matched nothing -- a dead regex that reports a clean scan, which
# is the same class of defect as feature 108's own unmatchable patterns (1 hit against ~400 real
# ones, with the exit code and the report both looking fine). Pinned by a negative-control test.
COMPILED = tuple((kind, re.compile(rx, re.MULTILINE), name) for kind, rx, name in PATTERNS)

# ---------------------------------------------------------------------------
# Declared suffix set (FR-018, feature 109).
#
# This used to be a bare 5-tuple, and that was a measured defect, not a style problem.
# `scan()` dropped a file whose suffix was absent BEFORE testing whether it was in scope, so an
# unscannable file never reached `unexamined` and never appeared in the report. The audit
# therefore printed `regions UNREAD 0` while never opening 1651 files INSIDE the regions it
# called examined: 223 `.gleam`, 1416 `.glp` and 12 `.mjs`, measured 2026-09-06. `glp_gleam/src`
# scanned to `examined=0, sites=0`, which reads as CLEAN and means NEVER LOOKED AT.
#
# That is this feature's own thesis turned on itself: an unexamined surface counts against the
# total. The set is now DECLARED -- every suffix present in a scoped region is listed with a
# rationale, and the unscanned ones are PRINTED on every run, so the gap is visible rather than
# structural and silent.
SUFFIX_DECLARATIONS: tuple[tuple[str, bool, str], ...] = (
    (".py",    True,  "Python: the audit, the harness and codeconv. Patterns cover returncode, "
                      "check_call and len()==0 decision sites."),
    (".cs",    True,  "C#: the YNET client and transport this lane owns. Patterns cover ExitCode, "
                      "WaitFor*, IsIdle, IsHealthy and Count==0."),
    (".dart",  True,  "Dart: the GLP runtime and REPL."),
    (".sh",    True,  "Bash: the REPL suite, which is the repo's largest exit-status consumer."),
    (".ps1",   True,  "PowerShell: launchers and host probes."),
    (".gleam", False, "NOT SCANNED -- 223 files. Gleam's idioms (Result/Option, `case`) share no "
                      "token with the five kinds, so the current patterns would find nothing and "
                      "report a confident zero. Closing this needs Gleam-specific patterns and is "
                      "a declared follow-up, NOT a claim of cleanliness."),
    (".glp",   False, "NOT SCANNED -- 1416 files. GLP has no exit status and no process; its "
                      "evidence signals are suspension and guard outcomes, a different kind set "
                      "that this feature does not define. Declared gap, not an omission."),
    (".mjs",   False, "NOT SCANNED -- 12 files, and this is the one that matters most: "
                      "prereq-patterns/pglite/pglite_bridge.mjs is the repo's most load-bearing "
                      "readiness/liveness surface and is invisible to this audit. Highest-priority "
                      "follow-up of the three."),
)
SCAN_SUFFIXES = tuple(s for s, scanned, _ in SUFFIX_DECLARATIONS if scanned)
UNSCANNED_SUFFIXES = tuple(s for s, scanned, _ in SUFFIX_DECLARATIONS if not scanned)

# Suffixes that carry executable logic and could therefore hold an evidence signal. A file whose
# suffix is here but which is NOT in SCAN_SUFFIXES is a real, countable gap. Anything else in a
# scoped region (documents, archives, binaries, build leftovers) is recorded but never censused
# by suffix -- see the note at its use site.
SOURCE_SUFFIXES = frozenset(SCAN_SUFFIXES) | frozenset(UNSCANNED_SUFFIXES) | frozenset({
    ".erl", ".ex", ".exs", ".js", ".ts", ".mts", ".cjs", ".go", ".rs", ".java", ".scala",
    ".kt", ".rb", ".pl", ".psm1", ".bash", ".zsh", ".fs", ".fsx", ".c", ".h", ".cc", ".cpp",
})

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
# The enforcing gate (feature 109 US2, FR-009..FR-015)
#
# 108 shipped an audit that NAMES non-conforming signals and stops nothing. codexreview
# finding 8 said so plainly: the classifier, the size detector and the override logic were
# simulators in the test harness, not enforcement in the audit. A report that names a defect
# and permits it has the same shape as the defects it names -- it answers "did we notice?"
# while the reader hears "are we safe?".
#
# The rules come from ONE shared implementation (engineer ruling Q-olg17-02); this module never
# re-implements them. Loading is by relative path with NO venv, because FR-014 requires the
# audit to keep running where codeconv is absent.
# ---------------------------------------------------------------------------
_GATE_MODULE_NAME = "glpnet_adoption_gate"


def load_gate():
    """Import the shared adoption/override rules, stdlib only.

    Registers in ``sys.modules`` BEFORE executing: the module defines dataclasses, and
    ``@dataclass`` looks its own class's module up in ``sys.modules`` while decorating. Skipping
    the registration raises a bare AttributeError from inside dataclasses -- measured while
    writing this, and exactly the kind of failure that would read as "the gate is unavailable".
    """
    if _GATE_MODULE_NAME in sys.modules:
        return sys.modules[_GATE_MODULE_NAME]
    import importlib.util
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib", "adoption_gate.py")
    if not os.path.isfile(path):
        raise RuntimeError(
            f"the shared adoption/override rules are missing at {path}. This is NOT a reason to "
            "fall back to a local copy -- a second override mechanism is what feature 108 FR-006b "
            "forbids. Restore the file.")
    spec = importlib.util.spec_from_file_location(_GATE_MODULE_NAME, path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[_GATE_MODULE_NAME] = mod
    spec.loader.exec_module(mod)
    return mod


def resolve_refusals(manifest: dict, verdicts: list[dict], repo: str,
                     overrides: list | None = None) -> tuple[list[dict], list[str]]:
    """Decide which non-conforming surfaces REFUSE (FR-009/010/011/012).

    Returns ``(refusals, errors)``.

    Three states, and the third is the one that matters:
      * area declared ``adopted``      -> a non-conforming signal REFUSES, unless a valid,
                                          in-scope, unexpired override covers it;
      * area declared ``non-adopted``  -> no refusal; the surface carries a visible marker;
      * area NOT DECLARED AT ALL       -> an ERROR, never non-adoption, never a pass. This
                                          mirrors 078 FR-019/FR-020 exactly, so one rule
                                          governs both features (FR-010).
    """
    gate = load_gate()
    errors: list[str] = []
    refusals: list[dict] = []

    region_area = {r["path"]: r.get("area") for r in manifest.get("scoped_regions", [])}
    undeclared_regions = [p for p, a in region_area.items() if not a]
    if undeclared_regions:
        errors.append(
            "scoped_regions without an 'area': %s -- an area with NO declaration is an error, "
            "not non-adoption (FR-010, mirroring 078 FR-019/FR-020). Declare the region's area "
            "and declare that area's adoption state in .specify/receipts/adoption.json."
            % sorted(undeclared_regions))
        return refusals, errors

    try:
        adoption = gate.load_adoption(os.path.join(repo, gate.ADOPTION_MANIFEST_REL))
    except (gate.MissingDeclaration, gate.UndeclaredState) as exc:
        errors.append(f"adoption manifest: {exc}")
        return refusals, errors

    # A verdict carries no path -- it is keyed by id -- so the surface's path comes from the
    # manifest. Looking it up rather than trusting a field the verdict does not have is the
    # difference between a gate and a KeyError at the moment it is first asked to refuse.
    surface_path = {s["id"]: s["path"] for s in manifest.get("surfaces", [])}

    for v in verdicts:
        if v["classification"] != "non-conforming":
            continue
        spath = surface_path.get(v["id"])
        if spath is None:
            errors.append(f"surface {v['id']}: not present in the manifest")
            continue
        area = None
        # Longest declared region wins, so a nested region overrides its parent rather than
        # whichever happened to be declared first.
        for path, a in sorted(region_area.items(), key=lambda kv: -len(kv[0])):
            if spath == path or spath.startswith(path.rstrip("/") + "/"):
                area = a
                break
        if area is None:
            errors.append(f"surface {v['id']}: no scoped region covers {spath}")
            continue
        try:
            state = gate.adoption_state(adoption, area)
        except gate.MissingDeclaration as exc:
            errors.append(f"surface {v['id']}: {exc}")
            continue
        if state != "adopted":
            v["non_adoption_marker"] = (
                f"area {area!r} is declared {state!r}: this signal is NOT gated, and that is a "
                "declared decision, not a clean result")
            continue

        reason = ",".join(v["failed_frs"]) or "non-conforming"
        covered = None
        for ov in (overrides or []):
            if gate.applies(ov, area, "evidence-signal-audit", reason):
                covered = ov
                break
        if covered is not None:
            # FR-015: an override converts a refusal into a RECORDED, EXPIRING, SCOPED proceed.
            # It is never a pass, and it stays in the receipt permanently.
            v["override"] = covered.to_json()
            continue
        refusals.append({"id": v["id"], "area": area, "path": spath,
                         "failed_frs": v["failed_frs"]})
    return refusals, errors


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


def _req_str(obj: dict, field: str, where: str, allow_none: bool = False) -> None:
    """Type-check before use.

    Without this a manifest carrying `"path": 1` reached `"\\" in s["path"]` and raised a bare
    TypeError -- bypassing the field-named refusal this module promises and turning a bad
    manifest into a stack trace. A refusal that a crash can pre-empt is not a refusal.
    Found by adversarial review, 2026-09-06.
    """
    v = obj.get(field)
    if allow_none and v is None:
        return
    if not isinstance(v, str) or not v:
        raise ManifestError(f"{where}.{field}: must be a non-empty string, got {v!r}")


def validate_manifest(doc: object) -> None:
    if not isinstance(doc, dict):
        raise ManifestError("manifest: top level must be an object")
    if doc.get("version") != 1:
        raise ManifestError("manifest.version: must be 1")
    _req_str(doc, "lane", "manifest")
    scoped = doc.get("scoped_regions")
    if not isinstance(scoped, list) or not scoped:
        raise ManifestError(
            "manifest.scoped_regions: at least one region required. The declared scope IS the "
            "SC-002 denominator; a manifest with no scope could claim 100% coverage of nothing.")
    for i, r in enumerate(scoped):
        where = f"manifest.scoped_regions[{i}]"
        if not isinstance(r, dict):
            raise ManifestError(f"{where}: must be an object")
        for field in ("path", "rationale", "area"):
            if field not in r:
                raise ManifestError(
                    f"{where}.{field}: each region needs 'path', 'rationale' and 'area' -- an "
                    "undocumented scope boundary is indistinguishable from an oversight, and a "
                    "region with no 'area' cannot be gated because nothing says whether its "
                    "producing area has declared adoption (109 FR-010, mirroring 078 FR-020: "
                    "no declaration is an ERROR, never non-adoption).")
        _req_str(r, "path", where)
        _req_str(r, "rationale", where)
        _req_str(r, "area", where)
        if "\\" in r["path"]:
            raise ManifestError(f"{where}.path: use forward slashes, not backslashes")
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

        for field in ("id", "path", "symbol", "kind", "owner", "disposition"):
            _req_str(s, field, where)
        for field in ("conformance_check", "negative_control", "contention", "notes",
                      "rationale", "disclosed_to"):
            _req_str(s, field, where, allow_none=True)
        if s.get("iterations") is not None and not isinstance(s["iterations"], int):
            raise ManifestError(f"{where}.iterations: must be an integer or null")
        sites = s.get("sites", 1)
        if not isinstance(sites, int) or sites < 1:
            raise ManifestError(
                f"{where}.sites: must be a positive integer (default 1). It declares how many "
                "scan hits of this (path, kind) the entry covers, so widening coverage is a "
                "visible act rather than an invisible one.")

        sid = s["id"]
        if not re.fullmatch(r"[a-z0-9]+(-[a-z0-9]+)*", sid):
            raise ManifestError(f"{where}.id: must be kebab-case, got {sid!r}")
        if sid in seen:
            raise ManifestError(f"{where}.id: duplicate id {sid!r}")
        seen.add(sid)

        if "\\" in s["path"]:
            raise ManifestError(
                f"{where}.path: use forward slashes so the manifest is identical on every host")
        if s["kind"] not in KINDS:
            raise ManifestError(f"{where}.kind: must be one of {KINDS}, got {s['kind']!r}")
        cons = s["consumers"]
        if (not isinstance(cons, list) or not cons
                or any(not isinstance(c, str) or not c for c in cons)):
            raise ManifestError(
                f"{where}.consumers: at least one non-empty consumer required -- a surface nobody "
                "reads as evidence is not evidence-bearing and does not belong here (FR-002)")
        gov = s["governed_by"]
        if not isinstance(gov, list) or not gov or any(g not in GOVERNED for g in gov):
            raise ManifestError(f"{where}.governed_by: non-empty subset of {GOVERNED}")
        if s["disposition"] not in DISPOSITIONS:
            raise ManifestError(f"{where}.disposition: unrecognised {s['disposition']!r}; "
                                f"must be one of {DISPOSITIONS}")

        # FR-019 (feature 109), engineer ruling Q-olg17-03. The declaration burden is
        # PROPORTIONAL TO STANDING, and each disposition is refused unless it carries the field
        # that makes it honest. Without this, `not-a-signal` is a free pass: a lane could dispose
        # of its way to a clean report one word at a time. With it, every disposition has to say
        # something a reviewer can disagree with.
        if s["disposition"] == "owned":
            if not s.get("conformance_check"):
                raise ManifestError(
                    f"{where}: disposition 'owned' requires a conformance_check. Owning a signal "
                    "and not checking it is the claim this feature exists to refuse.")
            if not s.get("negative_control"):
                raise ManifestError(
                    f"{where}: disposition 'owned' requires a negative_control. A check that "
                    "cannot fail has measured nothing, whatever it printed.")
        elif s["disposition"] == "not-a-signal":
            if not s.get("rationale"):
                raise ManifestError(
                    f"{where}: disposition 'not-a-signal' requires a 'rationale' saying why this "
                    "site is not read as evidence. An unexplained dismissal is indistinguishable "
                    "from an oversight, and is the cheapest way to fake coverage.")
        elif s["disposition"] == "disclosed":
            if not s.get("disclosed_to"):
                raise ManifestError(
                    f"{where}: disposition 'disclosed' requires 'disclosed_to' naming the owning "
                    "lane. A defect disclosed to nobody is a defect kept.")

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

    Every file not examined is recorded with a reason: 'out-of-declared-scope',
    'excluded-directory', 'excluded-glob', or a read error. It is REPORTED, never omitted
    (FR-020): an unexamined region that vanishes from the denominator is how a coverage claim
    becomes a lie. Exclusions used to be pruned SILENTLY, contradicting this module's own
    comment -- found by adversarial review, 2026-09-06.
    """
    hits: list[dict] = []
    examined: list[str] = []
    unexamined: list[dict] = []

    for dirpath, dirnames, filenames in os.walk(repo):
        rel_dir = os.path.relpath(dirpath, repo).replace(os.sep, "/")
        if rel_dir == ".":
            rel_dir = ""
        pruned = [d for d in dirnames if d in EXCLUDE_DIRS]
        dirnames[:] = [d for d in dirnames if d not in EXCLUDE_DIRS]
        for d in pruned:
            rel_d = f"{rel_dir}/{d}" if rel_dir else d
            if _in_scope(rel_d, scoped):
                unexamined.append({"path": rel_d + "/", "reason": "excluded-directory"})

        for fn in filenames:
            rel = f"{rel_dir}/{fn}" if rel_dir else fn
            if not fn.endswith(SCAN_SUFFIXES):
                # FR-016 (feature 109). This `continue` used to sit BEFORE the in-scope test, so
                # an unscannable file inside a declared region vanished from the report entirely
                # and the region was still called examined. A file we cannot open inside a region
                # we claim to have examined is UNEXAMINED, and it is now recorded as such, named
                # by suffix, so the census can be reported per region.
                if _in_scope(rel, scoped) and not _excluded(rel):
                    ext = os.path.splitext(fn)[1].lower()
                    # Only SOURCE files are censused by suffix. A `.pdf`, `.zip` or `.md` inside a
                    # scoped region is not an unaudited evidence signal, and counting it as one
                    # would inflate the gap number into something nobody can act on -- the mirror
                    # image of the confident zero, and just as useless. Non-source files are still
                    # recorded, but aggregated under one reason so they cannot pad the census.
                    reason = (f"unscannable-suffix:{ext}" if ext in SOURCE_SUFFIXES
                              else "non-source-file")
                    unexamined.append({"path": rel, "reason": reason})
                continue
            in_scope = _in_scope(rel, scoped)
            if _excluded(rel):
                if in_scope:
                    unexamined.append({"path": rel, "reason": "excluded-glob"})
                continue
            if not in_scope:
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
# Executing the cited checks (FR-016) -- existence is not evidence
# ---------------------------------------------------------------------------
def _executable_refs(manifest: dict) -> list[str]:
    refs = []
    for s in manifest["surfaces"]:
        for field in ("conformance_check", "negative_control"):
            ref = s.get(field)
            if ref and ref.split("::")[0].endswith(".py"):
                refs.append(ref)
    return sorted(set(refs))


def execute_checks(repo: str, manifest: dict) -> dict:
    """Run every cited Python check; return {ref: 'pass'|'fail'|'not-executable'}.

    A cited check that merely EXISTS is not evidence. A test can be broken, emptied or skipped
    while its NAME stays in the file, and the surface would still read `conforming` -- which is
    precisely this feature's class committed by its own audit: a signal reporting completion for
    work that did not happen. Found by adversarial review, 2026-09-06.

    Non-Python references (a C# test method, say) cannot be executed from here. They are
    reported 'not-executable' and classify the surface UNPROVEN, never conforming. "I could not
    check this" is the honest answer; assuming it passed is measured instance 2.
    """
    refs = _executable_refs(manifest)
    if not refs:
        return {}
    if os.environ.get(DEPTH_ENV):
        return {r: "not-executable" for r in refs}

    env = dict(os.environ)
    env[DEPTH_ENV] = "1"
    env["PYTHONUTF8"] = "1"
    with tempfile.TemporaryDirectory() as td:
        xml = os.path.join(td, "junit.xml")
        try:
            subprocess.run(
                [sys.executable, "-m", "pytest", "-q", f"--junit-xml={xml}", *refs],
                cwd=repo, env=env, capture_output=True, text=True, timeout=900)
        except (OSError, subprocess.SubprocessError):
            return {r: "not-executable" for r in refs}
        if not os.path.isfile(xml):
            return {r: "not-executable" for r in refs}
        try:
            root = ElementTree.parse(xml).getroot()
        except ElementTree.ParseError:
            return {r: "not-executable" for r in refs}

    seen = {}
    for case in root.iter("testcase"):
        name = case.get("name") or ""
        bad = any(case.find(t) is not None for t in ("failure", "error", "skipped"))
        seen[name] = "fail" if bad else "pass"

    out = {}
    for ref in refs:
        _, _, sym = ref.partition("::")
        out[ref] = seen.get(sym, "not-executable")
    return out


# ---------------------------------------------------------------------------
# Classification
# ---------------------------------------------------------------------------
def classify(surface: dict, repo: str, results: dict | None = None) -> dict:
    """Classify one declared surface. Absence of evidence is UNPROVEN, never conforming."""
    results = results or {}
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
    unexecuted = False
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
                        continue
            except OSError:
                failed.append("FR-016")
                continue
        outcome = results.get(ref)
        if outcome == "fail":
            failed.append("FR-016")
        elif outcome != "pass":
            unexecuted = True

    failed = sorted(set(failed))
    if failed:
        classification = "non-conforming"
    elif unexecuted:
        # We could not RUN the evidence. Not a pass, not a failure -- an unproven surface, and
        # saying so is the whole reason the classification exists.
        classification = "unproven"
        failed = sorted(set(surface["governed_by"]) | {"FR-016"})
    else:
        classification = "conforming"

    return {
        "id": surface["id"],
        "classification": classification,
        "failed_frs": failed,
        "consumers": surface["consumers"],
        "evidence": check if classification != "unproven" else f"{check} (not executed here)",
    }


def cross_check(manifest: dict, hits: list[dict], repo: str) -> tuple[list[dict], list[str]]:
    """Both directions of disagreement are errors (FR-014b)."""
    # Matched on (path, kind) AND ON COUNT.
    #
    # Matching on path alone let one entry silence every other KIND in a file. Matching on
    # (path, kind) without a count let one entry silence every other hit OF THAT KIND in the
    # same file -- so a file with three waits was covered by declaring one, and the denominator
    # shrank when you looked at it. Surplus hits beyond the number of declared surfaces for a
    # (path, kind) are now reported. Found by adversarial review, 2026-09-06.
    declared: dict = {}
    for s in manifest["surfaces"]:
        key = (s["path"], s["kind"])
        declared[key] = declared.get(key, 0) + int(s.get("sites", 1))

    scan_only = []
    used: dict = {}
    for h in sorted(hits, key=lambda x: (x["path"], x["kind"], x["line"])):
        key = (h["path"], h["kind"])
        if used.get(key, 0) < declared.get(key, 0):
            used[key] = used.get(key, 0) + 1
            continue
        scan_only.append(dict(h, surplus=key in declared))
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
    results = execute_checks(repo, manifest)
    verdicts = [classify(s, repo, results) for s in manifest["surfaces"]]

    totals = {
        "conforming": sum(1 for v in verdicts if v["classification"] == "conforming"),
        "non_conforming": sum(1 for v in verdicts if v["classification"] == "non-conforming"),
        "unproven": sum(1 for v in verdicts if v["classification"] == "unproven"),
        "errors": len(scan_only) + len(manifest_only),
    }

    # A stated scope boundary is not a read failure. A region we FAILED to read is a different
    # thing and still trips EXIT_UNEXAMINED. Both stay in the report either way.
    #
    # FR-016/FR-018 (feature 109) add a THIRD category, and keeping it distinct is the whole
    # point. A file inside a scoped region whose suffix this audit does not scan is:
    #   - NOT a scope boundary  -- it is inside the declared scope;
    #   - NOT a read failure    -- we can read it, we have declared that we do not scan it.
    # It is a DECLARED GAP. Folding it into `unread` would make every run exit UNEXAMINED and the
    # signal would be turned off within a day; folding it into BOUNDARY would hide it, which is
    # the confident zero this feature exists to remove. So it is counted, named by suffix, and
    # printed on every run -- visible without being fatal.
    BOUNDARY = ("out-of-declared-scope", "excluded-directory", "excluded-glob")
    unread = [u for u in unexamined
              if u["reason"] not in BOUNDARY and not _is_declared_gap(u["reason"])]
    unopened_by_suffix = _suffix_census(unexamined)
    # FR-009: the gate. Resolved AFTER classification and BEFORE the exit-code decision, because
    # a refusal must outrank a mere finding -- an adopted area holding a non-conforming signal is
    # not "one more row in a list", it is the thing that stops the pipeline.
    refusals, gate_errors = resolve_refusals(manifest, verdicts, repo)
    totals["refusals"] = len(refusals)
    totals["errors"] += len(gate_errors)

    if totals["errors"]:
        code, outcome = EXIT_DISAGREEMENT, "FAIL"
    elif refusals:
        code, outcome = EXIT_REFUSED, "REFUSED"
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
        "check_results": results,
        "scan_only": scan_only,
        "manifest_only": manifest_only,
        "regions_examined": examined,
        "regions_unexamined": unexamined,
        "unopened_by_suffix": unopened_by_suffix,
        "suffix_declarations": [
            {"suffix": s, "scanned": scanned, "rationale": why}
            for s, scanned, why in SUFFIX_DECLARATIONS
        ],
        "disposition_counts": _disposition_counts(manifest),
        "refusals": refusals,
        "gate_errors": gate_errors,
        "totals": totals,
        "receipt_path": os.path.relpath(receipt_path, repo).replace(os.sep, "/"),
    }
    os.makedirs(os.path.dirname(report_path), exist_ok=True)
    with open(report_path, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2)
        fh.write("\n")
    return report, code


BOUNDARY = ("out-of-declared-scope", "excluded-directory", "excluded-glob")


def _disposition_counts(manifest: dict) -> dict[str, int]:
    """Per-disposition counts — the coverage statement (FR-021).

    Deliberately NOT collapsed into one percentage. A blended figure makes `owned` (checked, with
    a negative control) indistinguishable from `not-a-signal` (dismissed with a sentence), so a
    lane could raise its coverage number by dismissing things. Four numbers cannot be gamed that
    way without the gaming being visible in which number grew.
    """
    counts = {d: 0 for d in DISPOSITIONS}
    for s in manifest.get("surfaces", []):
        d = s.get("disposition")
        if d in counts:
            counts[d] += 1
    return counts


def _is_declared_gap(reason: str) -> bool:
    """A file inside scope that this audit has declared it does not scan (FR-018)."""
    return reason.startswith("unscannable-suffix:") or reason == "non-source-file"


def _suffix_census(unexamined: list[dict]) -> dict[str, int]:
    """Count in-scope source files left unopened, by suffix (FR-016).

    This is the number that did not exist before feature 109. The audit reported
    `regions UNREAD 0` while never opening 1651 source files inside regions it called examined,
    because an unscannable file was dropped before the in-scope test and so never entered the
    report at all. A region is no longer callable 'examined' on the strength of the subset the
    scanner happens to read.
    """
    census: dict[str, int] = {}
    for u in unexamined:
        r = u["reason"]
        if r.startswith("unscannable-suffix:"):
            census[r.split(":", 1)[1]] = census.get(r.split(":", 1)[1], 0) + 1
    return dict(sorted(census.items(), key=lambda kv: -kv[1]))


def render(report: dict) -> str:
    t = report["totals"]
    ux = report["regions_unexamined"]
    cr = report.get("check_results", {})
    out = [
        "evidence-signal audit (feature 108)",
        f"  manifest sha256   {report['manifest_sha256'][:16]}...",
        f"  regions examined  {len(report['regions_examined'])}",
        f"  scope boundary    {sum(1 for u in ux if u['reason'] in BOUNDARY)}",
        f"  regions UNREAD    "
        f"{sum(1 for u in ux if u['reason'] not in BOUNDARY and not _is_declared_gap(u['reason']))}",
        f"  checks executed   {sum(1 for v in cr.values() if v == 'pass')} pass"
        f" / {sum(1 for v in cr.values() if v == 'fail')} fail"
        f" / {sum(1 for v in cr.values() if v == 'not-executable')} not-executable",
        f"  conforming        {t['conforming']}",
        f"  non-conforming    {t['non_conforming']}",
        f"  unproven          {t['unproven']}",
        f"  errors            {t['errors']}",
        f"  REFUSALS          {t.get('refusals', 0)}",
        f"  receipt           {report['receipt_path']}",
    ]
    for r in report.get("refusals", []):
        out.append(f"  [      REFUSED] {r['id']}  area={r['area']} (ADOPTED)  "
                   f"fails={','.join(r['failed_frs'])}")
    for e in report.get("gate_errors", []):
        out.append(f"  GATE ERROR: {e}")

    # FR-021: per-disposition counts ARE the coverage statement. No blended percentage is
    # printed anywhere, deliberately -- see _disposition_counts.
    dc = report.get("disposition_counts") or {}
    if dc:
        out.append("  disposition       "
                   + "  ".join(f"{k}={v}" for k, v in dc.items() if v or k == "owned"))

    # FR-016: in-scope source files this audit did NOT open, by suffix. Before feature 109 this
    # number did not exist and the same run printed `regions UNREAD 0`.
    census = report.get("unopened_by_suffix") or {}
    if census:
        total = sum(census.values())
        out.append(f"  UNOPENED in scope {total} source file(s) -- "
                   + ", ".join(f"{k} x{v}" for k, v in census.items()))
        out.append("                    (declared gaps, not clean: see suffix_declarations "
                   "in the report for the rationale on each)")
    for v in report["surfaces"]:
        if v["classification"] != "conforming":
            frs = ",".join(v["failed_frs"]) or "-"
            out.append(f"  [{v['classification']:>14}] {v['id']}  fails={frs}  "
                       f"consumers={len(v['consumers'])}")
    if report["scan_only"]:
        surplus = sum(1 for h in report["scan_only"] if h.get("surplus"))
        out.append(f"  ERROR: {len(report['scan_only'])} scan hit(s) not covered by the manifest "
                   f"({surplus} surplus beyond a declared (path,kind)) -- FR-014b")
        for h in report["scan_only"][:10]:
            tag = " [SURPLUS]" if h.get("surplus") else ""
            out.append(f"    {h['path']}:{h['line']}  {h['symbol']} [{h['kind']}]{tag}")
        if len(report["scan_only"]) > 10:
            out.append(f"    ... and {len(report['scan_only']) - 10} more")
    for mid in report["manifest_only"]:
        out.append(f"  ERROR: manifest entry {mid!r} names a path the scan cannot locate (FR-014b)")
    for r in ux:
        if r["reason"] not in BOUNDARY:
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
