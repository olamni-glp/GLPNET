# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

"""BK-REPORT v1 — the fleet-standard report. ONE generator, every host, every repo.

**THE STANDARDISED ORDER IS FIXED AND MANDATORY** (engineer directive):

    1. ROADMAP          — all epics and features NOT closed, TABULAR
    2. PROGRESS REVIEW  — what moved
    3. STATUS UPDATE    — where the run stands now
    4. SITREP           — the backlog breakdown and what is in flight
    5. TAKT             — era = one feature, measured against the normative bands
    6. WHAT'S NEXT      — the ordered next actions, derived not typed

A "standard" each host re-implements is four dialects that agree on a header row, so this
file is the ONLY implementation and the order above is not configurable.

Design rules, each answering a defect this fleet has measured:

* **Every number is read from a live CLI, never typed.** A hardcoded value reads as evidence.
* **Absent is not zero.** An unreadable source prints ``UNAVAILABLE`` with its reason, never
  an empty table — an empty table and a broken reader look identical to a reader.
* **Identity is stamped.** host / repo / project_id / branch / commit / ahead / behind / UTC.
* **Counts reconcile in-band**, so arithmetic is checked rather than trusted.
* **An era is a FEATURE** (specify -> close, nine stages). The nine are stages WITHIN one
  era, never eras of their own, and nothing here may summarise or fragment a feature.
* **Takt reads the TAKT DUCKLAKE.** Per-phase token use is recorded in the lake and
  RETRIEVED from it (engineer standing order, 2026-08-24), never recomputed in-process
  — two hosts recomputing disagree with no way to see why. Coverage travels with every
  figure: a row whose tokens were never supplied is NOT MEASURED, never zero-cost.

Usage::

    python .specify/standards/bk_report_v1.py all --feature <id>
    python .specify/standards/bk_report_v1.py roadmap|progress|status|sitrep|takt|next
"""

# ---------------------------------------------------------------------------
# PROVENANCE — added 2026-08-24T20:55Z by gavriella@GAVRIELLA (olamnit-assistant lane).
#
# The bytes below this header were propagated to D:/coop, D:/coop/_takt-lake, G:/coop and
# H:/coop within 29 seconds at 2026-08-24T20:42Z by an agent NEITHER the buildkit lane nor
# this lane can identify. Both of us checked: buildkit-69 performed read-only ops only, and
# this lane's converge script REFUSED to write because its precondition (the unpatched
# line 67) was already gone. 13 lanes are instructed to execute this file, so it is stamped
# rather than left anonymous. If you wrote the 20:42Z propagation, claim it on the channel.
#
# ENGINE OVERRIDE — the removal of `env.setdefault("BUILDKIT_ENGINE_OVERRIDE","ambient")`
# is KEPT, by engineer ruling 2026-08-24, but understand what it costs:
#   * the pinned bundle 2026.08.23.7 ships marathon/takt.py + takt_stage.py and NO takt_lake.py
#   * so on a pinned-clean lane, lake-sourced takt is UNAVAILABLE, and must print `unmeasured`
#   * a lane that still sees takt numbers is reading a SHADOWING install on sys.path, not the
#     pin — verify with `python -c "import buildkit_cli.marathon.takt_lake as m; print(m.__file__)"`
#     (measured on GAVRIELLA: it resolved under site-packages, NOT under deploy-home/versions)
# The ROOT-CAUSE fix is PACKAGING — ship takt_lake.py + duckdb in the released bundle — and it
# is owned by the buildkit lane. Re-adding the ambient override here is NOT the fix and was
# ruled against; it merely hides which tree the numbers came from.
# ---------------------------------------------------------------------------
from __future__ import annotations

import argparse
import json
import os
import platform
import subprocess
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

FORMAT_ID = "BK-REPORT-v1"
UNAVAILABLE = "UNAVAILABLE"

# NORMATIVE takt bands (engineer ruling: an era IS a feature).
PHASE_BAND = "30m - 3h"
ERA_BAND = "1.5h - 6h"
ERA_STAGES = ("specify", "clarify", "plan", "tasks", "analyze",
              "implement", "codexreview", "ship", "close")

