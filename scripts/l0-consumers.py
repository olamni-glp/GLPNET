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


_TEST_SDK = re.compile(
    r"Microsoft\.NET\.Test\.Sdk|\bxunit\b|\bnunit\b|MSTest\.TestFramework|\bTUnit\b",
    re.IGNORECASE)
_TEST_NAME = re.compile(r"(^|[.\-_])tests?$", re.IGNORECASE)


def _nearest_project(path: str):
    """The .csproj that owns `path`, searching INWARD-OUT. Nearest wins.

    Nearest, not outermost: a test project nested inside a production tree is a test project, and
    walking to the top would call it production -- the exact misread this function exists to end.
    """
    d = path if os.path.isdir(path) else os.path.dirname(path)
    for _ in range(8):
        try:
            projects = [f for f in os.listdir(d) if f.endswith(".csproj")]
        except OSError:
            return None
        if projects:
            return os.path.join(d, sorted(projects)[0])
        nd = os.path.dirname(d)
        if nd == d:
            break
        d = nd
    return None


def classify_project(path: str) -> str:
    """'production' | 'test' | 'unbuildable' -- which KIND of assembly would compile this file?

    WHY THIS REPLACED A BOOLEAN (measured 2026-09-06, and the defect was in THIS file):
        The old `buildable()` asked only "is there a .csproj above me?". A test project has one.
        So a seam whose only callers were its own unit tests reported CONSUMED, and this lane
        nearly published a refutation of the feature-020 zero-consumer claim on that output.
        @gavriella-olamnit named the mechanism at 21:15Z: "A seam is verified by its own unit
        tests, which construct their own consumer" -- so test-only wiring is indistinguishable
        from real wiring on every dashboard the fleet owns. It is distinguishable here now.
    """
    project = _nearest_project(path)
    if project is None:
        return "unbuildable"
    stem = os.path.basename(project)[: -len(".csproj")]
    if _TEST_NAME.search(stem):
        return "test"
    try:
        with open(project, "r", encoding="utf-8", errors="replace") as handle:
            if _TEST_SDK.search(handle.read()):
                return "test"
    except OSError:
        # Unreadable csproj: refuse to upgrade it to production. An unread file is not evidence.
        return "test"
    return "production"


def verdict(production: int, test: int) -> str:
    """CONSUMED | TEST-ONLY | ZERO -- and TEST-ONLY is NOT closure.

    One production call site closes a seam no matter how many tests there are; a thousand tests
    close nothing. That asymmetry IS the requirement: "at least one call site exists in an
    assembly that a RUNNING host composes -- not merely a test."
    """
    if production > 0:
        return "CONSUMED"
    return "TEST-ONLY" if test > 0 else "ZERO"


def buildable(path: str) -> bool:
    """Retained for callers that only ask compiles-or-not. Prefer classify_project()."""
    return classify_project(path) != "unbuildable"


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
        prod, tests, definitions, errors, searched = [], [], [], [], []
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
                bucket = {"production": prod, "test": tests}.get(
                    classify_project(path), definitions)
                bucket.append(h)

        prod_files = {c.split(":", 1)[0] for c in prod}
        test_files = {c.split(":", 1)[0] for c in tests}
        print(f"  production consumers : {len(prod)} hit(s) in {len(prod_files)} file(s)")
        for f in sorted(prod_files)[:8]:
            print(f"      {f}")
        print(f"  TEST-ONLY consumers  : {len(tests)} hit(s) in {len(test_files)} file(s)")
        for f in sorted(test_files)[:5]:
            print(f"      {f}")
        print(f"  non-buildable (projection/definition) hits: {len(definitions)}")

        if errors:
            print("  🔴 INCONCLUSIVE — these roots could not be searched:")
            for e in errors:
                print(f"      {e}")
            print("      An unreadable root is NOT evidence of absence.")
            worst = max(worst, 2)
            continue

        call = verdict(len(prod), len(tests))
        if call == "ZERO":
            print("  🔴 ZERO CONSUMERS — reportable, because every root above was read:")
            for r in searched:
                print(f"      searched {r}")
            worst = max(worst, 1)
        elif call == "TEST-ONLY":
            print("  🔴 TEST-ONLY — NOT CLOSURE. Every call site is in a test assembly, so this")
            print("      seam is verified by a consumer it constructs itself and is composed by")
            print("      no running host. This is the feature-020 defect, correctly named.")
            worst = max(worst, 1)
        else:
            print("  ✅ CONSUMED IN PRODUCTION. A 'zero consumers' claim for this symbol is a")
            print("      search-scope artifact.")
            print("      ⚠ LIMIT, STATED SO IT IS NOT MISREAD: this proves a production call site")
            print("        EXISTS. It does NOT prove the assembly holding it is composed by a")
            print("        RUNNING host. @gavriella-olamnit measured exactly that third state on")
            print("        2026-09-06T21:15Z - the R-03 kernel binder is merged and has production")
            print("        call sites, and still never executes, because no process on that host")
            print("        runs the YNGENIOS kernel. Static closure and live closure are two")
            print("        different questions and this tool answers only the first.")
    return worst


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
