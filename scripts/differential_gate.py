# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""The differential acceptance harness (feature 109, US1).

THE QUESTION THIS ANSWERS, AND THE ONE IT REPLACES
--------------------------------------------------
A criterion that says "Dart, C# and Gleam agree on goal-term acceptance" is a claim about
AGREEMENT BETWEEN RUNTIMES. Until this harness existed, the suite discharged such criteria by
running ONE runtime and reporting green. Measured on 2026-09-04 (feature 101): the feature was
recorded implemented, `CLAUDE.md` and `docs/known-issues.md` both named the exact C# lines the
fix had landed at, THOSE LINES WERE STILL DEFECTIVE, C# still returned a silent wrong answer for
an improper list tail, and the Gleam half had shipped with no test file at all. None of it was
visible, because nothing in the 566-check suite had ever started a second runtime.

So the harness reports exactly one of three outcomes per criterion, and only the first may be
treated as discharged:

    MEASURED-AGREE     every declared participant ran, produced non-empty output, and the
                       normalised transcripts are byte-identical
    MEASURED-DIVERGE   every participant ran and the transcripts differ; the divergence is
                       printed
    NOT-MEASURED       something prevented the measurement, and the reason and the participant
                       are named

NOT-MEASURED is not a skip. A skipped check disappears; a NOT-MEASURED criterion is reported,
counted, and makes this tool exit non-zero. That distinction is the whole point: "the tool did
not run" being read as "nothing to report" is measured instance 4.

FOUR PROPERTIES THAT ARE EASY TO GET WRONG, AND ARE ENFORCED HERE RATHER THAN ADVISED
--------------------------------------------------------------------------------------
1.  **Two empty transcripts compare equal.**  A runtime that starts, exits 0 and prints nothing
    produces a clean diff against another runtime that did the same, and a naive comparator
    calls that agreement.  FR-004: the non-emptiness guard runs BEFORE the comparison, on the
    RAW transcript, and two empty transcripts yield NOT-MEASURED.

2.  **A normaliser is a claim about what is irrelevant.**  "Strip the prompt" is a claim that
    the prompt cannot carry a real divergence.  Each normalisation therefore carries its own
    negative control -- a pair of strings that differ in a way that MATTERS -- and the harness
    EXECUTES it, asserting the pair still differs after the rule is applied.  A normaliser that
    erases its own control erases real divergences, and the criterion is NOT-MEASURED.
    (FR-006.)

3.  **A comparator that has never failed has measured nothing.**  Every criterion declares a
    negative control of its own: a perturbation applied to one participant's RAW transcript,
    which must make the criterion report MEASURED-DIVERGE.  It is executed on every single run,
    against the transcripts actually captured on that run -- not against a fixture.  A criterion
    whose negative control did not run, or ran and did not diverge, is reported NOT-MEASURED,
    exactly as a missing participant is.  (FR-007, SC-002.)

4.  **A one-participant "differential" is a category error.**  Not a degenerate case to be
    tolerated -- a declaration with fewer than two participants is refused at LOAD time, before
    anything runs.  (FR-005.)

WHAT MEASURED-AGREE DOES NOT MEAN (FR-008)
-------------------------------------------
Agreement is a statement about agreement.  Participants that are broken identically agree.
Differential testing cannot detect that and this tool does not claim to; the renderer prints the
disclaimer next to the result rather than leaving the reader to supply it.

RELATIONSHIP TO `test/run_all_tests.sh` V-18..V-23
---------------------------------------------------
V-18..V-23 is the hand-written reference implementation of exactly one criterion (Dart vs C#
goal-term acceptance).  This harness generalises it; it does not replace it.  Suite Section Y
runs this harness and Y-4 asserts the two agree on that shared criterion, so a future divergence
between the general mechanism and the hand-written original is a suite failure rather than a
silent drift.

