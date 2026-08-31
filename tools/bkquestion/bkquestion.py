#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
"""bkquestion — the pre-coded interactive question template, as a runnable tool.

WHY THIS EXISTS. Searched 2026-08-24 across `ospark/**`, `.specify/templates/`,
`D:\\coop` and `D:\\yngenios`: there is **no** pre-coded interactive question
template anywhere on this host. Every lane that has asked the engineer a
question has invented its own shape — the same *five lanes, five shapes* problem
the R-1/R-2/R-3 report standard exists to end.

WHAT IT IS. A modifiable template (`TEMPLATE-question-set.json`) plus a
stdlib-only validator/renderer/recorder. It is modelled on the harness's
built-in interactive prompt so a validated set renders straight into it, and it
adds the five things that shape does not carry:

1. **A stable id** per set and per question, so an answer can be CITED later.
2. **A declared kind** — a `risk-acceptance` expires; a `ruling` does not.
3. **A validated `cost`** on every option. An option with no stated downside is
   advocacy wearing the costume of a choice.
4. **A recorded decision.** This is the gap that mattered most: there is no verb
   anywhere that records an engineer decision, so today's rulings survive only
   where somebody hand-wrote them. `bkquestion record` appends to a git-tracked
   JSONL ledger, and `bkquestion decisions` reads it back.
5. **An escalation path**, so a blocking question does not simply sit.

Stdlib only, per Constitution I. The JSON-Schema check is written by hand for
exactly the keyword subset the schema uses, so it cannot quietly pass a document
by ignoring a keyword it did not understand.

    bkquestion validate  <set.json>
    bkquestion payload   <set.json>        # -> the built-in prompt's JSON
    bkquestion render    <set.json>        # -> Markdown, for a coop letter
    bkquestion record    <set.json> --answer Q-020-01="Ship mine" ...
    bkquestion decisions [--set-id ...] [--kind ...] [--expired]
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from datetime import datetime, timedelta, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
SCHEMA_PATH = os.path.join(HERE, "question-set.schema.json")

#: Append-only decision ledger. Git-tracked on purpose: the PGlite catalogs on
#: this host are unreplicated, and a ruling that exists in exactly one
#: unreplicated place is a ruling that can vanish without anyone noticing.
DEFAULT_LEDGER = os.path.join(".specify", "decisions", "engineer-decisions.jsonl")


# ── a small, strict JSON-Schema check (stdlib only) ───────────────────────


def _validate(value, node, where, errors):
    if "const" in node and value != node["const"]:
        errors.append(f"{where}: expected {node['const']!r}, got {value!r}")
    if "enum" in node and value not in node["enum"]:
        errors.append(f"{where}: {value!r} not in {node['enum']}")

    declared = node.get("type")
    if declared is not None:
        allowed = declared if isinstance(declared, list) else [declared]
        kinds = {"object": dict, "array": list, "string": str,
                 "number": (int, float), "boolean": bool}
        ok = False
        for name in allowed:
            if name == "null":
                ok = ok or value is None
            elif name == "integer":
                ok = ok or (isinstance(value, int) and not isinstance(value, bool))
            elif name == "boolean":
                ok = ok or isinstance(value, bool)
            else:
                ok = ok or isinstance(value, kinds[name])
        if not ok:
            errors.append(f"{where}: {value!r} is not of type {declared}")
            return

    if isinstance(value, str):
        if "pattern" in node and not re.match(node["pattern"], value):
            errors.append(f"{where}: {value!r} does not match {node['pattern']}")
        if "minLength" in node and len(value) < node["minLength"]:
            errors.append(f"{where}: {len(value)} chars, minimum "
                          f"{node['minLength']} — say more")
        if "maxLength" in node and len(value) > node["maxLength"]:
            errors.append(f"{where}: {len(value)} chars, maximum "
                          f"{node['maxLength']}")

    if isinstance(value, (int, float)) and not isinstance(value, bool):
        if "minimum" in node and value < node["minimum"]:
            errors.append(f"{where}: {value} < minimum {node['minimum']}")

    if isinstance(value, dict):
        properties = node.get("properties", {})
        for name in node.get("required", []):
            if name not in value:
                errors.append(f"{where}: missing required property {name!r}")
        if node.get("additionalProperties") is False:
            for name in value:
                if name not in properties:
                    errors.append(f"{where}: {name!r} is not an allowed property")
        for name, sub in value.items():
            if name in properties:
                _resolve(sub, properties[name], f"{where}.{name}", errors)

    if isinstance(value, list):
        if "minItems" in node and len(value) < node["minItems"]:
            errors.append(f"{where}: {len(value)} item(s), minimum "
                          f"{node['minItems']}")
        if "maxItems" in node and len(value) > node["maxItems"]:
            errors.append(f"{where}: {len(value)} item(s), maximum "
                          f"{node['maxItems']} — split the set rather than "
                          f"squeezing it")
        for index, item in enumerate(value):
            _resolve(item, node.get("items", {}), f"{where}[{index}]", errors)


def _resolve(value, node, where, errors, root=None):
    if "$ref" in node:
        target = _SCHEMA
        for part in node["$ref"].lstrip("#/").split("/"):
            target = target[part]
        node = target
    _validate(value, node, where, errors)


with open(SCHEMA_PATH, encoding="utf-8") as _handle:
    _SCHEMA = json.load(_handle)


# ── the rules the schema alone cannot express ─────────────────────────────


def semantic_errors(document):
    """The checks that make this a template rather than a data format.

    Each one exists because its absence produced a real failure: an unlabelled
    recommendation buried mid-list, a risk acceptance with no expiry that became
    permanent policy, and a question whose options had no stated cost — which is
    not a question, it is an announcement.
    """
    errors = []
    seen_ids = set()

    for index, question in enumerate(document.get("questions", [])):
        where = f"questions[{index}]"
        qid = question.get("id", "?")

        if qid in seen_ids:
            errors.append(f"{where}: duplicate question id {qid!r} — an id that "
                          f"names two questions cannot be cited")
        seen_ids.add(qid)

        text = question.get("question", "")
        if text and not text.rstrip().endswith("?"):
            errors.append(f"{where} ({qid}): the question must end with '?'. "
                          f"If it cannot, it is a statement and does not belong "
                          f"in an interactive prompt")

        kind = question.get("kind")
        expires = question.get("expires_after_days")
        if kind == "risk-acceptance" and not expires:
            errors.append(f"{where} ({qid}): kind 'risk-acceptance' REQUIRES "
                          f"expires_after_days. An acceptance that never expires "
                          f"is a ruling nobody agreed to make")
        if kind == "ruling" and expires:
            errors.append(f"{where} ({qid}): kind 'ruling' must NOT expire. If "
                          f"it should, it is a risk-acceptance")

        if kind in ("ruling", "prioritisation", "tie-break"):
            if not question.get("blocks"):
                errors.append(f"{where} ({qid}): kind {kind!r} must name what it "
                              f"BLOCKS. A question blocking nothing does not "
                              f"need an interactive prompt")
            if question.get("escalate_after_hours") is None:
                errors.append(f"{where} ({qid}): kind {kind!r} needs "
                              f"escalate_after_hours, or a blocking question can "
                              f"sit unanswered forever with nothing noticing")

        options = question.get("options", [])
        recommended = [o for o in options if o.get("recommended")]
        if len(recommended) > 1:
            errors.append(f"{where} ({qid}): {len(recommended)} options marked "
                          f"recommended; at most one")
        for opt_index, option in enumerate(options):
            label = option.get("label", "")
            if "(Recommended)" in label:
                errors.append(
                    f"{where}.options[{opt_index}]: do not write "
                    f"'(Recommended)' into the label — set recommended: true "
                    f"so exactly one option can carry it")
            if len(label.split()) > 6:
                errors.append(f"{where}.options[{opt_index}]: label is "
                              f"{len(label.split())} words; keep it to 1-5")

    return errors


def validate(document):
    errors = []
    _resolve(document, _SCHEMA, "$", errors)
    errors.extend(semantic_errors(document))
    return errors


# ── renderers ─────────────────────────────────────────────────────────────


def _ordered_options(question):
    """Recommended first, then authored order. One keystroke to accept."""
    options = list(question.get("options", []))
    options.sort(key=lambda o: 0 if o.get("recommended") else 1)
    return options


def to_payload(document):
    """The built-in interactive prompt's own shape, ready to hand straight to it."""
    questions = []
    for question in document["questions"]:
        options = []
        for option in _ordered_options(question):
            label = option["label"]
            if option.get("recommended"):
                label = f"{label} (Recommended)"
            options.append({
                "label": label,
                "description": f"{option['means']} Cost: {option['cost']}",
            })
        questions.append({
            "question": question["question"],
            "header": question["header"],
            "multiSelect": bool(question.get("multi_select", False)),
            "options": options,
        })
    return {"questions": questions}