# The mandatory section order. Index = position in every BK-REPORT, on every host.
SECTION_ORDER = ("roadmap", "progress", "status", "sitrep", "takt", "next")


def _run(args, timeout: float = 300.0):
    """Run a buildkit CLI; return (ok, stdout, detail). Never raises."""
    env = dict(os.environ)
    # DELIBERATELY NOT setting BUILDKIT_ENGINE_OVERRIDE. This used to
    # `setdefault(..., "ambient")`, which FORCED the override on every child even when
    # the caller had a converged pin — and the override's own "engine pin DISPLACED"
    # banner then became the last stderr line, which this function reported as the
    # cause. The generator was manufacturing the very symptom it blamed. Inherit the
    # caller's environment instead: if the pin is converged there is no banner, and if
    # it is genuinely displaced that is a real finding the operator should see.
    env["PYTHONUTF8"] = "1"
    try:
        p = subprocess.run([sys.executable, "-m", *args], capture_output=True, text=True,
                           encoding="utf-8", errors="replace", timeout=timeout, env=env)
    except BaseException as exc:                              # noqa: BLE001
        return False, "", f"{type(exc).__name__}: {exc}"
    if p.returncode != 0:
        return False, p.stdout or "", _failure_cause(p.stdout, p.stderr, p.returncode)
    return True, p.stdout, ""


def _failure_cause(stdout: str, stderr: str, returncode: int) -> str:
    """Prefer the CLI's OWN structured error over anything on stderr.

    MEASURED DEFECT (shiras/glpnet, 2026-09-02). Several buildkit CLIs write their real
    error to STDOUT as JSON (``{"error": "..."}``) and put only advisory chatter on
    stderr. `_diagnostic_line` then reported the advisory line as the cause, so a
    registry-contention failure was rendered as::

        2. PROGRESS REVIEW: UNAVAILABLE — engine resolution degraded: pin mirror absent...

    when the actual stdout said ``the machine registry ... is busy ... pgdb/.lock held by
    PID 291...``. That is the same class of defect this file's header already records for
    the engine-override banner — the generator naming a symptom it happened to see rather
    than the cause the child reported. Structured error first, stderr second.
    """
    try:
        obj = json.loads((stdout or "").strip() or "null")
        if isinstance(obj, dict) and obj.get("error"):
            return str(obj["error"])
    except BaseException:                                     # noqa: BLE001
        pass
    return _diagnostic_line(stderr, returncode)


# Advisory chatter this toolchain writes to stderr on almost every invocation. None of
# it is ever the CAUSE of a non-zero exit, but all of it can be the LAST line — which is
# why "the last stderr line" was a coin flip dressed up as a diagnosis.
_STDERR_NOISE = (
    "co: capture spilled",
    "co: capture observed",
    "buildkit: reaped orphaned",
    "engine pin DISPLACED",
    "engine identity:",
    "docs in sync:",
    "RuntimeWarning",
    "found in sys.modules",
)


def _diagnostic_line(stderr: str, returncode: int) -> str:
    """Pick the stderr line that actually explains a failure, not merely the last one.

    Prefers a line that names a real failure mode; falls back to the last non-noise
    line; and only then to the bare exit code. Says so explicitly when every line was
    advisory, because "exit N and nothing but chatter" is itself the finding — it means
    the child failed without explaining itself.
    """
    lines = [ln.strip() for ln in (stderr or "").splitlines() if ln.strip()]
    signal = [ln for ln in lines if not any(n in ln for n in _STDERR_NOISE)]
    for pat in ("unavailable", "refus", "error", "Error", "held by", "is busy",
                "not found", "Traceback", "denied", "failed"):
        for ln in reversed(signal):
            if pat in ln:
                return ln
    if signal:
        return signal[-1]
    return (f"exit {returncode} with no diagnostic on stderr "
            f"({len(lines)} advisory line(s) suppressed)")