STDLIB ONLY, deliberately -- same reasoning as `scripts/lib/adoption_gate.py`.  A gate that
cannot run because a virtual environment is missing is the failure mode it exists to prevent.
"""

from __future__ import annotations

import argparse
import difflib
import json
import os
import re
import shutil
import subprocess
import sys

# ---------------------------------------------------------------------------
# Outcomes and exit codes
# ---------------------------------------------------------------------------
AGREE = "MEASURED-AGREE"
DIVERGE = "MEASURED-DIVERGE"
NOT_MEASURED = "NOT-MEASURED"

EXIT_AGREE = 0          # every declared criterion is MEASURED-AGREE
EXIT_DIVERGE = 1        # at least one criterion is MEASURED-DIVERGE
EXIT_USAGE = 2          # the declaration itself was refused (load time)
EXIT_NOT_MEASURED = 3   # at least one criterion could not be measured; NEVER 0

DEFAULT_TIMEOUT = 180


class DeclarationError(Exception):
    """The declaration is refused. Raised at LOAD time, before anything is started."""


# ---------------------------------------------------------------------------
# Placeholder resolution
#
# The declaration names tools symbolically (`${DART}`, `${CSREPL}`) so that the same file is
# readable on a host that does not have them. Resolution failure is a MEASUREMENT failure with a
# named reason -- never a silent skip and never a substituted default.
# ---------------------------------------------------------------------------
_PLACEHOLDER = re.compile(r"\$\{([A-Z_][A-Z0-9_]*)\}")


def build_environment(repo: str) -> dict[str, str]:
    """Resolve the symbols a declaration may reference. Unresolvable symbols are ABSENT from the
    returned mapping rather than empty-string, so that referencing one is a loud failure."""
    env: dict[str, str] = {"REPO": repo}
    env["GLP_RUNTIME"] = os.path.join(repo, "glp_runtime")
    # Forward slashes: this symbol is substituted into REPL INPUT (a `load` line), not into an
    # argv entry, and the suite feeds the same path through `cygpath -m` for the same reason --
    # a backslash inside REPL input is a lexical hazard, not a path separator.
    env["TYPED"] = os.path.join(repo, "programs", "tests", "typed").replace("\\", "/")

    dart = os.environ.get("DART") or shutil.which("dart")
    if dart and os.path.exists(dart):
        env["DART"] = dart

    # Mirror the suite: prefer the compiled kernel snapshot, fall back to source. Both are
    # relative to GLP_RUNTIME, which is the participant's declared cwd.
    snapshot = os.path.join(env["GLP_RUNTIME"], ".dart_tool", "repl.dill")
    env["DART_REPL"] = ".dart_tool/repl.dill" if os.path.exists(snapshot) else "bin/glp_repl.dart"

    tfm = _csproj_tfm(os.path.join(repo, "out", "csharp", "glp_repl", "glp_repl.csproj"))
    if tfm:
        env["CSREPL_TFM"] = tfm
        env["CSREPL"] = os.path.join(
            repo, "out", "csharp", "glp_repl", "bin", "Debug", tfm, "glp_repl.exe")
    return env


def _csproj_tfm(path: str) -> str | None:
    """Read the target framework moniker out of a csproj, so the binary path is never guessed."""
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            text = fh.read()
    except OSError:
        return None
    m = re.search(r"<TargetFramework>([^<]+)</TargetFramework>", text)
    return m.group(1).strip() if m else None


def resolve(text: str, env: dict[str, str]) -> str:
    """Substitute `${SYMBOL}`. An unknown symbol raises -- it is never left literal, because a
    literal `${CSREPL}` on a command line becomes a confusing exec failure two layers down."""
    def sub(m: re.Match[str]) -> str:
        key = m.group(1)
        if key not in env:
            raise KeyError(key)
        return env[key]
    return _PLACEHOLDER.sub(sub, text)


# ---------------------------------------------------------------------------
# Normalisation rules
#
# Each rule is a declared, named transformation with a rationale and its own negative control.
# The set is closed on purpose: an `eval`-shaped rule kind would let a normalisation do anything,
# and "anything" cannot be argued about in review.
# ---------------------------------------------------------------------------
def apply_rule(rule: dict, text: str) -> str:
    kind = rule["kind"]
    if kind == "strip_line_prefix":
        pre = rule["prefix"]
        return "\n".join(ln[len(pre):] if ln.startswith(pre) else ln
                         for ln in text.split("\n"))
    if kind == "keep_lines_matching":
        pat = re.compile(rule["pattern"])
        return "\n".join(ln for ln in text.split("\n") if pat.search(ln))
    if kind == "drop_lines_matching":
        pat = re.compile(rule["pattern"])
        return "\n".join(ln for ln in text.split("\n") if not pat.search(ln))
    if kind == "regex_sub":
        return re.sub(rule["pattern"], rule["replacement"], text, flags=re.MULTILINE)
    if kind == "strip_trailing_whitespace":
        return "\n".join(ln.rstrip() for ln in text.split("\n"))
    raise DeclarationError(f"unknown normalisation kind: {kind!r}")


def apply_rules(rules: list[dict], text: str) -> str:
    for rule in rules:
        text = apply_rule(rule, text)
    return text


def check_normalisation_controls(rules: list[dict]) -> list[dict]:
    """EXECUTE every normalisation's negative control (FR-006).

    A control is a pair of strings that differ in a way that MATTERS. If the rule makes them
    equal, the rule can erase a real divergence, and every AGREE it contributed to is suspect.
    Returns one result record per rule; the caller treats any failure as NOT-MEASURED."""
    results = []
    for rule in rules:
        control = rule["negative_control"]
        a = apply_rule(rule, control["a"])
        b = apply_rule(rule, control["b"])
        ok = a != b
        results.append({
            "normalisation": rule["id"],
            "executed": True,
            "passed": ok,
            "detail": ("the rule preserves a real difference"
                       if ok else
                       "THE RULE ERASED ITS OWN NEGATIVE CONTROL: two inputs that differ in a "
                       f"way that matters became identical ({a!r})"),
        })
    return results


# ---------------------------------------------------------------------------
# Declaration loading -- every refusal below happens BEFORE anything is started
# ---------------------------------------------------------------------------
_REQUIRED_CRITERION = ("id", "title", "requirement", "participants", "script",
                       "normalisations", "negative_control")
_RULE_KINDS = {"strip_line_prefix", "keep_lines_matching", "drop_lines_matching",
               "regex_sub", "strip_trailing_whitespace"}


def load(path: str) -> list[dict]:
    try:
        with open(path, "r", encoding="utf-8") as fh:
            doc = json.load(fh)
    except FileNotFoundError:
        raise DeclarationError(f"no criteria declaration at {path}")
    except json.JSONDecodeError as exc:
        raise DeclarationError(f"{path}: not valid JSON: {exc}")

    if doc.get("schema") != "glpnet/differential-criteria/1":
        raise DeclarationError(
            f"{path}: unknown schema {doc.get('schema')!r}; expected "
            "'glpnet/differential-criteria/1'")

    criteria = doc.get("criteria")
    if not isinstance(criteria, list) or not criteria:
        raise DeclarationError(f"{path}: 'criteria' must be a non-empty list")

    seen: set[str] = set()
    for crit in criteria:
        missing = [k for k in _REQUIRED_CRITERION if k not in crit]
        if missing:
            raise DeclarationError(
                f"{path}: criterion {crit.get('id', '<unnamed>')!r} is missing "
                f"required key(s): {', '.join(missing)}")
        cid = crit["id"]
        if cid in seen:
            raise DeclarationError(f"{path}: duplicate criterion id {cid!r}")
        seen.add(cid)

        parts = crit["participants"]
        # FR-005. A single-participant declaration is not a weak differential; it is not a
        # differential. Refusing at load is what stops it being run and reported as one.
        if not isinstance(parts, list) or len(parts) < 2:
            raise DeclarationError(
                f"{path}: criterion {cid!r} declares "
                f"{len(parts) if isinstance(parts, list) else 0} participant(s). A differential "
                "criterion requires at least 2 -- a one-participant 'differential' is a category "
                "error, not a degenerate case (FR-005).")
        pnames: set[str] = set()
        for p in parts:
            for key in ("name", "command", "cwd", "why"):
                if key not in p:
                    raise DeclarationError(
                        f"{path}: criterion {cid!r} participant "
                        f"{p.get('name', '<unnamed>')!r} is missing {key!r}")
            if p["name"] in pnames:
                raise DeclarationError(
                    f"{path}: criterion {cid!r} has two participants named {p['name']!r}")
            pnames.add(p["name"])

        for rule in crit["normalisations"]:
            for key in ("id", "kind", "rationale", "negative_control"):
                if key not in rule:
                    raise DeclarationError(
                        f"{path}: criterion {cid!r} normalisation "
                        f"{rule.get('id', '<unnamed>')!r} is missing {key!r} -- FR-006 requires "
                        "every normalisation to be declared WITH a negative control")
            if rule["kind"] not in _RULE_KINDS:
                raise DeclarationError(
                    f"{path}: criterion {cid!r} normalisation {rule['id']!r} has unknown kind "
                    f"{rule['kind']!r}; known kinds: {', '.join(sorted(_RULE_KINDS))}")
            nc = rule["negative_control"]
            for key in ("a", "b", "why"):
                if key not in nc:
                    raise DeclarationError(
                        f"{path}: criterion {cid!r} normalisation {rule['id']!r} negative "
                        f"control is missing {key!r}")
            if nc["a"] == nc["b"]:
                raise DeclarationError(
                    f"{path}: criterion {cid!r} normalisation {rule['id']!r} negative control "
                    "has identical 'a' and 'b' -- a control whose inputs already agree cannot "
                    "demonstrate that the rule preserves a difference")

        neg = crit["negative_control"]
        for key in ("participant", "rule", "why"):
            if key not in neg:
                raise DeclarationError(
                    f"{path}: criterion {cid!r} negative_control is missing {key!r} -- FR-007 "
                    "requires every criterion to carry a perturbation that MUST make it diverge")
        if neg["participant"] not in pnames:
            raise DeclarationError(
                f"{path}: criterion {cid!r} negative_control perturbs participant "
                f"{neg['participant']!r}, which is not one of {sorted(pnames)}")
        if neg["rule"].get("kind") not in _RULE_KINDS:
            raise DeclarationError(
                f"{path}: criterion {cid!r} negative_control rule has unknown kind "
                f"{neg['rule'].get('kind')!r}")

    return criteria


# ---------------------------------------------------------------------------
# Running one participant
# ---------------------------------------------------------------------------
def run_participant(part: dict, script: str, env: dict[str, str],
                    timeout: int = DEFAULT_TIMEOUT) -> dict:
    """Start one participant and capture its RAW transcript and exit status.

    Every failure path returns a record carrying `started=False` and a human `reason`. FR-003
    requires the reason and the participant to reach the report; returning None here, or raising,
    is how a participant quietly becomes a skip."""
    name = part["name"]
    try:
        command = [resolve(tok, env) for tok in part["command"]]
        cwd = resolve(part["cwd"], env)
    except KeyError as exc:
        return {"name": name, "started": False,
                "reason": f"unresolved symbol ${{{exc.args[0]}}} -- the tool it names was not "
                          "found on this host"}

    if not os.path.isdir(cwd):
        return {"name": name, "started": False,
                "reason": f"working directory does not exist: {cwd}"}

    exe = command[0]
    if os.path.sep in exe or (os.path.altsep and os.path.altsep in exe):
        if not os.path.exists(exe):
            return {"name": name, "started": False,
                    "reason": f"executable not found: {exe}"}
    elif shutil.which(exe) is None:
        return {"name": name, "started": False,
                "reason": f"executable not on PATH: {exe}"}

    # A stale binary that runs happily is worse than a missing one: it answers, and the answer
    # is about code that is no longer the code. Same gate the suite applies before Sections
    # I/T/U/V-18..23, in the harness's own vocabulary.
    stale = _staleness(part, env, exe)
    if stale:
        return {"name": name, "started": False, "reason": stale}

    try:
        proc = subprocess.run(command, cwd=cwd, input=script, capture_output=True,
                              text=True, timeout=timeout, encoding="utf-8", errors="replace")
    except subprocess.TimeoutExpired:
        return {"name": name, "started": False,
                "reason": f"timed out after {timeout}s"}
    except OSError as exc:
        return {"name": name, "started": False, "reason": f"could not start: {exc}"}

    return {
        "name": name,
        "started": True,
        "exit_status": proc.returncode,
        "raw": (proc.stdout or "") + (proc.stderr or ""),
        "command": command,
    }


def _staleness(part: dict, env: dict[str, str], exe: str) -> str | None:
    """Return a reason string if the participant's binary is not newer than its declared sources."""
    fresh = part.get("freshness")
    if not fresh:
        return None

    # WHICH artefact's age counts. Measured 2026-09-06: `glp_repl.exe` is the .NET APPHOST STUB,
    # and an incremental build does NOT rewrite it when only a referenced library's method bodies
    # change -- after editing `out/csharp/lib/engine/glp_engine.cs` and rebuilding successfully,
    # the exe was 15 minutes older than the assembly that actually carried the change. Statting
    # the stub therefore measures the age of a launcher, not of the code. `artefacts` names the
    # output location whose newest file genuinely dates the build; `exe` remains the default only
    # for participants that really are a single self-contained binary.
    targets = [resolve(a, env) for a in fresh.get("artefacts", [])] or [exe]
    bin_mtime = 0.0
    bin_path = None
    for t in targets:
        if os.path.isdir(t):
            for dirpath, _dirs, files in os.walk(t):
                for fn in files:
                    try:
                        m = os.path.getmtime(os.path.join(dirpath, fn))
                    except OSError:
                        continue
                    if m > bin_mtime:
                        bin_mtime, bin_path = m, os.path.join(dirpath, fn)
        else:
            try:
                m = os.path.getmtime(t)
            except OSError:
                continue
            if m > bin_mtime:
                bin_mtime, bin_path = m, t
    if bin_path is None:
        return f"could not establish build freshness: no build artefact found at {targets}"
    newest = 0.0
    newest_path = None
    # `bin` and `obj` hold BUILD OUTPUT, including generated `.cs`. Walking them would compare
    # the binary against artefacts the build itself just wrote, so every fresh build would read
    # as stale. The suite prunes the same two directories for the same reason.
    pruned = set(fresh.get("exclude_dirs", ["bin", "obj"]))
    for rel in fresh.get("newer_than", []):
        root = resolve(rel, env)
        for dirpath, dirs, files in os.walk(root):
            dirs[:] = [d for d in dirs if d not in pruned]
            for fn in files:
                if not fn.endswith(tuple(fresh.get("suffixes", [".cs"]))):
                    continue
                p = os.path.join(dirpath, fn)
                try:
                    m = os.path.getmtime(p)
                except OSError:
                    continue
                if m > newest:
                    newest, newest_path = m, p
    if newest_path is None:
        return f"could not establish build freshness: no source files found under {fresh.get('newer_than')}"
    if newest >= bin_mtime:
        return (f"build is NOT NEWER than its source (newest artefact "
                f"{os.path.basename(bin_path)} vs source "
                f"{_relpath_or_abs(newest_path, env['REPO'])}) -- rebuild before trusting it")
    return None