def to_markdown(document):
    """The fallback surface: a coop letter, a PR comment, or an unattended run."""
    lines = [
        f"# ENGINEER QUESTIONS — `{document['set_id']}`",
        "",
        f"**lane** `{document['lane']}` · **repo** `{document['repo']}` · "
        f"**raised** {document['raised_at']}",
        "",
    ]
    if document.get("context"):
        lines += [document["context"], ""]

    for question in document["questions"]:
        lines.append(f"## {question['id']} · {question['header']} "
                     f"*({question['kind']})*")
        lines.append("")
        lines.append(question["question"])
        lines.append("")
        if question.get("blocks"):
            lines.append("**Blocks:** " + " · ".join(question["blocks"]))
        if question.get("escalate_after_hours") is not None:
            lines.append(f"**Escalates after:** "
                         f"{question['escalate_after_hours']} h")
        if question.get("expires_after_days"):
            lines.append(f"**Expires after:** "
                         f"{question['expires_after_days']} days")
        lines.append("")
        lines.append("| option | what it means | cost |")
        lines.append("|---|---|---|")
        for option in _ordered_options(question):
            mark = " **(Recommended)**" if option.get("recommended") else ""
            lines.append(f"| **{option['label']}**{mark} | {option['means']} | "
                         f"{option['cost']} |")
        lines.append("")
    return "\n".join(lines)


