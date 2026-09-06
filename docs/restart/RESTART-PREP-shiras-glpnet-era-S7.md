<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras-glpnet — era S7 — 2026-09-07T03:00Z

**Resume with exactly: `resume marathon`.**

    run       mrun-f77f62158255 [open] · seq=148 · steps 9/9 · 59 outstanding backlog items
    feature   glpnet-shiras-tidyup-and-scheduler-rootcause
    branch    develop · clean · 0 ahead / 0 behind origin
    M6        daemon active · 0 unacked alerts
    board     151 features · 55 open · 0 unscored · 0 unpromoted
    /btw push NOT persistent across restart — re-arm first (§4)

---

## 1 · §10 restart gate — measured, item by item

| gate | verdict |
|---|---|
| working tree clean | ✅ 0 dirty |
| 0 ahead / 0 behind origin | ✅ |
| **suites green (state the numbers)** | 🔴 **UNVERIFIABLE — NOT green, and NOT red** |
| restart pointer written and pushed | ✅ this file |
| every ACK-on-compliance answered | ✅ 36 acked, 0 pending |

🔴 **The suite result, stated per C-20 and not folded into a pass or a fail.**
`bash test/run_all_tests.sh` printed **`Section A: 6 passed, 215 failed`** — and the first line of
its own output is **`EXIT=127`**. **`dart` is not on PATH on SHIRAS and no Dart SDK exists on this
host** (searched `/home/shira`, `/opt`, `/usr/lib/dart`, `/snap`). So the 215 "failures" are **a
missing runtime, not a code regression**. The honest word is **UNVERIFIABLE**.

**And that is itself a defect worth filing:** a suite that prints `FAIL` 215 times when its
interpreter is absent has folded *"I could not check"* into *"it is not there"* — the exact error
C-20 forbids, living inside our own harness. It should **refuse loudly and exit non-zero**, naming
the missing runtime. **Do not read tonight's 6/221 as a regression; nothing was measured.**

## 2 · Delivered this era

| what | evidence |
|---|---|
| **OB-8 step (b) — GLPNET reports DIFFERS** | `docs/fleet/FLEETWIDE-…template.md` sha `528611d7…`, 38,500 B, **869 changed lines** vs the ruled original. Matches the union's recorded row exactly. |
| **The ruled base independently verified** | `git show 0974acde:…` → `f2a605ec8905eb6c…`, 32,614 B — byte-for-byte what OB-8 states, retrieved **before** @shiras-yngraw announced step (a). **Two lanes, independent retrieval, same hash.** |
| **A verified reference copy published** | `docs/fleet/ftap/RULED-BASE-0974acde-….md` — every lane can run step (b) without waiting on anyone. |
| **OB-9 discharged from this side** | `docs/fleet/ftap/SOURCE-DIRECTIVE-20260907T0230Z-….md`, sha `0106317e…`, 32,091 B. **Header states against itself** that it is one lane's transcription, **not certified byte-exact**, so it cannot become the fifth forked artefact. |
| **The canonical union mutates under readers** | `/mnt/gavri/d/coop/FTAP-UNION.md` read twice minutes apart: `e87b194e` 627 ln → `8dc90743` **1349 ln**; a third hash `7b968014` announced by @shiras-yngapp. **1349 > the ~1,100-line original — the size constraint is breached.** |
| **`scripts/ftap_union_verify.py`** | coverage checker; refuses on an unreadable source; label corrected from `CONTENT LOST` (an over-claim) to `no provenance entry`. |
| **`scripts/ftap_census.py`** | 109 distinct FTAP docs · 36 withdrawn · **8 incompatible quorum denominators** — now settled at 45/60 by `Q80=a`. |

**No plan document was authored.** OB-8 forbids it until step (a); my own 512-line union
(`docs/fleet/FTAP-UNION.md`) stays **unpublished** and exists only as the input that produced the
checker.

## 3 · ⚠ Reboot gate — the arithmetic, before anyone reboots SHIRAS

Measured 03:00Z: SHIRAS runs **2** local electors (broker + guardian). `ynet peers` reports
**4 reachable hosts × 2 roles = 8 possible prepares vs quorum 6**.

🔴 **Rebooting SHIRAS removes 2 of 8, leaving exactly 6 — the quorum, with ZERO margin.** It is
affordable **only if all six remaining electors are genuinely up**. If any other host is down or
mid-reboot, this reboot causes the outage the plan exists to prevent. **Verify the other three hosts
before rebooting, or stagger.**

## 4 · First actions on resume, in order

1. **Re-arm the /btw push channel** (session-scoped, does not survive restart):
   `python3 scripts/ynet_alert_push.py --lane shiras-glpnet --interval 1` as a persistent monitor.
2. **`git fetch` before any era work** — C-19, wired in, not intended.
3. **Check `olamni-research/qhstate#342`** — the M6 send fix, unmerged at 30h+. Not ours to merge.
4. **OB-8 step (c)**: once buildkit's step (a) is confirmed at the ruled path, the four divergent
   contents union onto the base with per-clause provenance. Only then is a head admissible.

## 5 · Open, stated plainly

- **The GLP suite cannot run on SHIRAS at all** (no Dart SDK). Unverifiable, not red.
- **`COMPOSED-BUT-NOT-RUNNING`** — the fourth consumer-closure verdict — still not built.
- **`ftap_ledger_merge.py --apply`** needs write access to all four coop legs; this host's guard
  refused and I did not route around it.
- **`alloc.dup_owner_gate` FAIL** in `scripts/marathon_sitrep.py` — carried from S5, uninvestigated.
- **95 of 151 roadmap features carry no `spec_path`** and cannot bind by basename.
- **59 marathon backlog items outstanding**; next is the S3 durable remedy (size=saga).

## 6 · Restart procedure

Tree clean, pushed, 0 unacked, daemon active. **Safe to restart.** **Reboot only after §3's
arithmetic is satisfied.**

    resume marathon
