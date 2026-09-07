#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Conflict-free Feature Requirements document: append-only CRDT, derived head, preserved dissent.

WHY A CRDT AND NOT A MARKDOWN FILE
----------------------------------
OB-8 is the reason. The ruled FTAP template forked FOUR WAYS UNDER ONE FILENAME -- 23,718 to
40,636 bytes, four distinct contents, same claimed authority -- and no lane could tell which one
it had opened. A versioned fork announces itself; that one was invisible. Any requirements
document that lanes edit in place will do the same thing, because a shared filesystem plus
concurrent editors IS a fork generator.

So requirements here are not a file anyone edits. They are an APPEND-ONLY LOG OF RECORDS, one
stream per actor, and the document is DERIVED. Two lanes writing at once cannot corrupt each
other because neither ever rewrites a byte the other wrote.

THE MERGE RULE, AND THE ONE THING IT REFUSES TO DO
-------------------------------------------------
Grow-only union, grouped by clause id, ordered deterministically. When two actors assert
DIFFERENT text for the SAME clause, that is a CONFLICT and it is REPORTED, never resolved.

Last-write-wins is banned here on purpose. The estate's own ratification clause says it:
*"Dissent is a first-class value: conflicting revisions of one clause from different actors are
kept and reported as a conflict, never resolved by last-write-wins."* A merge that silently
picks the later timestamp destroys exactly the signal an engineer needs to rule, and it does so
invisibly -- which is how the fleet got a ~44-way versioned fork nobody could see.

RECORD SHAPE
------------
    {"actor":"<lane>@<HOST>", "ts":"<UTC>", "op":"assert|contest|second",
     "clause":"<stable id>", "text":"...", "evidence":"...", "supersedes":"<null|record_id>"}

``op`` semantics:
  assert   -- this clause should read as ``text``
  second   -- I independently agree with an existing assertion (corroboration, not a duplicate)
  contest  -- I disagree, and ``evidence`` MUST carry a measurement with hostname + timestamp
              (C-20: a contest without evidence is a preference, not a contest)
"""
from __future__ import annotations

import argparse
import json
import sys
from collections import defaultdict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_LOG = REPO_ROOT / "docs" / "fleet" / "crdt" / "ynet-coordinator-requirements.crdt.jsonl"


def read(log: Path) -> list[dict]:
    if not log.is_file():
        raise FileNotFoundError(f"no CRDT log at {log} — UNVERIFIABLE, not empty")
    out = []
    for n, line in enumerate(log.read_text(encoding="utf-8").splitlines(), 1):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        try:
            rec = json.loads(line)
        except json.JSONDecodeError as exc:
            # A corrupt line is surfaced, never skipped silently -- skipping is how a fork hides.
            out.append({"_malformed": True, "_line": n, "_error": str(exc)})
            continue
        out.append(rec)
    return out


def fold(records: list[dict]) -> dict:
    """Deterministic grow-only fold. Conflicts are OUTPUT, not decided."""
    by_clause: dict[str, list[dict]] = defaultdict(list)
    malformed = [r for r in records if r.get("_malformed")]
    superseded = {r["supersedes"] for r in records if r.get("supersedes")}
    for rec in records:
        if rec.get("_malformed"):
            continue
        # Accept both key spellings. The fleet's live doc (YNET-COORD-TIERS-FR) uses clause_id;
        # an independent folder that only understood one spelling would silently report an empty
        # document -- the false-clean failure again, this time in the verifier.
        cid = rec.get("clause") or rec.get("clause_id") or "<unclaused>"
        by_clause[cid].append(rec)

    clauses = {}
    conflicts = []
    for clause, recs in sorted(by_clause.items()):
        live = [r for r in recs if r.get("record_id") not in superseded]
        def op(r): return r.get("op") or r.get("kind")
        asserts = [r for r in live if op(r) in ("assert", "clause")]
        seconds = [r for r in live if op(r) == "second"]
        contests = [r for r in live if op(r) == "contest"]
        # Distinct assertion TEXTS is the conflict test -- not distinct actors. Two lanes
        # asserting identical text is corroboration and must not read as disagreement.
        texts = sorted({r.get("text", "") for r in asserts})
        # Agreement is DERIVED, per the live doc's own schema: conflict set empty AND seconds
        # from >= 2 DISTINCT HOSTS. Host is the part after '@'.
        second_hosts = sorted({(r.get("actor") or "?").split("@")[-1] for r in seconds})
        if len(texts) > 1 or contests:
            conflicts.append({
                "clause": clause,
                "distinct_assertions": len(texts),
                "contests": len(contests),
                "actors": sorted({r.get("actor", "?") for r in live}),
            })
        clauses[clause] = {
            "assertions": sorted(asserts, key=lambda r: (r.get("ts", ""), r.get("actor", ""))),
            "seconds": sorted({r.get("actor", "?") for r in seconds}),
            "second_hosts": second_hosts,
            "contests": contests,
            "settled": len(texts) == 1 and not contests,
            "agreed": len(texts) == 1 and not contests and len(second_hosts) >= 2,
        }
    return {"clauses": clauses, "conflicts": conflicts, "malformed": malformed,
            "actors": sorted({r.get("actor") for r in records if r.get("actor")})}


def render(head: dict) -> None:
    total = len(head["clauses"])
    settled = sum(1 for c in head["clauses"].values() if c["settled"])
    agreed = sum(1 for c in head["clauses"].values() if c.get("agreed"))
    print("# YNET Coordinator Tiers — Feature Requirements (DERIVED, never edited in place)")
    print()
    print(f"clauses {total} · uncontested {settled} · AGREED (>=2 distinct hosts seconding) {agreed} · "
          f"CONFLICTED {len(head['conflicts'])} · contributing actors {len(head['actors'])}")
    if head["malformed"]:
        print(f"🔴 MALFORMED RECORDS: {len(head['malformed'])} — surfaced, not skipped")
    print()
    for clause, body in head["clauses"].items():
        mark = "✅" if body.get("agreed") else ("🟡" if body["settled"] else "🔴")
        print(f"## {mark} {clause}")
        for a in body["assertions"]:
            print(f"  - [{a.get('actor')}] {a.get('text')}")
            if a.get("evidence"):
                print(f"      evidence: {a['evidence']}")
        if body["seconds"]:
            print(f"      seconded by: {', '.join(body['seconds'])} "
                  f"[hosts: {', '.join(body.get('second_hosts', []))}]")
        for c in body["contests"]:
            print(f"  🔴 CONTESTED by [{c.get('actor')}]: {c.get('text')}")
            print(f"      evidence: {c.get('evidence') or 'NONE — a contest without evidence is a preference (C-20)'}")
        print()
    if head["conflicts"]:
        print("## 🔴 CONFLICTS — reported, deliberately NOT resolved")
        print("Last-write-wins is banned here: it would destroy the signal an engineer needs to rule.")
        for c in head["conflicts"]:
            print(f"  - {c['clause']}: {c['distinct_assertions']} distinct assertions, "
                  f"{c['contests']} contest(s), actors {', '.join(c['actors'])}")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--log", type=Path, default=DEFAULT_LOG)
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args(argv)
    try:
        records = read(args.log)
    except FileNotFoundError as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2
    head = fold(records)
    print(json.dumps(head, indent=2, default=str)) if args.json else render(head)
    return 1 if head["conflicts"] or head["malformed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
