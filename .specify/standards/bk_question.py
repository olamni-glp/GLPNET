# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

"""BK-STD-2 — the pre-coded ENGINEER QUESTION surface.

`BK-STD-1` mandates a §4 ENGINEER QUESTIONS section and defines no per-question
shape, so the shape was improvised every session. `ariellas` searched two hosts
for a pre-coded template, found none, broadcast for help twice, and asked one
lane to harden a shape into an artefact rather than a paragraph. This is the
executable half of that artefact; `BK-STD-2-ENGINEER-QUESTION.md` is the prose
half.

stdlib only, single file, no repo imports — so a lane adopts it by copying it,
which is what "portable across all hosts and all lanes" has to mean when a share
can be down.

    validate     exit 2 and name (qid, field) on any violation; gates a sitrep
    render       Markdown — the non-interactive fallback, same content
    interactive  the picker payload: header / question / options, recommended FIRST
    decide       write the engineer's answer back onto the qid

Why `decide` matters as much as `validate`: a decided question must be CITED,
never re-asked. Re-asking is the failure the format exists to prevent, and it
cannot be prevented unless the answer lands back on the record.
"""

from __future__ import annotations

import argparse
import datetime as _dt
import json
import sys
from pathlib import Path

ORIGINS = ("measurement", "contradiction", "missing-requirement", "assumption-weakness")
SEVERITIES = ("critical", "high", "medium", "low")
#: The ONLY permitted scale (BK-STD-1). Points are carried so a reader never
#: has to remember the mapping.
SIZES = {"nano": 1, "micro": 3, "mini": 7, "midi": 11, "maxi": 17, "saga": 35}
REVERSIBILITY = ("reversible", "one-way")

MIN_BACKGROUND, MAX_BACKGROUND = 2, 6
MIN_OPTIONS, MAX_OPTIONS = 2, 4
MAX_HEADER = 12


def load(path: Path) -> list[dict]:
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    if isinstance(data, dict):
        data = [data]
    if not isinstance(data, list):
        raise ValueError("questions file must be an array of question objects")
    return data


def validate(questions: list[dict]) -> list[str]:
    """Return a list of violations. Empty means conformant."""
    problems: list[str] = []
    seen: set[str] = set()

    def bad(qid, field, msg):
        problems.append(f"{qid or '<no qid>'} :: {field} :: {msg}")

    for q in questions:
        qid = q.get("qid")
        if not qid or not isinstance(qid, str):
            bad(qid, "qid", "missing or not a string")
            continue
        if qid in seen:
            bad(qid, "qid", "duplicate — a qid is stable AND unique")
        seen.add(qid)

        block = q.get("block")
        if not block or not isinstance(block, str):
            bad(qid, "block", "required, one sentence naming what cannot proceed")
        elif block.count(".") > 1:
            bad(qid, "block", "must be ONE sentence")

        if q.get("origin") not in ORIGINS:
            bad(qid, "origin", f"must be one of {', '.join(ORIGINS)}")
        if q.get("severity") not in SEVERITIES:
            bad(qid, "severity", f"must be one of {', '.join(SEVERITIES)}")

        bg = q.get("background")
        if not isinstance(bg, list) or not all(isinstance(x, str) and x.strip() for x in bg):
            bad(qid, "background", "must be a list of non-empty evidence lines")
        elif not (MIN_BACKGROUND <= len(bg) <= MAX_BACKGROUND):
            bad(qid, "background", f"{len(bg)} lines; must be {MIN_BACKGROUND}-{MAX_BACKGROUND}")

        if not q.get("impact_if_unanswered"):
            bad(qid, "impact_if_unanswered",
                "required — write 'nothing' out in full rather than omitting it")

        opts = q.get("options")
        keys: list[str] = []
        if not isinstance(opts, list) or not (MIN_OPTIONS <= len(opts) <= MAX_OPTIONS):
            bad(qid, "options", f"must be {MIN_OPTIONS}-{MAX_OPTIONS} options")
            opts = []
        for i, o in enumerate(opts):
            where = f"options[{i}]"
            if not isinstance(o, dict):
                bad(qid, where, "must be an object")
                continue
            key = o.get("key")
            if not key:
                bad(qid, where + ".key", "required")
            elif key in keys:
                bad(qid, where + ".key", f"duplicate key {key!r}")
            else:
                keys.append(key)
            if not o.get("label"):
                bad(qid, where + ".label", "required")
            if not o.get("consequence"):
                bad(qid, where + ".consequence", "required — what follows if chosen")
            if o.get("size") not in SIZES:
                bad(qid, where + ".size", f"must be one of {', '.join(SIZES)}")
            rev = o.get("reversibility")
            if rev not in REVERSIBILITY:
                bad(qid, where + ".reversibility", f"must be one of {', '.join(REVERSIBILITY)}")
            elif rev == "one-way" and "foreclos" not in str(o.get("consequence", "")).lower():
                bad(qid, where + ".consequence",
                    "a one-way option MUST name what it forecloses")

        rec = q.get("recommendation")
        if not isinstance(rec, dict):
            bad(qid, "recommendation", "required — exactly one option")
        else:
            if rec.get("option") not in keys:
                bad(qid, "recommendation.option", "must name one of this question's option keys")
            because = str(rec.get("because") or "")
            if not because:
                bad(qid, "recommendation.because", "required — why it beats the runner-up")
            elif because.strip().lower().startswith("it depends"):
                bad(qid, "recommendation.because", "'it depends' is not a recommendation")

        hdr = q.get("header")
        if hdr is not None and len(str(hdr)) > MAX_HEADER:
            bad(qid, "header", f"{len(str(hdr))} chars; max {MAX_HEADER}")

        dec = q.get("decision")
        if dec is not None and not isinstance(dec, dict):
            bad(qid, "decision", "must be an object with option/date/rationale")
    return problems