# ---------------------------------------------------------------------------
# Evaluating one criterion
# ---------------------------------------------------------------------------
def _diff(a_name: str, a: str, b_name: str, b: str) -> str:
    return "\n".join(difflib.unified_diff(
        a.split("\n"), b.split("\n"), fromfile=a_name, tofile=b_name, lineterm=""))


def _compare(runs: list[dict], rules: list[dict]) -> tuple[str, str]:
    """Compare normalised transcripts pairwise against the first participant.

    Returns (outcome, detail). Assumes every run started and is non-empty -- the caller
    establishes both BEFORE calling, because doing it after is how two empty transcripts become
    an agreement."""
    base = runs[0]
    base_norm = apply_rules(rules, base["raw"])
    for other in runs[1:]:
        other_norm = apply_rules(rules, other["raw"])
        if base_norm != other_norm:
            return DIVERGE, _diff(base["name"], base_norm, other["name"], other_norm)
    return AGREE, ""


def evaluate(crit: dict, env: dict[str, str], timeout: int = DEFAULT_TIMEOUT) -> dict:
    """Run one criterion end to end and return its record.

    The order of the steps below is load-bearing and is the specification, not an optimisation:
    normalisation controls, then start, then non-emptiness, then compare, then the criterion's own
    negative control. Each step can only be trusted if the ones before it held."""
    cid = crit["id"]
    result: dict = {
        "id": cid,
        "title": crit["title"],
        "requirement": crit["requirement"],
        "participants": [p["name"] for p in crit["participants"]],
        "outcome": NOT_MEASURED,
        "reason": None,
        "not_measured_participant": None,
        "normalisation_controls": [],
        "negative_control": {"executed": False, "passed": False, "detail": None},
        "runs": [],
    }

    # (1) FR-006 -- a normaliser that erases its own control cannot be trusted with the
    # comparison, so this runs before anything is started.
    try:
        controls = check_normalisation_controls(crit["normalisations"])
    except DeclarationError as exc:
        result["reason"] = f"normalisation is not executable: {exc}"
        return result
    result["normalisation_controls"] = controls
    failed = [c for c in controls if not c["passed"]]
    if failed:
        result["reason"] = ("a declared normalisation erased its own negative control: "
                            + "; ".join(f"{c['normalisation']}: {c['detail']}" for c in failed))
        return result

    # (2) Start every declared participant.
    try:
        script = resolve(crit["script"], env)
    except KeyError as exc:
        result["reason"] = (f"the script references ${{{exc.args[0]}}}, which does not resolve "
                            "on this host")
        return result

    runs = [run_participant(p, script, env, timeout) for p in crit["participants"]]
    result["runs"] = [{k: v for k, v in r.items() if k != "raw"} for r in runs]

    # FR-003 -- name the participant and the reason. Never green, never silently skipped.
    for r in runs:
        if not r["started"]:
            result["reason"] = f"participant {r['name']!r} was not started: {r['reason']}"
            result["not_measured_participant"] = r["name"]
            return result

    # (3) FR-004 -- the non-emptiness guard, BEFORE the comparison. Two empty transcripts also
    # compare equal, and that equality is the vacuous pass this whole harness exists to prevent.
    empty = [r["name"] for r in runs if not r["raw"].strip()]
    if empty:
        result["reason"] = (
            f"empty transcript from {', '.join(repr(n) for n in empty)} -- an empty transcript "
            "compares EQUAL to another empty transcript, so agreement here would be vacuous")
        result["not_measured_participant"] = empty[0]
        return result

    # (4) Compare.
    outcome, detail = _compare(runs, crit["normalisations"])

    # (5) FR-007 / SC-002 -- the criterion's own negative control, EXECUTED, against the
    # transcripts captured on THIS run rather than against a fixture. Perturb one participant's
    # raw transcript in a declared way and require the comparison to diverge. If it does not, the
    # comparator did not discriminate on this run, and this run measured nothing -- whatever the
    # comparison above happened to say.
    neg = crit["negative_control"]
    perturbed = []
    for r in runs:
        if r["name"] == neg["participant"]:
            perturbed.append({**r, "raw": apply_rule(neg["rule"], r["raw"])})
        else:
            perturbed.append(r)
    if any(not p["raw"].strip() for p in perturbed):
        result["negative_control"] = {
            "executed": True, "passed": False,
            "detail": "the perturbation emptied a transcript, so the divergence it produced "
                      "would be the empty-transcript case, not a real one"}
    else:
        neg_outcome, _ = _compare(perturbed, crit["normalisations"])
        result["negative_control"] = {
            "executed": True,
            "passed": neg_outcome == DIVERGE,
            "detail": (f"perturbing {neg['participant']!r} produced {neg_outcome} as required"
                       if neg_outcome == DIVERGE else
                       f"perturbing {neg['participant']!r} produced {neg_outcome}: the "
                       "comparator did NOT discriminate, so this run's comparison proves "
                       "nothing"),
            "why": neg["why"],
        }
    if not result["negative_control"]["passed"]:
        result["reason"] = ("the criterion's negative control did not diverge: "
                            + str(result["negative_control"]["detail"]))
        return result

    result["outcome"] = outcome
    if outcome == DIVERGE:
        result["reason"] = "normalised transcripts differ"
        result["divergence"] = detail
    return result


