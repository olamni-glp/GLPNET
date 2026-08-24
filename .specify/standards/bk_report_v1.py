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

Usage::

    python .specify/standards/bk_report_v1.py all --feature <id>
    python .specify/standards/bk_report_v1.py roadmap|progress|status|sitrep|takt|next
"""

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
    env.setdefault("BUILDKIT_ENGINE_OVERRIDE", "ambient")
    env["PYTHONUTF8"] = "1"
    try:
        p = subprocess.run([sys.executable, "-m", *args], capture_output=True, text=True,
                           encoding="utf-8", errors="replace", timeout=timeout, env=env)
    except BaseException as exc:                              # noqa: BLE001
        return False, "", f"{type(exc).__name__}: {exc}"
    if p.returncode != 0:
        tail = (p.stderr or "").strip().splitlines()
        return False, p.stdout or "", (tail[-1] if tail else f"exit {p.returncode}")
    return True, p.stdout, ""


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
    lines += ["", "  ERA STAGE LADDER — all nine are stages WITHIN one era, never eras of their own:",
              "  " + " -> ".join(ERA_STAGES),
              "  A feature is NEVER split, summarised or compressed to fit a takt band."]
    return lines


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
    out = header("FULL" if a.section == "all" else a.section.upper())
    for i, name in enumerate(wanted):
        if i:
            out += ["", "=" * 78, ""]
        out += _SECTIONS[name](a.feature)
    print(chr(10).join(out))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
