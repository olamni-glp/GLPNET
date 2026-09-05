#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Fan a COOP message out to the root plus every lane/host channel.

Why this exists
---------------
Broadcasts were being fanned out by hand, one `cp` per channel. That is slow, it
silently misses channels added since the last fan-out, and on 2026-08-16 one
hand-written fan-out **overwrote** `status-ariellas.md` in fourteen channels and
lost 2990 lines (`/d/coop/qhstate/URGENT-20260816T134500Z-...`).

Two rules follow from that incident and are enforced here, not documented:

1. **Never overwrite.** A destination that already exists is REFUSED and counted,
   never clobbered. Use `--force` only to repair a partial fan-out, and even then
   the pre-existing bytes are compared first: identical content is a no-op.
2. **Channels are enumerated, never listed by hand.** Any directory that appears
   under the COOP root is a channel unless it is explicitly an infrastructure
   directory (leading `_`) or a URL-encoded per-node scratch channel.

Every message is written with its REUSE `.license` sidecar, because the fleet
licence gate rejects a bare `.md` (root-caused 2026-08-28).

Usage
-----
    python scripts/coop_broadcast.py <message.md> [--root /d/coop] [--dry-run]
                                     [--also-root] [--force]

The basename of <message.md> is used verbatim as the destination filename, so it
must already carry the fleet naming convention:
    <KIND>-<YYYYMMDD>T<HHMM>Z-<host>-<lane>-<SUBJECT-IN-CAPS>.md
"""
from __future__ import annotations

import argparse
import pathlib
import sys

LICENSE_SIDECAR = (
    "SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, "
    "The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK\n"
    "\n"
    "SPDX-License-Identifier: MIT\n"
)

# Infrastructure directories under the COOP root that are stores, not channels.
# They are skipped by name so that adding a new *channel* needs no code change,
# while adding a new *store* does — the safe direction for a fan-out.
NON_CHANNEL_EXACT = {
    "_archive",
    "_roadmap-exports",
    "_snapshots",
    "_takt-lake",
    "_takt-presence-backup",
    "_takt-repair-backup",
    "_trust",
    "_ynet-board",
    ".specify",
    ".git",
}


def is_channel(d: pathlib.Path) -> bool:
    name = d.name
    if not d.is_dir():
        return False
    if name in NON_CHANNEL_EXACT:
        return False
    if name.startswith("_"):
        return False
    # URL-encoded per-node scratch channels (e.g. "GAVRIELLA%2Fgavriella%2Eprobe~90f7")
    # are node-private working areas, not addressable lane channels.
    if "%2F" in name or "%2E" in name or "~" in name:
        return False
    return True


# Windows refuses a path over MAX_PATH unless long-path support is enabled for
# the process; a long descriptive fleet filename plus a deep channel directory
# crosses it easily. The budget leaves headroom for the ".license" sidecar, so we
# never write the .md and then fail on its licence -- an unlicensed .md is
# REJECTED by the fleet licence gate, which makes a half-written pair worse than
# no write at all (that gate's failure mode was root-caused 2026-08-28).
_PATH_LIMIT = 259
_SIDECAR_SUFFIX = ".license"


def check_path_budget(targets, name):
    """Return a list of complaints; empty means every destination pair will fit."""
    problems = []
    for d in targets:
        longest = len(str(d / name)) + len(_SIDECAR_SUFFIX)
        if longest > _PATH_LIMIT:
            problems.append(
                f"{longest} chars (limit {_PATH_LIMIT}) for {d / name}{_SIDECAR_SUFFIX}"
            )
    return problems


def write_pair(dest_md: pathlib.Path, body: str, force: bool) -> str:
    """Return one of: written | identical | REFUSED-exists.

    The licence sidecar is written FIRST. If the second write then fails, we are
    left with a stray .license and no .md -- inert -- rather than an .md the
    licence gate will reject. Ordering is the cheapest atomicity available here.
    """
    if dest_md.exists():
        existing = dest_md.read_text(encoding="utf-8")
        if existing == body:
            return "identical"
        if not force:
            return "REFUSED-exists"
    dest_md.with_suffix(dest_md.suffix + _SIDECAR_SUFFIX).write_text(
        LICENSE_SIDECAR, encoding="utf-8", newline="\n"
    )
    dest_md.write_text(body, encoding="utf-8", newline="\n")
    return "written"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("message", type=pathlib.Path)
    ap.add_argument("--root", type=pathlib.Path, default=pathlib.Path("/d/coop"))
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument(
        "--also-root",
        action="store_true",
        help="also place a copy at the COOP root (the fleet-wide noticeboard)",
    )
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()

    if not args.message.is_file():
        print(f"coop_broadcast: no such message: {args.message}", file=sys.stderr)
        return 2
    if not args.root.is_dir():
        print(f"coop_broadcast: no such COOP root: {args.root}", file=sys.stderr)
        return 2

    body = args.message.read_text(encoding="utf-8")
    name = args.message.name

    targets = sorted(d for d in args.root.iterdir() if is_channel(d))
    if args.also_root:
        targets.insert(0, args.root)

    # Refuse the WHOLE fan-out before writing anything, rather than discovering
    # the path limit part-way through and leaving the message in some channels
    # only. A partial fan-out is invisible: the lanes that got it see nothing
    # wrong, and the ones that did not have nothing to notice.
    problems = check_path_budget(targets, name)
    if problems:
        print(
            f"coop_broadcast: REFUSED — {len(problems)} destination(s) exceed the "
            f"path budget. Shorten the filename; NOTHING was written.",
            file=sys.stderr,
        )
        for pr in problems[:3]:
            print(f"  {pr}", file=sys.stderr)
        return 2

    tally: dict[str, int] = {}
    for d in targets:
        dest = d / name
        if args.dry_run:
            outcome = "would-write" if not dest.exists() else "REFUSED-exists"
        else:
            outcome = write_pair(dest, body, args.force)
        tally[outcome] = tally.get(outcome, 0) + 1
        if outcome.startswith("REFUSED"):
            print(f"  REFUSED (exists, not overwritten): {dest}")

    print(f"coop_broadcast: {name}")
    print(f"  channels considered: {len(targets)}")
    for k in sorted(tally):
        print(f"  {k}: {tally[k]}")
    # A fan-out that wrote nothing is a failure, not a success.
    if not args.dry_run and tally.get("written", 0) == 0:
        print("coop_broadcast: NOTHING WAS WRITTEN", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
