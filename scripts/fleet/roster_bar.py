#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
"""WP-02 rekey, part one: DEDUPE THE ROSTER BY RESOLVED TARGET, AND STATE THE BAR WITH ITS n AND f.

Two defects this fixes, both measured rather than argued:

1. A roster keyed on drive letters over-counts hosts. Measured on ARIELLAS 2026-09-05:

       G: -> \\\\192.168.0.129\\Olamnit_D
       H: -> \\\\192.168.0.108\\GAVRI_D     <-- same
       I: -> \\\\192.168.0.108\\GAVRI_D     <-- target
       J: -> \\\\192.168.0.170\\Shiras_Share

   Four letters, THREE hosts. A dedupe that special-cases "this host's own share" does not
   catch it, because neither H: nor I: is this host's own share -- they are one PEER mounted
   twice. Round 72's sync barrier reported 5/4 hosts for a related reason (`gavriella` and
   `gavriellas` counted as two). Over-counting the denominator makes a quorum unreachable;
   over-counting the numerator elects a leader that a third of the fleet never voted for.

2. A bar quoted as a number is not a bar. `3` is simultaneously the answer given by three
   different formulas, so quoting it proves nothing about which rule a lane implemented.
   At n=4 the three rival bars in this estate AGREE, which is exactly why n=4 is the worst
   possible size to test at. Every bar here is reported WITH its n and f, and `bar --table`
   shows the sizes where the rules diverge.

Stdlib only, single file, no repo imports -- a lane adopts it by copying it, the same rule that
makes .specify/standards/bk_question.py portable when a share is down.

    resolve                    what this host's mounts actually point at
    dedupe --roster <f>        collapse a roster by resolved target; exit 2 on a single domain
    bar --n N [--f F]          the bar, with n and f, and the rival bars beside it
    bar --table                the sizes where the three rules diverge
    selftest                   run the built-in checks (no files, no network)
"""
from __future__ import annotations

import argparse
import json
import math
import re
import subprocess
import sys
from pathlib import Path

# --------------------------------------------------------------------------------------------
# quorum bars
# --------------------------------------------------------------------------------------------


def faults_tolerated(n: int) -> int:
    """The PBFT f for a roster of n: the largest f with n >= 3f+1."""
    if n < 1:
        raise ValueError("n must be >= 1")
    return (n - 1) // 3


def bar_byzantine(n: int, f: int) -> int:
    """ceil((n+f+1)/2) -- the bar that keeps two quorums intersecting in a correct node."""
    return math.ceil((n + f + 1) / 2)


def bar_simple_majority(n: int, _f: int) -> int:
    """floor(n/2)+1 -- the Guardian default. Crash-fault reasoning, not Byzantine."""
    return n // 2 + 1


def bar_two_f_plus_one(_n: int, f: int) -> int:
    """2f+1 -- correct for the commit phase, too small when used as the whole bar."""
    return 2 * f + 1


RIVALS = {
    "byzantine ceil((n+f+1)/2)": bar_byzantine,
    "majority floor(n/2)+1": bar_simple_majority,
    "2f+1": bar_two_f_plus_one,
}


def bar_report(n: int, f: int | None = None) -> dict:
    """The bar, never bare: always carrying the n and f it was computed from."""
    if f is None:
        f = faults_tolerated(n)
    rivals = {name: fn(n, f) for name, fn in RIVALS.items()}
    return {
        "n": n,
        "f": f,
        "bar": bar_byzantine(n, f),
        "rule": "byzantine ceil((n+f+1)/2)",
        "rivals": rivals,
        "rivals_agree": len(set(rivals.values())) == 1,
        "single_failure_domain": n < 2,
        "meets_3f_plus_1": n >= 3 * f + 1,
    }


def divergence_table(max_n: int = 8) -> list[dict]:
    """The sizes where the three rules disagree. n=4 is the one place they do not."""
    rows = []
    for n in range(1, max_n + 1):
        f = faults_tolerated(n)
        vals = {name: fn(n, f) for name, fn in RIVALS.items()}
        rows.append({"n": n, "f": f, **vals, "agree": len(set(vals.values())) == 1})
    return rows