def _json_from(stdout: str):
    for i, ch in enumerate(stdout):
        if ch in "{[":
            try:
                return json.loads(stdout[i:])
            except BaseException:                             # noqa: BLE001
                continue
    return None


def _git(*args: str) -> str:
    try:
        p = subprocess.run(["git", *args], capture_output=True, text=True,
                           encoding="utf-8", errors="replace", timeout=60)
        return p.stdout.strip() if p.returncode == 0 else "?"
    except BaseException:                                     # noqa: BLE001
        return "?"


def _clean(out: str):
    return [ln for ln in out.splitlines()
            if ln.strip() and not ln.startswith(("co:", "engine identity", "buildkit:"))]


def header(kind: str):
    root = Path(_git("rev-parse", "--show-toplevel") or ".")
    pid = "?"
    f = root / ".specify" / "roadmap-sync" / "project-id"
    if f.is_file():
        try:
            pid = f.read_text(encoding="utf-8").strip()
        except BaseException:                                 # noqa: BLE001
            pass
    return [f"{FORMAT_ID} :: {kind}",
            f"host={platform.node()}  repo={root.name}  project_id={pid}",
            f"branch={_git('rev-parse','--abbrev-ref','HEAD')}  commit={_git('rev-parse','--short','HEAD')}"
            f"  ahead={_git('rev-list','--count','github/develop..HEAD')}"
            f" behind={_git('rev-list','--count','HEAD..github/develop')}",
            f"generated_utc={datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')}",
            "section_order=" + " -> ".join(s.upper() for s in SECTION_ORDER),
            ""]


def table(headers, rows):
    if not rows:
        return ["(no rows)"]
    cells = [[("" if c is None else str(c)) for c in r] for r in rows]
    w = [max(len(str(headers[i])), max(len(r[i]) for r in cells)) for i in range(len(headers))]
    out = ["| " + " | ".join(str(headers[i]).ljust(w[i]) for i in range(len(headers))) + " |",
           "|" + "|".join("-" * (x + 2) for x in w) + "|"]
    out += ["| " + " | ".join(c[i].ljust(w[i]) for i in range(len(headers))) + " |" for c in cells]
    return out


# Pipeline order, not alphabetical: reading top-down walks the pipeline.
STATE_ORDER = {"released": 0, "shipped": 1, "implemented": 2, "analyzed": 3, "specified": 4,
               "promoted": 5, "refined": 6, "captured": 7, "agreed": 8}


# ---------------------------------------------------------------- 1. ROADMAP
def sec_roadmap(_feature=None):
    ok, out, detail = _run(["buildkit_cli.roadmap", "--json", "status"])
    d = _json_from(out) if ok else None
    if d is None:
        return ["1. ROADMAP: " + UNAVAILABLE + " — " + str(detail),
                "   (read failure, NOT an empty roadmap — never read this as zero)"]
    epic_of = {}
    for e in d.get("epics", []):
        for f in e.get("features", []):
            epic_of[f.get("feature_id") if isinstance(f, dict) else f] = e.get("name", "")
    feats = d.get("features", [])
    open_f = [f for f in feats if f.get("state") != "closed"]
    open_f.sort(key=lambda f: (STATE_ORDER.get(f.get("state"), 99),
                               -(f.get("wsjf") or 0), f.get("feature_id") or ""))

    def n(x):
        return "" if x in (None, "") else (f"{x:.2f}" if isinstance(x, float) else str(x))

    rows = [(f.get("state"), n(f.get("priority_rank")), (f.get("feature_id") or "")[:48],
             n(f.get("wsjf")), n(f.get("rice")), "Y" if f.get("spec_path") else "-",
             "Y" if f.get("parallel_safe") else "-", "Y" if f.get("blocked_by") else "-",
             (epic_of.get(f.get("feature_id")) or "(standalone)")[:30]) for f in open_f]
    by = Counter(f.get("state") for f in feats)
    return (["1. ROADMAP — ALL EPICS AND FEATURES NOT CLOSED", ""]
            + table(("STATE", "RANK", "FEATURE", "WSJF", "RICE", "SPEC", "PAR", "BLKD", "EPIC"), rows)
            + ["",
               f"TOTALS: epics={len(d.get('epics', []))}  features={len(feats)}  "
               f"open={len(open_f)}  closed={len(feats)-len(open_f)}",
               "BY STATE: " + "  ".join(f"{k}={v}" for k, v in sorted(by.items())),
               f"RECONCILES: open + closed = {len(open_f)} + {len(feats)-len(open_f)} = {len(feats)}"])


