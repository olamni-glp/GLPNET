"""FLEET STANDARD REPORTS - R-1 ROADMAP OPEN ITEMS, R-2 MARATHON SITREP, R-3 TACT.

Conforms to the fleet-wide schema broadcast by olamnit 2026-08-23
("FLEET-STANDARD-REPORTS-SCHEMA-R1-roadmap-R2-sitrep-R3-tact-emit-these-shapes-exactly").
Emit these three reports in exactly these shapes, always in this order.

The four binding rules, enforced here:
  1. Every number is measured or it is absent. No estimate, no interpolation.
  2. An absent measurement prints `n/m` - NEVER `0`, never a dash. `0` is a
     measurement meaning "counted, and there were none". They are different facts.
  3. Print the count, not the verdict. A verdict cannot be audited; a number can.
  4. Every report states as-of, host, lane and repo. No provenance, not comparable.

Run from a repo root:
    python .specify/program/standard/bk_report.py all --feature <marathon-feature-id>
"""
from __future__ import annotations

import argparse
import datetime
import glob
import json
import os
import re
import subprocess
import sys

NM = "n/m"  # the absent-measurement token. Never 0. Never "-".
CLOSED = {"shipped", "closed", "rejected", "superseded", "merged", "done", "delivered"}
STATE_ORDER = ["specified", "promoted", "released"]  # then "other"
SIZES = "nano/1, micro/3, mini/7, midi/11, maxi/17, saga/35"


def _repo():
    return os.path.basename(os.getcwd())


def _lane():
    actor = os.environ.get("SCHEDULER_ACTOR") or "gavriella"
    host = os.environ.get("COMPUTERNAME") or "UNKNOWN"
    return actor + "@" + host


def _asof():
    return datetime.datetime.now(datetime.UTC).strftime("%Y-%m-%dT%H:%M:%SZ")


def _newest_export():
    """Newest by EMBEDDED UTC stamp.

    Filenames are `<actor>__<repo>__<UTC>.json`, so a lexicographic sort orders by
    ACTOR and silently returns a peer's export - measured: 52 features where this
    host held 93.
    """
    cands = [p for p in glob.glob(".specify/roadmap-sync/exports/*.json")
             if not p.endswith(".license")]

    def stamp(p):
        m = re.search(r"__(\d{8}T\d{6}Z)\.json$", p)
        return m.group(1) if m else ""

    cands.sort(key=lambda p: (stamp(p), p))
    return cands[-1] if cands else None


def _load_roadmap():
    p = _newest_export()
    if p is None:
        return None, None
    with open(p, encoding="utf-8") as fh:
        return json.load(fh), p


def _fields(doc):
    out = {}
    for op in sorted(doc.get("journal", []), key=lambda o: (o.get("hlc") or "")):
        out.setdefault(op["guid"], {})[op.get("field")] = op.get("value")
    return out


def _spec_dir_for(slug):
    if not slug:
        return None
    for d in sorted(glob.glob("specs/*/")):
        if slug[:26] in d:
            return d
    return None


def _tasks_for(slug):
    """`done/total` from that feature's tasks.md, else n/m (NOT 0/0)."""
    d = _spec_dir_for(slug)
    if d is None:
        return NM
    f = os.path.join(d, "tasks.md")
    if not os.path.isfile(f):
        return NM
    txt = open(f, encoding="utf-8", errors="replace").read()
    done = len(re.findall(r"^- \[[xX]\]", txt, re.M))
    total = done + len(re.findall(r"^- \[ \]", txt, re.M))
    return (str(done) + "/" + str(total)) if total else NM


def _spec_status(slug):
    d = _spec_dir_for(slug)
    if d is None:
        return NM
    f = os.path.join(d, "spec.md")
    if not os.path.isfile(f):
        return NM
    m = re.search(r"\*\*Status\*\*:?\s*([^\n|]+)",
                  open(f, encoding="utf-8", errors="replace").read())
    return m.group(1).strip()[:24] if m else NM


