#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
"""FLEET STANDARD — the marathon SITREP.

Canonical, portable renderer for the marathon situation report + "what's next", in
the ONE format every host and every repo uses. Engineer ruling 2026-08-23: a sitrep
described in prose drifts the moment two lanes render it by hand ("performance
theater"), so this ships the IMPLEMENTATION, not the convention.

    python scripts/marathon_sitrep.py                 # markdown (default)
    python scripts/marathon_sitrep.py --format tsv    # paste into a sheet
    python scripts/marathon_sitrep.py --format json   # machine-readable
    python scripts/marathon_sitrep.py --check         # exit 2 if NOT restart-safe

STANDARD (enforced below; none optional). Fixed field order, one row per fact:

    host · utc · repo · git.branch · git.clean · git.behind · git.ahead · git.head
    marathon.run · marathon.feature · marathon.open_items · marathon.done_items
    roadmap.not_closed · roadmap.specified · roadmap.promoted · roadmap.captured · roadmap.epics
    alloc.dup_owner_gate · next.action

* Determinism: given the same repo + catalog state, two hosts emit byte-identical
  bodies EXCEPT the intrinsically per-host facts (host, utc, git.head) which are
  labelled as such. No prose, no editorialising.
* A fact that cannot be read renders ``—`` (unknown), NEVER a guessed value.
* ``restart-safe`` ⇔ git.clean AND git.behind==0 AND git.ahead==0. ``--check`` exits
  2 when not restart-safe, so the safe-restart signal is an executable gate, not a
  sentence a lane can type optimistically.

WHY read the CLIs' text and not import buildkit: the pinned engine re-execs and the
text surface is the stable contract; importing would bind this tool to one engine.
"""
import argparse
import datetime
import json
import os
import subprocess
import sys

FIELDS = [
    "host", "utc", "repo",
    "git.branch", "git.clean", "git.behind", "git.ahead", "git.head",
    "marathon.run", "marathon.feature", "marathon.open_items", "marathon.done_items",
    "roadmap.not_closed", "roadmap.specified", "roadmap.promoted", "roadmap.captured", "roadmap.epics",
    "alloc.dup_owner_gate", "next.action",
]
DASH = "—"


def _run(cmd, cwd=None):
    try:
        p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=120)
        return p.returncode, (p.stdout or ""), (p.stderr or "")
    except Exception:
        return 1, "", ""


def _clean_lines(text):
    out = []
    for ln in text.splitlines():
        if "PGlite" in ln or ln.startswith("co:"):
            continue
        out.append(ln)
    return out


def git_facts(root):
    f = {}
    rc, out, _ = _run(["git", "rev-parse", "--abbrev-ref", "HEAD"], root)
    f["git.branch"] = out.strip() or DASH
    rc, out, _ = _run(["git", "status", "--porcelain"], root)
    f["git.clean"] = "yes" if (rc == 0 and out.strip() == "") else "no"
    _run(["git", "fetch", "origin", f["git.branch"], "-q"], root)
    rc, out, _ = _run(["git", "rev-list", "--left-right", "--count",
                       "origin/%s...%s" % (f["git.branch"], f["git.branch"])], root)
    if rc == 0 and out.strip():
        b, a = (out.split() + ["?", "?"])[:2]
        f["git.behind"], f["git.ahead"] = b, a
    else:
        f["git.behind"] = f["git.ahead"] = DASH
    rc, out, _ = _run(["git", "rev-parse", "--short", "HEAD"], root)
    f["git.head"] = out.strip() or DASH
    return f


def marathon_facts(root, mar_cmd, home):
    f = {"marathon.run": DASH, "marathon.feature": DASH,
         "marathon.open_items": DASH, "marathon.done_items": DASH}
    if not mar_cmd:
        return f
    cmd = list(mar_cmd) + ["status"]
    if home:
        cmd += ["--home", home]
    rc, out, _ = _run(cmd, root)
    for ln in _clean_lines(out):
        s = ln.strip()
        if s.startswith("run ") and "[" in s:
            f["marathon.run"] = s.split()[1]
            for tok in s.split():
                if tok.startswith("feature="):
                    f["marathon.feature"] = tok.split("=", 1)[1]
        if "outstanding items:" in s:
            f["marathon.open_items"] = s.split("outstanding items:")[1].strip().split()[0]
    return f