def _marathon(sub, feature, extra=()):
    args = ["buildkit_cli.marathon", sub, *extra]
    if feature:
        args += ["--feature", feature]
    return _run(args)


def _backlog(feature):
    ok, out, detail = _marathon("backlog", feature, ("--json",))
    return (_json_from(out) if ok else None), detail


# ------------------------------------------------------- 2. PROGRESS REVIEW
def sec_progress(feature):
    d, detail = _backlog(feature)
    if d is None:
        return ["2. PROGRESS REVIEW: " + UNAVAILABLE + " — " + str(detail),
                "   (read failure, NOT an absence of progress)"]
    items = d.get("items", [])
    by = Counter(i.get("state") for i in items)
    total = len(items)
    done = by.get("done", 0)
    terminal = done + by.get("deferred", 0)
    ok, out, _ = _marathon("status", feature)
    steps = "?"
    if ok:
        for ln in _clean(out):
            if ln.strip().startswith("steps:"):
                steps = ln.strip().replace("steps: ", "")
                break
    rows = [("backlog resolved (done)", f"{done} / {total}",
             f"{100.0*done/total:.1f}%" if total else "-"),
            ("terminal (done + deferred)", f"{terminal} / {total}",
             f"{100.0*terminal/total:.1f}%" if total else "-"),
            ("still open (parked + in_progress)", f"{total-terminal} / {total}", "-"),
            ("run steps", steps, "-")]
    return (["2. PROGRESS REVIEW — WHAT MOVED", ""]
            + table(("MEASURE", "VALUE", "PCT"), rows)
            + ["", "NOTE: percentages are of RECORDED items only. Work never captured is not",
               "      counted as complete — absent is not zero."])


# ---------------------------------------------------------- 3. STATUS UPDATE
def sec_status(feature):
    ok, out, detail = _marathon("status", feature)
    if not ok:
        return ["3. STATUS UPDATE: " + UNAVAILABLE + " — " + str(detail),
                "   (read failure, NOT an idle run)"]
    return ["3. STATUS UPDATE — WHERE THE RUN STANDS", ""] + ["  " + ln for ln in _clean(out)]


# ----------------------------------------------------------------- 4. SITREP
def sec_sitrep(feature):
    d, detail = _backlog(feature)
    if d is None:
        return ["4. SITREP: " + UNAVAILABLE + " — " + str(detail)]
    items = d.get("items", [])
    by_state = Counter(i.get("state") for i in items)
    by_kind = Counter(i.get("kind") for i in items)
    active = [i for i in items if i.get("state") == "in_progress"]
    lines = ["4. SITREP — BACKLOG AND WHAT IS IN FLIGHT", "",
             "BY STATE: " + "  ".join(f"{k}={v}" for k, v in sorted(by_state.items())),
             "BY KIND:  " + "  ".join(f"{k}={v}" for k, v in sorted(by_kind.items())),
             f"RECONCILES: sum(states) = {sum(by_state.values())} = items {len(items)}", ""]
    if active:
        lines += ["IN PROGRESS:"] + table(
            ("ITEM", "KIND", "TITLE"),
            [(i["item_id"][:22], i.get("kind", ""), (i.get("title") or "")[:66]) for i in active])
    else:
        lines += ["IN PROGRESS: none"]
    return lines


