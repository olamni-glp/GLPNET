<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# BK-STD-1 — NOT-CLOSED EPICS AND FEATURES — `ariellas` / `glpnet`

```
HOST     ARIELLAS 192.168.0.142      LANE  glpnet      REPO  D:/BSTDEV/research/glp/GLPNET
AT       2026-09-04T22:10Z
SOURCE   ariellas__glpnet__20260904T215650Z.json   (sync round 72)
METHOD   folded from the export `heads` array, NOT from `buildkit-roadmap status`
         (status under-reports: it renders only epic-bound features, and 18 of the 36
         below are epic-less)
```

## Totals

| scope | count | vs 17:00Z (round 71) |
|---|---|---|
| epics | 21 | = |
| features (all) | 131 | +7 |
| features CLOSED | 95 | +1 |
| **features NOT-CLOSED** | **36** | **+6** |

| state | count | change |
|---|---|---|
| analyzed | 2 | = |
| implemented | 3 | = |
| **reviewed** | **1** | **+1** - this session's fix |
| specified | 5 | = |
| promoted | 24 | +5 |
| captured | 1 | = |

**The +6 is peer work arriving, not local drift.** Round 72's import applied 102 records from 22 new
peer files; five of the six new not-closed features came in from other hosts' roadmaps, and one
(`stable-federation-identity...`) is this lane's. Nothing was closed by the import.

---

## The 36 not-closed features

