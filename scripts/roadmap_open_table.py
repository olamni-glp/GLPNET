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

    rows = sort_rows(parse(run_roadmap(cmd, root, ["status"])),
                     load_owners(root, a.owner, a.owners_file))
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
