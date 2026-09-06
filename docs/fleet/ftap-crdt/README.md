# FTAP clause CRDT

`clauses.jsonl` is the truth. `../FTAP-CONSOLIDATED.md` is **derived** — never hand-edit it.

    python build_clauses.py                      # regenerate clauses.jsonl from source
    python render_ftap.py > ../FTAP-CONSOLIDATED.md
    python render_ftap.py shuffled.jsonl         # same bytes — the fold is order-independent

**Why a CRDT and not a v19.** `olamnit-yngraw` measured 44 distinct rival copies of this one
artifact — 4,080 files, 271.6 MB, 18 chain versions, +17.6 KB per version monotonic — because
"without summarisation or compression" was read as *re-embed the whole predecessor*. Content
preservation and ancestor duplication are not the same thing. Here losslessness is carried by a
grow-only clause set merged union-by-id, so adding a requirement appends one record and two lanes
editing different clauses merge instead of forking.

**To change the plan:** append a clause with a higher `hlc`, re-render, append your ballot, publish
to every reachable coop root and assert the root COUNT against the probed-reachable union.

Verified properties (measured, not asserted): two renders are byte-identical; a shuffled op-log
renders byte-identically.