| # | state | WSJF | RICE | feature | epic | spec |
|---|---|---|---|---|---|---|
| 1 | analyzed | 2.00 | 738 | Full-scope Gleam GLP implementation | Full Gleam implementation | `specs/059-full-scope-gleam-glp-implementation` |
| 2 | analyzed | 0.85 | 62 | Wave6 consolidation | Roadmap sweep 2026-07 consoli… | `specs/066-wave6-consolidation` |
| 3 | implemented | 7.80 | 1173 | Verification receipts and loud failure (no check may pass w… | — *(epic-less)* | `specs/078-verification-receipts` |
| 4 | implemented | 5.33 | 2667 | madGLP writer-reader address-discipline closure (N/N+1 audi… | Issue-backlog root-cause clos… | `specs/079-madglp-writer-reader-discipline` |
| 5 | implemented | 4.00 | 252 | QR-code link + cert provisioning via generated PDF or hub d… | Distributed GLP connectivity | `specs/067-qr-link-provisioning` |
| 6 | reviewed | 34.00 | 4800 | Stable federation identity: persisted QUIC keypair so SPKI … | — *(epic-less)* | — |
| 7 | specified | 7.00 | 5400 | bk-onrestart per-host configurable auto-installable fleet r… | — *(epic-less)* | `specs/085-onrestart-fleet-resume` |
| 8 | specified | 6.50 | 1700 | glptutorial corpus-golden reconciliation (stale goldens + d… | Issue-backlog root-cause clos… | `specs/083-glptutorial-corpus-goldens` |
| 9 | specified | 6.00 | 2000 | Occurs-checked substitution pipeline (compiler bind-time oc… | GLP compiler robustness (occu… | `specs/080-occurs-checked-substitution` |
| 10 | specified | 4.25 | 2625 | Coordination feature-stream durable superset fix — automate… | Issue-backlog root-cause clos… | `specs/082-feature-stream-superset` |
| 11 | specified | 2.62 | 900 | YNET--consolidation | — *(epic-less)* | `specs/065-ynet-consolidation` |
| 12 | promoted | 5.20 | 810 | YNET minted lane identity: address-independent ids, Resolve… | — *(epic-less)* | — |
| 13 | promoted | 4.80 | 3600 | Renderers must read the signed-export heads fold, never bui… | Issue-backlog root-cause clos… | — |
| 14 | promoted | 3.62 | 3938 | CPM-CRDT: cross-ecosystem package state, version history, c… | Fleet interconnectivity and o… | — |
| 15 | promoted | 3.60 | 3000 | Front-end goal-term acceptance completeness (parser + REPL … | Issue-backlog root-cause clos… | — |
| 16 | promoted | 3.60 | 960 | Per-host toolchain and environment contract (declared, mach… | — *(epic-less)* | — |
| 17 | promoted | 3.38 | 368 | Fleet Central Package Management over a CRDT record (BK-CPM… | — *(epic-less)* | — |
| 18 | promoted | 3.00 | 3600 | Cross-repo cross-host ERA TAKT: CRDT schema, closed repo-sl… | Fleet interconnectivity and o… | — |
| 19 | promoted | 3.00 | 680 | Multi-host state discipline (reversible states, untracked d… | — *(epic-less)* | — |
| 20 | promoted | 3.00 | 3200 | Persist takt and per-phase token use to the DuckLake and se… | Issue-backlog root-cause clos… | — |
| 21 | promoted | 2.88 | 394 | GLP REPL front/middle/back separation over the ynet transpo… | — *(epic-less)* | — |
| 22 | promoted | 2.62 | 625 | 041 cross-runtime and two-host acceptance completion (T055 … | Distributed GLP connectivity | — |
| 23 | promoted | 2.62 | 578 | Seam specification: normative contracts at every trust, lif… | — *(epic-less)* | — |
| 24 | promoted | 2.62 | 277 | Consolidated hardening spine: full hardened specify-design-… | — *(epic-less)* | — |
| 25 | promoted | 2.62 | 312 | Scheduler feature-stream durable healing and hardening (the… | Issue-backlog root-cause clos… | — |
| 26 | promoted | 2.60 | 540 | Single source of truth: one authority per subject, provenan… | — *(epic-less)* | — |
| 27 | promoted | 2.46 | 512 | /yx-ypm — the Yngenios Package Manager (uniform cross-langu… | — *(epic-less)* | — |
| 28 | promoted | 2.40 | 420 | crdtmsg post-MVP completion (COSE_Sign1 wrapper + 1.14-gate… | Issue-backlog root-cause clos… | — |
| 29 | promoted | 2.00 | 400 | buildkit coordination optimisation (GEPA/DSPy) — coop, sche… | — *(epic-less)* | — |
| 30 | promoted | 1.85 | 138 | iroh tier-0 QUIC provider: vendored Rust iroh/quinn behind … | — *(epic-less)* | — |
| 31 | promoted | 1.62 | 692 | Distributed unification + quiescence protocol (two-runtime,… | Distributed GLP connectivity | — |
| 32 | promoted | 1.38 | 400 | YNET mobile background/battery-budget scheduling policy | YNET overlay — deferred BUILD… | — |
| 33 | promoted | 1.23 | 86 | GLP REPL front/middle/back separation with a YNGENIOS-app t… | — *(epic-less)* | — |
| 34 | promoted | 1.23 | 240 | Product-defect burn-down with regression proof (no defect c… | — *(epic-less)* | — |
| 35 | promoted | 1.23 | 185 | YNET human-memorable decentralized-naming resolver | YNET overlay — deferred BUILD… | — |
| 36 | captured | — | — | Oracle-managed elastic lane pool: converge the two onrestar… | — *(epic-less)* | — |

---

## Two defects visible in this table, stated rather than left for a reader to trip over

1. **`stable-federation-identity...` (row 6) carries NO `spec` pointer, and `specs/103-stable-federation-identity/` exists.**
   `buildkit-roadmap link` refuses it: *"is 'reviewed', past 'specified'. Linking only moves
   promoted -> specified."* The pointer is therefore unwritable for any feature whose implementation
   preceded its spec dir - which is exactly what an urgent defect fix looks like. **The artifact is
   real; the roadmap simply cannot be told about it.** Recorded, not worked around.

2. **The round-72 barrier reports `5/4 hosts` - `ariellas, gavriella, gavriellas, olamnit, shiras`.**
   `gavriella` and `gavriellas` are almost certainly one host counted twice. A roster that exceeds
   its own expected count is not "satisfied", it is **mis-keyed**, and this is the third independent
   sighting of address/name-keyed peer identity producing a wrong census on this fleet. It is the
   same argument for keying peers by `nodeId = SHA-256(SPKI)` that today's broadcast makes from the
   other end.

---

**`@ariellas-glpnet` - BK-STD-1 - 2026-09-04T22:10Z**
