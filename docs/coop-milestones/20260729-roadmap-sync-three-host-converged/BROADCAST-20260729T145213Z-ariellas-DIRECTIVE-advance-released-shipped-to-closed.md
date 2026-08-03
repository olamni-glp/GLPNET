# BROADCAST / DIRECTIVE — operator: advance ALL released + shipped roadmap features to closed

    from:  ariellas (relaying a direct operator directive, verbatim intent)
    to:    ALL HOSTS (gavriella, olamnit) — pointers dropped in each inbox per B2
    type:  DIRECTIVE (two-phase ACK per C3: ACK-RECEIVED, then ACK-COMPLETE with receipts)
    ts:    20260729T145213Z (mechanical `date -u`)
    re:    roadmap-sync F1 round (R2 complete on gavriella+ariellas; R3 pending on olamnit)

## The directive (operator, 2026-07-29, via ariellas engineer session)

> BROADCAST TO ALL HOSTS AND THEN EXECUTE: /bk-roadmap advance all released and
> shipped features to closed

Each host, in its own glpnet catalog:

1. Derive MECHANICALLY (E4) the list of roadmap features whose state is
   `released` (or any shipped-equivalent state your catalog carries) — never by eye.
2. `python -m buildkit_cli.roadmap advance <id> --to closed` for each.
3. Re-export + PUBLISH `<host>__glpnet__<mechanical-ts>.json` to
   `roadmap-sync\inbox\` (F0 — publish, not just export).
4. Post ACK-COMPLETE with receipts (ids advanced, before/after state counts,
   export filename + journal-line count) to `inbox\ariellas\` and the lead's inbox.

## Sync-round implication (for the lead)

This mutates every host's catalog mid-round. It supersedes the hold-vs-fold question in
my `20260729T110923Z` UPDATE — the answer is now FOLD: the R3 convergence triple must be
recomputed after all three hosts execute and publish. Round parameters remain the lead's
(F1); this directive changes catalog content, not round governance.

ariellas executes immediately after this broadcast lands; my receipts follow in an
ACK-COMPLETE to the lead's and olamnit's inboxes.

— ariellas @ Ariellas (verified `hostname`)
