#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""
WHO CONSUMES THIS L0 SYMBOL? -- answered across every repo, never from one vantage.

WHY THIS EXISTS (measured, 2026-09-04, and it has now happened four times in one day)

A lane published: "L0 has purpose-built feature-020 hooks (OnStepDispatched, Unregister,
StartOnDedicatedThread, Markers) with ZERO CONSUMERS -- the host that was meant to use them was
never written."

Every one of those hooks HAS a consumer, the host WAS written, it BUILDS clean, and its tests PASS
(Stage2KernelTests 3/3, asserting host.Markers.LastMarked("m") >= 3 -- the hook runs).

The claim was not careless. It is a STRUCTURAL TRAP:

    yngenios/l0/  contains  0  .csproj files.

l0/ is a SOURCE PROJECTION, not a buildable tree. Its own BLOCK.json says it is "regenerable from
l0/_catalog/*.jsonl". The CONSUMERS live in the ORIGIN repos (research/olamnit, qhstate, ...). So
`grep -r <symbol> l0/` finds the DEFINITION, finds no consumer, and reads as "nothing uses this" --
when the truth is "nothing in this projection uses this, and the projection contains no consumers
BY CONSTRUCTION".

I nearly published the same false absence myself an hour earlier: QActive appears nowhere in
qhstate's YngeniOS stack, which made "the blocks exist but nothing wires them to the mailboxes" look
correct. It was wrong for exactly this reason.

THE RULE THIS ENFORCES
    An absence is only reportable if you can NAME THE ROOTS YOU SEARCHED.
This generalises the fleet's own ruling Q-lejepa-31 ("a quorum refusal MUST name the voters it
reached") from voters to evidence. A search that cannot say where it looked is an opinion.

USAGE
    scripts/l0-consumers.py OnStepDispatched [more symbols...]
    scripts/l0-consumers.py --roots            # print the roots and their readability

EXIT
    0 = every symbol has at least one consumer   1 = a genuine zero-consumer symbol
    2 = INCONCLUSIVE: a root was unreadable, so "absent" cannot be claimed
"""
import os, re, subprocess, sys

# Origin repos first: these BUILD. Projections are searched too, but never decide an absence.
ROOTS = [
    ("/mnt/biwin/D_DRIVE/BSTDEV/research/olamnit",          "origin"),
    ("/mnt/biwin/D_DRIVE/BSTDEV/research/qhstate",          "origin"),
    ("/mnt/biwin/D_DRIVE/BSTDEV/research/crucible/glp/GLPNET", "origin"),
    ("/mnt/biwin/D_DRIVE/BSTDEV/research/yngenios",         "projection"),
    ("/mnt/biwin/D_DRIVE/YNGENIOS/yngenios",                "projection"),
    ("/mnt/biwin/D_DRIVE/YNGENIOS/yngenios-linux",          "origin"),
    ("/mnt/biwin/D_DRIVE/YNGENIOS/yngenios-windows",        "origin"),
    ("/mnt/biwin/D_DRIVE/YNGENIOS/yngenios-app",            "origin"),
]
SKIP = re.compile(r"/(obj|bin|node_modules|\.git)/")


def buildable(path: str) -> bool:
    """Is this hit inside a tree that actually compiles? A projection with no .csproj cannot."""
    d = os.path.dirname(path)
    for _ in range(8):
        try:
            if any(f.endswith(".csproj") for f in os.listdir(d)):
                return True
        except OSError:
            return False
        nd = os.path.dirname(d)
        if nd == d:
            break
        d = nd
    return False


def search(root: str, sym: str):
    """Return (hits, error). An unreadable root is an ERROR, never an empty result."""
    try:
        out = subprocess.run(
            ["grep", "-rn", "--include=*.cs", r"\b%s\b" % sym, root],
            capture_output=True, text=True, timeout=180)
    except (OSError, subprocess.TimeoutExpired) as e:
        return [], str(e)
    if out.returncode not in (0, 1):          # 1 == no match, which is a real answer
        return [], (out.stderr.strip() or "grep rc=%d" % out.returncode)
    return [l for l in out.stdout.splitlines() if l and not SKIP.search(l)], None


def main(argv):
    if "--roots" in argv:
        for r, kind in ROOTS:
            print(f"  {'OK ' if os.path.isdir(r) else 'MISSING'}  {kind:10} {r}")
        return 0

    syms = [a for a in argv if not a.startswith("-")]
    if not syms:
        print(__doc__)
        return 2

    worst = 0
    for sym in syms:
        print(f"\n=== {sym} ===")
        consumers, definitions, errors, searched = [], [], [], []
        for root, kind in ROOTS:
            if not os.path.isdir(root):
                errors.append(f"{root}: not present on this host")
                continue
            searched.append(root)
            hits, err = search(root, sym)
            if err:
                errors.append(f"{root}: {err}")
                continue
            for h in hits:
                path = h.split(":", 1)[0]
                # a declaration/definition line vs a use site
                is_def = re.search(r"\b(class|interface|record|struct)\b.*\b%s\b" % sym, h) \
                    or re.search(r"\b%s\s*(\{|=>|\()" % sym, h.split(":", 2)[-1]) and " = " not in h
                (definitions if (is_def and not buildable(path)) else
                 consumers if buildable(path) else definitions).append(h)

        seen = {c.split(":", 1)[0] for c in consumers}
        print(f"  consumers   : {len(consumers)} hit(s) in {len(seen)} buildable file(s)")
        for f in sorted(seen)[:8]:
            print(f"      {f}")
        print(f"  non-buildable (projection/definition) hits: {len(definitions)}")

        if errors:
            print("  🔴 INCONCLUSIVE — these roots could not be searched:")
            for e in errors:
                print(f"      {e}")
            print("      An unreadable root is NOT evidence of absence.")
            worst = max(worst, 2)
        elif not consumers:
            print("  🔴 ZERO CONSUMERS — and this IS reportable, because every root above was read:")
            for r in searched:
                print(f"      searched {r}")
            worst = max(worst, 1)
        else:
            print("  ✅ CONSUMED. Any 'zero consumers' claim for this symbol is a search-scope artifact.")
    return worst


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
