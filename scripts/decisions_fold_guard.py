# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

"""Fold guard for the append-only engineer-decisions ledger.

WHY THIS EXISTS. `.specify/decisions/engineer-decisions.jsonl` is append-only and
shared across lanes, and its standing conflict rule is *union by count*. Union by
count cannot see a same-`question_id` row whose only change is an **emptied
answer** — the counts agree, and the decision is gone anyway.

MEASURED 2026-09-01 in glpnet: `Q-glpnetshiras-01` appeared twice. The 2026-08-30
row answered *"Codexreview first, then ship"*; the 2026-08-31 row answered `""`.
A last-wins fold therefore reported a ruling that gates `buildkit release`, `S6
discharge` and *any main-trunk advance* as **UNANSWERED**, and this session's own
first pass over the ledger did exactly that.

This is instance 12 of the 078 inventory in miniature: a reader that reports a
clean, plausible answer from a record it never really examined. So the guard is
written the way 078 requires — an erasure is **named and loud**, never inferred
away, and the exit code is non-zero so a caller cannot pipe the finding into a
silent pass.

THE FOLD RULE THIS GUARD ENFORCES (ruled `Q-GLPNETS14-09`, option C):

    For a repeated question_id, the FIRST non-empty answer wins.
    A later row that empties an existing answer is an ERASURE and is reported.
    A later row that *changes* a non-empty answer to a different non-empty answer
    is a SUPERSESSION — legitimate, but reported too, because a silent change of
    a decided ruling is exactly as dangerous as an erasure.

Read-only: this guard never writes to the ledger. Repair is a fresh appended row
(append-only is preserved), which is what the correcting row for
`Q-glpnetshiras-01` does.

    python scripts/decisions_fold_guard.py                     # default ledger
    python scripts/decisions_fold_guard.py --ledger <path>
    python scripts/decisions_fold_guard.py --json

Exit codes: 0 conformant · 1 findings · 2 the ledger could not be read.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

DEFAULT_LEDGER = Path(".specify/decisions/engineer-decisions.jsonl")

ERASURE = "erasure"
SUPERSESSION = "supersession"
UNREADABLE = "unreadable-row"
UNKEYED = "unkeyed-row"

#: The ledger has grown THREE row shapes over its life (measured 2026-09-01 over
#: 89 rows). Keying only on ``question_id`` silently ignores five of them, which
#: is the anonymous-tally defect this guard exists to catch, one level up. So the
#: identity key is resolved across the known aliases, and anything still unkeyed
#: is REPORTED rather than skipped.
ID_KEYS = ("question_id", "id", "qid")

#: ...and the ANSWER field forked too. Four `Q-GLPNETS8-0x` rows carry the
#: engineer's decision under ``ruling``; the rest use ``answer``. A fold keyed on
#: ``answer`` alone reads all four as undecided. Same class, one field over.
ANSWER_KEYS = ("answer", "ruling", "decision")


def row_id(row: dict) -> str | None:
    for key in ID_KEYS:
        value = row.get(key)
        if value:
            return str(value)
    return None


def row_answer(row: dict) -> str:
    """The decision text under whichever alias this row's shape used."""
    for key in ANSWER_KEYS:
        value = row.get(key)
        if isinstance(value, str) and value.strip():
            return value.strip()
        if isinstance(value, dict):
            for inner in ("option", "answer", "label"):
                nested = value.get(inner)
                if isinstance(nested, str) and nested.strip():
                    return nested.strip()
    return ""


def load_rows(path: Path) -> tuple[list[dict], list[dict]]:
    """Return ``(rows, unreadable)``.

    A line that will not parse is NOT skipped silently — it is returned so the
    caller can report it. A guard that quietly drops what it cannot read is the
    defect this file exists to catch.
    """
    rows: list[dict] = []
    unreadable: list[dict] = []
    for lineno, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            continue
        try:
            obj = json.loads(line)
        except json.JSONDecodeError as exc:
            unreadable.append({"kind": UNREADABLE, "line": lineno, "detail": str(exc)})
            continue
        if not isinstance(obj, dict):
            unreadable.append({"kind": UNREADABLE, "line": lineno,
                               "detail": "row is not a JSON object"})
            continue
        obj["_line"] = lineno
        rows.append(obj)
    return rows, unreadable


def _sort_key(row: dict) -> tuple:
    """Ledger order is append order; decided_at is advisory and may be absent."""
    return (str(row.get("decided_at") or ""), row.get("_line", 0))