# ------------------------------------------------------------------- 5. TAKT
def sec_takt(feature):
    lines = ["5. TAKT — AN ERA IS ONE FEATURE (specify -> close, nine stages)", "",
             f"  NORMATIVE BANDS   phase: {PHASE_BAND}    era (whole feature): {ERA_BAND}",
             "  DURATION RULE     the generic band, or an experience-based estimate computed",
             "                    from ACTUAL recorded measurements. LLM ESTIMATES ARE NEVER",
             "                    PERMITTED. An unmeasured era reports 'unmeasured', never zero.",
             ""]
    ok, out, detail = _marathon("takt", feature)
    if not ok:
        lines += ["  TAKT: " + UNAVAILABLE + " — " + str(detail),
                  "  (read failure, NOT a zero-duration era)"]
    else:
        body = _clean(out)
        lines += ["  " + ln for ln in body] if body else ["  (takt returned no rows)"]
    lines += [""] + _takt_lake_tokens()
    lines += ["", "  ERA STAGE LADDER — all nine are stages WITHIN one era, never eras of their own:",
              "  " + " -> ".join(ERA_STAGES),
              "  A feature is NEVER split, summarised or compressed to fit a takt band."]
    return lines


def _takt_lake_tokens():
    """PER-PHASE TOKEN USE, read back FROM THE TAKT DUCKLAKE.

    Engineer standing order: all standardised takt data AND per-phase token use are
    RECORDED IN the takt ducklake and RETRIEVED FROM IT for any and all takt
    reporting. Duration alone answers "how long"; it never answers "what did this
    phase COST", and cost is the other half of a schedule.

    READ, never recomputed. A report that recalculates in-process is reporting its
    own arithmetic, and two hosts doing that disagree with no way to see why. The
    lake is the shared surface, so the lake is what this reads.

    COVERAGE TRAVELS WITH THE FIGURE. A row whose tokens were never supplied reads
    as NOT MEASURED, never as a zero-cost phase — the same rule the whole takt
    discipline rests on, and the same one this file already applies to duration.
    """
    out = ["  PER-PHASE TOKEN USE — read from the TAKT DUCKLAKE"]
    try:
        from buildkit_cli.marathon import takt_lake as tl
    except BaseException as exc:
        return out + ["    " + UNAVAILABLE + " — takt_lake unimportable: "
                      f"{type(exc).__name__}: {exc}",
                      "    (read failure, NOT a zero-cost era)"]
    roll = None
    try:
        roll = tl.phase_token_rollup()
    except AttributeError:
        return out + ["    " + UNAVAILABLE + " — this buildkit has the takt lake but no "
                      "phase_token_rollup(); the RETRIEVAL half is not installed here",
                      "    (read failure, NOT a zero-cost era)"]
    except BaseException as exc:
        return out + ["    " + UNAVAILABLE + f" — {type(exc).__name__}: {exc}",
                      "    (read failure, NOT a zero-cost era)"]

    if not roll.get("available"):
        return out + ["    " + UNAVAILABLE + " — " + str(roll.get("reason") or "no reason given"),
                      "    (read failure, NOT a zero-cost era)"]
    phases = roll.get("phases") or {}
    if not phases:
        return out + ["    lake reachable, ZERO stage rows — nothing recorded yet.",
                      "    That is an EMPTY LAKE, not a zero cost."]

    hdr = ("| PHASE         | ROWS | MEASURED | TOKENS_IN  | TOKENS_OUT | TOTAL      | MODEL |")
    sep = ("|---------------|------|----------|------------|------------|------------|-------|")
    out += ["    " + hdr, "    " + sep]
    for name in sorted(phases):
        b = phases[name]
        meas = b["rows_with_tokens"]
        tot = f"{b['tokens_total']:,}" if meas else "unmeasured"
        tin = f"{b['tokens_in']:,}" if meas else "-"
        tou = f"{b['tokens_out']:,}" if meas else "-"
        mdl = ",".join(b["models"]) or "-"
        out += ["    | %-13s | %4d | %8s | %10s | %10s | %10s | %s |"
                % (name[:13], b["rows"], f"{meas}/{b['rows']}", tin, tou, tot, mdl)]

    t = roll["totals"]
    cov = roll.get("coverage")
    covs = f"{cov * 100:.0f}%" if cov is not None else "n/a"
    out += ["",
            f"    TOTALS: {t['tokens_total']:,} tokens over "
            f"{t['rows_with_tokens']}/{t['rows']} rows carrying a measurement "
            f"(coverage {covs})"]
    if not cov:
        out += ["    COVERAGE 0% — rows exist but NONE carries tokens. UNMEASURED,",
                "    never a zero-cost phase."]
    elif cov < 1.0:
        out += [f"    {t['rows'] - t['rows_with_tokens']} row(s) carry NO token measurement.",
                "    NOT counted as zero; the denominator is shown above."]
    return out