# ── the decision sink ─────────────────────────────────────────────────────


def record(document, answers, ledger_path, decided_by):
    """Append one decision row per answered question. Append-only, never rewritten.

    THE GAP THIS CLOSES. Before this, an engineer decision existed only where
    somebody happened to write it down — a commit message, a spec section, a
    chat transcript. A ruling nobody can query is a ruling that gets
    re-litigated, and this fleet has re-litigated the same contract question in
    four consecutive review cycles.
    """
    os.makedirs(os.path.dirname(ledger_path) or ".", exist_ok=True)
    now = datetime.now(timezone.utc)
    by_id = {q["id"]: q for q in document["questions"]}

    unknown = sorted(set(answers) - set(by_id))
    if unknown:
        raise SystemExit(f"no such question id(s) in this set: {', '.join(unknown)}")

    written = []
    with open(ledger_path, "a", encoding="utf-8") as handle:
        for qid, answer in answers.items():
            question = by_id[qid]
            expires = None
            if question.get("expires_after_days"):
                expires = (now + timedelta(
                    days=question["expires_after_days"])).isoformat()
            row = {
                "schema_version": "1",
                "set_id": document["set_id"],
                "question_id": qid,
                "kind": question["kind"],
                "header": question["header"],
                "question": question["question"],
                "answer": answer,
                "decided_by": decided_by,
                "decided_at": now.isoformat(),
                "expires_at": expires,
                "blocks": question.get("blocks", []),
                "lane": document["lane"],
                "repo": document["repo"],
            }
            handle.write(json.dumps(row, ensure_ascii=False) + "\n")
            written.append(row)
    return written


