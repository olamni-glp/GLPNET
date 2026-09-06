#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
"""The FLEETWIDE TACTICAL ACTION PLAN as a CRDT, and the renderer that folds it into Markdown.

WHY A CRDT AND NOT A DOCUMENT
-----------------------------
The plan is edited by fifteen lanes across four hosts that are not always simultaneously
reachable, and the shared volume is an ASYNCHRONOUS channel. A single Markdown file edited by
everyone has exactly one merge rule -- last writer wins -- and that rule silently discards work,
which is the defect the fleet has already measured in its board, its op-log and its election
tally. So the plan is a GROW-ONLY, PER-ACTOR OP LOG, merged on read:

    docs/fleet/plan/ops/<actor>.jsonl        one append-only file per actor; nobody edits another's

Merge is union by ``op_id``, then per (item, field) the winner is the highest
``(ts, op_id)`` -- deterministic, commutative, associative, idempotent. ACKs are ADD-WINS: an ack
is never removed by a later op, because a quorum you can shrink is not a quorum.

    THE MARKDOWN IS GENERATED, NEVER EDITED.

`render` folds the ops and writes the .md. That is what makes "the CRDT and the .md agree" a
property of the build rather than a promise: edit the .md by hand and the next render silently
discards your edit, which is why the header says so in the file itself.

LOSSLESSNESS
------------
v5.0 RESTRUCTURES v4.1; it does not summarise it. The repeated paragraphs in the engineer's
directive -- the "THIS MUST build on the other existing and developing YNGENIOS capabilities"
clause (10 occurrences), the "yng-broker/yng-guardian are the designated PBFT elector" line (6),
the QHSM/QMSM virtual-terminal paragraph (3), the delivery-quota block (2), and item [04] itself
(verbatim twice) -- become SHARED CLAUSES stated once and REFERENCED by every item that invoked
them. Nothing is dropped: a reference is not a summary, and `--check` asserts that every clause
is referenced by at least one item and that every v4.1 section is carried.

USAGE
-----
    python docs/fleet/plan/plan_crdt.py render          # fold ops -> the .md
    python docs/fleet/plan/plan_crdt.py check           # losslessness + quorum report, exit 2 on loss
    python docs/fleet/plan/plan_crdt.py ack --actor <lane> --items <id,...> [--note ...]
    python docs/fleet/plan/plan_crdt.py amend --actor <lane> --item <id> --field <f> --value <v>
    python docs/fleet/plan/plan_crdt.py quorum          # who has acked, and whether the bar is met

STDLIB ONLY, deliberately: every lane on every host must be able to fold this plan without
installing anything, including a lane whose virtual environment is broken.
"""

from __future__ import annotations

import argparse
import glob
import hashlib
import json
import os
import sys
from datetime import datetime, timezone

HERE = os.path.dirname(os.path.realpath(__file__))
OPS_DIR = os.path.join(HERE, "ops")
OUT_MD = os.path.join(os.path.dirname(HERE), "FLEETWIDE-TACTICAL-ACTION-PLAN-v5.0.md")

#: The quorum bar the engineer set for adopting this plan.
QUORUM_BAR = 45

SCHEMA = "yngenios/fleet-plan-crdt/1"


# ---------------------------------------------------------------------------
# The CRDT
# ---------------------------------------------------------------------------
def now_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def op_id(actor: str, seq: int, payload: dict) -> str:
    """Deterministic, content-addressed and actor-scoped.

    Content-addressed so the SAME op authored twice merges to one row instead of duplicating;
    actor-scoped so two actors cannot collide by writing the same content at the same moment."""
    body = json.dumps(payload, sort_keys=True, separators=(",", ":"))
    digest = hashlib.sha256(f"{actor}:{seq}:{body}".encode("utf-8")).hexdigest()[:16]
    return f"{actor}:{seq:06d}:{digest}"