def _header_for(q: dict) -> str:
    h = q.get("header")
    if h:
        return str(h)[:MAX_HEADER]
    return str(q.get("qid", "Q")).split("-")[-1][:MAX_HEADER]


def _ordered_options(q: dict) -> list[dict]:
    """Recommended option FIRST and marked, per the interactive contract."""
    opts = list(q.get("options") or [])
    rec = (q.get("recommendation") or {}).get("option")
    opts.sort(key=lambda o: (o.get("key") != rec,))
    out = []
    for o in opts:
        label = str(o.get("label", ""))
        if o.get("key") == rec and "(Recommended)" not in label:
            label = f"{label} (Recommended)"
        out.append({
            "label": label,
            "description": "%s [%s/%dp · %s]" % (
                o.get("consequence", ""), o.get("size"),
                SIZES.get(o.get("size"), 0), o.get("reversibility")),
        })
    return out


def render(questions: list[dict], qid: str | None = None) -> str:
    lines: list[str] = []
    for q in questions:
        if qid and q.get("qid") != qid:
            continue
        dec = q.get("decision") or {}
        lines.append(f"### `{q.get('qid')}` — {q.get('block')}")
        lines.append("")
        lines.append("**origin** `%s` · **severity** `%s`%s" % (
            q.get("origin"), q.get("severity"),
            "" if not dec.get("option") else
            f" · **DECIDED** `{dec.get('option')}` on {dec.get('date')}"))
        lines.append("")
        lines.append("**Background — evidence:**")
        for b in q.get("background") or []:
            lines.append(f"- {b}")
        lines.append("")
        lines.append(f"**If unanswered:** {q.get('impact_if_unanswered')}")
        lines.append("")
        lines.append("| opt | what would be done | consequence | size | reversibility |")
        lines.append("|---|---|---|---|---|")
        rec = (q.get("recommendation") or {}).get("option")
        for o in q.get("options") or []:
            mark = " ⭐" if o.get("key") == rec else ""
            lines.append("| **%s**%s | %s | %s | %s/%dp | %s |" % (
                o.get("key"), mark, o.get("label"), o.get("consequence"),
                o.get("size"), SIZES.get(o.get("size"), 0), o.get("reversibility")))
        lines.append("")
        lines.append("**→ Recommend `%s`.** %s" % (
            rec, (q.get("recommendation") or {}).get("because")))
        if dec.get("option"):
            lines.append("")
            lines.append("**DECISION `%s`** (%s) — %s" % (
                dec.get("option"), dec.get("date"), dec.get("rationale")))
        lines.append("")
    return "\n".join(lines)


def interactive_payload(questions: list[dict], qid: str | None = None) -> list[dict]:
    out = []
    for q in questions:
        if qid and q.get("qid") != qid:
            continue
        if (q.get("decision") or {}).get("option"):
            continue  # decided questions are cited, never re-asked
        out.append({
            "header": _header_for(q),
            "question": q.get("block"),
            "multiSelect": bool(q.get("multi_select", False)),
            "options": _ordered_options(q),
        })
    return out


def decide(path: Path, qid: str, option: str, rationale: str, date: str | None) -> int:
    questions = load(path)
    for q in questions:
        if q.get("qid") != qid:
            continue
        keys = [o.get("key") for o in q.get("options") or []]
        if option not in keys:
            print(f"{qid}: option {option!r} is not one of {keys}", file=sys.stderr)
            return 2
        q["decision"] = {
            "option": option,
            "date": date or _dt.date.today().isoformat(),
            "rationale": rationale,
        }
        Path(path).write_text(json.dumps(questions, indent=2, ensure_ascii=False) + "\n",
                              encoding="utf-8")
        print(f"{qid}: decided {option}")
        return 0
    print(f"no question with qid {qid!r}", file=sys.stderr)
    return 2


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="bk_question", description="BK-STD-2 engineer questions")
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("validate", "render", "interactive"):
        p = sub.add_parser(name)
        p.add_argument("--file", required=True)
        if name != "validate":
            p.add_argument("--qid", default=None)
        if name == "interactive":
            p.add_argument("--indent", type=int, default=2)
    d = sub.add_parser("decide")
    d.add_argument("--file", required=True)
    d.add_argument("--qid", required=True)
    d.add_argument("--option", required=True)
    d.add_argument("--rationale", required=True)
    d.add_argument("--date", default=None)

    args = ap.parse_args(argv)
    path = Path(args.file)

    if args.cmd == "decide":
        return decide(path, args.qid, args.option, args.rationale, args.date)

    questions = load(path)
    problems = validate(questions)
    if args.cmd == "validate":
        if problems:
            print("BK-STD-2 VIOLATIONS (%d):" % len(problems))
            for p in problems:
                print("  " + p)
            return 2
        print("BK-STD-2 conformant: %d question(s)" % len(questions))
        return 0

    if problems:
        print("refusing to present a non-conformant question set; run `validate`",
              file=sys.stderr)
        return 2
    if args.cmd == "render":
        print(render(questions, args.qid))
        return 0
    print(json.dumps(interactive_payload(questions, args.qid),
                     indent=args.indent, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
