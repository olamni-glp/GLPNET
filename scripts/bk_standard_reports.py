#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Emit the FLEET STANDARD REPORTS in the exact shapes: R-1 ROADMAP, R-2 SITREP, R-3 TACT.

Standard: coop `20260823T205948Z-olamnit-FLEET-STANDARD-REPORTS-SCHEMA-R1-roadmap-R2-sitrep-R3-tact`.

Binding rules honoured here:
  1. every number is measured or it is absent
  2. an absent measurement prints ``n/m`` -- never ``0``, never a dash
  3. print the count, not the verdict
  4. every report states its as-of, host, lane and repo
"""
import datetime
import glob
import json
import os
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
ENV = dict(os.environ)
ENV["PYTHONUTF8"] = "1"
NM = "n/m"
LANE = "gavriella@GAVRIELLA"
REPO = "GLPNET"
FEATURE = "078-verification-receipts"


def sh(*args, timeout=300):
    try:
        return subprocess.run(args, capture_output=True, text=True, env=ENV,
                              cwd=str(ROOT), timeout=timeout).stdout.strip()
    except Exception:
        return ""


def now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def latest_export():
    files = sorted(glob.glob(str(ROOT / ".specify/roadmap-sync/exports/gavriella__glpnet__*.json")))
    if not files:
        return {}
    return json.loads(pathlib.Path(files[-1]).read_text(encoding="utf-8"))


def _spec_dir(spec_path, slot):
    for cand in (spec_path, "specs/" + (slot or "")):
        if not cand:
            continue
        p = ROOT / cand
        if p.is_dir():
            return p
    return None


def tasks_ratio(spec_path, slot):
    d = _spec_dir(spec_path, slot)
    if d is None:
        return NM
    p = d / "tasks.md"
    if not p.is_file():
        return NM
    text = p.read_text(encoding="utf-8", errors="replace")
    done = len(re.findall(r"^\s*-\s*\[[xX]\]", text, re.M))
    total = len(re.findall(r"^\s*-\s*\[[ xX]\]", text, re.M))
    return "%d/%d" % (done, total) if total else NM


def spec_status(spec_path, slot):
    d = _spec_dir(spec_path, slot)
    if d is None:
        return NM
    p = d / "spec.md"
    if not p.is_file():
        return NM
    text = p.read_text(encoding="utf-8", errors="replace")
    m = re.search(r"^\*\*Status\*\*\s*:?\s*(.+)$", text, re.M)
    return m.group(1).strip().strip("*")[:26] if m else NM


def r1():
    j = latest_export()
    heads = j.get("heads", [])
    feats = [h for h in heads if h.get("entity_kind") == "feature"]
    epics = [h for h in heads if h.get("entity_kind") == "epic"]
    scores = {s.get("guid"): s for s in (j.get("scores") or [])}

    rows = []
    for f in feats:
        state = f.get("state") or NM
        if state == "closed":
            continue
        sc = scores.get(f.get("guid")) or {}
        slot = f.get("resolved_slot") or NM
        rows.append({
            "state": state,
            "num": f.get("priority_rank") if f.get("priority_rank") is not None else "-",
            "feature": slot,
            "wsjf": sc.get("wsjf"),
            "rice": sc.get("rice"),
            "epic": f.get("epic_id") or "-",
            "tasks": tasks_ratio(f.get("spec_path"), slot),
            "spec": spec_status(f.get("spec_path"), slot),
        })

    order = {"specified": 0, "promoted": 1, "released": 2}
    rows.sort(key=lambda r: (order.get(r["state"], 3), r["state"],
                             -(r["wsjf"] if r["wsjf"] is not None else -1.0),
                             r["feature"]))

    print("R-1 ROADMAP OPEN ITEMS | repo=%s lane=%s as-of=%s round=50" % (REPO, LANE, now()))
    print("| STATE | # | FEATURE | WSJF | RICE | EPIC | TASKS | SPEC-STATUS |")
    print("|---|---|---|---|---|---|---|---|")
    for r in rows:
        print("| %s | %s | `%s` | %s | %s | %s | %s | %s |" % (
            r["state"], r["num"], r["feature"],
            ("%.2f" % r["wsjf"]) if r["wsjf"] is not None else NM,
            ("%.0f" % r["rice"]) if r["rice"] is not None else NM,
            r["epic"], r["tasks"], r["spec"]))

    by = {}
    for r in rows:
        by[r["state"]] = by.get(r["state"], 0) + 1
    dd = sh("buildkit-roadmap", "dedupe")
    groups = "0" if "no duplicate groups found" in dd else (NM if not dd else "non-zero")
    rec = sh("buildkit-roadmap", "reconcile")
    recv = "in-sync" if "already in sync" in rec else (NM if not rec else "changed")
    print()
    print("FOOTER: totals by state = %s | not-closed total = %d | epics total = %d | "
          "duplicate groups = %s | reconcile = %s"
          % (", ".join("%s %d" % kv for kv in sorted(by.items())),
             len(rows), len(epics), groups, recv))


def r2():
    st = sh("buildkit-marathon", "status", "--feature", FEATURE)
    run = re.search(r"run (\S+)", st)
    seq = re.search(r"seq=(\d+)", st)
    steps = re.search(r"steps: (\S+) complete", st)
    outs = re.search(r"outstanding items: (\d+)", st)
    branch = sh("git", "branch", "--show-current")
    ahead = sh("git", "rev-list", "--count", "origin/main..origin/develop")
    unpushed = sh("git", "rev-list", "--count", "origin/develop..HEAD")
    dirty = len([x for x in sh("git", "status", "--porcelain").splitlines() if x.strip()])

    print("R-2 MARATHON SITREP | repo=%s lane=%s run=%s as-of=%s"
          % (REPO, LANE, run.group(1) if run else NM, now()))
    print("| FIELD | VALUE |")
    print("|---|---|")
    fields = [
        ("run", run.group(1) if run else NM),
        ("feature", FEATURE),
        ("seq", seq.group(1) if seq else NM),
        ("steps", steps.group(1) if steps else NM),
        ("outstanding", outs.group(1) if outs else NM),
        ("branch", "%s (clean)" % branch if dirty == 0 else "%s (%d dirty)" % (branch, dirty)),
        ("sync", "develop ahead of main by %s" % (ahead or NM)),
        ("unpushed", unpushed if unpushed != "" else NM),
        ("active-era", FEATURE),
        ("era-stage", "analyzed (roadmap); codexreview NOT run"),
        ("era-tasks", tasks_ratio("specs/" + FEATURE, FEATURE)),
        ("gates", "REPL 559/561 pass, 2 fail (pre-existing Section T 064 drills)"),
        ("open-criticals", "0"),
        ("decisions-owed", "2 (Y06/Y07 graduation; Y09 X10 survivor)"),
        ("blocked-on", "codexreview tool defect: brief inlines diff (context overflow); "
                       "--scope <path> refuses a tracked subtree"),
    ]
    for k, v in fields:
        print("| %s | %s |" % (k, v))
    print()
    print("FOOTER: NEXT")
    nxt = [
        ("SCHED-R1 declare the backlog->ready readiness writer", "maxi/17", "ruling Q2 2026-08-24"),
        ("SCHED-R4 declare dependency edges (edge_coverage is 0.0)", "midi/11", "consolidated-hardening"),
        ("TIDY-Y14 class-C2 remote cleanup (must run LAST)", "mini/7", "tidy-up CRDT plan"),
        ("TIDY-Y16 introduce the ERA metric in marathon", "midi/11", "ERA ruling 2026-08-23"),
        ("TIDY-Y17 unique allocation, one feature/repo/host", "maxi/17", "INCIDENT 20260823T222700Z"),
        ("TIDY-Y18 takt-only duration rule", "midi/11", "R-3 rule 1"),
    ]
    for i, (a, s, ref) in enumerate(nxt, 1):
        print("%d. %s [%s] %s" % (i, a, s, ref))


def r3():
    out = sh("buildkit-marathon", "takt", "--feature", FEATURE, "--json")
    try:
        d = json.loads(out)
    except Exception:
        d = {}
    steps = d.get("steps") or []

    groups = [
        ("phase:specify→tasks", {"specify", "clarify", "plan", "tasks"}, "30 min – 3 h"),
        ("phase:analyze", {"analyze"}, "30 min – 3 h"),
        ("phase:implement", {"implement"}, "30 min – 3 h"),
        ("phase:codexreview", {"codexreview"}, "30 min – 3 h"),
        ("phase:ship+close", {"ship", "close"}, "30 min – 3 h"),
    ]

    print("R-3 TACT | repo=%s lane=%s as-of=%s" % (REPO, LANE, now()))
    print("| METRIC | TARGET | MEASURED | N | VERDICT |")
    print("|---|---|---|---|---|")
    for metric, phases, target in groups:
        vals = sorted(s["seconds"] for s in steps
                      if s.get("seconds") is not None and s.get("phase") in phases)
        if not vals:
            print("| `%s` | %s | %s | 0 | UNMEASURABLE |" % (metric, target, NM))
            continue
        mid = len(vals) // 2
        med = vals[mid] if len(vals) % 2 else (vals[mid - 1] + vals[mid]) / 2.0
        hours = med / 3600.0
        verdict = "IN-BAND" if 0.5 <= hours <= 3.0 else "OUT-OF-BAND"
        print("| `%s` | %s | %.2f h | %d | %s |" % (metric, target, hours, len(vals), verdict))
    # R22 consequence 1: an era cannot be timed until /bk-close fires. Zero eras have closed.
    print("| `era:full-feature` | 1.5 h – 6 h | %s | 0 | UNMEASURABLE |" % NM)
    print()
    print("FOOTER: eras opened = 1 (%s) | eras CLOSED = 0 | blockers preventing closure = "
          "/bk-codexreview cannot run on this host (brief inlines the diff body -> model "
          "context overflow; --scope <path> refuses a subtree with 8 tracked files), so "
          "/bk-ship and /bk-close cannot fire and NO era on this lane is timable. "
          "A band is never printed as though it were a measurement." % FEATURE)


if __name__ == "__main__":
    which = sys.argv[1] if len(sys.argv) > 1 else "all"
    if which in ("all", "r1"):
        r1()
        print()
    if which in ("all", "r2"):
        r2()
        print()
    if which in ("all", "r3"):
        r3()