# --------------------------------------------------------------------------------------------
# mount resolution
# --------------------------------------------------------------------------------------------

_NET_USE_ROW = re.compile(r"^\s*\S+\s+([A-Za-z]):\s+(\\\\\S+)")


def resolve_mounts(net_use_output: str | None = None) -> dict[str, str]:
    """Map drive letter -> UNC target. Parses `net use`; empty off Windows, never a guess."""
    if net_use_output is None:
        if sys.platform != "win32":
            return {}
        try:
            net_use_output = subprocess.run(
                ["net", "use"], capture_output=True, text=True, timeout=30, check=False
            ).stdout
        except (OSError, subprocess.SubprocessError):
            return {}

    mounts: dict[str, str] = {}
    for line in net_use_output.splitlines():
        m = _NET_USE_ROW.match(line)
        if m:
            mounts[m.group(1).upper() + ":"] = m.group(2).rstrip("\\")
    return mounts


def canonical_target(path: str, mounts: dict[str, str]) -> str:
    """
    The comparable identity of a location: a drive letter becomes the UNC it resolves to, a UNC
    is normalised, and anything else is returned lower-cased. Two locations are the same peer
    when and only when this function returns the same string for both.
    """
    if not path:
        return ""
    p = path.strip().replace("/", "\\").rstrip("\\")
    if len(p) >= 2 and p[1] == ":" and p[0].isalpha():
        letter = p[:2].upper()
        target = mounts.get(letter)
        if target:
            return (target + p[2:]).lower()
        return p.lower()
    return p.lower()


# --------------------------------------------------------------------------------------------
# roster dedupe
# --------------------------------------------------------------------------------------------


def dedupe_roster(members: list[dict], mounts: dict[str, str]) -> dict:
    """
    Collapse roster members that resolve to the same target.

    A member is {"id": ..., "path": ...} (an "at" or "root" key is accepted for the path).
    Members with no path cannot be proven distinct OR duplicate, so they are carried through
    UNMERGED and reported as unresolved -- silently merging them would be the same over-confident
    move in the opposite direction.
    """
    groups: dict[str, list[dict]] = {}
    unresolved: list[dict] = []

    for m in members:
        raw = m.get("path") or m.get("at") or m.get("root") or ""
        if not raw:
            unresolved.append(m)
            continue
        groups.setdefault(canonical_target(raw, mounts), []).append(m)

    collapsed = []
    for target, group in sorted(groups.items()):
        collapsed.append(
            {
                "target": target,
                "kept": group[0].get("id", "?"),
                "merged": [g.get("id", "?") for g in group[1:]],
                "count": len(group),
            }
        )

    distinct = len(collapsed) + len(unresolved)
    duplicates = sum(c["count"] - 1 for c in collapsed)

    return {
        "declared": len(members),
        "distinct": distinct,
        "duplicates_removed": duplicates,
        "groups": collapsed,
        "unresolved": [u.get("id", "?") for u in unresolved],
        "single_failure_domain": distinct < 2,
    }


# --------------------------------------------------------------------------------------------
# selftest
# --------------------------------------------------------------------------------------------

_ARIELLAS_NET_USE = """
OK           G:        \\\\192.168.0.129\\Olamnit_D    Microsoft Windows Network
OK           H:        \\\\192.168.0.108\\GAVRI_D      Microsoft Windows Network
OK           I:        \\\\192.168.0.108\\GAVRI_D      Microsoft Windows Network
OK           J:        \\\\192.168.0.170\\Shiras_Share Microsoft Windows Network
"""