def roadmap_facts(root):
    f = {k: DASH for k in ("roadmap.not_closed", "roadmap.specified",
                           "roadmap.promoted", "roadmap.captured", "roadmap.epics",
                           "alloc.dup_owner_gate")}
    tbl = os.path.join(root, "scripts", "roadmap_open_table.py")
    if os.path.exists(tbl):
        rc, out, _ = _run([sys.executable, tbl, "--format", "json"], root)
        try:
            body = out[out.index("{"):out.rindex("}") + 1]
            rows = json.loads(body).get("rows", [])
            # count from the machine-readable rows (the peer's `totals` is prose)
            by = {}
            epics = set()
            for r in rows:
                by[r.get("state", "?")] = by.get(r.get("state", "?"), 0) + 1
                if r.get("epic"):
                    epics.add(r["epic"])
            f["roadmap.not_closed"] = str(len(rows))
            f["roadmap.specified"] = str(by.get("specified", 0))
            f["roadmap.promoted"] = str(by.get("promoted", 0))
            f["roadmap.captured"] = str(by.get("captured", 0))
            f["roadmap.epics"] = str(len(epics))
        except Exception:
            pass
        rc, _, _ = _run([sys.executable, tbl, "--check"], root)
        f["alloc.dup_owner_gate"] = "PASS" if rc == 0 else "FAIL"
    return f


def collect(root, mar_cmd, home, next_action):
    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    f = {
        "host": (os.environ.get("COMPUTERNAME") or os.environ.get("HOSTNAME") or DASH).lower(),
        "utc": now,
        "repo": os.path.basename(os.path.abspath(root)),
        "next.action": next_action or DASH,
    }
    f.update(git_facts(root))
    f.update(marathon_facts(root, mar_cmd, home))
    f.update(roadmap_facts(root))
    f["marathon.done_items"] = f.get("marathon.done_items", DASH)
    return f


def restart_safe(f):
    return f.get("git.clean") == "yes" and f.get("git.behind") == "0" and f.get("git.ahead") == "0"


def render(f, fmt):
    if fmt == "json":
        return json.dumps({"sitrep": {k: f.get(k, DASH) for k in FIELDS},
                           "restart_safe": restart_safe(f)}, indent=2)
    if fmt == "tsv":
        return "\n".join("%s\t%s" % (k, f.get(k, DASH)) for k in FIELDS)
    lines = ["| field | value |", "|:---|:---|"]
    for k in FIELDS:
        lines.append("| %s | %s |" % (k, f.get(k, DASH)))
    lines.append("")
    lines.append("**restart-safe: %s**" % ("YES" if restart_safe(f) else "NO"))
    return "\n".join(lines)


def main(argv=None):
    ap = argparse.ArgumentParser(description="Fleet-standard marathon sitrep.")
    ap.add_argument("--repo", default=".")
    ap.add_argument("--format", choices=("md", "tsv", "json"), default="md")
    ap.add_argument("--marathon-cmd", default=None,
                    help="marathon CLI (space-split); default tries buildkit-marathon then python -m")
    ap.add_argument("--home", default=os.environ.get("BUILDKIT_DEPLOY_HOME"))
    ap.add_argument("--next", default=None, help="the decided next action")
    ap.add_argument("--check", action="store_true", help="exit 2 if NOT restart-safe")
    a = ap.parse_args(argv)
    root = os.path.abspath(a.repo)
    if a.marathon_cmd:
        mar = a.marathon_cmd.split()
    else:
        mar = ["buildkit-marathon"]
        rc, _, _ = _run(mar + ["--help"], root)
        if rc != 0:
            mar = [sys.executable, "-m", "buildkit_cli.marathon"]
    f = collect(root, mar, a.home, a.next)
    print(render(f, a.format))
    if a.check and not restart_safe(f):
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
