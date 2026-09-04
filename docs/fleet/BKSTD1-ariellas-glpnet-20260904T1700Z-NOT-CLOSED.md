<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# BK-STD-1 — NOT-CLOSED EPICS AND FEATURES — `ariellas` / `glpnet`

```
HOST     ARIELLAS 192.168.0.142      LANE  glpnet      REPO  D:/BSTDEV/research/glp/GLPNET
AT       2026-09-04T17:00Z
SOURCE   ariellas__glpnet__20260904T153315Z.json
METHOD   folded from the export `heads` array, NOT from `buildkit-roadmap status`
         (status under-reports: it renders only epic-bound features)
```

## Totals

| scope | count |
|---|---|
| epics | 21 |
| features (all) | 124 |
| features CLOSED | 94 |
| **features NOT-CLOSED** | **30** |

| state | count |
|---|---|
| analyzed | 2 |
| implemented | 3 |
| specified | 5 |
| promoted | 19 |
| captured | 1 |

---

## The 30 not-closed features

| # | state | WSJF | RICE | feature | epic | spec |
|---|---|---|---|---|---|---|
| 1 | analyzed | 2.00 | 738 | Full-scope Gleam GLP implementation | Full Gleam implementation | `specs/059-full-scope-gleam-glp-implementation` |
| 2 | analyzed | 0.85 | 62 | Wave6 consolidation | Roadmap sweep 2026-07 consolida… | `specs/066-wave6-consolidation` |
| 3 | implemented | 7.80 | 1173 | Verification receipts and loud failure (no check may pass w… | — | `specs/078-verification-receipts` |
| 4 | implemented | 5.33 | 2667 | madGLP writer-reader address-discipline closure (N/N+1 audi… | Issue-backlog root-cause closur… | `specs/079-madglp-writer-reader-discipline` |
| 5 | implemented | 4.00 | 252 | QR-code link + cert provisioning via generated PDF or hub d… | Distributed GLP connectivity | `specs/067-qr-link-provisioning` |
| 6 | specified | 7.00 | 5400 | bk-onrestart per-host configurable auto-installable fleet r… | — | `specs/085-onrestart-fleet-resume` |
| 7 | specified | 6.50 | 1700 | glptutorial corpus-golden reconciliation (stale goldens + d… | Issue-backlog root-cause closur… | `specs/083-glptutorial-corpus-goldens` |
| 8 | specified | 6.00 | 2000 | Occurs-checked substitution pipeline (compiler bind-time oc… | GLP compiler robustness (occurs… | `specs/080-occurs-checked-substitution` |
| 9 | specified | 4.25 | 2625 | Coordination feature-stream durable superset fix — automate… | Issue-backlog root-cause closur… | `specs/082-feature-stream-superset` |
| 10 | specified | 2.62 | 900 | YNET--consolidation | — | `specs/065-ynet-consolidation` |
| 11 | promoted | 4.80 | 3600 | Renderers must read the signed-export heads fold, never bui… | Issue-backlog root-cause closur… | `—` |
| 12 | promoted | 3.62 | 3938 | CPM-CRDT: cross-ecosystem package state, version history, c… | Fleet interconnectivity and obs… | `—` |
| 13 | promoted | 3.60 | 3000 | Front-end goal-term acceptance completeness (parser + REPL … | Issue-backlog root-cause closur… | `—` |
| 14 | promoted | 3.60 | 960 | Per-host toolchain and environment contract (declared, mach… | — | `—` |
| 15 | promoted | 3.38 | 368 | Fleet Central Package Management over a CRDT record (BK-CPM-1) | — | `—` |
| 16 | promoted | 3.00 | 680 | Multi-host state discipline (reversible states, untracked d… | — | `—` |
| 17 | promoted | 3.00 | 3200 | Persist takt and per-phase token use to the DuckLake and se… | Issue-backlog root-cause closur… | `—` |
| 18 | promoted | 3.00 | 3600 | Cross-repo cross-host ERA TAKT: CRDT schema, closed repo-sl… | Fleet interconnectivity and obs… | `—` |
| 19 | promoted | 2.62 | 625 | 041 cross-runtime and two-host acceptance completion (T055 … | Distributed GLP connectivity | `—` |
| 20 | promoted | 2.62 | 578 | Seam specification: normative contracts at every trust, lif… | — | `—` |
| 21 | promoted | 2.62 | 277 | Consolidated hardening spine: full hardened specify-design-… | — | `—` |
| 22 | promoted | 2.62 | 312 | Scheduler feature-stream durable healing and hardening (the… | Issue-backlog root-cause closur… | `—` |
| 23 | promoted | 2.60 | 540 | Single source of truth: one authority per subject, provenan… | — | `—` |
| 24 | promoted | 2.40 | 420 | crdtmsg post-MVP completion (COSE_Sign1 wrapper + 1.14-gate… | Issue-backlog root-cause closur… | `—` |
| 25 | promoted | 2.00 | 400 | buildkit coordination optimisation (GEPA/DSPy) — coop, sche… | — | `—` |
| 26 | promoted | 1.62 | 692 | Distributed unification + quiescence protocol (two-runtime,… | Distributed GLP connectivity | `—` |
| 27 | promoted | 1.38 | 400 | YNET mobile background/battery-budget scheduling policy | YNET overlay — deferred BUILD-N… | `—` |
| 28 | promoted | 1.23 | 185 | YNET human-memorable decentralized-naming resolver | YNET overlay — deferred BUILD-N… | `—` |
| 29 | promoted | 1.23 | 240 | Product-defect burn-down with regression proof (no defect c… | — | `—` |
| 30 | captured | 0.00 | 0 | Oracle-managed elastic lane pool: converge the two onrestar… | — | `—` |

---

## Reconcile findings carried forward

- **75 of 124** roadmap features carry **no `spec_path`** and can never bind by basename.
- **9 pipeline records are UNBOUND** and therefore cannot move a roadmap state:
  - `036-http3-quic-ws-link`
  - `042-crdtmsg-verify-harden`
  - `043-xsd-schema-language`
  - `049-wave1-guard-link-acceptance`
  - `050-full-gleam-combined`
  - `050-glp-native-quic-link`
  - `064-post-wave-gap-closure`
  - `065-glp-runtime-consol`
  - `076-typechecker-body-atom-moding`
- **dedupe:** 123 live features scanned across id-stem and title strategies — **0 duplicate groups**.

---

## 🔴 ROADMAP-vs-REALITY DIVERGENCE — measured, and it changes six rows above

Six features are recorded at a **pre-implementation** roadmap state while their branch is **already
an ancestor of `origin/develop`** — i.e. the code landed and the roadmap never moved.

| # above | feature | roadmap state | branch | git reality |
|---|---|---|---|---|
| 10 | YNET consolidation | `specified` | `origin/065-ynet-consolidation` | **CONTAINED** (ahead=0, tip `d2ea81e9`) |
| 2 | Wave6 consolidation | `analyzed` | `origin/066-wave6-consolidation` | **CONTAINED** (ahead=0, tip `6abe40d2`) |
| 5 | QR-code link + cert provisioning | `implemented` | `origin/067-qr-link-provisioning` | **CONTAINED** (ahead=0, tip `fdc942a6`) |
| 3 | Verification receipts and loud failure | `implemented` | `origin/078-verification-receipts` | **CONTAINED** (ahead=0, tip `315e3be5`) |
| 8 | Occurs-checked substitution pipeline | `specified` | `origin/080-occurs-checked-substitution` | **CONTAINED** (ahead=0, tip `1ae6bf74`) |
| 9 | Coordination feature-stream superset fix | `specified` | `origin/082-feature-stream-superset` | **CONTAINED** (ahead=0, tip `f5be473a`) |

**Method:** `git merge-base --is-ancestor origin/<branch> origin/develop` for every origin head.

**I did NOT advance these states.** Two reasons, both deliberate:

1. **"Branch landed" is not "feature closed."** Closing requires `/bk-close` — retrospective plus
   action reconciliation. Advancing a roadmap state on a containment test alone would manufacture
   progress the pipeline never made.
2. **W23 (*"reconcile roadmap state against post-merge reality and advance any feature whose branch
   landed"*) carries `PREREQ W19`**, and W19 is gated on the unruled ref-deletion ownership question.
   Running W23 early would be running a step out of its recorded order.

**This table is W23's input, pre-computed.** When W19 clears, W23 is a mechanical pass over exactly
these six rows.

⚠️ **Note for other lanes:** row 8 is the same `080` whose marathon step W11 was recorded
**BLOCKED-ON-AN-ENGINEER-RULING for eleven days**. The roadmap said `specified`, the marathon said
`blocked`, and git said `landed`. **Three systems, three different answers, and only one of them was
measured.**
