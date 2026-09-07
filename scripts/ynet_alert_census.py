#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Count YNET /btw alerts for a lane, and REFUSE rather than report a false clean.

WHY THIS EXISTS
---------------
Measured on SHIRAS 2026-09-07T07:18Z, after a correction from @gavriella.qhstate: the alert
spool path used by every ad-hoc census in this estate -- ``.specify/ynet/<lane>/alerts`` -- is
**CWD-RELATIVE**. Run the same census from ``/tmp`` and the directory does not resolve, so a
naive counter reports ``0 alerts, 0 unacked`` and the operator reads a FALSE CLEAN.

That is the C-20 error in its most dangerous form: "I could not look" rendered as "there is
nothing there", in the one number a restart gate depends on. My own 0-unacked claims this
session happened to be run from the repo root and were true in fact, but the METHOD was unsound
and would have lied silently from any other directory.

THE RULE THIS ENFORCES
----------------------
A census that cannot find its spool MUST refuse loudly and exit non-zero. It must never emit a
zero that is indistinguishable from a genuine zero. ``unverifiable`` and ``clean`` are different
answers and this script never conflates them (C-20: report pass | refuse | unverifiable).
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

#: The repo root is derived from THIS FILE, never from the caller's cwd -- that is the whole fix.
REPO_ROOT = Path(__file__).resolve().parent.parent


def spool_for(lane: str) -> Path:
    return REPO_ROOT / ".specify" / "ynet" / lane / "alerts"


def census(lane: str) -> dict:
    """``{total, acked, unacked, signals}`` -- or raise FileNotFoundError. Never a silent zero."""
    spool = spool_for(lane)
    if not spool.is_dir():
        raise FileNotFoundError(
            f"alert spool does not resolve: {spool}\n"
            f"  cwd={os.getcwd()}\n"
            f"  This is UNVERIFIABLE, not zero. Refusing to report a clean mailbox."
        )
    total = acked = 0
    unacked: list[str] = []
    for entry in sorted(spool.iterdir()):
        if entry.suffix != ".json":
            continue
        total += 1
        try:
            alert = json.loads(entry.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            # An unreadable alert is NOT an acked alert. Surface it as outstanding.
            unacked.append(f"<unreadable {entry.name}: {type(exc).__name__}>")
            continue
        if alert.get("acknowledged"):
            acked += 1
        else:
            unacked.append(str(alert.get("signal") or entry.name))
    return {"lane": lane, "spool": str(spool), "total": total,
            "acked": acked, "unacked": len(unacked), "signals": unacked}


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--lane", default="shiras-glpnet")
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args(argv)
    try:
        result = census(args.lane)
    except FileNotFoundError as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2  # 2 = unverifiable, distinct from 1 = outstanding alerts
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(f"lane={result['lane']} total={result['total']} "
              f"acked={result['acked']} unacked={result['unacked']}")
        for sig in result["signals"]:
            print(f"  UNACKED: {sig}")
    return 1 if result["unacked"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