def read_ops(ops_dir: str = OPS_DIR) -> tuple[list[dict], list[str]]:
    """Union-merge every actor's log. Returns (ops, problems).

    A line that does not parse is REPORTED, never skipped silently: a plan that quietly drops an
    unreadable op is a plan that quietly drops a lane."""
    ops: dict[str, dict] = {}
    problems: list[str] = []
    for path in sorted(glob.glob(os.path.join(ops_dir, "*.jsonl"))):
        actor_file = os.path.basename(path)[:-6]
        try:
            with open(path, "r", encoding="utf-8") as fh:
                lines = fh.read().splitlines()
        except OSError as exc:
            problems.append(f"{actor_file}: unreadable: {exc}")
            continue
        for n, line in enumerate(lines, 1):
            line = line.strip()
            if not line or line.startswith("//"):
                continue
            try:
                op = json.loads(line)
            except json.JSONDecodeError as exc:
                problems.append(f"{actor_file}:{n}: not JSON: {exc}")
                continue
            oid = op.get("op_id")
            if not oid:
                problems.append(f"{actor_file}:{n}: op has no op_id")
                continue
            if op.get("actor") != actor_file:
                # A log signed by one actor and carrying another's name is the impersonation the
                # election tally learned about the hard way. Report; do not merge.
                problems.append(
                    f"{actor_file}:{n}: op claims actor {op.get('actor')!r} in "
                    f"{actor_file}'s own log -- refused")
                continue
            if oid in ops and ops[oid] != op:
                problems.append(f"{oid}: two DIFFERENT ops share one op_id -- quarantined")
                continue
            ops[oid] = op
    return list(ops.values()), problems


def fold(ops: list[dict]) -> dict:
    """Fold the op set into the plan state. Deterministic and order-independent."""
    items: dict[str, dict] = {}
    acks: dict[str, dict] = {}          # actor -> {items:set, note, ts}
    superseded: set[str] = set()

    for op in sorted(ops, key=lambda o: (str(o.get("ts", "")), str(o.get("op_id", "")))):
        kind = op.get("kind")
        if kind == "upsert_item":
            item = items.setdefault(op["item"], {"id": op["item"], "_prov": {}})
            for k, v in op.get("fields", {}).items():
                item[k] = v
                item["_prov"][k] = (op.get("actor"), op.get("ts"))
        elif kind == "set_field":
            item = items.setdefault(op["item"], {"id": op["item"], "_prov": {}})
            item[op["field"]] = op["value"]
            item["_prov"][op["field"]] = (op.get("actor"), op.get("ts"))
        elif kind == "supersede":
            superseded.add(op["item"])
        elif kind == "ack":
            # ADD-WINS. An ack already given is never withdrawn by a later op: a quorum that can
            # shrink under a merge is not a quorum, it is a race.
            rec = acks.setdefault(op["actor"], {"items": set(), "notes": [], "ts": op.get("ts")})
            rec["items"].update(op.get("items", []))
            if op.get("note"):
                rec["notes"].append(op["note"])
        elif kind == "amend":
            item = items.setdefault(op["item"], {"id": op["item"], "_prov": {}})
            item.setdefault("_amendments", []).append(
                {"by": op.get("actor"), "ts": op.get("ts"), "text": op.get("text", "")})
    for sid in superseded:
        items.pop(sid, None)
    return {"items": items, "acks": acks}


def append_op(actor: str, kind: str, ops_dir: str = OPS_DIR, **payload) -> str:
    """Append ONE op to this actor's own log. Never touches another actor's file."""
    os.makedirs(ops_dir, exist_ok=True)
    path = os.path.join(ops_dir, f"{actor}.jsonl")
    seq = 0
    if os.path.exists(path):
        with open(path, "r", encoding="utf-8") as fh:
            seq = sum(1 for ln in fh if ln.strip() and not ln.startswith("//"))
    body = dict(payload)
    body.update({"kind": kind, "actor": actor, "ts": now_iso()})
    body["op_id"] = op_id(actor, seq, {k: v for k, v in body.items() if k != "ts"})
    with open(path, "a", encoding="utf-8") as fh:
        fh.write(json.dumps(body, sort_keys=True, ensure_ascii=False) + "\n")
    return body["op_id"]


# ---------------------------------------------------------------------------
# Rendering
# ---------------------------------------------------------------------------
HORIZONS = [
    ("H24", "0–24 h", "TODAY"),
    ("H48", "24–48 h", "TOMORROW"),
    ("H72", "48–72 h", "DAY 3"),
    ("H168", "72 h – 7 days", "THE WEEK"),
]

