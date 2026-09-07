#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Is this host's elector reading a board that agrees with the shared one? Refuses to guess.

WHY THIS EXISTS
---------------
Measured on SHIRAS 2026-09-07T10:03Z, and independently on OLAMNIT minutes later: the elector
daemon resolves its board to a PER-USER root, and that replica was short exactly ONE prepare
record -- ``broker@gavris -> broker@ariellas`` at 09:44:26Z. That single missing row takes the
winning candidate from 6 prepares to 5, i.e. from *quorum met* to *quorum not met*.

The daemon's arithmetic was never wrong. Its INPUT was one marginal record short, so it
correctly found no quorum, correctly fell back to the previous term, correctly reported
``leader: None`` -- and was wrong about the fleet. Two hosts reached opposite conclusions from
the same election while both were internally consistent.

That is the F-1 generator: **if each host folds a different subset of ballots, hosts disagree
about who leads forever, and every host is certain and correct on its own evidence.** Worse, a
short replica makes a host want to open a NEW term on an already-decided one, so the defect
manufactures the very churn the liveness work exists to prevent.

WHAT THIS TOOL REFUSES TO DO
----------------------------
It never reports "in sync" because it could not look. If the shared board is unreachable, or
the daemon does not answer, it exits 2 (UNVERIFIABLE) -- never 0. `resolve_board_root()` already
documents this failure class from ARIELLAS on 2026-09-05, and Q-ARI-YNETROOT-01 already ruled
the shared root correct; a lane still needs a way to find out that its own host never got the memo.

EXIT CODES
----------
0 = the daemon's board agrees with the shared board on the election outcome
1 = DIVERGENT -- they disagree, and the missing/extra records are listed
2 = UNVERIFIABLE -- could not read one of the boards, or could not resolve the daemon's root
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

DEFAULT_SHARED = "/mnt/gavri/d/coop/ynet"
MEMBERSHIP = [
    "broker@ariellas", "broker@gavris", "broker@olamnit", "broker@shiras",
    "guardian@ariellas", "guardian@gavris", "guardian@olamnit", "guardian@shiras",
]


def load_pbft(root: str) -> list[dict]:
    """Every pbft record under ``root``. Raises rather than returning a misleading empty list."""
    base = Path(root) / "pbft"
    if not base.is_dir():
        raise FileNotFoundError(f"no pbft stream under {root} — UNVERIFIABLE, not empty")
    out: list[dict] = []
    for path in sorted(base.rglob("*")):
        if path.suffix not in (".jsonl", ".json") or not path.is_file():
            continue
        for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
            line = line.strip()
            if not line:
                continue
            try:
                rec = json.loads(line)
            except json.JSONDecodeError:
                continue  # a malformed line is not a missing record; the fold reports counts
            if isinstance(rec, dict):
                out.append(rec)
    return out


def daemon_root(ynetd: str) -> str:
    """The root the DAEMON actually resolved — not the one we assume it used."""
    proc = subprocess.run([sys.executable, ynetd, "board"], capture_output=True, text=True, timeout=120)
    if proc.returncode != 0:
        raise RuntimeError(f"ynetd board failed rc={proc.returncode}: {proc.stderr.strip()[:200]}")
    root = json.loads(proc.stdout).get("root")
    if not root:
        raise RuntimeError("ynetd board returned no 'root' field — cannot tell which board answered")
    return root


def key(rec: dict) -> tuple:
    return (rec.get("ts"), rec.get("actor"), rec.get("kind"), rec.get("for"), rec.get("term"))


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--shared", default=DEFAULT_SHARED)
    ap.add_argument("--ynetd", required=True, help="path to ynetd.py")
    ap.add_argument("--core", required=True, help="dir containing ynet_core.py")
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args(argv)

    sys.path.insert(0, args.core)
    try:
        import ynet_core  # noqa: PLC0415 - located at runtime, not import time
    except ImportError as exc:
        print(f"REFUSED: cannot import ynet_core from {args.core}: {exc}", file=sys.stderr)
        return 2

    try:
        local_root = daemon_root(args.ynetd)
        shared = load_pbft(args.shared)
        local = load_pbft(local_root)
    except Exception as exc:  # noqa: BLE001 - any failure here is UNVERIFIABLE, never "in sync"
        print(f"REFUSED (UNVERIFIABLE): {type(exc).__name__}: {exc}", file=sys.stderr)
        return 2

    import datetime
    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    ds = ynet_core.decide_pbft(shared, MEMBERSHIP, at=now, require_signatures=False)
    dl = ynet_core.decide_pbft(local, MEMBERSHIP, at=now, require_signatures=False)

    missing = sorted({key(r) for r in shared} - {key(r) for r in local})
    extra = sorted({key(r) for r in local} - {key(r) for r in shared})
    agree = (ds.get("outcome"), ds.get("term"), ds.get("leader")) == \
            (dl.get("outcome"), dl.get("term"), dl.get("leader"))

    report = {
        "at": now, "shared_root": args.shared, "daemon_root": local_root,
        "daemon_root_is_per_user": not str(local_root).startswith("/mnt"),
        "shared": {k: ds.get(k) for k in ("outcome", "term", "leader", "prepares", "quorum_needed")},
        "daemon": {k: dl.get(k) for k in ("outcome", "term", "leader", "prepares", "quorum_needed")},
        "records": {"shared": len(shared), "daemon": len(local)},
        "missing_from_daemon": [list(m) for m in missing],
        "extra_in_daemon": [list(e) for e in extra],
        "agree": agree,
    }
    if args.json:
        print(json.dumps(report, indent=2))
    else:
        print(f"ynet board divergence · {now}")
        print(f"  shared root  {args.shared}   records {len(shared)}")
        print(f"  daemon root  {local_root}   records {len(local)}")
        if report["daemon_root_is_per_user"]:
            print("  🔴 THE DAEMON IS READING A PER-USER BOARD, NOT THE SHARED ONE")
        print(f"  shared says  {ds.get('outcome')} term={ds.get('term')} leader={ds.get('leader')} "
              f"{ds.get('prepares')}/{ds.get('quorum_needed')}")
        print(f"  daemon says  {dl.get('outcome')} term={dl.get('term')} leader={dl.get('leader')} "
              f"{dl.get('prepares')}/{dl.get('quorum_needed')}")
        if missing:
            print(f"  🔴 {len(missing)} record(s) ON SHARED BUT MISSING FROM THE DAEMON'S BOARD:")
            for m in missing[:20]:
                print(f"      {m}")
        if extra:
            print(f"  ⚠ {len(extra)} record(s) in the daemon's board but not on shared:")
            for e in extra[:20]:
                print(f"      {e}")
        print(f"  VERDICT: {'AGREE' if agree else '🔴 DIVERGENT — do not quote `elect` on this host'}")
    return 0 if agree else 1


if __name__ == "__main__":
    raise SystemExit(main())