# ------------------------------------------------------------- 6. WHAT'S NEXT
def sec_next(feature):
    lines = ["6. WHAT'S NEXT — DERIVED FROM RECORDED STATE, NOT TYPED", ""]
    ok, out, detail = _marathon("position", feature)
    if ok:
        nxt = [ln.strip() for ln in _clean(out) if ln.strip().startswith("next:")]
        lines += ["  MARATHON NEXT STEP:"] + ["    " + x for x in (nxt or ["(none recorded)"])]
    else:
        lines += ["  MARATHON NEXT STEP: " + UNAVAILABLE + " — " + str(detail)]
    lines += [""]
    ok2, out2, detail2 = _run(["buildkit_cli.roadmap", "next"])
    if ok2:
        body = _clean(out2)[:6]
        lines += ["  ROADMAP RECOMMENDATION:"] + ["    " + ln for ln in (body or ["(none)"])]
    else:
        lines += ["  ROADMAP RECOMMENDATION: " + UNAVAILABLE + " — " + str(detail2)]
    return lines


_SECTIONS = {"roadmap": sec_roadmap, "progress": sec_progress, "status": sec_status,
             "sitrep": sec_sitrep, "takt": sec_takt, "next": sec_next}


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(prog="bk_report_v1", description=FORMAT_ID)
    ap.add_argument("section", choices=[*SECTION_ORDER, "all"])
    ap.add_argument("--feature")
    a = ap.parse_args(argv)
    wanted = SECTION_ORDER if a.section == "all" else (a.section,)

    # ENGINEER RULING Q-GLPNETA17-04 (2026-09-02T15:52Z, ariellas/glpnet), taken AGAINST
    # this lane's own recommendation. It grants a NARROW exception to Q-GLPNETS15-03
    # (gavriella/glpnet, 2026-09-02T14:39Z, "shiras publishes, glpnet authors nothing")
    # for LINE-BUFFERED OUTPUT ONLY. The single-author rule otherwise stands and shiras
    # remains the publisher; any adoption of shiras's file must carry this forward
    # alongside a14f10f8, or it silently re-opens the defect below.
    #
    # MEASURED DEFECT (ARIELLAS, 2026-09-02): the report accumulated every section into
    # one list and printed it once at exit, so a run that reached 1372s of CPU across
    # 85+ minutes had written ZERO bytes. Python buffers redirected stdout, so an empty
    # artefact and a hung process are INDISTINGUISHABLE for the whole run — the operator
    # cannot tell a slow takt-lake scan from a wedged one, and the standing fleet advice
    # is not to reap on absence of output. Sections are now flushed as they complete.
    #
    # The emitted bytes are UNCHANGED: this reproduces `print(chr(10).join(out))`
    # exactly, one chunk at a time, including the single trailing newline.
    first = True

    def emit(lines):
        nonlocal first
        text = chr(10).join(lines)
        sys.stdout.write(text if first else chr(10) + text)
        first = False
        sys.stdout.flush()

    emit(header("FULL" if a.section == "all" else a.section.upper()))
    for i, name in enumerate(wanted):
        if i:
            emit(["", "=" * 78, ""])
        emit(_SECTIONS[name](a.feature))
    sys.stdout.write(chr(10))
    sys.stdout.flush()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
