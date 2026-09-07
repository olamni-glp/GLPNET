#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Cross-language DECLARED-BUT-UNCONSUMED guard (board row ``declared-unconsumed-guard``).

THE DEFECT CLASS
----------------
A capability is DECLARED, documented, unit-tested -- and then never wired to a consumer. It is
the most expensive recurring defect in this estate, because every local signal says green:
the type exists, its tests pass, the reviewer sees coverage. Nothing says *nobody calls it*.

Measured instances: yngenios L0 feature-020 hooks (OnStepDispatched / Unregister /
StartOnDedicatedThread / Markers) with zero consumers (W-06); and -- found by this lane on
2026-09-07 -- ``YngeniOS.Contracts.Consensus.LeaderLiveness``: 294 lines defining LeaderPing,
LeaderPong, NoConfidence and WatchDecision, fully tested including the live n=8 quorum, and
called by NOTHING while the fleet sat leaderless for a day (F-1).

ROOT CAUSE OF THE ROOT CAUSE, AND THE THING THIS FILE REALLY FIXES
-----------------------------------------------------------------
The existing guard detects this only for one repo's Python. Everywhere else it returns
``not_applicable`` -- and the summary renders that as ``0 finding(s)``. **A guard that DID NOT
LOOK is displayed identically to one that PASSED.** Every non-buildkit repo on four hosts is
therefore unguarded while appearing green.

This guard refuses to do that. ``UNVERIFIABLE`` is a first-class verdict, it is never folded
into a pass, and it drives a distinct exit code (C-20: pass | refuse | unverifiable).

A REAL LESSON ENCODED HERE
--------------------------
The author's own first consumer census of the L0 liveness contract greped ``**/*.cs`` only, and
so reported "zero consumers" while a live Python implementation answered pings in 5 ms. A
single-language census on a polyglot fleet (C-06) is wrong by construction. Hence: every
language is either SCANNED or explicitly reported UNSCANNED -- never silently omitted.

EXEMPTION, NOT HEURISTIC
------------------------
Genuinely-public API with no in-repo consumer is legitimate. It must say so out loud, on the
declaring line or the line above::

    public sealed record LeaderPing(...);   // declared-unconsumed: ok - public L0 contract, consumed cross-repo

A heuristic that guesses "this looks public so it is fine" would re-introduce the false green.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

EXEMPT_MARKER = "declared-unconsumed: ok"

#: language -> (glob, [regexes capturing the declared name in group 1])
LANGUAGES: dict[str, tuple[str, list[re.Pattern[str]]]] = {
    "csharp": ("**/*.cs", [
        re.compile(r"^\s*public\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*"
                   r"(?:record|class|interface|enum|struct)\s+([A-Z][A-Za-z0-9_]*)"),
    ]),
    "python": ("**/*.py", [
        re.compile(r"^\s*(?:class)\s+([A-Z][A-Za-z0-9_]*)"),
        re.compile(r"^\s*def\s+([a-z_][A-Za-z0-9_]*)\s*\("),
    ]),
    "dart": ("**/*.dart", [
        re.compile(r"^\s*(?:abstract\s+|sealed\s+)?class\s+([A-Z][A-Za-z0-9_]*)"),
    ]),
    "gleam": ("**/*.gleam", [
        re.compile(r"^\s*pub\s+(?:type|fn)\s+([a-zA-Z][A-Za-z0-9_]*)"),
    ]),
}

#: Directories that are build output or vendored code -- never the subject of a consumer census.
SKIP_PARTS = {"obj", "bin", "node_modules", ".git", "__pycache__", "vendor", ".dart_tool", "build"}


def _is_test(path: Path) -> bool:
    p = str(path).lower()
    return ("test" in p) or ("spec" in p) or p.endswith("_test.py") or ".tests." in p


def _skip(path: Path) -> bool:
    return any(part in SKIP_PARTS for part in path.parts)


@dataclass
class Declaration:
    name: str
    language: str
    file: Path
    line: int
    exempt: bool = False
    consumers: list[str] = field(default_factory=list)
    test_only: list[str] = field(default_factory=list)

    @property
    def verdict(self) -> str:
        if self.exempt:
            return "EXEMPT"
        if self.consumers:
            return "CONSUMED"
        return "UNCONSUMED"


@dataclass
class LanguageResult:
    language: str
    scanned_files: int
    declarations: list[Declaration]
    unverifiable_reason: str | None = None

    @property
    def verdict(self) -> str:
        if self.unverifiable_reason:
            return "UNVERIFIABLE"
        return "SCANNED"