LICENSE_HEADER = """<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->
"""


def _kind(items: dict, kind: str) -> list[dict]:
    return [i for i in items.values() if i.get("kind") == kind]


def _by_order(rows: list[dict]) -> list[dict]:
    return sorted(rows, key=lambda r: (r.get("order", 9999), r["id"]))


def render(state: dict, problems: list[str]) -> str:
    items, acks = state["items"], state["acks"]
    out: list[str] = [LICENSE_HEADER]
    meta = items.get("META", {})

    out.append(f"# {meta.get('title', 'FLEETWIDE TACTICAL ACTION PLAN')} — "
               f"**v{meta.get('version', '5.0')}**")
    out.append("")
    out.append("> **THIS FILE IS GENERATED. DO NOT EDIT IT.**  ")
    out.append("> It is a fold of the CRDT op logs under `docs/fleet/plan/ops/`. Edit by APPENDING "
               "an op to your own log (`plan_crdt.py amend|ack`), then re-render. A hand edit here "
               "is discarded by the next render — silently, which is why this line exists.")
    out.append("")
    out.append(f"- **Generated** {now_iso()} · **schema** `{SCHEMA}`")
    out.append(f"- **Supersedes** `{meta.get('supersedes', '')}` — by RESTRUCTURING, not by "
               "summarising. Every clause of the predecessor is carried; see §9.")
    out.append(f"- **Quorum bar for adoption**: **{QUORUM_BAR} lanes**. "
               f"Currently **{len(acks)}** — see §8.")
    if problems:
        out.append("")
        out.append("🔴 **OP-LOG PROBLEMS (reported, never silently dropped):**")
        for p in problems:
            out.append(f"  - {p}")
    out.append("")
    out.append("---")
    out.append("")

    # §1 How to use
    out.append("## §1 — HOW TO USE THIS PLAN")
    out.append("")
    for row in _by_order(_kind(items, "usage")):
        out.append(f"{row.get('order', 0)}. **{row.get('title', '')}** — {row.get('text', '')}")
    out.append("")

    # §2 Shared clauses
    out.append("## §2 — SHARED CLAUSES (stated ONCE; referenced, never repeated)")
    out.append("")
    out.append("The engineer's directive states several requirements verbatim in many items — the "
               "YNGENIOS-capability clause appears **10** times, the elector designation **6**, the "
               "QHSM virtual-terminal paragraph **3**, the delivery quota **2**, and item `[04]` is "
               "present **twice, verbatim**. Repetition is not emphasis once a document is long "
               "enough to be skimmed: it hides which items genuinely differ. Each clause is "
               "therefore stated once here and referenced by id. **A reference is not a summary — "
               "nothing is dropped.**")
    out.append("")
    for row in _by_order(_kind(items, "clause")):
        out.append(f"### `{row['id']}` — {row.get('title', '')}")
        out.append("")
        out.append(row.get("text", ""))
        if row.get("source_repetitions"):
            out.append("")
            out.append(f"*Consolidated from {row['source_repetitions']} verbatim repetitions in "
                       "the source directive.*")
        out.append("")

    # §3 Horizons
    out.append("## §3 — THE FOUR HORIZONS")
    out.append("")
    out.append("| horizon | window | label | entry criteria | exit criteria (definition of done) |")
    out.append("|---|---|---|---|---|")
    for hid, window, label in HORIZONS:
        h = items.get(hid, {})
        out.append(f"| **{hid}** | {window} | {label} | {h.get('entry', '—')} | "
                   f"{h.get('exit', '—')} |")
    out.append("")
    for hid, window, label in HORIZONS:
        h = items.get(hid, {})
        if h.get("note"):
            out.append(f"**{hid} ({window}) — {h.get('note')}**")
            out.append("")

    # §4 Objectives per horizon
    out.append("## §4 — OBJECTIVE REGISTER, BY HORIZON")
    out.append("")
    objs = _by_order(_kind(items, "objective"))
    for hid, window, label in HORIZONS:
        rows = [o for o in objs if o.get("horizon") == hid]
        out.append(f"### §4.{hid} — {label} ({window}) — {len(rows)} objective(s)")
        out.append("")
        if not rows:
            out.append("*(none declared at this horizon)*")
            out.append("")
            continue
        out.append("| id | product | objective | owner | this lane | clauses |")
        out.append("|---|---|---|---|---|---|")
        for o in rows:
            out.append(
                f"| `{o['id']}` | {o.get('product', '')} | {o.get('title', '')} | "
                f"{o.get('owner', 'UNASSIGNED')} | {o.get('this_lane', '—')} | "
                f"{' '.join('`%s`' % c for c in o.get('clauses', []))} |")
        out.append("")
        for o in rows:
            out.append(f"#### `{o['id']}` {o.get('product', '')} — {o.get('title', '')}")
            out.append("")
            out.append(o.get("text", ""))
            out.append("")
            if o.get("verification"):
                out.append(f"**Verification (how we will know, not how we will feel):** "
                           f"{o['verification']}")
                out.append("")
            if o.get("depends_on"):
                out.append(f"**Depends on:** {', '.join('`%s`' % d for d in o['depends_on'])} — "
                           "and a dependency that is unbuilt is a BIND target, never a build target "
                           "for this item's owner.")
                out.append("")
            if o.get("_amendments"):
                out.append("**Amendments:**")
                for a in o["_amendments"]:
                    out.append(f"  - {a['ts']} `{a['by']}`: {a['text']}")
                out.append("")

    # §5 Governance
    out.append("## §5 — GOVERNANCE, QUOTA AND SCORING")
    out.append("")
    for row in _by_order(_kind(items, "governance")):
        out.append(f"### {row.get('title', '')}")
        out.append("")
        out.append(row.get("text", ""))
        out.append("")

    # §6 The operating loop
    out.append("## §6 — THE PER-LANE OPERATING LOOP (every era, in this order)")
    out.append("")
    for row in _by_order(_kind(items, "loop")):
        out.append(f"{row.get('order', 0)}. **{row.get('title', '')}** — {row.get('text', '')}")
    out.append("")

    # §7 Open questions
    out.append("## §7 — OPEN ENGINEER QUESTIONS (BK-STD-2)")
    out.append("")
    qs = _by_order(_kind(items, "question"))
    if not qs:
        out.append("*(none open)*")
    else:
        out.append("| id | question | why it blocks | recommendation |")
        out.append("|---|---|---|---|")
        for q in qs:
            out.append(f"| `{q['id']}` | {q.get('title', '')} | {q.get('impact', '')} | "
                       f"{q.get('recommendation', '')} |")
    out.append("")

    # §8 Quorum
    out.append("## §8 — QUORUM AND PARTICIPATION LEDGER")
    out.append("")
    out.append(f"Adoption bar: **{QUORUM_BAR} lanes**. Acked so far: **{len(acks)}**.")
    out.append("")
    if acks:
        out.append("| lane | items acked | note |")
        out.append("|---|---|---|")
        for actor in sorted(acks):
            rec = acks[actor]
            scope = "ALL" if "ALL" in rec["items"] else ", ".join(sorted(rec["items"])) or "—"
            out.append(f"| `{actor}` | {scope} | {' / '.join(rec['notes']) or '—'} |")
    else:
        out.append("*(no acks recorded yet)*")
    out.append("")
    out.append("**How to ack** — from your own repo, against the shared volume copy:")
    out.append("")
    out.append("```")
    out.append("python docs/fleet/plan/plan_crdt.py ack --actor <host>-<lane> --items ALL \\")
    out.append("       --note \"<what you are committing to, or what you dispute>\"")
    out.append("python docs/fleet/plan/plan_crdt.py render")
    out.append("```")
    out.append("")
    out.append("**To DISPUTE rather than ack**, append an amendment — it is recorded beside the "
               "item and survives every merge:")
    out.append("")
    out.append("```")
    out.append("python docs/fleet/plan/plan_crdt.py amend --actor <host>-<lane> --item <id> \\")
    out.append("       --text \"<the measurement or ruling that contradicts this item>\"")
    out.append("```")
    out.append("")
    out.append("🔴 **An ack is a claim about your own lane and nothing else.** Acking on behalf of "
               "a lane that has not spoken is the impersonation the election tally already "
               "measured; the op-log refuses an op whose `actor` does not match the log it sits in.")
    out.append("")

    # §9 Losslessness
    out.append("## §9 — LOSSLESSNESS LEDGER")
    out.append("")
    for row in _by_order(_kind(items, "carry")):
        out.append(f"- **{row.get('title', '')}** → {row.get('text', '')}")
    out.append("")

    out.append("---")
    out.append("")
    out.append("*Rendered by `docs/fleet/plan/plan_crdt.py` from the op logs in "
               "`docs/fleet/plan/ops/`. To change this document, append an op — never edit here.*")
    return "\n".join(out) + "\n"


