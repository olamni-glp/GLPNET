# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""
Derive the CRDT twin of the fleet tactical action plan from the Markdown, and prove they agree.

WHY DERIVED AND NOT HAND-MAINTAINED
    The engineer needs the plan in two forms: Markdown to cut, paste and edit back into an agent
    prompt, and a CRDT record set so 45 lanes can ratify and amend it without coordination. Two
    hand-maintained copies of one plan is two plans by Wednesday. So the Markdown is the SOURCE and
    the CRDT is DERIVED, and `check` fails loudly when the committed twin no longer matches.

    This is the house rule applied to ourselves: "Do not restate a rule to make it true. Make it
    derivable or enforceable - a script, a test - never a longer paragraph."

CRDT SHAPE - conformant to BK-CPM-1 (.specify/standards/BK-CPM-1-DRAFT-crdt-schema.md)
    A G-Set of immutable records: the simplest CRDT that exists and the hardest to get wrong.
    - R1 identity is `record_id`; dedup is by record_id, never by file or row position.
    - R2 order is a Hybrid Logical Clock, total, with actor_id breaking the final tie.
    - D3 kind names are CLOSED; a reader MUST reject an unknown kind loudly, never ignore it.

    `record_id` here is CONTENT-DERIVED (uuid5 over plan_id + kind + section key), not random.
    That is deliberate: a generated record must have a stable identity, or every regeneration
    would look like a new record and the G-Set would grow without converging. Two hosts deriving
    from the same Markdown produce byte-identical records, which is what makes ratification
    meaningful - a lane ratifies a record_id, and everyone agrees what that names.

    Lane ratification records (kind=plan_ack) are NOT derived. They are written by lanes, appended
    to the same set, and never overwritten - that is the whole point of the union merge.

USAGE
    python3 scripts/fleet_plan_sync.py emit    # regenerate the twin from the Markdown
    python3 scripts/fleet_plan_sync.py check   # exit 1 if the committed twin has drifted
    python3 scripts/fleet_plan_sync.py acks    # tally ratification against the quorum target
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import uuid

PLAN_ID = "fleet-tactical-24h-v1"
ACTOR = "shiras/shiras-glpnet"
QUORUM_TARGET = 45
NAMESPACE = uuid.UUID("6ba7b810-9dad-11d1-80b4-00c04fd430c8")

MD = os.path.join("docs", "fleet", "FLEETWIDE-TACTICAL-ACTION-PLAN-v1.md")
CRDT = os.path.join("docs", "fleet", "fleetwide-tactical-action-plan-v1.crdt.json")

# D3: closed kind set. A reader must reject anything else loudly.
KINDS = ("plan_meta", "governance", "cross_cutting", "work_item", "horizon",
         "open_question", "plan_ack")

_HORIZON = {"4": "T+24h", "5": "T+48h", "6": "T+72h", "7": "T+7d"}


def _rid(kind: str, key: str) -> str:
    return str(uuid.uuid5(NAMESPACE, f"{PLAN_ID}|{kind}|{key}"))


def _sections(text: str):
    """Every '### N.M Title' and '## N · Title' heading, with the body that follows it."""
    out = []
    lines = text.splitlines()
    idx = [i for i, l in enumerate(lines) if re.match(r"^#{2,3} ", l)]
    for n, i in enumerate(idx):
        end = idx[n + 1] if n + 1 < len(idx) else len(lines)
        heading = lines[i].lstrip("# ").strip()
        body = "\n".join(lines[i + 1:end]).strip()
        key = heading.split("·")[0].split(" ")[0].strip()
        out.append((key, heading, body))
    return out


def _kind_for(key: str, heading: str) -> str:
    if re.match(r"^0(\.|$)", key):
        return "plan_meta"
    if re.match(r"^1(\.|$)", key):
        return "governance"
    if re.match(r"^2(\.|$)", key):
        return "cross_cutting"
    if re.match(r"^3(\.|$)", key):
        return "plan_meta"
    if re.match(r"^8(\.|$)", key):
        return "open_question"
    if re.match(r"^9(\.|$)", key):
        return "governance"
    if re.search(r"\[\d\d\]", heading):
        return "work_item"
    return "horizon"


