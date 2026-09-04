#!/usr/bin/env python3
"""test/ring/analyze_imports.py — measure which glp_gleam modules are runtime-free TODAY.

Feature 101, T012. This fixes the contract boundary for T013 (research R2): the L0
contract is the runtime-free surface, and which modules qualify is MEASURED here, never
assumed from a module's name or its doc comment.

A module is RUNTIME-TAINTED if it, or anything it transitively imports:
  * imports `gleam/erlang...`  — the BEAM via the gleam_erlang package, or
  * declares `@external(erlang, ...)` — an Erlang FFI call.

Taint is transitive: a pure-looking module that imports a tainted one is tainted, because
building it drags the runtime in. That transitivity is the entire reason this is a script
and not a grep. A direct-only scan would report ~87 of 98 modules "runtime-free" and be
wrong about most of them.

Reported, deliberately, alongside the answer:
  * the DENOMINATOR (modules scanned) — a count with no denominator is unparseable (C4/SC-002);
  * every module NOT read and why (FR-006);
  * for each tainted module, the shortest import path to its taint source, so the boundary
    can be argued with rather than trusted.

Usage:  python test/ring/analyze_imports.py [--json] [--root <glp_gleam dir>]
Exit:   0 always (this is a measurement, not a gate). The gate is check_contract_purity.sh.
"""

from __future__ import annotations

import argparse
import collections
import json
import pathlib
import re
import sys

IMPORT_RE = re.compile(r"^\s*import\s+([a-zA-Z0-9_/]+)", re.MULTILINE)
FFI_RE = re.compile(r"@external\s*\(\s*erlang", re.MULTILINE)
ERLANG_IMPORT_RE = re.compile(r"^\s*import\s+gleam/erlang", re.MULTILINE)

# Codex review 20260904T055230Z (P1): recognising ONLY `gleam/erlang` + Erlang FFI meant that
# adding any other third-party runtime package to gleam.toml and importing it from a contract
# module would classify that module runtime-free — a successfully-building third-party runtime
# dependency straight through C1-R. So runtime-freedom is now decided by an ALLOW-LIST of
# first-party and stdlib prefixes: anything else is a third-party dependency by default.
#
# Deny-by-default is the only safe direction here. A deny-list has to enumerate every runtime
# package that will ever exist; an allow-list only has to enumerate the two that are ours.
FIRST_PARTY_PREFIXES = ("glp/", "glp_")
STDLIB_PREFIXES = ("gleam/",)          # gleam_stdlib — pure, no runtime of its own

# 🔴 PACKAGE NAME != IMPORT PREFIX, and assuming otherwise is how the first version of this
# fix failed its own positive control. Three different Hex packages all publish modules under
# `gleam/`:
#     gleam_stdlib -> gleam/list, gleam/string, ...   (pure)
#     gleam_erlang -> gleam/erlang/...                (the BEAM binding)
#     gleam_otp    -> gleam/otp/...                   (OTP behaviours, proc_lib)
# So a prefix allow-list on "gleam/" waves both runtime packages straight through. The runtime
# ones are therefore named explicitly, and this tuple must grow whenever a runtime-bearing
# package is added to gleam.toml. `test_codexreview_fixes.sh` holds a positive control that
# fails if gleam/otp ever stops being caught.
RUNTIME_STDLIB_EXCEPTIONS = ("gleam/erlang", "gleam/otp")


def third_party_runtime_imports(imports, first_party):
    """Imports that are neither first-party modules nor pure stdlib."""
    out = []
    for imp in sorted(imports):
        if imp in first_party:
            continue
        if imp.startswith("gleam/erlang"):
            continue  # counted separately by ERLANG_IMPORT_RE, with a clearer reason
        if imp.startswith(RUNTIME_STDLIB_EXCEPTIONS):
            out.append(imp)   # gleam/otp — a runtime package wearing a stdlib-shaped path
            continue
        if imp.startswith(FIRST_PARTY_PREFIXES) or imp.startswith(STDLIB_PREFIXES):
            continue
        out.append(imp)
    return out


def module_name(path: pathlib.Path, src: pathlib.Path) -> str:
    return path.relative_to(src).with_suffix("").as_posix()