# ---------------------------------------------------------------------------
# Driving the whole declaration
# ---------------------------------------------------------------------------
def _relpath_or_abs(path: str, start: str) -> str:
    try:
        return os.path.relpath(path, start)
    except ValueError:
        return path


def run(repo: str, criteria_path: str, only: str | None = None,
        timeout: int = DEFAULT_TIMEOUT) -> tuple[dict, int]:
    criteria = load(criteria_path)
    if only:
        criteria = [c for c in criteria if c["id"] == only]
        if not criteria:
            raise DeclarationError(f"no criterion with id {only!r} in {criteria_path}")

    env = build_environment(repo)
    results = [evaluate(c, env, timeout) for c in criteria]

    totals = {
        "declared": len(results),
        "measured_agree": sum(1 for r in results if r["outcome"] == AGREE),
        "measured_diverge": sum(1 for r in results if r["outcome"] == DIVERGE),
        "not_measured": sum(1 for r in results if r["outcome"] == NOT_MEASURED),
    }
    report = {
        "schema": "glpnet/differential-report/1",
        # A declaration may legitimately live off the repo's drive (a temp dir on C: while the
        # repo is on D:). `relpath` RAISES across Windows mounts, and a reporting convenience
        # must never be the thing that takes the gate down.
        "declaration": _relpath_or_abs(criteria_path, repo),
        "totals": totals,
        # FR-008, stated in the artefact rather than left to the reader.
        "agreement_is_not_correctness": (
            "MEASURED-AGREE states that the participants AGREED. Participants that are broken "
            "identically also agree. This harness cannot detect that and does not claim to."),
        "criteria": results,
    }

    if totals["measured_diverge"]:
        code = EXIT_DIVERGE
    elif totals["not_measured"]:
        code = EXIT_NOT_MEASURED
    else:
        code = EXIT_AGREE
    return report, code