def derive(text: str):
    records = []
    for seq, (key, heading, body) in enumerate(_sections(text)):
        kind = _kind_for(key, heading)
        item = None
        m = re.search(r"\[(\d\d)\]", heading)
        if m:
            item = m.group(1)
        records.append({
            "record_id": _rid(kind, key),
            "plan_id": PLAN_ID,
            "kind": kind,
            "section": key,
            "heading": heading,
            "item_id": item,
            "horizon": _HORIZON.get(key.split(".")[0]),
            "body_sha256": hashlib.sha256(body.encode("utf-8")).hexdigest(),
            "body": body,
            "hlc": {"physical_ms": 0, "logical": seq, "actor_id": ACTOR},
            "derived_from": MD,
        })
    return records


def _load_twin():
    if not os.path.exists(CRDT):
        return None
    with open(CRDT, "r", encoding="utf-8") as handle:
        return json.load(handle)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("emit", "check", "acks"))
    args = parser.parse_args()

    if not os.path.exists(MD):
        print(f"fleet_plan_sync: REFUSED - source Markdown missing: {MD}", file=sys.stderr)
        return 2
    with open(MD, "r", encoding="utf-8") as handle:
        derived = derive(handle.read())

    twin = _load_twin()
    acks = [r for r in (twin or {}).get("records", []) if r.get("kind") == "plan_ack"]

    if args.action == "emit":
        payload = {
            "plan_id": PLAN_ID,
            "schema": "BK-CPM-1 G-Set (record_id + HLC, union merge, closed kinds)",
            "kinds": list(KINDS),
            "quorum_target": QUORUM_TARGET,
            "source": MD,
            # Lane acks are never regenerated - they are other actors' writes.
            "records": derived + acks,
        }
        with open(CRDT, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, indent=1, ensure_ascii=False)
            handle.write("\n")
        print(f"emitted {len(derived)} derived record(s) + {len(acks)} preserved ack(s) -> {CRDT}")
        return 0

    if twin is None:
        print(f"fleet_plan_sync: REFUSED - no twin at {CRDT}; run `emit` first", file=sys.stderr)
        return 2

    if args.action == "acks":
        ratified = {r.get("actor") for r in acks if r.get("verdict") == "ratify"}
        print(f"ratify={len(ratified)} / quorum {QUORUM_TARGET}")
        for verdict in ("amend", "object"):
            rows = [r for r in acks if r.get("verdict") == verdict]
            if rows:
                print(f"  {verdict}: {len(rows)}")
                for r in rows:
                    print(f"      {r.get('actor')} :: {r.get('section') or '-'}")
        return 0 if len(ratified) >= QUORUM_TARGET else 1

    # check
    have = {r["record_id"]: r["body_sha256"] for r in twin.get("records", [])
            if r.get("kind") != "plan_ack"}
    want = {r["record_id"]: r["body_sha256"] for r in derived}
    added = sorted(set(want) - set(have))
    removed = sorted(set(have) - set(want))
    changed = sorted(k for k in set(want) & set(have) if want[k] != have[k])
    if not (added or removed or changed):
        print(f"fleet_plan_sync: OK - twin matches the Markdown ({len(want)} records)")
        return 0
    by_id = {r["record_id"]: r for r in derived}
    for k in added:
        print(f"  ONLY IN MARKDOWN  {by_id[k]['section']:<6} {by_id[k]['heading'][:60]}")
    for k in removed:
        print(f"  ONLY IN TWIN      {k}")
    for k in changed:
        print(f"  BODY DIFFERS      {by_id[k]['section']:<6} {by_id[k]['heading'][:60]}")
    print(f"\nfleet_plan_sync: DRIFT - {len(added)} added, {len(removed)} removed, "
          f"{len(changed)} changed. Run `emit` and commit the twin.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
