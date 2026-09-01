# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

"""Compare two receipt documents for cross-emitter parity (T044 half B).

WHAT IS ALLOWED TO DIFFER, AND WHY EXACTLY THIS MUCH. The two emitters write into
two different roots, so ``verdict_pointer`` legitimately differs — it is compared
by its LAST THREE PATH SEGMENTS (``<area>/<run-id>/<check>.receipt.json``), which
is the part the contract actually specifies. ``ran_at`` is pinned identically by
the caller, so it is compared like any other field; if it ever stops matching,
that is a real divergence and must fail.

NOTHING ELSE IS NORMALISED. A comparison that quietly ignores a field it finds
inconvenient is the same defect as a check that never ran: it would report parity
it did not establish. So an unexpected key on EITHER side is a failure, and the
diff names every differing field rather than reporting a bare boolean.

Exit codes: 0 identical · 1 divergent · 2 unreadable.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

#: Compared by tail rather than in full, because the roots differ by design.
PATH_FIELDS = ("verdict_pointer",)


def tail3(value: str) -> str:
    parts = Path(str(value).replace("\\", "/")).parts
    return "/".join(parts[-3:])


def normalise(doc: dict) -> dict:
    out = dict(doc)
    for field in PATH_FIELDS:
        if field in out:
            out[field] = tail3(out[field])
    return out


def diff(a: dict, b: dict) -> list[str]:
    problems: list[str] = []
    for key in sorted(set(a) | set(b)):
        if key not in a:
            problems.append(f"{key}: absent from bash receipt, present in python")
        elif key not in b:
            problems.append(f"{key}: present in bash receipt, absent from python")
        elif a[key] != b[key]:
            problems.append(
                f"{key}: bash={json.dumps(a[key])[:160]} != python={json.dumps(b[key])[:160]}"
            )
    return problems


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print("usage: parity_compare.py <bash-receipt> <python-receipt>", file=sys.stderr)
        return 2
    try:
        a = normalise(json.loads(Path(argv[1]).read_text(encoding="utf-8")))
        b = normalise(json.loads(Path(argv[2]).read_text(encoding="utf-8")))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"parity: unreadable receipt ({exc})", file=sys.stderr)
        return 2

    problems = diff(a, b)
    if problems:
        for p in problems:
            print(f"    parity divergence: {p}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