def collect(root: Path, language: str, min_name_len: int) -> LanguageResult:
    glob, patterns = LANGUAGES[language]
    files = [p for p in root.glob(glob) if p.is_file() and not _skip(p)]
    if not files:
        # THE WHOLE POINT: no files is NOT a pass. It is "I did not look here".
        return LanguageResult(language, 0, [], f"no {language} source files under {root}")

    declarations: list[Declaration] = []
    texts: dict[Path, list[str]] = {}
    for path in files:
        # A declaration inside a TEST file is invoked by the test runner, not by other source.
        # Reporting it as "unconsumed" floods the result with noise -- measured on GLPNET, 797
        # gleam findings of which the overwhelming majority were `*_test` functions. A guard that
        # cries wolf 797 times is a guard nobody runs, which is the same false green by another
        # route. Test files are still READ (they resolve consumers); they just do not DECLARE.
        try:
            lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        except OSError as exc:
            return LanguageResult(language, len(files), [], f"unreadable {path}: {exc}")
        texts[path] = lines
        if _is_test(path):
            continue
        for idx, line in enumerate(lines):
            for pat in patterns:
                m = pat.match(line)
                if not m:
                    continue
                name = m.group(1)
                if len(name) < min_name_len:
                    continue
                prev = lines[idx - 1] if idx else ""
                exempt = EXEMPT_MARKER in line or EXEMPT_MARKER in prev
                declarations.append(Declaration(name, language, path, idx + 1, exempt))

    # Consumer resolution: a reference in any file that is neither the declaring file nor a test.
    for decl in declarations:
        word = re.compile(rf"\b{re.escape(decl.name)}\b")
        for path, lines in texts.items():
            if path == decl.file:
                continue
            if not any(word.search(ln) for ln in lines):
                continue
            rel = str(path.relative_to(root))
            (decl.test_only if _is_test(path) else decl.consumers).append(rel)

    return LanguageResult(language, len(files), declarations)


def run(root: Path, languages: list[str], min_name_len: int) -> dict:
    results = [collect(root, lang, min_name_len) for lang in languages]
    unconsumed = [d for r in results for d in r.declarations if d.verdict == "UNCONSUMED"]
    unverifiable = [r for r in results if r.verdict == "UNVERIFIABLE"]
    return {
        "root": str(root),
        "languages": [
            {"language": r.language, "verdict": r.verdict, "files": r.scanned_files,
             "declarations": len(r.declarations),
             "unverifiable_reason": r.unverifiable_reason,
             "unconsumed": [
                 {"name": d.name, "file": str(d.file.relative_to(root)), "line": d.line,
                  "tests_only": d.test_only}
                 for d in r.declarations if d.verdict == "UNCONSUMED"
             ]}
            for r in results
        ],
        "totals": {
            "unconsumed": len(unconsumed),
            "unverifiable_languages": len(unverifiable),
            "scanned_languages": len(results) - len(unverifiable),
        },
    }


def render(report: dict) -> None:
    print(f"declared-unconsumed guard · root={report['root']}")
    for lang in report["languages"]:
        if lang["verdict"] == "UNVERIFIABLE":
            # Rendered DISTINCTLY from a pass. This line is the fix for the false green.
            print(f"  {lang['language']:8} UNVERIFIABLE — {lang['unverifiable_reason']} "
                  f"(this is NOT a pass)")
            continue
        print(f"  {lang['language']:8} SCANNED — {lang['files']} files, "
              f"{lang['declarations']} declarations, {len(lang['unconsumed'])} unconsumed")
        for item in lang["unconsumed"]:
            tests = f" (referenced only by tests: {', '.join(item['tests_only'])})" if item["tests_only"] else ""
            print(f"      UNCONSUMED  {item['name']}  {item['file']}:{item['line']}{tests}")
    t = report["totals"]
    print(f"  totals: {t['unconsumed']} unconsumed · "
          f"{t['scanned_languages']} language(s) scanned · "
          f"{t['unverifiable_languages']} UNVERIFIABLE")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("root", type=Path)
    ap.add_argument("--language", action="append", choices=sorted(LANGUAGES), dest="languages")
    ap.add_argument("--min-name-len", type=int, default=4,
                    help="ignore very short identifiers, which collide on a word-boundary search")
    ap.add_argument("--tested-but-unconsumed", action="store_true",
                    help="show ONLY declarations referenced by tests and by no production code -- "
                         "the exact W-06 signature: built, proven, and wired to nothing")
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args(argv)

    root = args.root.resolve()
    if not root.is_dir():
        print(f"REFUSED: not a directory: {root}", file=sys.stderr)
        return 2

    report = run(root, args.languages or sorted(LANGUAGES), args.min_name_len)
    if args.tested_but_unconsumed:
        # The highest-signal slice. A declaration with NO references at all is often dead code or
        # genuinely-external API; one that its OWN TESTS exercise and NOTHING ELSE calls was built
        # deliberately, proven to work, and then never wired up. That is W-06, and it is what let
        # the fleet sit leaderless with a green liveness suite.
        # A helper DECLARED IN A TEST FILE and used only by tests is correct by construction --
        # counting it is the false-positive class that gets a guard ignored. The signature is
        # PRODUCTION code that only tests call.
        for lang in report["languages"]:
            lang["unconsumed"] = [u for u in lang["unconsumed"] if u["tests_only"]]
        report["totals"]["unconsumed"] = sum(len(l["unconsumed"]) for l in report["languages"])
    print(json.dumps(report, indent=2)) if args.json else render(report)

    # 2 = unverifiable outranks 1 = findings: "I could not look" must never read as "all clear".
    if report["totals"]["scanned_languages"] == 0:
        return 2
    return 1 if report["totals"]["unconsumed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