def analyse(rows: list[dict]) -> tuple[dict[str, dict], list[dict]]:
    """Fold the ledger and report every erasure and supersession.

    Returns ``(fold, findings)`` where *fold* maps question_id → the winning row
    under the first-non-empty-answer-wins rule.
    """
    by_qid: dict[str, list[dict]] = {}
    findings: list[dict] = []
    for row in rows:
        qid = row_id(row)
        if not qid:
            # Never skipped silently. A row that carries no resolvable identity
            # can never be CITED, and an uncitable ruling is functionally the
            # same as an erased one.
            nested = row.get("decisions")
            findings.append({
                "kind": UNKEYED,
                "question_id": row.get("set_id") or "<no id>",
                "line": row.get("_line"),
                "kept": None,
                "kept_line": None,
                "detail": (
                    "row carries none of %s%s; it cannot be cited and is invisible "
                    "to any qid-keyed fold" % (
                        "/".join(ID_KEYS),
                        f" (it nests {len(nested)} decision(s) in a 'decisions' array)"
                        if isinstance(nested, list) else "",
                    )
                ),
            })
            continue
        by_qid.setdefault(qid, []).append(row)

    fold: dict[str, dict] = {}

    for qid, group in by_qid.items():
        group = sorted(group, key=_sort_key)
        winner = None
        for row in group:
            answer = row_answer(row)
            if not answer:
                if winner is not None:
                    findings.append({
                        "kind": ERASURE,
                        "question_id": qid,
                        "line": row.get("_line"),
                        "kept": row_answer(winner),
                        "kept_line": winner.get("_line"),
                        "detail": (
                            "a later row empties an answer already on the record; "
                            "a last-wins fold would report this ruling as UNANSWERED"
                        ),
                    })
                continue
            if winner is None:
                winner = row
            elif answer != row_answer(winner):
                # A correcting row deliberately restates the SAME answer; that is
                # not a supersession and must not be reported as one.
                findings.append({
                    "kind": SUPERSESSION,
                    "question_id": qid,
                    "line": row.get("_line"),
                    "kept": row_answer(winner),
                    "kept_line": winner.get("_line"),
                    "later": answer,
                    "detail": (
                        "a later row carries a DIFFERENT non-empty answer for a "
                        "decided ruling; first-non-empty wins, so confirm which "
                        "the engineer meant"
                    ),
                })
        if winner is not None:
            fold[qid] = winner

    findings.sort(key=lambda f: (f["kind"], f["question_id"]))
    return fold, findings


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(
        prog="decisions_fold_guard",
        description="Report answer-erasures and silent supersessions in the "
                    "append-only engineer-decisions ledger (read-only).",
    )
    ap.add_argument("--ledger", default=str(DEFAULT_LEDGER))
    ap.add_argument("--json", action="store_true", dest="as_json")
    args = ap.parse_args(argv)

    path = Path(args.ledger)
    if not path.is_file():
        print(f"decisions_fold_guard: no ledger at {path}", file=sys.stderr)
        return 2

    rows, unreadable = load_rows(path)
    fold, findings = analyse(rows)
    findings = unreadable + findings

    if args.as_json:
        print(json.dumps({
            "ledger": str(path),
            "rows": len(rows),
            "question_ids": len(fold),
            "findings": findings,
        }, indent=2, ensure_ascii=False))
        return 1 if findings else 0

    keyed = {row_id(r) for r in rows if row_id(r)}
    undecided = sorted(keyed - set(fold))
    print(f"ledger {path}: {len(rows)} rows, {len(keyed)} distinct question ids, "
          f"{len(fold)} decided, {len(undecided)} with no answer under "
          f"{'/'.join(ANSWER_KEYS)}")
    if undecided:
        print("  still open: " + ", ".join(undecided))
    if not findings:
        print("fold guard: conformant — no erasure, no silent supersession")
        return 0
    print(f"fold guard: {len(findings)} FINDING(S)")
    for f in findings:
        if f["kind"] == UNREADABLE:
            print(f"  [{f['kind']}] line {f['line']}: {f['detail']}")
            continue
        print(f"  [{f['kind']}] {f['question_id']} @ line {f['line']}: {f['detail']}")
        print(f"      kept (line {f['kept_line']}): {f['kept']!r}")
        if "later" in f:
            print(f"      later: {f['later']!r}")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