def read_ledger(ledger_path, *, include_superseded=False):
    """Return decisions from the append-only ledger, newest-per-question_id LIVE.

    The ledger is append-only by design, so re-recording a question (for example
    reclassifying an expiring ``risk-acceptance`` as a non-expiring ``ruling``)
    leaves BOTH rows on disk. Without supersession a consumer sees two
    contradictory authoritative decisions under one id — and, worse, ``--expired``
    keeps reporting the stale expiring row after its date passes while the same id
    also denotes a live non-expiring prohibition. That is not hypothetical: it
    happened to ``Q-glpnetshiras-03``, a reboot prohibition first filed as a
    risk-acceptance expiring 2026-09-02.

    So: the FILE is never rewritten (history is preserved and auditable), but the
    READER collapses to the newest row per ``question_id`` by ``decided_at``.
    Superseded rows are returned only with ``include_superseded=True`` and are
    always tagged, never silently dropped.
    """
    if not os.path.exists(ledger_path):
        return []
    rows = []
    with open(ledger_path, encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if line:
                rows.append(json.loads(line))

    newest = {}
    for index, row in enumerate(rows):
        # A row with no question_id cannot be superseded by, or supersede, anything:
        # give it a key unique to itself rather than merging every such row into one.
        qid = row.get("question_id") or f"\x00no-id:{index}"
        # decided_at is an ISO-8601 UTC stamp, so string order is time order;
        # index breaks ties so two same-instant rows still resolve deterministically.
        key = (row.get("decided_at") or "", index)
        if qid not in newest or key > newest[qid][0]:
            newest[qid] = (key, index)
    live = {index for _, index in newest.values()}

    out = []
    for index, row in enumerate(rows):
        row = dict(row)
        row["superseded"] = index not in live
        if row["superseded"]:
            qid = row.get("question_id") or f"\x00no-id:{index}"
            winner = rows[newest[qid][1]]
            row["superseded_by"] = winner.get("decided_at")
        if include_superseded or not row["superseded"]:
            out.append(row)
    return out


def id_collisions(rows):
    """Question ids that differ ONLY by case — the schema's charset permits both.

    Case is not folded into the supersession key: ``Q-GLPNETSHIRAS-03`` and
    ``Q-glpnetshiras-03`` really were two different decisions taken in two
    different sessions, and merging them would destroy one. But a human reading a
    citation cannot tell them apart, so they are surfaced rather than left silent.
    """
    seen = {}
    for row in rows:
        qid = row.get("question_id") or ""
        if qid:
            seen.setdefault(qid.lower(), set()).add(qid)
    return {k: sorted(v) for k, v in seen.items() if len(v) > 1}


# ── CLI ───────────────────────────────────────────────────────────────────


def _load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def main(argv=None):
    parser = argparse.ArgumentParser(
        prog="bkquestion",
        description="Pre-coded interactive question template: validate, render, "
                    "record.")
    sub = parser.add_subparsers(dest="command", required=True)

    for name, help_text in (
        ("validate", "check a question set against the schema AND the rules"),
        ("payload", "emit the built-in interactive prompt's JSON"),
        ("render", "emit Markdown (coop letter / PR comment / unattended run)"),
    ):
        node = sub.add_parser(name, help=help_text)
        node.add_argument("set_file")

    node = sub.add_parser("record", help="append engineer decisions to the ledger")
    node.add_argument("set_file")
    node.add_argument("--answer", action="append", default=[], metavar="ID=TEXT",
                      help="repeatable, e.g. --answer Q-020-01=\"Ship mine\"")
    node.add_argument("--by", default="engineer")
    node.add_argument("--ledger", default=DEFAULT_LEDGER)

    node = sub.add_parser("decisions", help="read the decision ledger back")
    node.add_argument("--ledger", default=DEFAULT_LEDGER)
    node.add_argument("--set-id")
    node.add_argument("--kind")
    node.add_argument("--superseded", action="store_true",
                      help="also show rows superseded by a later decision on the "
                           "same question_id (hidden by default; the ledger file "
                           "itself is append-only and never rewritten)")
    node.add_argument("--expired", action="store_true",
                      help="only rows whose expiry has passed — a risk "
                           "acceptance that has quietly become policy")
    node.add_argument("--json", action="store_true")

    args = parser.parse_args(argv)

    if args.command in ("validate", "payload", "render"):
        document = _load(args.set_file)
        errors = validate(document)
        if errors:
            print(f"INVALID — {len(errors)} problem(s):", file=sys.stderr)
            for error in errors:
                print(f"  - {error}", file=sys.stderr)
            return 2
        if args.command == "validate":
            count = len(document["questions"])
            print(f"valid: {document['set_id']} — {count} question(s), "
                  f"every option carries a cost")
            return 0
        if args.command == "payload":
            print(json.dumps(to_payload(document), indent=2, ensure_ascii=False))
            return 0
        print(to_markdown(document))
        return 0

    if args.command == "record":
        document = _load(args.set_file)
        errors = validate(document)
        if errors:
            print("refusing to record against an INVALID set; run validate",
                  file=sys.stderr)
            return 2
        answers = {}
        for pair in args.answer:
            if "=" not in pair:
                print(f"--answer needs ID=TEXT, got {pair!r}", file=sys.stderr)
                return 1
            qid, text = pair.split("=", 1)
            answers[qid.strip()] = text.strip()
        if not answers:
            print("nothing to record: pass at least one --answer", file=sys.stderr)
            return 1
        rows = record(document, answers, args.ledger, args.by)
        for row in rows:
            print(f"recorded {row['question_id']} [{row['kind']}] -> "
                  f"{row['answer']!r}"
                  + (f" (expires {row['expires_at'][:10]})"
                     if row["expires_at"] else ""))
        print(f"ledger: {args.ledger}")
        return 0

    rows = read_ledger(args.ledger, include_superseded=args.superseded)
    collisions = id_collisions(rows)
    if args.set_id:
        rows = [r for r in rows if r["set_id"] == args.set_id]
    if args.kind:
        rows = [r for r in rows if r["kind"] == args.kind]
    if args.expired:
        now = datetime.now(timezone.utc).isoformat()
        # A superseded row cannot expire into force: only LIVE acceptances count.
        rows = [r for r in rows
                if r.get("expires_at") and r["expires_at"] < now
                and not r.get("superseded")]
    if args.json:
        print(json.dumps(rows, indent=2, ensure_ascii=False))
        return 0
    if not rows:
        print("no decisions recorded (this is not the same as no decisions taken)")
        return 0
    print(f"| {'ID':<12} | {'KIND':<16} | {'DECIDED':<10} | ANSWER")
    print(f"|{'-' * 14}|{'-' * 18}|{'-' * 12}|{'-' * 40}")
    for row in rows:
        mark = " (superseded)" if row.get("superseded") else ""
        # A malformed row must be VISIBLE, not a traceback that hides every row after it.
        qid = row.get("question_id") or "(no id)"
        kind = row.get("kind") or "(no kind)"
        when = (row.get("decided_at") or "")[:10] or "(no date)"
        print(f"| {qid:<12} | {kind:<16} | {when:<10} | "
              f"{row.get('answer', '(no answer)')}{mark}")
    live = sum(1 for r in rows if not r.get("superseded"))
    print(f"\n{live} live decision(s)"
          + (f", {len(rows) - live} superseded shown" if len(rows) != live else "")
          + f" — ledger {args.ledger}")
    if not args.superseded:
        print("  (superseded rows are hidden by default; --superseded shows the history)")
    for folded, variants in sorted(collisions.items()):
        print(f"  WARNING id collision differing only by case: {' vs '.join(variants)}"
              " — these are NOT merged; cite them exactly")
    return 0


if __name__ == "__main__":
    sys.exit(main())
