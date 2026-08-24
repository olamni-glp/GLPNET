#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
"""FLEET STANDARD — the TAKT report (actual measurements only).

Canonical, portable takt renderer in the ONE format every host and repo uses.
Engineer ruling 2026-08-23. An ERA IS A FEATURE (full /bk-specify..close span). Takt
is measured PER PHASE and PER ERA from ACTUAL clock measurements — LLM estimates are
NEVER permitted, and an era cannot be TIMED until /bk-close actually fires.

    python scripts/takt_report.py --measurements actuals.json           # markdown
    python scripts/takt_report.py --measurements actuals.json --format json
    python scripts/takt_report.py --measurements actuals.json --check    # exit 2 on a band breach

STANDARD (enforced below; none optional):

* The four measurable PHASES of an era: analyze · implement · codexreview · ship-close.
* Bands (targets, NOT authority to fragment a feature): phase 30m–3h, era 1.5–6h.
* A measurement is a real elapsed wall-clock duration in seconds. Absent → ``—``
  (never a guess, never an LLM estimate, never 0).
* An era duration counts ONLY when its ``closed`` flag is true (/bk-close fired);
  an open era shows ``—`` for era-takt and is excluded from era aggregates.
* Deterministic: sort eras by slug, phases in canonical order → byte-identical.
* ``--check`` exits 2 if any CLOSED measurement breaches its band (tail-control alarm).

MEASUREMENTS INPUT (JSON) — the stable contract, populated from marathon actuals:
    {"eras": [
       {"feature": "<slug>", "closed": true|false,
        "phases": {"analyze": <sec|null>, "implement": <sec|null>,
                   "codexreview": <sec|null>, "ship-close": <sec|null>},
        "era_seconds": <sec|null>}          # measured specify..close span; null if open
    ]}
No measurements file, or an empty one, is a VALID state — it renders an honest
"0 measured eras" report, which is the truth until the first era closes with timings.
"""
import argparse
import json
import sys

PHASES = ["analyze", "implement", "codexreview", "ship-close"]
PHASE_MIN, PHASE_MAX = 30 * 60, 3 * 3600          # 30 min .. 3 h
ERA_MIN, ERA_MAX = int(1.5 * 3600), 6 * 3600      # 1.5 h .. 6 h
DASH = "—"


def hhmm(sec):
    if sec is None:
        return DASH
    sec = int(sec)
    return "%dh%02dm" % (sec // 3600, (sec % 3600) // 60)


def band(sec, lo, hi):
    if sec is None:
        return DASH
    if sec < lo:
        return "UNDER"
    if sec > hi:
        return "OVER"
    return "ok"


def load(path):
    if not path:
        return {"eras": []}
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def analyse(doc):
    eras = sorted(doc.get("eras", []), key=lambda e: e.get("feature", ""))
    rows, breaches = [], []
    per_phase = {p: [] for p in PHASES}
    era_secs = []
    for e in eras:
        slug = e.get("feature", DASH)
        closed = bool(e.get("closed"))
        ph = e.get("phases", {}) or {}
        row = {"feature": slug, "closed": "yes" if closed else "no"}
        for p in PHASES:
            s = ph.get(p)
            row[p] = hhmm(s)
            row[p + ".band"] = band(s, PHASE_MIN, PHASE_MAX)
            if closed and s is not None:
                per_phase[p].append(s)
                if row[p + ".band"] in ("UNDER", "OVER"):
                    breaches.append("%s/%s %s (%s)" % (slug, p, hhmm(s), row[p + ".band"]))
        es = e.get("era_seconds") if closed else None
        row["era"] = hhmm(es)
        row["era.band"] = band(es, ERA_MIN, ERA_MAX)
        if es is not None:
            era_secs.append(es)
            if row["era.band"] in ("UNDER", "OVER"):
                breaches.append("%s ERA %s (%s)" % (slug, hhmm(es), row["era.band"]))
        rows.append(row)

    def avg(xs):
        return int(sum(xs) / len(xs)) if xs else None
    takt = {p: hhmm(avg(per_phase[p])) for p in PHASES}
    takt["era"] = hhmm(avg(era_secs))
    return rows, takt, breaches, len(era_secs)


def render(rows, takt, breaches, n_closed, fmt):
    if fmt == "json":
        return json.dumps({"rows": rows, "takt_avg": takt,
                           "closed_eras": n_closed, "breaches": breaches}, indent=2)
    if fmt == "tsv":
        out = ["feature\tclosed\t" + "\t".join(PHASES) + "\tera"]
        for r in rows:
            out.append("\t".join([r["feature"], r["closed"]] +
                                  [r[p] for p in PHASES] + [r["era"]]))
        out.append("TAKT\t\t" + "\t".join(takt[p] for p in PHASES) + "\t" + takt["era"])
        return "\n".join(out)
    lines = ["| Feature (era) | closed | " + " | ".join(PHASES) + " | era |",
             "|:---|:---|" + "---:|" * (len(PHASES) + 1)]
    for r in rows:
        lines.append("| `%s` | %s | %s | %s |" % (
            r["feature"], r["closed"],
            " | ".join("%s %s" % (r[p], "" if r[p + ".band"] in ("ok", DASH) else "⚠" + r[p + ".band"]) for p in PHASES),
            "%s %s" % (r["era"], "" if r["era.band"] in ("ok", DASH) else "⚠" + r["era.band"])))
    lines.append("| **TAKT (avg of measured)** | %d closed | %s | %s |" % (
        n_closed, " | ".join(takt[p] for p in PHASES), takt["era"]))
    lines.append("")
    lines.append("Bands: phase 30m–3h · era 1.5–6h · actuals only, no LLM estimates · "
                 "an era times only after /bk-close.")
    if breaches:
        lines.append("**⚠ band breaches (tail-control alarm):** " + "; ".join(breaches))
    elif n_closed == 0:
        lines.append("**0 measured eras yet** — no era has closed with timings; nothing to time "
                     "(correct per the ERA rule, not a gap to paper over).")
    return "\n".join(lines)


def main(argv=None):
    ap = argparse.ArgumentParser(description="Fleet-standard takt report (actuals only).")
    ap.add_argument("--measurements", default=None,
                    help="JSON of actual era/phase durations; omit for the honest empty report")
    ap.add_argument("--format", choices=("md", "tsv", "json"), default="md")
    ap.add_argument("--check", action="store_true", help="exit 2 on any band breach")
    a = ap.parse_args(argv)
    rows, takt, breaches, n_closed = analyse(load(a.measurements))
    print(render(rows, takt, breaches, n_closed, a.format))
    if a.check and breaches:
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
