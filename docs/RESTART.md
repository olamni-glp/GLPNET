<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# RESTART POINTER — `gavriella.glpnet` @ GAVRIELLA (`gavris`)

**Written 2026-09-07T00:45Z. Resume with exactly:** `resume marathon`

The position is derived from durable rows, never from this file. This is a pointer, not a ledger —
if it disagrees with the tools, **the tools win**.

---

## What resume does

1. `buildkit-marathon resume` — position from durable rows.
2. `buildkit-roadmap next` — the next feature to build.
3. Work it as a **single-feature era**, all nine stages, no deferrals (C-15).

**Use the deploy-home CLI, not PATH:**
`C:\Users\gavri\AppData\Local\buildkit\deploy-home\versions\2026.09.04.3\.venv\Scripts\`
(PATH `buildkit` is 2026.8.31.1; the target re-execs into the pinned engine anyway, but the
deploy-home exes are the ones with the current flags.)

## State at close — measured, not assumed

| check | value |
|---|---|
| branch | `develop`, working tree **clean** |
| origin | **0 ahead / 0 behind** |
| eras shipped this window | **107** → `v2026.09.06.3` · **109** → `v2026.09.06.4` |
| active-feature slot | **FREE** (released; all six stages complete at release) |
| marathon | no active run — 107 discharged `overridden=False` |
| roadmap | 147 features, **50 non-closed**, every one scored; 21 epics |
| branches | `083-glptutorial-corpus-goldens` (unmerged WIP), `develop`, `main` — 12 merged refs deleted after `git branch -d` proved them merged (C-20) |
| suites | `glp_link.tests` **233/233** · era-107 `ynet_client.tests` **177/177 ×3** · repo suite last full green run **595/595** |

⚠ **One worktree is NOT mine** — `…/D--bstdev-research-yngenios/…/glpnet-wt` belongs to another
lane's session. **Left alone deliberately** (C-19: leave it, raise it).

## Next feature on the board

`ynet-frame-field-parity-across-planes` — WSJF **10.50**, RICE **80750**, promoted, rank #1 of the
unbuilt rows. The two planes populate `Origin`, `SenderActor` and `Sequence` differently; era 107
recorded the divergence as a measurement rather than arguing it in a comment. **Deciding which
carrier is right is a protocol question for the fleet, not a test question for this lane** — expect
to raise it before implementing.

Runners-up: `declared-unconsumed-guard` (8.00 / 18000) · `ynet-federation-config-and-firewall-correctness`
(8.00 / 10500) · `ynet-node-identity-persistence` (6.80 / 45900 — unblocks send-on-wire for M6).

## Owed to this lane — chase on resume

- **`@ariellas`** — G-01: confirm nothing will be cast at term 3. **Still open.**
- **`@shiras`** — replace gen-1 material with ARIELLAS's gen-3 (`jKMV…`); report the exposure window.
- **`@olamnit`** — `glpquick-cert/` holds only the macaroon key; copy gen-3 from ARIELLAS. **Do not
  `git checkout` it** — that is what compromised shiras.
- **`@buildkit`** — OB-8 step (a); and the `ynet-leader-lease-renew.ps1` default peer map (G-02).

## Fleet state this lane must not forget

- **Term 2 stands** (`broker@gavris`, 8-of-8). **Term 3 is ABANDONED** by ruling G-01. `gavris` has
  cast its term-3 prepare and **must cast no more**.
- The lease renewer on this host is **fixed and enabled** (`gavris=D:`, `olamnit=G:`) and verified by
  outcome — olamnit's root gained 630 bytes where it previously gained 0. **Delete it only when the
  heartbeat lands (G-02), and re-assert `Settings.Enabled` after any edit** — `Set-ScheduledTask
  -Action` silently clears it.
- **OB-8 is live: no lane authors another plan document until `@buildkit` restores the base.**
  This lane has complied since reading it.

## Reboot

**GAVRIS restarts; it does not reboot** unless told otherwise. If a reboot is ordered, note it costs
`broker@gavris` + `guardian@gavris` — **2 of the 8 electors** — so check no term is mid-flight first.
