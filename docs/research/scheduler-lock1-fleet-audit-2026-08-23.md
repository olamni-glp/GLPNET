# SCHED-R5 — Lock 1 measured on every board on this host

**Date:** 2026-08-23 · **Host:** GAVRIELLA · **Method:** patched `audit_board` (FR-011),
read-only, over `D:/coop/*/sched`. Companion to
`scheduler-feature-stream-rootcause-2026-08-22.md`, whose Scope note required exactly this
before the fix is called fleet-wide: *"the same three links should be checked on every board
before the fix is called fleet-wide — that verification is **not** done here and must not be
assumed."* It is done here.

## What changed in the reading

Lock 1 was `missing_proposed_actor > 0` — a presence test. FR-011 classifies the addressee
instead: blank / **pool** (`unassigned` and kin) / **unknown here** / real. Pool-addressed work
now opens Lock 1, because it is legitimately minted but not *dispatchable*.

## Measurement

| board | allocs | blank | pool | unknown | Lock 1 before | Lock 1 after |
|---|---:|---:|---:|---:|---|---|
| buildkit | 146 | 73 | 0 | 0 | open | open |
| crucible | 128 | 66 | 45 | 0 | open | open |
| **glpnet** | 33 | 0 | **26** | 0 | **CLOSED — false all-clear** | **open** |
| hatzinor | 36 | 17 | 18 | 0 | open | open |
| lejepa | 42 | 5 | 0 | **2** | open | open |
| mstack | 86 | 44 | 5 | 0 | open | open |
| olamnit-assistant | 109 | 100 | 0 | 0 | open | open |
| olamnit | 11 | 11 | 0 | 0 | open | open |
| ospark | 22 | 0 | 0 | 0 | closed | closed |
| qhstate | 81 | 39 | 36 | 0 | open | open |
| tefl | 37 | 19 | 14 | 0 | open | open |
| yngenios-research | 102 | 94 | 0 | 0 | open | open |
| **yngenios-windows** | 30 | 0 | **28** | 0 | **CLOSED — false all-clear** | **open** |
| yngenios | 0 | 0 | 0 | 0 | UNMEASURED | UNMEASURED |

## What it says

- **Two boards were reading a false all-clear**, not one: `glpnet` (26 of 33) and
  **`yngenios-windows` (28 of 30)** — both had zero blanks, so the presence test found nothing
  to report while nearly all their work sat in the pool with no owner. `yngenios-windows` was
  not previously known to be affected.
- **Pool-addressed work is widespread but not universal:** 8 of 14 boards carry it, 172
  allocations in total. On the other 6 boards Lock 1 was already open on blanks, so FR-011 adds
  precision there rather than a flip — the fix is worth propagating, but it is not the whole
  remedy anywhere except those two boards.
- **`ospark` is the only genuinely healthy board** — 22 allocations, every one addressed to a
  real actor, both locks closed.
- **`lejepa` carries 2 addressees unknown to its own board.** Reported, deliberately not gated
  (it is what a first address to a newly-onboarded host looks like). Worth a human glance.
- **`yngenios` is empty and stays UNMEASURED** — an empty board has been shown empty, not
  healthy.
- **`yngenios-research` is the only board with Lock 2 open** (`e_t_s <= 0`).

## Bearing on `enable-and-deploy-other-hosts`

This measures 14 boards **as visible from this host**, on the shared substrate. It does **not**
establish anything about engines installed on the peer hosts, and no fix has been propagated:
the change is branch-local (`086-sched-r3-placeholder-addressee`, unpushed) and the two-repo
ship ruling is still owed.
