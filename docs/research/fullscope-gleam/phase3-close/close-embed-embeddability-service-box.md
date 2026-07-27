<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T067 close-embed-embeddability-service-box` (b3-c1-039)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Paired verify**: `verify-embed-embeddability-service-box.md` (ABSENT / requirements-promoted)
**Nature**: requirements-level close — **no code work** (per verify §Activation). Hands a ratified,
traced requirements set to the Wave-4 `build-yngenios-embeddability` (b3-c2-047).

## Ratification

The near-term missing artifact the verify flagged (**A1 service-box contract, "ABSENT — no file"**)
now **EXISTS and is RATIFIED**: `specs/059-full-scope-gleam-glp-implementation/contracts/service-box.contract.md`
(Option C full wiring; yngenios S1/S2/S3/spine delivery frame over the shared mailbox binding;
SC-008 acceptance block). The verify's "ABSENT" is **superseded** by that file. This close ratifies it
as the FE/BE process-boundary + host-embedding payload, reusing the frozen ED-1 envelope (A3) and the
`StepOutcome` single-reduction seam (A4).

## Requirement dispositions (P7 dossier §4 checklist — 21 items)

| Item | Requirement | Tag | Disposition |
|---|---|---|---|
| A1 | Ratify service-box contract | build-bound | **DONE** — contract exists + ratified here |
| A2 | Service-box API on engine facade (engine.gleam) | build-bound | build-bound → b3-c2-047; anchors on delivered engine-value API |
| A3 | Value surface = ED-1 result seam (0x01/0x11 envelope) | reuse | DELIVERED (verify-codec b3-c1-011) |
| A4 | Host-driven stepping at StepOutcome seam | reuse | DELIVERED (freeze-engine-runtime) |
| **A5** | **store_put/store_get kernels vs host-owned log** | **gate** | **CARRIED FORWARD — engineer decision, never team-resolved (FR-010); recommendation below** |
| B1–B4 | QHSM packaging (QActive, lifecycle HSM, Q-internal, Quiescent-only snapshot) | build-bound | build-bound → b3-c2-047 (dossier §2.3) |
| B5 | M1 = Option B (port/FFI ~5-state supervisor); Option A only at M2/ACID | gate | build-bound decision recorded: **Option B for M1** |
| C1–C2, C4 | YngeniOS attach (AOK_PURE_ACTOR, QP/AOK port, beacon-wrapper PAT-01..04) | build-bound | build-bound → b3-c2-047 (dossier §3) |
| C3 | Embedding path: PATH-A native (plain-BEAM) vs PATH-B guardian (AtomVM) | gate | target-dependent; carried forward |
| C5 | Realization target = `olamnit/Olamnit.Kernel` (DurableQF/GlpUnit/WAL) | reuse | recorded as preferred target |
| **D1–D6** | Build-blocking gates | gate | **Recorded as preconditions handed to b3-c2-047 (see below)** |

## A5 store-kernel scope — carried to the engineer (recommendation)

Per FR-010 (`spec.md:143`) and the contract's "Store-kernel scope (escalated, not team-resolved)"
section, this is **not resolved here**. Recommendation for the engineer's Wave-4 call:

- **Recommended: host-owned log.** Rationale: (1) G3-A ruling delivers the feature *inside* the
  yngenios architecture where the durable store/WAL is host-side (the CRDT store is C#, per T090 /
  `Olamnit.Kernel` DurableQF+GlpUnit per C5); (2) Language Authority (DISCIPLINE §1.14 / IV-a) forbids
  adding new GLP body kernels without explicit owner approval — a host-owned log needs no language
  extension, whereas `store_put`/`store_get` kernels *do*; (3) FR-010's end-to-end object-PUT path runs
  *across the fabric* (host services via the service-box API), consistent with host-owned storage.
- **Alternative: `store_put`/`store_get` kernels** — first-class GLP body kernels; couples the pure
  engine to durable I/O and **requires an owner §1.14 language-authority approval**.

Engineer picks `host-owned log` or `kernels`; the Wave-4 build binds to that choice.

## D1–D6 build-blocking preconditions (handed to b3-c2-047)

D1 diana docs ABSENT (product-altitude stack provisional) · D2 AOK-OS Draft (prefer `Olamnit.Kernel`
until owner-gated kernel-verification) · D3 in-process `libAtomVM` FFI not grounded (spike a
`generic_unix` `.avm` file-seam round-trip first) · D4 ED-6 float-decode on AtomVM unverified · D5
FB-M1-40 parity reference line unpinned · D6 beacon PAT-01..04 sources outside the sanctioned read set.
Each must clear (or be owner-ruled) before the dependent build step commits.

**Close status: CLOSED** — requirements ratified + traced to the P7 dossier + marked build-bound;
A5 store-kernel scope + C3/D1–D6 gates carried forward to the engineer / `build-yngenios-embeddability`
(b3-c2-047) as explicit unresolved decisions, per FR-010's "never team-resolved" discipline.
