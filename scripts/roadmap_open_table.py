#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
"""BK-STD-1 §2 — the ROADMAP NOT-CLOSED table. Reference implementation.

Conforms to **BK-STD-1** (proposed by ariellas-tefl, ruled 2026-08-23, broadcast to all coop
channels). This is NOT a competing standard: an earlier version of this file carried a different
column set and a different sort, and it was simply WRONG. **BK-STD-1 governs.**

    python scripts/roadmap_open_table.py                 # §1 header + §2 table + mandatory footer
    python scripts/roadmap_open_table.py --format tsv
    python scripts/roadmap_open_table.py --format json
    python scripts/roadmap_open_table.py --check         # exit 2 on a duplicate allocation

BK-STD-1 §2, enforced here and NOT negotiable:

* sort = **WSJF descending, then feature_id ascending** (NOT grouped by state);
* **every not-closed feature, NO TRUNCATION OF THE ROW SET.** A summarised table is a falsified
  table — truncating it is the same compression the ERA ruling forbids;
* columns fixed, in this order:
  ``| # | EPIC | FEATURE | STATE | WSJF | RICE | SPEC | DLV | BLK |``
  ``FEATURE`` is the **feature_id, never the title** — ids are the join key across lanes;
* mandatory footer — the honesty counters:
  ``SPEC=NONE: n/total   DEDUPE_GROUPS=n (kth consecutive)   RECONCILE=<result>``
  They exist because ``reconcile`` and ``dedupe`` both report clean while being structurally
  blind, so a report that omits them looks healthier than the data is.

MEASURED TRAPS THIS HANDLES (BK-STD-1 §4) — do not "simplify" them away:

* **``reconcile`` compares only the LOCAL pipeline.** It answered "already in sync" while this
  catalog was 90 journal lines behind a peer. A green reconcile is NOT evidence of currency.
* **``roadmap import`` with no ``--in-dir`` scans the LOCAL ``exports/``, not the coop inbox** —
  it imports nothing from peers and still reports success. Always pass
  ``--in-dir <coop>/<repo>/roadmap-sync/inbox``.
* buildkit CLIs print PGlite/``co:`` banners on STDOUT; filtered per line, never through a pipe
  (a pipe reports the FILTER's exit status, not the command's).
* ``BUILDKIT_ENGINE_OVERRIDE`` cleared; ``PYTHONIOENCODING=utf-8`` forced.
* ``--roadmap-cmd`` exists because ambient ``buildkit_cli`` has been replaced mid-session by a
  copy missing whole sub-packages.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import socket
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

NONE = "-"
OWNERS_FILE = ".specify/roadmap-owners.json"

ROW_RE = re.compile(
    r"^\s+\[(?P<state>\w+)\s*\]\s+#(?P<slot>\S+)\s+(?P<fid>\S+)\s+"
    r"WSJF=(?P<wsjf>\S+)\s+RICE=(?P<rice>\S+)\s+[—-]\s*(?P<rest>.*)$"
)
EPIC_RE = re.compile(r"^Epic:\s+(?P<name>.*?)\s*\((?P<eid>[^()]*)\)\s*$")


def _clean(line):
    s = line.lstrip()
    return not (s.startswith("co:") or "PGlite" in line)


def run_roadmap(cmd, cwd, argv):
    env = dict(os.environ)
    env["PYTHONIOENCODING"] = "utf-8"
    env.pop("BUILDKIT_ENGINE_OVERRIDE", None)
    p = subprocess.run(cmd + argv, cwd=str(cwd), env=env, capture_output=True,
                       text=True, encoding="utf-8", errors="replace")
    if p.returncode != 0 and not p.stdout.strip():
        sys.stderr.write(p.stderr or "roadmap failed\n")
        raise SystemExit(1)
    return p.stdout


def parse(text):
    epic, rows = "(none)", []
    for line in text.splitlines():
        if not _clean(line):
            continue
        m = EPIC_RE.match(line.strip())
        if m:
            epic = m.group("eid") or m.group("name")
            continue
        m = ROW_RE.match(line.rstrip())
        if not m or m.group("state") == "closed":
            continue
        rest = m.group("rest")
        flags = rest[rest.rfind("[") + 1: rest.rfind("]")] if rest.rstrip().endswith("]") else ""
        b = re.search(r"blocked-by:\s*([^;\]]*)", flags)
        rows.append({
            "epic": epic,
            "feature": m.group("fid"),
            "state": m.group("state"),
            "wsjf": m.group("wsjf"),
            "rice": m.group("rice"),
            "spec": "Y" if re.search(r"\bspec:\s*\S", flags) else NONE,
            "dlv": "Y" if "delivered" in flags else NONE,
            "blk": len([x for x in b.group(1).split(",") if x.strip()]) if b else 0,
        })
    return rows


def correct_epics_from_export(rows, root):
    """OVERWRITE each row's epic from the signed-export fold (BK-STD-1 integrity).

    `parse()` reads `roadmap status` TEXT and carries a STATEFUL `epic` variable
    forward across rows, so a feature with a NULL `epic_id` — or one pointing at a
    different epic — silently inherits whichever epic header preceded it. Measured
    2026-09-02 on glpnet: 11 of 29 not-closed features have NO epic at all and 1
    points at a tombstoned epic, yet all 12 rendered under a real epic's name.

    The fold is authoritative and agrees with the catalog. A feature the fold does
    not carry keeps its parsed value and is flagged `(unfolded)` rather than being
    silently trusted — an absent row is reported, never assumed correct.
    """
    fold = {}
    host = socket.gethostname().lower()
    exdir = root / ".specify" / "roadmap-sync" / "exports"
    exports = sorted(exdir.glob("%s__%s__*.json" % (host, root.name.lower())))
    if not exports:
        return rows  # no export from THIS lane -> never correct from a peer's
    try:
        with open(exports[-1], encoding="utf-8") as fh:
            doc = json.load(fh)
        if True:
            for h in doc.get("heads") or []:
                if h.get("entity_kind") != "feature":
                    continue
                slot = h.get("resolved_slot") or h.get("claimed_slot") or h.get("name")
                if slot:
                    fold[slot] = h.get("epic_id") or "(standalone)"
    except Exception:
        return rows  # fold unreadable -> leave rows untouched, never invent

    if not fold:
        return rows
    for r in rows:
        fid = r.get("feature")
        if fid in fold:
            r["epic"] = fold[fid]
        else:
            r["epic"] = "%s (unfolded)" % r.get("epic", "(none)")
    return rows


def backfill_from_export(rows, root):
    """Add not-closed features that `roadmap status` omits (BK-STD-1 defect, 2026-08-27).

    MEASURED on host Gavriella, glpnet, roadmap round 53: `buildkit-roadmap status`
    emits NO row for a feature in state `implemented`, so this table reported 25
    not-closed while BK-REPORT-v1 section 1 reported open=26 on the same data. The
    dropped row was `qr-link-provisioning` (067) -- the feature FURTHEST along the
    pipeline. `implemented` is a legal not-closed state, so a table that hides it
    under-reports open work and hides it precisely where it matters most.

    The parse filter above is NOT the bug; it drops only `closed`. The loss is
    upstream in the `status` command, which this script should not have been the
    sole consumer of -- standing fleet guidance is "never parse `roadmap status`
    for counts; use the signed-export heads fold". This backfills from that signed
    export (heads joined to scores by guid) so the table matches the report without
    waiting on a fix in another lane's code.

    Additive and conservative: a feature already present from `status` is never
    replaced, so WSJF/RICE/flags for existing rows are untouched.
    """
    # BIND TO THIS LANE'S OWN EXPORT. The exports dir holds every peer's published
    # export too (measured 2026-08-27: gavriella 115, ariellas 83, olamnit 50, and
    # 15 under a misspelled `gavriellas` host). A bare `*__*.json` + sorted()[-1]
    # selects `olamnit__glpnet__20260823...` -- ANOTHER HOST'S FOUR-DAY-STALE DATA --
    # and reports it as this repo's state. That is the exact failure this feature
    # exists to stop, so the host prefix is matched exactly, not by sort order.
    host = socket.gethostname().lower()
    exdir = root / ".specify" / "roadmap-sync" / "exports"
    exports = sorted(exdir.glob("%s__%s__*.json" % (host, root.name.lower())))
    if not exports:
        return rows  # no export from THIS lane => report status as-is, never a peer's
    try:
        with open(exports[-1], encoding="utf-8") as fh:
            doc = json.load(fh)
    except (OSError, ValueError):
        return rows  # unreadable export => report what status gave, never invent
    scores = {s.get("guid"): s for s in doc.get("scores", []) if isinstance(s, dict)}
    have = {r["feature"] for r in rows}
    for h in doc.get("heads", []):
        if not isinstance(h, dict) or h.get("entity_kind") != "feature":
            continue
        state = h.get("state")
        if state == "closed" or not state:
            continue
        fid = h.get("resolved_slot") or h.get("name")
        if not fid or fid in have:
            continue
        sc = scores.get(h.get("guid"), {})
        rows.append({
            "epic": h.get("epic_id") or "(standalone)",
            "feature": fid,
            "state": state,
            "wsjf": sc.get("wsjf", NONE),
            "rice": sc.get("rice", NONE),
            "spec": "Y" if h.get("spec_path") else NONE,
            "dlv": NONE,
            "blk": 0,
        })
        have.add(fid)
    return rows


def _num(x):
    try:
        return float(x)
    except (TypeError, ValueError):
        return -1.0


def _wsjf(x):
    return NONE if _num(x) < 0 else "%.2f" % _num(x)


def _rice(x):
    return NONE if _num(x) < 0 else "%d" % round(_num(x))


def load_owners(root, extra, files):
    """feature_id -> {host}. A SET: one feature claimed by TWO hosts is the incident."""
    owners = {}

    def absorb(mapping):
        for fid, host in mapping.items():
            for h in (host if isinstance(host, (list, tuple, set)) else [host]):
                owners.setdefault(fid, set()).add(str(h))

    default = root / OWNERS_FILE
    for cand in [default] + [Path(f) for f in files]:
        if not cand.is_file():
            if cand != default:
                sys.stderr.write("warning: owners file not found: %s\n" % cand)
            continue
        try:
            absorb(json.loads(cand.read_text(encoding="utf-8")))
        except (OSError, ValueError) as exc:
            sys.stderr.write("warning: could not read %s (%s)\n" % (cand, exc))
    if extra:
        for pair in extra.split(","):
            if "=" in pair:
                fid, host = pair.split("=", 1)
                owners.setdefault(fid.strip(), set()).add(host.strip())
    return owners


def sort_rows(rows, owners):
    """BK-STD-1 §2: WSJF DESC, then feature_id ASC. Deterministic across hosts."""
    for r in rows:
        r["owners"] = sorted(owners.get(r["feature"], ()))
    rows.sort(key=lambda r: (-_num(r["wsjf"]), r["feature"]))
    return rows


def duplicates(rows):
    return [(r["feature"], r["owners"]) for r in rows if len(r.get("owners") or ()) > 1]


def header(root, lane, rnd, run):
    return "HOST=%s  REPO=%s  LANE=%s  ROUND=%s  RUN=%s  UTC=%s" % (
        socket.gethostname(), root.name, lane, rnd, run,
        datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"))


def footer(rows, groups, run_n, reconcile):
    nospec = sum(1 for r in rows if r["spec"] == NONE)
    return "SPEC=NONE: %d/%d   DEDUPE_GROUPS=%s (%s consecutive)   RECONCILE=%s" % (
        nospec, len(rows), groups, run_n, reconcile)


def totals(rows):
    c = {}
    for r in rows:
        c[r["state"]] = c.get(r["state"], 0) + 1
    return "%d not-closed = %s, across %d epics" % (
        len(rows), " · ".join("%d %s" % (c[k], k) for k in sorted(c)),
        len({r["epic"] for r in rows}))


def render(rows, fmt, hdr, ftr):
    if fmt == "json":
        return json.dumps({"header": hdr, "rows": rows, "footer": ftr,
                           "totals": totals(rows)}, indent=2)
    if fmt == "tsv":
        out = [hdr, "#\tEPIC\tFEATURE\tSTATE\tWSJF\tRICE\tSPEC\tDLV\tBLK"]
        out += ["%d\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%d" % (
            i, r["epic"], r["feature"], r["state"], _wsjf(r["wsjf"]), _rice(r["rice"]),
            r["spec"], r["dlv"], r["blk"]) for i, r in enumerate(rows, 1)]
        return "\n".join(out + ["", ftr, totals(rows)])
    out = ["```", hdr, "```", "",
           "| # | EPIC | FEATURE | STATE | WSJF | RICE | SPEC | DLV | BLK |",
           "|---:|:---|:---|:---|---:|---:|:--:|:--:|---:|"]
    for i, r in enumerate(rows, 1):
        own = (" **[%s]**" % "/".join(r["owners"])) if len(r["owners"]) > 1 else ""
        out.append("| %d | %s | `%s`%s | %s | %s | %s | %s | %s | %d |" % (
            i, r["epic"], r["feature"], own, r["state"], _wsjf(r["wsjf"]),
            _rice(r["rice"]), r["spec"], r["dlv"], r["blk"]))
    return "\n".join(out + ["", "```", ftr, "```", "", "**%s**" % totals(rows)])


def main(argv=None):
    ap = argparse.ArgumentParser(description="BK-STD-1 2 roadmap NOT-CLOSED table.")
    ap.add_argument("--repo", default=".")
    ap.add_argument("--format", choices=("md", "tsv", "json"), default="md")
    ap.add_argument("--owner", default=None)
    ap.add_argument("--owners-file", action="append", default=[])
    ap.add_argument("--check", action="store_true", help="exit 2 on a duplicate allocation")
    ap.add_argument("--roadmap-cmd", default=None)
    ap.add_argument("--lane", default=None, help="BK-STD-1 LANE = <actor>-<repo>")
    ap.add_argument("--round", default="-")
    ap.add_argument("--run", default="-")
    ap.add_argument("--dedupe-groups", default="?")
    ap.add_argument("--dedupe-run", default="?th")
    ap.add_argument("--reconcile-note", default="?",
                    help="reconcile result; it compares only the LOCAL pipeline")
    a = ap.parse_args(argv)

    root = Path(a.repo).resolve()
    cmd = [a.roadmap_cmd] if a.roadmap_cmd else [sys.executable, "-m", "buildkit_cli.roadmap"]
    lane = a.lane or ("%s-%s" % (socket.gethostname().lower(), root.name))

    rows = correct_epics_from_export(
        backfill_from_export(parse(run_roadmap(cmd, root, ["status"])), root), root)
    rows = sort_rows(rows, load_owners(root, a.owner, a.owners_file))
    print(render(rows, a.format, header(root, lane, a.round, a.run),
                 footer(rows, a.dedupe_groups, a.dedupe_run, a.reconcile_note)))

    d = duplicates(rows)
    if d:
        sys.stderr.write("\nDUPLICATE ALLOCATION - one feature, two hosts:\n")
        for fid, hosts in d:
            sys.stderr.write("  %s -> %s\n" % (fid, ", ".join(hosts)))
        if a.check:
            return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