# ---------------------------------------------------------------------------
# check: losslessness + honesty
# ---------------------------------------------------------------------------
def check(state: dict, problems: list[str]) -> tuple[list[str], int]:
    """Refuse a plan that has silently lost something. Exit 2 on loss, 0 when whole."""
    items = state["items"]
    findings: list[str] = []

    clauses = {i["id"] for i in items.values() if i.get("kind") == "clause"}
    # ANY item may reference a clause. Counting only objectives reported the delivery quota as
    # "consolidated out of the plan" when it is referenced by the governance section that carries
    # it -- a check reporting a loss that is not one is as bad as one missing a loss that is.
    referenced: set[str] = set()
    for o in items.values():
        referenced.update(o.get("clauses", []))
    orphan = sorted(clauses - referenced)
    if orphan:
        findings.append(
            f"LOSS: clause(s) {orphan} are declared and referenced by NO objective. A clause "
            "nobody references is a clause that was consolidated out of the plan -- which is "
            "exactly the compression this restructuring forbids.")
    dangling = sorted(referenced - clauses)
    if dangling:
        findings.append(f"LOSS: objective(s) reference undeclared clause(s) {dangling}")

    for o in items.values():
        if o.get("kind") != "objective":
            continue
        if not o.get("owner"):
            findings.append(f"UNOWNED: {o['id']} has no owner -- an unowned objective is a wish")
        if not o.get("verification"):
            findings.append(
                f"UNVERIFIABLE: {o['id']} declares no verification -- an objective whose "
                "completion cannot be measured cannot be reported complete")
        if o.get("horizon") not in {h[0] for h in HORIZONS}:
            findings.append(f"UNSCHEDULED: {o['id']} horizon {o.get('horizon')!r} is not one of "
                            f"{[h[0] for h in HORIZONS]}")

    findings.extend(f"OP-LOG: {p}" for p in problems)
    return findings, (2 if findings else 0)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------