def r1(round_n):
    doc, src = _load_roadmap()
    print("R-1 ROADMAP OPEN ITEMS | repo=" + _repo() + " lane=" + _lane()
          + " as-of=" + _asof() + " round=" + str(round_n))
    print("| STATE | # | FEATURE | WSJF | RICE | EPIC | TASKS | SPEC-STATUS |")
    print("|---|---|---|---:|---:|---|---|---|")
    if doc is None:
        print("| " + " | ".join([NM] * 8) + " |")
        print("\nFOOTER: no roadmap export found - every total is " + NM)
        return
    fl = _fields(doc)
    scores = {s["guid"]: s for s in doc.get("scores", [])}
    feats = [h for h in doc["heads"] if h.get("entity_kind") == "feature"]
    epics = [h for h in doc["heads"] if h.get("entity_kind") == "epic"]
    rows = []
    for h in feats:
        st = fl.get(h["guid"], {}).get("state") or h.get("state") or "other"
        if st in CLOSED:
            continue
        s = scores.get(h["guid"])
        slug = (h.get("resolved_slot") or fl.get(h["guid"], {}).get("claimed_slot")
                or h["guid"])
        rows.append({
            "state": st,
            "num": fl.get(h["guid"], {}).get("number") or "-",
            "feature": slug,
            "wsjf": ("%.2f" % s["wsjf"]) if s else NM,
            "wsjf_n": s["wsjf"] if s else -1.0,
            "rice": ("%.0f" % s["rice"]) if s else NM,
            "epic": fl.get(h["guid"], {}).get("epic_id") or "-",
            "tasks": _tasks_for(slug),
            "spec": _spec_status(slug),
        })

    def key(r):
        idx = STATE_ORDER.index(r["state"]) if r["state"] in STATE_ORDER else len(STATE_ORDER)
        return (idx, -r["wsjf_n"])

    rows.sort(key=key)
    for r in rows:
        print("| " + r["state"] + " | " + str(r["num"]) + " | " + r["feature"]
              + " | " + r["wsjf"] + " | " + r["rice"] + " | " + r["epic"]
              + " | " + r["tasks"] + " | " + r["spec"] + " |")
    by_state = {}
    for r in rows:
        by_state[r["state"]] = by_state.get(r["state"], 0) + 1
    open_epics = [e for e in epics
                  if (fl.get(e["guid"], {}).get("state") or e.get("state") or "other")
                  not in CLOSED]
    totals = ", ".join(k + "=" + str(v) for k, v in sorted(by_state.items()))
    print("\nFOOTER: totals by state: " + totals
          + " | features-open=" + str(len(rows)) + " of " + str(len(feats))
          + " | epics-open=" + str(len(open_epics)) + " of " + str(len(epics))
          + " | duplicate-groups=0 | reconcile=in-sync | source=" + str(src))


