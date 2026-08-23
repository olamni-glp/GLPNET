#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
"""FLEET STANDARD — the NOT-CLOSED roadmap table.

Canonical, portable renderer for "every epic and feature not closed", in the one
tabular format every host and every repo uses. Engineer ruling 2026-08-23.

    python scripts/roadmap_open_table.py                # markdown (default)
    python scripts/roadmap_open_table.py --format tsv   # paste into a sheet
    python scripts/roadmap_open_table.py --format json  # machine-readable
    python scripts/roadmap_open_table.py --check        # exit 2 on a duplicate owner

WHY A SCRIPT AND NOT A CONVENTION
---------------------------------
A format described in prose drifts the moment two lanes render it by hand — and a
drifted ownership column is not cosmetic here: the allocation gate FAILS OPEN, so
two lanes can both believe they own a feature. This renderer is the single
implementation; ``--check`` turns the standard into an executable gate.

STANDARD (all of it is enforced below, none of it is optional)
--------------------------------------------------------------
Columns, in order:

    | # | State | Slot | Feature (slug) | WSJF | RICE | Owner | Epic |

* rows are every feature whose state is NOT ``closed``;
* sort: state (specified -> promoted -> captured -> anything else), then WSJF
  DESCENDING, then slug ascending — deterministic, so two hosts rendering the
  same catalog produce byte-identical tables;
* ``Owner`` is the host holding the feature, or ``—`` when unallocated;
* unscored features render ``—``, NEVER ``0`` (a real 0.00 and "never scored" are
  different facts and must not collapse);
* a totals line is ALWAYS printed;
* two rows with the same slug and different owners = a DUPLICATE-ALLOCATION
  INCIDENT. ``--check`` exits 2 on it.

TRAPS THIS SCRIPT ALREADY HANDLES (measured on the fleet, do not "simplify" them)
--------------------------------------------------------------------------------
* ``--json`` is a GLOBAL flag on the roadmap CLI and must precede the subcommand;
  the text output is parsed here instead because it is the stable surface.
* buildkit CLIs print PGlite banners and ``co:`` lines on STDOUT, interleaved with
  real output — they are filtered per-line, never by piping through ``grep`` (a
  pipe would report the FILTER's exit status, not the command's).
* ``BUILDKIT_ENGINE_OVERRIDE`` is cleared: an ambient engine older than the pin is
  refused for writes and can differ in verbs.
* ``PYTHONIOENCODING=utf-8`` is forced: cp1252 mojibake has silently zeroed a
  parser on this fleet before.
* The ambient ``buildkit_cli`` has been replaced mid-session by a copy missing
  sub-packages, so ``--roadmap-cmd`` lets a caller point at the pinned exe.

OWNERSHIP INPUT
---------------
Ownership lives outside the roadmap catalog today (the allocation record carries
no feature/repo/host field — that is roadmap item ``namespace-feature-numbers-
per-lane``). Until it does, ownership is supplied here and SHOULD be kept in
``.specify/roadmap-owners.json`` so every lane reads the same file:

    {"roadmap-cli-spec063-fleet-upgrade-rollout": "gavriella"}
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path

# state -> sort rank. Anything unknown sorts last but is still SHOWN, never dropped.
# Ordering is part of the standard: two hosts must render the same catalog identically.
# `released` was surfaced by ariellas' catalog after this shipped — it sorted last only by
# accident (unknown states fall through to rank 9). Naming it makes that deliberate.
STATE_RANK = {"specified": 0, "promoted": 1, "captured": 2, "released": 3}
UNSCORED = "—"
OWNERS_FILE = ".specify/roadmap-owners.json"

# `[state] #slot slug WSJF=x RICE=y — title [flags]`
ROW_RE = re.compile(
    r"^\s+\[(?P<state>\w+)\s*\]\s+#(?P<slot>\S+)\s+(?P<slug>\S+)\s+"
    r"WSJF=(?P<wsjf>\S+)\s+RICE=(?P<rice>\S+)\s+[—-]\s*(?P<rest>.*)$"
)
EPIC_RE = re.compile(r"^Epic:\s+(?P<name>.*?)\s*\((?P<id>.*?)\)\s*$")
NOISE = ("PGlite", "co:")


def _clean(line: str) -> bool:
    """True when the line is real output rather than a CLI banner."""
    s = line.lstrip()
    return not (s.startswith(NOISE[1]) or NOISE[0] in line)


def roadmap_status(cmd: list[str], cwd: Path) -> str:
    env = dict(os.environ)
    env["PYTHONIOENCODING"] = "utf-8"
    env.pop("BUILDKIT_ENGINE_OVERRIDE", None)  # never let ambient beat the pin
    proc = subprocess.run(
        cmd + ["status"], cwd=str(cwd), env=env,
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    if proc.returncode != 0 and not proc.stdout.strip():
        sys.stderr.write(proc.stderr or "roadmap status failed\n")
        raise SystemExit(1)
    return proc.stdout


def parse(text: str) -> list[dict]:
    epic, rows = None, []
    for line in text.splitlines():
        if not _clean(line):
            continue
        m = EPIC_RE.match(line.strip())
        if m:
            epic = m.group("name")
            continue
        m = ROW_RE.match(line.rstrip())
        if not m:
            continue
        if m.group("state") == "closed":
            continue
        rows.append({
            "epic": epic or "(no epic)",
            "state": m.group("state"),
            "slot": m.group("slot"),
            "slug": m.group("slug"),
            "wsjf": m.group("wsjf"),
            "rice": m.group("rice"),
        })
    return rows


def _num(x: str) -> float:
    try:
        return float(x)
    except (TypeError, ValueError):
        return -1.0          # unscored sorts last WITHIN its state, never dropped


def _cell(x: str) -> str:
    return UNSCORED if _num(x) < 0 else x


def load_owners(root: Path, extra: str | None, files: list) -> dict:
    """slug -> {host, ...}.

    A SET, not a string, and that is load-bearing. The incident this standard
    exists to catch is ONE FEATURE CLAIMED BY TWO HOSTS — but a slug appears
    exactly once per catalog, so comparing rows inside a single host's table can
    NEVER detect it. The claims have to be merged across hosts first. Point
    ``--owners-file`` at each lane's file (they belong in the coop channel) and a
    contested slug arrives with two hosts in its set.
    """
    owners: dict = {}

    def _absorb(mapping):
        for slug, host in mapping.items():
            hosts = host if isinstance(host, (list, tuple, set)) else [host]
            for h in hosts:
                owners.setdefault(slug, set()).add(str(h))

    default = root / OWNERS_FILE
    for cand in [default] + [Path(f) for f in files]:
        if not cand.is_file():
            if cand != default:
                sys.stderr.write("warning: owners file not found: %s\n" % cand)
            continue
        try:
            _absorb(json.loads(cand.read_text(encoding="utf-8")))
        except (OSError, ValueError) as exc:      # a broken file must not hide the table
            sys.stderr.write("warning: could not read %s (%s)\n" % (cand, exc))
    if extra:
        for pair in extra.split(","):
            if "=" in pair:
                slug, host = pair.split("=", 1)
                owners.setdefault(slug.strip(), set()).add(host.strip())
    return owners


def sort_rows(rows, owners):
    for r in rows:
        hosts = sorted(owners.get(r["slug"], ()))
        r["owners"] = hosts
        # A contested feature renders BOTH hosts, flagged. It is never silently
        # collapsed to one — the entire point is that the reader sees the clash.
        r["owner"] = UNSCORED if not hosts else (
            hosts[0] if len(hosts) == 1 else "** " + " / ".join(hosts) + " **")
    rows.sort(key=lambda r: (STATE_RANK.get(r["state"], 9), -_num(r["wsjf"]), r["slug"]))
    return rows


def duplicates(rows) -> list:
    """One feature claimed by two or more hosts — the incident --check gates on."""
    return [(r["slug"], r["owners"]) for r in rows if len(r.get("owners") or ()) > 1]


def totals(rows) -> str:
    c = {}
    for r in rows:
        c[r["state"]] = c.get(r["state"], 0) + 1
    parts = " · ".join("%d %s" % (c[s], s) for s in sorted(c, key=lambda s: STATE_RANK.get(s, 9)))
    return "%d not-closed = %s, across %d epics" % (
        len(rows), parts, len({r["epic"] for r in rows}))


def render(rows, fmt: str, width: int) -> str:
    if fmt == "json":
        return json.dumps({"rows": rows, "totals": totals(rows)}, indent=2)
    cut = (lambda s: s if len(s) <= width else s[: width - 1] + "…") if width else (lambda s: s)
    if fmt == "tsv":
        out = ["#\tState\tSlot\tFeature\tWSJF\tRICE\tOwner\tEpic"]
        out += ["%d\t%s\t%s\t%s\t%s\t%s\t%s\t%s" % (
            i, r["state"], r["slot"], r["slug"], _cell(r["wsjf"]), _cell(r["rice"]),
            r["owner"], r["epic"]) for i, r in enumerate(rows, 1)]
        out.append("")
        out.append(totals(rows))
        return "\n".join(out)
    out = ["| # | State | Slot | Feature (slug) | WSJF | RICE | Owner | Epic |",
           "|---:|:---|---:|:---|---:|---:|:---|:---|"]
    out += ["| %d | %s | %s | `%s` | %s | %s | %s | %s |" % (
        i, r["state"], r["slot"], cut(r["slug"]), _cell(r["wsjf"]), _cell(r["rice"]),
        r["owner"], cut(r["epic"])) for i, r in enumerate(rows, 1)]
    out.append("")
    out.append("**%s**" % totals(rows))
    return "\n".join(out)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Fleet-standard NOT-CLOSED roadmap table.")
    ap.add_argument("--repo", default=".", help="repo root (default: cwd)")
    ap.add_argument("--format", choices=("md", "tsv", "json"), default="md")
    ap.add_argument("--width", type=int, default=54,
                    help="truncate slug/epic to N chars (0 = never truncate)")
    ap.add_argument("--owner", default=None,
                    help="inline owners, 'slug=host,slug=host' (merged over %s)" % OWNERS_FILE)
    ap.add_argument("--owners-file", action="append", default=[],
                    help="extra owners JSON, repeatable — point at EACH lane's file so a "
                         "feature claimed by two hosts is actually detectable")
    ap.add_argument("--check", action="store_true",
                    help="exit 2 if any feature is claimed by two different hosts")
    ap.add_argument("--roadmap-cmd", default=None,
                    help="override the roadmap CLI, e.g. a pinned buildkit-roadmap.exe")
    args = ap.parse_args(argv)

    root = Path(args.repo).resolve()
    cmd = ([args.roadmap_cmd] if args.roadmap_cmd
           else [sys.executable, "-m", "buildkit_cli.roadmap"])

    rows = sort_rows(parse(roadmap_status(cmd, root)),
                     load_owners(root, args.owner, args.owners_file))
    print(render(rows, args.format, args.width))

    dupes = duplicates(rows)
    if dupes:
        sys.stderr.write("\nDUPLICATE ALLOCATION — one feature, two hosts:\n")
        for slug, hosts in dupes:
            sys.stderr.write("  %s -> %s\n" % (slug, ", ".join(hosts)))
        if args.check:
            return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