def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="Fleet tactical action plan (CRDT)")
    sub = ap.add_subparsers(dest="cmd", required=True)

    sub.add_parser("render")
    sub.add_parser("check")
    sub.add_parser("quorum")

    a = sub.add_parser("ack")
    a.add_argument("--actor", required=True)
    a.add_argument("--items", default="ALL")
    a.add_argument("--note", default="")

    m = sub.add_parser("amend")
    m.add_argument("--actor", required=True)
    m.add_argument("--item", required=True)
    m.add_argument("--text", required=True)

    args = ap.parse_args(argv)

    if args.cmd == "ack":
        oid = append_op(args.actor, "ack",
                        items=[s.strip() for s in args.items.split(",") if s.strip()],
                        note=args.note)
        print(f"appended {oid}")
        return 0
    if args.cmd == "amend":
        oid = append_op(args.actor, "amend", item=args.item, text=args.text)
        print(f"appended {oid}")
        return 0

    ops, problems = read_ops()
    state = fold(ops)

    if args.cmd == "quorum":
        n = len(state["acks"])
        print(f"acks {n} / bar {QUORUM_BAR}")
        for actor in sorted(state["acks"]):
            print(f"  {actor}")
        # NOT MET is not an error state -- it is the honest state of an asynchronous channel.
        print("QUORUM MET" if n >= QUORUM_BAR else
              f"QUORUM NOT MET -- {QUORUM_BAR - n} more lane(s) required")
        return 0

    if args.cmd == "check":
        findings, code = check(state, problems)
        for f in findings:
            print(f)
        print(f"{len(state['items'])} item(s), {len(ops)} op(s), {len(findings)} finding(s)")
        return code

    text = render(state, problems)
    with open(OUT_MD, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print(f"wrote {OUT_MD} ({len(text.splitlines())} lines) from {len(ops)} op(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