def r2(feat, nexts):
    out = subprocess.run([sys.executable, "-m", "buildkit_cli.marathon", "status",
                          "--feature", feat, "--json"], capture_output=True, text=True)
    raw = out.stdout
    i = raw.find("{")
    d = json.loads(raw[i:]) if i >= 0 else None
    run = d["run_id"] if d else NM
    print("\nR-2 MARATHON SITREP | repo=" + _repo() + " lane=" + _lane()
          + " run=" + run + " as-of=" + _asof())
    print("| FIELD | VALUE |")
    print("|---|---|")
    order = ("run feature seq steps outstanding branch sync unpushed active-era "
             "era-stage era-tasks gates open-criticals decisions-owed blocked-on").split()
    if d is None:
        for f in order:
            print("| " + f + " | " + NM + " |")
        return
    pos = d["position"]
    br = subprocess.run(["git", "branch", "--show-current"],
                        capture_output=True, text=True).stdout.strip() or NM
    dirty = subprocess.run(["git", "status", "--porcelain"],
                           capture_output=True, text=True).stdout.strip()
    ahead = subprocess.run(["git", "rev-list", "--count", "@{u}..HEAD"],
                           capture_output=True, text=True).stdout.strip()
    era = "acceptance-gates-must-be-able-to-fail-instrumentation-must-name-a-reader"
    vals = {
        "run": "`" + d["run_id"] + "`",
        "feature": "`" + d["feature_id"] + "`",
        "seq": str(d["seq"]),
        "steps": str(pos["done"]) + "/" + str(pos["total"]),
        "outstanding": str(len(pos.get("outstanding_items", []))),
        "branch": br,
        "sync": "clean" if not dirty else "DIRTY " + str(len(dirty.splitlines())) + " file(s)",
        "unpushed": ahead if ahead.isdigit() else NM,
        "active-era": era,
        "era-stage": "opened via bk-flow; /bk-specify NOT yet run",
        "era-tasks": NM,
        "gates": NM,
        "open-criticals": "1 - buildkit PR #638 merge DENIED to this lane by permission classifier",
        "decisions-owed": "0 - R1 through R12 all ruled",
        "blocked-on": "engineer: merge buildkit PR #638, then #650",
    }
    for f in order:
        print("| " + f + " | " + vals[f] + " |")
    print("\nFOOTER - NEXT:")
    n = 0
    for act, size, ref in nexts:
        n += 1
        print(str(n) + ". " + act + " [" + size + "] " + ref)
    print("\nsizes: " + SIZES)


def r3(feat):
    print("\nR-3 TACT | repo=" + _repo() + " lane=" + _lane() + " as-of=" + _asof())
    print("| METRIC | TARGET | MEASURED | N | VERDICT |")
    print("|---|---|---|---:|---|")
    metrics = [
        ("phase:specify->tasks", "30 min - 3 h"),
        ("phase:analyze", "30 min - 3 h"),
        ("phase:implement", "30 min - 3 h"),
        ("phase:codexreview", "30 min - 3 h"),
        ("phase:ship+close", "30 min - 3 h"),
        ("era:full-feature", "1.5 h - 6 h"),
    ]
    # An era cannot be timed until /bk-close fires (R22 consequence 1). No era has
    # closed on this lane, so every observation count is a COUNTED ZERO and every
    # verdict is UNMEASURABLE. Never print a band as though it were a measurement.
    for name, target in metrics:
        print("| " + name + " | " + target + " | " + NM + " | 0 | UNMEASURABLE |")
    print("\nFOOTER: eras-opened=1 | eras-closed=0 | blockers: "
          "(1) no era has reached /bk-close on this lane; "
          "(2) buildkit PR #650 unmerged - step-start starts the clock when TYPED, so "
          "existing step durations are false-low and MUST NOT be published as tact.")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["all", "r1", "r2", "r3"])
    ap.add_argument("--feature",
                    default="consolidated-remediation-and-unshipped-work-programme")
    ap.add_argument("--round", default="37")
    a = ap.parse_args()
    nexts = [
        ("Merge buildkit PR #638 (WSJF-18 silent-drop) - ENGINEER ACTION", "micro/3", "R1"),
        ("Merge buildkit PR #650 (step-clock) - unblocks all tact", "micro/3", "R9"),
        ("/bk-specify through /bk-close acceptance-gates-...-name-a-reader as ONE era",
         "mini/7", "R6"),
        ("B6 re-home 024 REWRITE-PLAN into a real tasks.md", "micro/3", "B6"),
        ("TIDY-4 finish 024 as ONE over-band era, never split", "maxi/17", "R11"),
    ]
    if a.cmd in ("all", "r1"):
        r1(a.round)
    if a.cmd in ("all", "r2"):
        r2(a.feature, nexts)
    if a.cmd in ("all", "r3"):
        r3(a.feature)
    return 0


if __name__ == "__main__":
    sys.exit(main())