def selftest() -> int:
    checks: list[tuple[str, bool]] = []

    def check(name: str, ok: bool) -> None:
        checks.append((name, bool(ok)))

    # --- the bar
    check("n=4 f=1 bar is 3", bar_report(4, 1)["bar"] == 3)
    check("n=4 is where all three rules AGREE (so it proves nothing)", bar_report(4, 1)["rivals_agree"])
    check("n=5 f=1 DIVERGES -- the size a conformance vector must include",
          not bar_report(5, 1)["rivals_agree"])
    check("n=5 f=1 byzantine bar is 4, not 3", bar_report(5, 1)["bar"] == 4)
    check("2f+1 at n=5 f=1 is 3 -- survives 2 faults while advertising 4",
          bar_two_f_plus_one(5, 1) == 3)
    check("f is derived, not assumed: n=4 -> f=1", faults_tolerated(4) == 1)
    check("f is derived, not assumed: n=7 -> f=2", faults_tolerated(7) == 2)
    check("n=3 tolerates no byzantine fault", faults_tolerated(3) == 0)
    check("n=1 is a single failure domain", bar_report(1)["single_failure_domain"])
    check("3f+1 is checked, not presumed", bar_report(4, 1)["meets_3f_plus_1"])
    check("a roster too small for its f is reported, not silently accepted",
          bar_report(3, 1)["meets_3f_plus_1"] is False)

    # --- mount resolution (the measured ARIELLAS case)
    mounts = resolve_mounts(_ARIELLAS_NET_USE)
    check("net use parses 4 mounts", len(mounts) == 4)
    check("H: and I: resolve to ONE target",
          canonical_target("H:\\coop", mounts) == canonical_target("I:\\coop", mounts))
    check("G: and J: stay distinct",
          canonical_target("G:\\coop", mounts) != canonical_target("J:\\coop", mounts))
    check("a bare UNC normalises to the same identity as its letter",
          canonical_target("\\\\192.168.0.108\\GAVRI_D\\coop", mounts)
          == canonical_target("I:/coop", mounts))
    check("an unmapped letter is not invented into a UNC",
          canonical_target("Z:\\x", mounts) == "z:\\x")

    # --- roster dedupe
    roster = [
        {"id": "olamnit", "path": "G:\\coop"},
        {"id": "gavriella", "path": "H:\\coop"},
        {"id": "gavriellas", "path": "I:\\coop"},   # the round-72 double count
        {"id": "shiras", "path": "J:\\coop"},
    ]
    d = dedupe_roster(roster, mounts)
    check("4 declared members collapse to 3 hosts", d["declared"] == 4 and d["distinct"] == 3)
    check("exactly one duplicate is removed", d["duplicates_removed"] == 1)
    check("the merged id is named, not dropped silently",
          any("gavriellas" in g["merged"] or "gavriella" in g["merged"] for g in d["groups"]))
    check("3 hosts is not a single failure domain", not d["single_failure_domain"])

    d1 = dedupe_roster([{"id": "a", "path": "H:\\c"}, {"id": "b", "path": "I:\\c"}], mounts)
    check("a roster that is really ONE host is flagged as a single failure domain",
          d1["distinct"] == 1 and d1["single_failure_domain"])

    du = dedupe_roster([{"id": "a", "path": "G:\\c"}, {"id": "nopath"}], mounts)
    check("a member with no path is carried unresolved, never merged away",
          du["unresolved"] == ["nopath"] and du["distinct"] == 2)

    # --- negative control: the checks can fail
    check("NEGATIVE CONTROL (must be False)", bar_byzantine(5, 1) == 3)

    failed = [n for n, ok in checks if not ok]
    expected_failures = {"NEGATIVE CONTROL (must be False)"}
    real_failures = [n for n in failed if n not in expected_failures]
    missing_neg = [n for n in expected_failures if n not in failed]

    for name, ok in checks:
        marker = "PASS" if ok else ("PASS(neg)" if name in expected_failures else "FAIL")
        print(f"  [{marker}] {name}")

    if real_failures or missing_neg:
        if missing_neg:
            print(f"\nSELFTEST BROKEN: the negative control PASSED -- {missing_neg}")
        print(f"\nSELFTEST FAILED: {len(real_failures)} check(s)")
        return 2

    print(f"\nSELFTEST OK: {len(checks) - 1} checks passed, 1 negative control failed as required")
    return 0


