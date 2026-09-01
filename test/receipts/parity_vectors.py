# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

"""Write the seven conformance vectors with the PYTHON emitter (T044 half A).

The inputs here MUST be the same inputs ``assert.sh`` feeds the bash emitter, and
they are deliberately the same ones ``codeconv/tests/faultinj/conformance.py``
drives — so the parity run compares the two emitters on the fixture's own case
set rather than on a private set invented for the comparison.

``ran_at`` is pinned from the caller so the only remaining legitimate difference
between the two documents is ``verdict_pointer`` (the roots differ), which
``parity_compare.py`` normalises. Everything else must match exactly.

Usage: ``parity_vectors.py <root> <run-id> <ran-at>``
"""

from __future__ import annotations

import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "codeconv" / "src"))

from codeconv.receipts import Target, bind, emit  # noqa: E402


def main(argv: list[str]) -> int:
    if len(argv) != 4:
        print("usage: parity_vectors.py <root> <run-id> <ran-at>", file=sys.stderr)
        return 2
    root, run_id, ran_at = argv[1], argv[2], argv[3]
    common = dict(area="reference", run_id=run_id, root=root, ran_at=ran_at)

    emit(check_id="conformance.pass",
         target=Target("path", "t", resolved=True),
         examined_count=5, total_count=5,
         examined=["a", "b", "c", "d", "e"], **common)

    emit(check_id="conformance.empty",
         target=Target("path", "t", resolved=True),
         examined_count=0, total_count=0, **common)

    emit(check_id="conformance.unread",
         target=Target("path", "t", resolved=True),
         examined_count=1, total_count=3, examined=["a"], **common)

    emit(check_id="conformance.unsearchable",
         target=Target("path", "t", resolved=False, unresolved_reason="target absent"),
         examined_count=0, total_count=None, **common)

    emit(check_id="conformance.fail",
         target=Target("path", "t", resolved=True),
         examined_count=5, total_count=5,
         examined=[f"item-{i}" for i in range(1, 6)],
         problems=["a problem"], **common)

    n = bind.MAX_ENUM + 7
    emit(check_id="conformance.bounded",
         target=Target("path", "t", resolved=True),
         examined_count=n, total_count=n,
         examined=[f"item-{i}" for i in range(n)], **common)

    emit(check_id="conformance.overridden",
         target=Target("path", "t", resolved=True),
         examined_count=1, total_count=1, examined=["a"],
         override={
             "area": "reference",
             "check": "conformance.overridden",
             "reason": "conformance fixture exercises the recorded-override case",
             "briefing": "contract F1 requires the fixture to drive an overridden case",
             "rationale": "demonstrates an override remains visible in the emitted receipt",
             "acknowledged": True,
             "expiry": "2099-01-01T00:00:00+00:00",
         },
         **common)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
