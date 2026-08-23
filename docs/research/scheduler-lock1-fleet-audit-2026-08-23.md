# SCHED-R5 — Lock 1 measured on every board on this host

**Date:** 2026-08-23 · **Host:** GAVRIELLA · **Method:** patched `audit_board` (FR-011),
read-only, over `D:/coop/*/sched`. Companion to
`scheduler-feature-stream-rootcause-2026-08-22.md`, whose Scope note required exactly this
before the fix is called fleet-wide: *"the same three links should be checked on every board
before the fix is called fleet-wide — that verification is **not** done here and must not be
assumed."* It is done here.

> **Revision 2 (same day) — the first figures published in this file were WRONG and are
> withdrawn.** They counted every historical `allocate` op instead of folding to the current
> addressee per WP, and they classified the pool from a hard-coded token list. A codex review
> (`20260823T022313Z`) caught both. Every number below is the corrected measurement; the
> differences are large and they change the conclusion for three boards.

## What changed in the reading

Lock 1 was `missing_proposed_actor > 0` — a presence test. FR-011 classifies the *current*
addressee instead: blank / **pool** / **unknown here** / real, where

- **current** means the last `allocate` per `wp_id` in R2 order — the same "last proposal wins"
  rule `confirm._addressing` admits on. Counting history instead made Lock 1 *uncloseable*: a WP
  minted to the pool and later reallocated kept its pool row for ever.
- **pool** is derived from the board's own ops, not guessed: `ingest` stamps
  `payload.roadmap_slot` on what it mints, so the addressee of such an op *is* that board's pool
  actor. `--pool-actor` is configurable, and a hard-coded vocabulary silently misses a renamed
  pool.

## Measurement

| board | ops | WPs | blank | pool | unknown | Lock 1 | Lock 2 | derived pool actor |
|---|---:|---:|---:|---:|---:|---|---|---|
| buildkit | 146 | 73 | 0 | 0 | 0 | **closed** | closed | |
| crucible | 128 | 104 | 49 | 40 | 0 | open | closed | |
| glpnet | 33 | 28 | 0 | **22** | 0 | open | closed | |
| hatzinor | 36 | 35 | 16 | 18 | 0 | open | closed | |
| lejepa | 42 | 35 | 0 | **30** | 2 | open | closed | **`ariellas-lejepa`** |
| mstack | 86 | 49 | 14 | 5 | 0 | open | closed | |
| olamnit-assistant | 109 | 37 | 31 | 0 | 0 | open | closed | |
| olamnit | 11 | 11 | 11 | 0 | 0 | open | closed | |
| ospark | 22 | 10 | 0 | 0 | 0 | **closed** | closed | |
| qhstate | 81 | 80 | 39 | 36 | 0 | open | closed | |
| tefl | 37 | 33 | 19 | 10 | 0 | open | closed | |
| yngenios-research | 102 | 46 | 6 | 0 | **3** | open | **open** | |
| yngenios-windows | 30 | 28 | 0 | **27** | 0 | open | closed | |
| yngenios | 0 | 0 | 0 | 0 | 0 | UNMEASURED | UNMEASURED | |

## What it says

- **Three boards were reading a false all-clear on Lock 1** — `glpnet` (22 of 28),
  `yngenios-windows` (27 of 28) and **`lejepa` (30 of 35)**. All three have zero blanks, so the
  old presence test found nothing to report while nearly all their work sat unowned.
- **`lejepa` is the live proof that a hard-coded pool vocabulary is not enough.** Its pool actor
  is `ariellas-lejepa`, which no built-in list would contain; before the fix its 30 unowned WPs
  fell through to *unknown*, which is reported but deliberately **not** gated — the false green
  in a new costume.
- **`buildkit` is healthy, and the earlier revision said otherwise.** Its 73 blank allocations
  are all superseded by later addressed ones; folding to the current addressee leaves 0 blank
  and 0 pool across 73 WPs. Judging on history alone libels a board that has done the work.
- **`ospark` is likewise clean** — 10 WPs, all addressed to real actors.
- **Pool-addressed work is widespread but not universal:** 8 of 14 boards carry it, 188 WPs.
- **`yngenios-research` is the only board with Lock 2 open** (`e_t_s <= 0`), and it carries 3
  addressees unknown to its own board. `lejepa` carries 2. Reported, not gated — that is what a
  first address to a newly-onboarded host looks like — but worth a human glance.
- **`yngenios` is empty and stays UNMEASURED** — an empty board has been shown empty, not
  healthy.

## Bearing on `enable-and-deploy-other-hosts`

This measures 14 boards **as visible from this host**, on the shared substrate. It establishes
nothing about the engines installed on peer hosts. The fix is branch-local
(`086-sched-r3-placeholder-addressee`, pushed, not merged) and the two-repo ship ruling is owed.