# --------------------------------------------------------------------------------------------
# cli
# --------------------------------------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="roster_bar", description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    sub.add_parser("resolve", help="show this host's mounts and their resolved targets")

    p_d = sub.add_parser("dedupe", help="collapse a roster by resolved target")
    p_d.add_argument("--roster", required=True, help="JSON: a list, or an object with a members/hosts key")
    p_d.add_argument("--json", action="store_true")

    p_b = sub.add_parser("bar", help="the quorum bar, with its n and f")
    p_b.add_argument("--n", type=int)
    p_b.add_argument("--f", type=int, default=None)
    p_b.add_argument("--table", action="store_true", help="where the three rules diverge")
    p_b.add_argument("--json", action="store_true")

    sub.add_parser("selftest", help="run the built-in checks")

    a = ap.parse_args(argv)

    if a.cmd == "selftest":
        return selftest()

    if a.cmd == "resolve":
        mounts = resolve_mounts()
        if not mounts:
            print("no network mounts resolved (not Windows, or none mapped)")
            return 0
        by_target: dict[str, list[str]] = {}
        for letter, target in sorted(mounts.items()):
            print(f"  {letter}  ->  {target}")
            by_target.setdefault(target.lower(), []).append(letter)
        dupes = {t: ls for t, ls in by_target.items() if len(ls) > 1}
        print(f"\n{len(mounts)} mount(s) -> {len(by_target)} distinct target(s)")
        for t, ls in dupes.items():
            print(f"  DUPLICATE MOUNT: {' and '.join(ls)} are the same target {t}")
        return 0

    if a.cmd == "bar":
        if a.table:
            rows = divergence_table()
            if a.json:
                print(json.dumps(rows, indent=2))
                return 0
            names = list(RIVALS)
            print(f"{'n':>3} {'f':>3} " + " ".join(f"{n:>26}" for n in names) + "   agree")
            for r in rows:
                print(f"{r['n']:>3} {r['f']:>3} " + " ".join(f"{r[n]:>26}" for n in names)
                      + ("   YES" if r["agree"] else "   no"))
            return 0

        if a.n is None:
            print("bar: --n is required (or use --table)", file=sys.stderr)
            return 1

        rep = bar_report(a.n, a.f)
        if a.json:
            print(json.dumps(rep, indent=2))
            return 0
        print(f"  bar = {rep['bar']}   n = {rep['n']}   f = {rep['f']}   rule = {rep['rule']}")
        for name, v in rep["rivals"].items():
            print(f"    rival {name:<28} {v}")
        if rep["rivals_agree"]:
            print("  NOTE: all three rules agree at this size, so this size cannot tell them apart.")
        if not rep["meets_3f_plus_1"]:
            print(f"  REFUSED SHAPE: n={rep['n']} does not meet 3f+1 for f={rep['f']}.")
            return 2
        if rep["single_failure_domain"]:
            print("  SINGLE FAILURE DOMAIN.")
            return 2
        return 0

    # dedupe
    raw = json.loads(Path(a.roster).read_text(encoding="utf-8"))
    members = raw if isinstance(raw, list) else (raw.get("members") or raw.get("hosts") or [])
    if not isinstance(members, list):
        print("dedupe: roster must be a list, or an object with a members/hosts list", file=sys.stderr)
        return 1

    result = dedupe_roster(members, resolve_mounts())
    if a.json:
        print(json.dumps(result, indent=2))
    else:
        print(f"  declared {result['declared']} -> distinct {result['distinct']} "
              f"(removed {result['duplicates_removed']} duplicate(s))")
        for g in result["groups"]:
            if g["merged"]:
                print(f"    {g['kept']:<20} absorbs {', '.join(g['merged'])}   [{g['target']}]")
            else:
                print(f"    {g['kept']:<20}                                  [{g['target']}]")
        for u in result["unresolved"]:
            print(f"    {u:<20} UNRESOLVED (no path) -- carried, not merged")
        rep = bar_report(result["distinct"])
        print(f"\n  bar = {rep['bar']}   n = {rep['n']}   f = {rep['f']}   rule = {rep['rule']}")
        if rep["rivals_agree"]:
            print("  NOTE: all three rules agree at this size; it cannot distinguish them.")

    return 2 if result["single_failure_domain"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