def scan(src: pathlib.Path):
    """Return (imports, direct_taint, unreadable)."""
    imports: dict[str, set[str]] = {}
    direct: dict[str, list[str]] = {}
    unreadable: list[tuple[str, str]] = []

    for path in sorted(src.rglob("*.gleam")):
        name = module_name(path, src)
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError) as exc:
            unreadable.append((name, f"{type(exc).__name__}: {exc}"))
            continue

        # Strip //// module docs and // line comments before matching, so a comment
        # that merely MENTIONS an import is not counted as one. (The ring suite already
        # caught one token-matching defect of exactly this shape; do not add a second.)
        code = "\n".join(
            line for line in text.splitlines() if not line.lstrip().startswith("//")
        )

        imports[name] = set(IMPORT_RE.findall(code))
        reasons = []
        if ERLANG_IMPORT_RE.search(code):
            hits = sorted(
                {m for m in IMPORT_RE.findall(code) if m.startswith("gleam/erlang")}
            )
            reasons.append("imports " + ", ".join(hits))
        if FFI_RE.search(code):
            reasons.append("declares @external(erlang, ...) FFI")
        if reasons:
            direct[name] = reasons

    # Second pass for the allow-list check: it needs the full first-party module set, which is
    # only known once every file has been scanned.
    first_party = set(imports)
    for name, imps in imports.items():
        extra = third_party_runtime_imports(imps, first_party)
        if extra:
            direct.setdefault(name, []).append(
                "imports third-party package(s) outside gleam_stdlib: " + ", ".join(extra)
            )

    return imports, direct, unreadable


def propagate(imports: dict[str, set[str]], direct: dict[str, list[str]]):
    """Breadth-first from every directly-tainted module, over the REVERSE import graph.

    Returns {module: (distance, [path])} — the shortest chain from the module to the
    taint source it cannot escape.
    """
    importers: dict[str, set[str]] = collections.defaultdict(set)
    for mod, deps in imports.items():
        for dep in deps:
            if dep in imports:  # first-party only; gleam/* stdlib is not a glp module
                importers[dep].add(mod)

    tainted: dict[str, list[str]] = {}
    queue = collections.deque()
    for mod in direct:
        tainted[mod] = [mod]
        queue.append(mod)

    while queue:
        cur = queue.popleft()
        for up in sorted(importers.get(cur, ())):
            if up not in tainted:
                tainted[up] = [up] + tainted[cur]
                queue.append(up)
    return tainted


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=None)
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args()

    here = pathlib.Path(__file__).resolve()
    repo = here.parents[2]
    root = pathlib.Path(args.root) if args.root else repo / "glp_gleam"
    src = root / "src"
    if not src.is_dir():
        print(f"analyze_imports: no such source tree: {src}", file=sys.stderr)
        return 2

    imports, direct, unreadable = scan(src)
    tainted = propagate(imports, direct)

    all_mods = sorted(imports)
    free = [m for m in all_mods if m not in tainted]
    tainted_sorted = sorted(tainted)

    if args.json:
        print(
            json.dumps(
                {
                    "denominator": len(all_mods),
                    "runtime_free": free,
                    "runtime_tainted": {
                        m: {
                            "reasons": direct.get(m, []),
                            "path_to_taint": tainted[m],
                            "direct": m in direct,
                        }
                        for m in tainted_sorted
                    },
                    "not_read": [{"module": m, "reason": r} for m, r in unreadable],
                },
                indent=2,
            )
        )
        return 0

    print("== T012 · runtime-freedom of glp_gleam/src, measured ==")
    print(f"  source tree:  {src}")
    print(f"  denominator:  {len(all_mods)} modules scanned")
    print(f"  runtime-free: {len(free)}")
    print(f"  tainted:      {len(tainted_sorted)}  ({len(direct)} directly, "
          f"{len(tainted_sorted) - len(direct)} transitively)")
    print(f"  not read:     {len(unreadable)}")
    print("")

    print("  -- directly tainted (the taint sources) --")
    for m in sorted(direct):
        print(f"    {m}")
        for r in direct[m]:
            print(f"        {r}")
    print("")

    transitive = [m for m in tainted_sorted if m not in direct]
    print(f"  -- transitively tainted ({len(transitive)}) --")
    for m in transitive:
        print(f"    {m}  <- {' -> '.join(tainted[m])}")
    print("")

    print(f"  -- runtime-free surface ({len(free)}) --")
    for m in free:
        print(f"    {m}")

    if unreadable:
        print("")
        print("  -- NOT READ (FR-006: named, never silently dropped) --")
        for m, r in unreadable:
            print(f"    {m}: {r}")

    print("")
    print(f"  reconciles: {len(free)} free + {len(tainted_sorted)} tainted "
          f"+ {len(unreadable)} not-read == {len(free) + len(tainted_sorted) + len(unreadable)} "
          f"(denominator {len(all_mods) + len(unreadable)})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