def render(report: dict) -> str:
    out: list[str] = []
    t = report["totals"]
    out.append("=" * 72)
    out.append("DIFFERENTIAL ACCEPTANCE GATE (feature 109 US1)")
    out.append("=" * 72)
    out.append(f"declaration: {report['declaration']}")
    out.append(f"declared {t['declared']} | AGREE {t['measured_agree']} | "
               f"DIVERGE {t['measured_diverge']} | NOT-MEASURED {t['not_measured']}")
    out.append("")
    for r in report["criteria"]:
        out.append(f"[{r['outcome']}] {r['id']}")
        out.append(f"    {r['title']}")
        out.append(f"    discharges: {r['requirement']}")
        out.append(f"    participants: {', '.join(r['participants'])}")
        for c in r["normalisation_controls"]:
            flag = "ok" if c["passed"] else "FAILED"
            out.append(f"    normalisation control {c['normalisation']}: {flag}")
        nc = r["negative_control"]
        if nc["executed"]:
            out.append(f"    negative control: {'ok' if nc['passed'] else 'FAILED'} -- "
                       f"{nc['detail']}")
        else:
            out.append("    negative control: NOT EXECUTED")
        if r["reason"]:
            out.append(f"    reason: {r['reason']}")
        if r.get("not_measured_participant"):
            out.append(f"    participant not measured: {r['not_measured_participant']}")
        if r.get("divergence"):
            out.append("    divergence:")
            for line in r["divergence"].split("\n"):
                out.append(f"      {line}")
        out.append("")
    out.append(report["agreement_is_not_correctness"])
    if t["not_measured"]:
        out.append("")
        out.append("NOT-MEASURED is not a skip and is not a pass. Each one above names the "
                   "participant and the reason.")
    return "\n".join(out)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(
        description="Differential acceptance gate (feature 109 US1)")
    default_repo = os.path.dirname(os.path.dirname(os.path.realpath(__file__)))
    ap.add_argument("--repo", default=default_repo,
                    help="repo root (defaults to this script's own repo, never the CWD)")
    ap.add_argument("--criteria", default=None,
                    help="declaration path (default .specify/differential/criteria.json)")
    ap.add_argument("--report", default=None,
                    help="where to write the JSON report "
                         "(default .specify/differential/report.json)")
    ap.add_argument("--only", default=None, help="run a single criterion by id")
    ap.add_argument("--timeout", type=int, default=DEFAULT_TIMEOUT)
    ap.add_argument("--json", action="store_true", help="print the report as JSON")
    args = ap.parse_args(argv)

    repo = os.path.realpath(args.repo)
    criteria_path = args.criteria or os.path.join(
        repo, ".specify", "differential", "criteria.json")
    report_path = args.report or os.path.join(
        repo, ".specify", "differential", "report.json")

    if not sys.stdout.isatty():
        # Same warning, same measured reason, as the evidence-signal audit: piping replaces $?
        # with the pipe's status, and that is how a caller loses an exit code.
        print("differential-gate: not a terminal -- if you piped this, $? is the PIPE's status, "
              "not mine. Run bare and check $? separately.", file=sys.stderr)

    try:
        report, code = run(repo, criteria_path, args.only, args.timeout)
    except DeclarationError as exc:
        print(f"differential-gate: REFUSED: {exc}", file=sys.stderr)
        return EXIT_USAGE

    os.makedirs(os.path.dirname(report_path), exist_ok=True)
    with open(report_path, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2)
        fh.write("\n")

    print(json.dumps(report, indent=2) if args.json else render(report))
    return code


if __name__ == "__main__":
    sys.exit(main())
