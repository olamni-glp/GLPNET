# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""
Union-merge the FTAP signature ledger across every coop leg. Nothing is ever overwritten.

WHY (measured 2026-09-06T23:35Z, and the first victim was this lane's own signature)
    @shiras-hatzinor raised a P0: "I signed BK-FTAP-2 and read a different document at the same
    canonical name - the unversioned path forks across trees."

    Re-measured here, and the finding needs SPLITTING, because the two halves have different fixes:

        FTAP-2026-09-06-PLAN.md   ce105926978cb107  571 lines  IDENTICAL on all four legs
        ftap.crdt.json            6f62428d / c4a10c02          FORKED

    So the PLAN has not forked. The SIGNATURE LEDGER has. And it forks for a dull reason: a lane
    signs by writing to the ONE leg it can see, and nothing fans that write to the others. Every
    lane then reads a different tally and believes a different quorum.

    This lane caused an instance of it fifteen minutes after praising the mechanism: I wrote eight
    entries into the gavri leg's ledger and to the other three legs my signature does not exist.

WHAT IT DOES
    Reads every leg, unions actors and their entries, writes the union back to every leg.
    - Union is per ACTOR and then per ENTRY HASH. Two legs holding different entries for one actor
      keep BOTH. No last-writer-wins anywhere -- the property the ledger format already promises
      and that copying-by-hand silently breaks.
    - Never deletes an actor, never edits another actor's entry, never reorders.
    - Writes atomically (temp + replace) so no leg is ever half-written, and keeps one .bak per leg.
    - --dry-run by default. You must pass --apply to write.

WHAT IT DELIBERATELY DOES NOT DO
    It does not touch the PLAN. The plan is identical across legs; re-copying it would risk
    creating the very fork this is cleaning up.

USAGE
    python3 scripts/ftap_ledger_merge.py            # report the divergence
    python3 scripts/ftap_ledger_merge.py --apply    # converge every leg
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys

LEGS = ["/mnt/gavri/d/coop", "/mnt/biwin/D_DRIVE/coop",
        "/mnt/ariellas/d/coop", "/mnt/olamnit/d/coop"]
LEDGER = "ftap.crdt.json"


def _entry_key(entry: dict) -> str:
    """Identity of an entry: its declared hash, else a hash of its content."""
    if entry.get("hash"):
        return f"h:{entry['hash']}"
    blob = json.dumps(entry, sort_keys=True, ensure_ascii=False)
    return "c:" + hashlib.sha256(blob.encode("utf-8")).hexdigest()[:16]


def _load(path: str):
    try:
        with open(path, "r", encoding="utf-8") as handle:
            return json.load(handle), None
    except FileNotFoundError:
        return None, "absent"
    except (OSError, ValueError) as exc:
        return None, str(exc)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--leg", action="append", default=None)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    legs = args.leg or LEGS

    docs, errors = {}, []
    for leg in legs:
        path = os.path.join(leg, LEDGER)
        doc, err = _load(path)
        if err:
            errors.append(f"{path}: {err}")
            continue
        docs[path] = doc

    if not docs:
        for e in errors:
            print(f"ftap_ledger_merge: REFUSED - {e}", file=sys.stderr)
        return 2

    # Build the union.
    union: dict[str, dict[str, dict]] = {}
    for path, doc in docs.items():
        for actor, blob in (doc.get("actors") or {}).items():
            slot = union.setdefault(actor, {})
            for entry in blob.get("entries", []):
                slot.setdefault(_entry_key(entry), entry)

    print(f"{'leg':<34} {'sha':<18} {'actors':>7} {'entries':>8}")
    for path, doc in docs.items():
        actors = doc.get("actors") or {}
        n = sum(len(b.get("entries", [])) for b in actors.values())
        with open(path, "rb") as handle:
            sha = hashlib.sha256(handle.read()).hexdigest()[:16]
        print(f"{os.path.dirname(path):<34} {sha:<18} {len(actors):>7} {n:>8}")

    total = sum(len(v) for v in union.values())
    print(f"\nUNION: {len(union)} actor(s), {total} distinct entr(ies)")
    for actor, entries in sorted(union.items()):
        missing = [os.path.dirname(p) for p, doc in docs.items()
                   if actor not in (doc.get("actors") or {})]
        flag = f"  MISSING FROM {len(missing)} leg(s)" if missing else ""
        print(f"  {actor:<28} {len(entries):>3} entr(ies){flag}")
    for e in errors:
        print(f"⚠ unread leg (NOT evidence of absence): {e}")

    if not args.apply:
        print("\n(dry run - pass --apply to converge every leg)")
        return 0

    for path, doc in docs.items():
        merged = dict(doc)
        merged["actors"] = {a: {"entries": list(e.values())} for a, e in sorted(union.items())}
        shutil.copy2(path, path + ".bak-ledger-merge")
        tmp = path + ".tmp-ledger-merge"
        with open(tmp, "w", encoding="utf-8") as handle:
            json.dump(merged, handle, indent=2, ensure_ascii=False)
            handle.write("\n")
        os.replace(tmp, path)
        print(f"converged {path}")

    shas = set()
    for path in docs:
        with open(path, "rb") as handle:
            shas.add(hashlib.sha256(handle.read()).hexdigest())
    print(f"\npost-merge distinct ledger contents: {len(shas)} (1 == converged)")
    return 0 if len(shas) == 1 else 1


if __name__ == "__main__":
    sys.exit(main())
