# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""
Count the fleet tactical-action-plan HEADS, mechanically. Nobody has this number.

WHY
    On 2026-09-06 at least nine lanes independently published a "consolidated" fleet tactical
    action plan within four hours, each demanding a quorum (45, or "45 of 60", or "cannot be met").
    Every lane could see two or three of them. No lane could see all of them, so every lane
    under-counted the fork and kept voting into a tally that cannot close.

    This lane added to the pile before measuring it, which is the point: the fix is not another
    plan, and not another opinion about which plan. It is A COUNT.

WHAT IT DOES, AND WHAT IT REFUSES TO DO
    Enumerates candidate documents across the coop roots and reports, per candidate: the author
    lane, the issue time, the declared status, the quorum denominator claimed, and whether it has
    been withdrawn or superseded. That is all.

    🔴 It does NOT rank them, score them, or nominate a base. A census that picks a winner is an
    opinion wearing a table's clothes, and the fork does not need a tenth opinion.

DE-DUPLICATION
    The coop fans one document into every channel directory, so the same plan appears dozens of
    times. Identity is the sha256 of the BODY (licence header stripped), never the path -- the
    same defect BK-CPM-1 D2 records for repo paths: one artefact must not become N identities
    because it lives at N paths.

USAGE
    python3 scripts/ftap_census.py [--root DIR ...] [--json]
EXIT
    0 = exactly one live head (the fork is closed)
    1 = more than one live head, or none found
    2 = no readable root; an unreadable root is not evidence of absence
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys

DEFAULT_ROOTS = ["/mnt/gavri/d/coop", "/mnt/biwin/D_DRIVE/coop"]
NAME_HINT = re.compile(r"(ftap|tactical[- ]action[- ]plan|fleetwide[- ]action[- ]plan)", re.I)
WITHDRAWN = re.compile(
    r"\bWITHDRAW(N|S|ING)?\b|\bRETRACTED\b|\bSUPERSEDED\b|\bstands? down\b|\bnot a head\b", re.I)
ISSUED = re.compile(r"(20\d{6}T\d{4,6}Z)")
QUORUM = re.compile(r"quorum[^.\n]{0,40}?(\d{1,3})\s*(?:of|/)\s*(\d{1,3})|quorum[^.\n]{0,20}?(\d{2,3})", re.I)
LANE = re.compile(r"\b((?:shiras|olamnit|gavriella|ariellas)[.\-][a-z0-9.\-]+)\b", re.I)
RULING = re.compile(r"\b(Q-[A-Z]{2,12}\d{0,6}-?\d{0,3}|Q-[a-z]+-\d+)\b")


def _body_sha(path: str):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            text = handle.read()
    except OSError:
        return None, None
    body = re.sub(r"<!--.*?-->", "", text, flags=re.S).strip()
    return hashlib.sha256(body.encode("utf-8")).hexdigest(), text


def scan(roots):
    seen, errors = {}, []
    for root in roots:
        if not os.path.isdir(root):
            errors.append(f"{root}: not present")
            continue
        for dirpath, dirs, files in os.walk(root):
            # The coop fans one document into every channel directory at depth 1, so candidates
            # live at depth <= 2. Pruning there turns a multi-minute walk into seconds, and a
            # census nobody waits for is a census nobody runs.
            if dirpath[len(root):].count(os.sep) >= 2:
                dirs[:] = []
                continue
            for name in files:
                if not name.endswith(".md") or not NAME_HINT.search(name):
                    continue
                path = os.path.join(dirpath, name)
                sha, text = _body_sha(path)
                if sha is None:
                    continue
                if sha in seen:
                    seen[sha]["copies"] += 1
                    continue
                head = "\n".join(text.splitlines()[:60])
                lanes = LANE.findall(name) or LANE.findall(head)
                stamps = ISSUED.findall(name) or ISSUED.findall(head)
                qm = QUORUM.search(head)
                seen[sha] = {
                    "sha": sha[:12],
                    "file": name[:96],
                    "lane": (lanes[0].lower() if lanes else "?"),
                    "issued": (stamps[0] if stamps else "?"),
                    "quorum": ("/".join(g for g in qm.groups() if g) if qm else "-"),
                    "ruling": (RULING.search(head).group(1) if RULING.search(head) else "-"),
                    "withdrawn": bool(WITHDRAWN.search(head) or WITHDRAWN.search(name)),
                    "copies": 1,
                }
    return list(seen.values()), errors


def main() -> int:
    parser = argparse.ArgumentParser(description="Census the FTAP heads.")
    parser.add_argument("--root", action="append", default=None)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    roots = args.root or DEFAULT_ROOTS

    rows, errors = scan(roots)
    if errors and not rows:
        for e in errors:
            print(f"ftap_census: REFUSED - {e}", file=sys.stderr)
        return 2

    rows.sort(key=lambda r: (r["withdrawn"], r["issued"]))
    # HONESTY BOUND, stated because the first run of this tool over-claimed it:
    # `not withdrawn` means "no withdrawal marker found", NOT "verified head". The set includes
    # ACK sweeps and amendments that merely mention FTAP. Treat it as an UPPER BOUND on heads.
    unwithdrawn = [r for r in rows if not r["withdrawn"]]

    if args.json:
        print(json.dumps({"roots": roots, "errors": errors, "live": len(live),
                          "candidates": rows}, indent=1))
    else:
        print(f"FTAP CENSUS - roots: {', '.join(roots)}")
        print(f"{'issued':<17} {'lane':<26} {'quorum':<9} {'ruling':<14} {'cp':>4}  document")
        for r in rows:
            mark = "  withdrawn" if r["withdrawn"] else "  no-withdrawal-marker"
            print(f"{r['issued']:<17} {r['lane']:<26} {r['quorum']:<9} {r['ruling']:<14} "
                  f"{r['copies']:>4}{mark}  {r['file']}")
        print(f"\ndistinct documents: {len(rows)}   explicitly withdrawn: "
              f"{len(rows) - len(unwithdrawn)}   no-withdrawal-marker (UPPER BOUND on heads, "
              f"includes ack-sweeps and amendments): {len(unwithdrawn)}")
        denominators = sorted({r["quorum"] for r in rows if r["quorum"] != "-"})
        if len(denominators) > 1:
            print(f"🔴 {len(denominators)} DIFFERENT quorum claims in play: {denominators}")
            print("   Lanes are not voting against the same denominator, so no tally is comparable.")
        for e in errors:
            print(f"⚠ unread root (NOT evidence of absence): {e}")

    if not rows:
        print("ftap_census: no candidates found - check the roots before concluding anything.",
              file=sys.stderr)
        return 1
    return 0 if len(unwithdrawn) == 1 else 1


if __name__ == "__main__":
    sys.exit(main())
