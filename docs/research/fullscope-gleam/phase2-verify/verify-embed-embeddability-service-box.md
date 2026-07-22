<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-embed-embeddability-service-box` (WP b3-c1-014, wave 2)

**Date**: 2026-07-22
**Method**: repo-wide existence check (source-verification only — this is a requirements-level WP with no embeddability code to execute) + extraction of the P7 QHSM/YngeniOS packaging dossier into a concrete build-bound checklist.
**Paired close**: `close-embed-embeddability-service-box` (b3-c1-039) — **activated**; the §4 checklist below is its input, to be ratified into a service-box/store-kernel requirements contract.
**Backing detail_ids**: `embeddability-service-box`, `qhsm-yngenios-integration-design`.

## Environment / commands run

- `rg -in 'yngenios|embeddab|service-box|store_put|store_get' specs/ glp_gleam/ gleam_quic/` → **61 matches / 39 lines / 6 files, all under `specs/`; 0 in `glp_gleam/`, 0 in `gleam_quic/`.**
- `find specs -ipath '*embed*'` and `-ipath '*service-box*'` → **no spec dir**. No service-box contract file anywhere (only prose in spec-059 + the roadmap/inventory docs).
- `ls glp_gleam/src/glp/engine.gleam` → present; `rg` of the sweep terms + `host` inside it → **0 hits** (the engine-value facade exists; the host-embedding surface does not).
- File-inspected the P7 dossier at `docs/research/glp-gleam-baseline/pipelines/P7-qhsm-yngenios/DOSSIER.md` (154 lines).

**Two path corrections vs the WP acceptance text** (recorded, not blocking):
1. The dossier lives at `docs/research/glp-gleam-baseline/pipelines/P7-qhsm-yngenios/DOSSIER.md`, **not** `specs/036-glp-gleam-baseline-program pipelines/...`. `036/tasks.md:44` (T010, marked `[X]`) writes it to the `docs/research/glp-gleam-baseline/` pipeline tree, consistent with the feature-036 read-only contract.
2. The acceptance's "**expected: dossier-only hits**" is **stale**. The hits are *not* dossier-only: the load-bearing matches are **spec-059 mandating requirements** (US4/US6, FR-007/008/010, SC-008) plus the **resolved** escalation `rule-embeddability-api-yngenios-wiring` — see the verdict below.

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `embeddability-service-box` | **ABSENT (code) / REQUIREMENTS-PROMOTED** | 0 code hits in `glp_gleam/`+`gleam_quic/`; no spec dir; no service-box contract file. But spec-059 (FR-008 full-wiring G3-A, FR-010 working-integration-not-a-stub, US4/US6, SC-008) + the **resolved** `rule-embeddability-api-yngenios-wiring` (2026-07-20) promote it from *gap-by-definition* to a **mandated but unstarted wave-4/5 build**. The engine-value facade anchor exists (`engine.gleam`); the host-embedding surface, service-box contract, and store-kernel scope decision are all absent. |
| 2 | `qhsm-yngenios-integration-design` | **DELIVERED (design dossier) — owner-gated; 8 provisional gates** | The P7 dossier is a complete, source-cited packaging design (QActive wrap §2, PURE_ACTOR/GATEWAY authority seam §3.1, PATH-A/PATH-B embeddings §3.3, beacon PAT-01..04 §3.3). The Option A/B fork and PATH selection are explicit **owner gates**, and the design rests on 8 flagged PROVISIONAL/missing-source items (§4) — notably diana ABSENT, AOK-OS Draft, no `libAtomVM` FFI grounding, ED-6 float-decode unverified. |

**Tally**: neither row is DELIVERED-in-code. #1 is the build target (absent code behind promoted requirements); #2 is delivered as a research dossier but every embedding choice is owner-gated.

## Per-capability evidence

### 1. `embeddability-service-box` — ABSENT in code, PROMOTED at requirements
- **No code, no contract, no spec dir.** The sweep terms return **0** matches in `glp_gleam/` and `gleam_quic/`; `find` shows no `*embed*`/`*service-box*` spec directory; no `contracts/` service-box file exists. `store_put`/`store_get` appear only as prose in `specs/059-.../spec.md:143` ("store_put/store_get kernels vs host-owned log"), never as kernels.
- **The anchor that *does* exist.** `glp_gleam/src/glp/engine.gleam` is the delivered engine-as-typed-value facade (construct / load / one-shot run to `ResultEnvelope` / interactive start-step / zero global state — plan `freeze-engine-facade`). It carries **no** `embeddab`/`service-box`/`yngenios`/`host` token — i.e. the in-process engine-value API is present, but the **host-embedding surface is absent** (matches the inventory note "host-embedding surface absent").
- **Why "promoted", not "gap-by-definition".** All 6 matched files are requirements docs, and the spec-059 hits are prescriptive, not descriptive:
  - `spec.md:25` — clarification resolves `rule-embeddability-api-yngenios-wiring` to **Option C, full wiring** (Gleam engine embedded as controller across all four spec-056 services, fabric tests green).
  - `spec.md:141` **FR-008** — MUST wire the Gleam engine as controller across S1/S2/S3/spine on the shared mailbox binding (cross-repo integration only; no yngenios sources imported).
  - `spec.md:143` **FR-010** — MUST be *working integration, not a stub*: a **ratified service-box contract**, a **service-box API on the engine facade**, and the engine driven by each service through the mailbox, exercised by an end-to-end object-PUT. **The store_put/store_get-vs-host-log scope call is escalated to the engineer, never resolved by the team.**
  - `spec.md:167` **SC-008** / US4 (`:75`) / US6 (`:105`) — the four services run against the embedded engine with their suites green + one e2e object-PUT, engineer sign-off recorded.
  - `spec.md:174,178` — the rule is **RESOLVED**; the yngenios repo (`D:\bstdev\research\yngenios-003`, frozen spec-056) is the wave-4 integration dependency.
  So the correct verify posture is **not** a no-op "still just a dossier" pass (the Risk the WP flags): embeddability is now a first-class, engineer-gated build obligation whose *code* is absent and whose *contract* is the load-bearing next output.

### 2. `qhsm-yngenios-integration-design` — DELIVERED as dossier, owner-gated
The P7 dossier answers the packaging question concretely and with firsthand citations:
- **Packaging unit** (§1): the M1 single-instance combined Gleam runtime — already a deterministic state machine (Reduce/Suspend/Fail over `(Q,S,F)` + immutable two-cell heap), fed by grammar→AST→IL→v2.16.3-bytecode, emitting `{succeeded|failed|suspended}`; the M2 term-link is the only place processes/Subjects appear.
- **Wrapper** (§2.3): wrap as **one `QActive`** (`QHsm` + priority + FIFO mailbox). 🔴 Load-bearing structural fact: the GLP goal-queue `Q` is **internal engine state, NOT the QActive mailbox**; the mailbox carries only boundary events; **one RTC step = one boundary event → drain the reduction to quiescence (`Q=∅`)**, which discharges the FB-M1-35 single-fire hazard at the boundary for free. `SNAPSHOT`/migrate is legal **only in Quiescent** (the only instant the heap is a clean serializable value).
- **Authority seam** (§3.1): control QHSM must be `AOK_PURE_ACTOR` (no resource cap); only a thin `AOK_GATEWAY` proxy holds the transport cap → **M1 = PURE_ACTOR, M2 = GATEWAY**.
- **Embeddings** (§3.3): **PATH-A** in-process native AOK is plain-BEAM-only (never AtomVM — `gleam_otp`/`proc_lib` absent); **PATH-B** out-of-process guardian is the only realizable AtomVM embedding, via the beacon-wrapper recipe PAT-01..04 (single-threaded Step/Drain scheduler, durable PGlite mailbox, static-macaroon verify-before-act, canonical Envelope over a file seam).
- **Owner-gated / provisional** (§2.4, §4): the Option A vs B fork and PATH-A vs B selection are **owner calls, not Claude calls**; the design rests on 8 flagged items — diana docs ABSENT (§4.1), AOK-OS Draft with an empty `examples/aok/` (§4.4), no grounded `libAtomVM` FFI (§4.5), ED-6 float-decode on AtomVM unverified (§4.6), FB-M1-40 Dart reference line unpinned (§4.7), beacon PAT sources outside the sanctioned read set (§4.3). Recommendation (owner-gated): **Option B / PATH-B** for the M1 deliverable; upgrade to Option A only if M2 or a product feature needs kernel-composable GLP-commit (`olamnit/.../GlpUnit.cs`).

## Extracted embeddability requirements checklist (load-bearing output for `build-yngenios-embeddability`)

Each item cites its source and is tagged **[build-bound]** (a concrete build obligation), **[reuse]** (an already-delivered seam the build layers onto), or **[gate]** (an owner/engineer decision that must be ruled before the dependent build step proceeds).

**A. Service-box contract & API (spec-059 FR-010; the missing near-term artifact)**
- **A1** Ratify a **service-box contract** — currently ABSENT (no file). Source: FR-010 `spec.md:143`, US6-AC1 `spec.md:115`. **[build-bound]** (produced by `close-embed-embeddability-service-box`).
- **A2** Add a **service-box API on the engine facade**, layered on the delivered engine-value API in `glp_gleam/src/glp/engine.gleam`. Source: FR-010; plan `freeze-engine-facade`. **[build-bound]**
- **A3** Value surface = the ED-1 result seam (0x01/0x11 envelope, byte-parity term codec, depth-bounded deep-resolve). Source: plan `freeze-codec-envelope`; delivered per `verify-codec-compiled-il-on-the-wire` (b3-c1-011). **[reuse]**
- **A4** Host-driven stepping attaches at the `StepOutcome` single-reduction seam (idle/reduced/suspended/failed/errored). Source: plan `freeze-engine-runtime`. **[reuse]**
- **A5** **Store-kernel scope decision**: `store_put`/`store_get` kernels **vs** host-owned log. Source: FR-010 `spec.md:143` — *escalated to the engineer, never team-resolved*. **[gate]**

**B. QHSM packaging (dossier §2)**
- **B1** Wrap the M1 engine as **one `QActive`** (`QHsm` + priority + FIFO mailbox). §2.3:58. **[build-bound]**
- **B2** Internal HSM = engine lifecycle `Booting → Idle/Suspended(Quiescent) ⇄ Reducing → Terminated`, with STOP/FAULT/STATUS in a superstate. §2.3:60. **[build-bound]**
- **B3** 🔴 Keep `Q` as **internal engine state**, not the mailbox; mailbox carries only boundary events (`RUN/INBOUND_TERM/LINK_*/SNAPSHOT/STOP/STATUS/TICK`); **one RTC step = one boundary event → drain to `Q=∅`**. §2.3:61. **[build-bound]**
- **B4** Permit `SNAPSHOT`/migrate **only in Quiescent** (`Q=∅`, heap = clean serializable value). §2.3:62. **[build-bound]**
- **B5** Fork: **Option B (port/FFI, ~5-state supervisor)** for M1; **Option A (rich QActive, kernel-composable GlpUnit commit)** only when M2/product needs ACID GLP-commit. §2.4:74. **[gate]**

**C. YngeniOS integration (dossier §3)**
- **C1** Control QHSM = `AOK_PURE_ACTOR` (no resource cap); a thin `AOK_GATEWAY` proxy holds the M2 transport cap. M1=PURE_ACTOR, M2=GATEWAY. §3.1:87. **[build-bound]**
- **C2** Attach via the QP/AOK port: `QActive_start_` → `aok_evtch_create` + `aok_actor_create(AOK_PURE_ACTOR)`; RTC boundary = `aok_evtch_get` (Zephyr `k_msgq` port is the alternate). §3.2:92–94. **[build-bound]**
- **C3** Choose embedding: **PATH-A** in-process native AOK = plain-BEAM only; **PATH-B** out-of-process guardian = the only realizable **AtomVM** embedding. §3.3:101–102. **[gate]** (target-dependent)
- **C4** For PATH-B, implement the beacon-wrapper: PAT-01 single-threaded Step/Drain AO scheduler; PAT-02 durable PGlite mailbox (`pending→claimed→consumed|retry|dead_letter`, atomic single-winner, `MaxAttempts=3`); PAT-03 static-macaroon verify-before-act; PAT-04 canonical Envelope over a request/reply **file seam** (the `PythonWorkerLauncher` "Python/C#/Gleam" pattern). §3.3:105–108. **[build-bound]**
- **C5** Prefer the mature **`olamnit/Olamnit.Kernel`** (C#, epic-013: `DurableQF`, `GlpUnit` two-level commit, WAL replay) as the realization target over the Draft AOK kernel. §3.4:112. **[reuse]**

**D. Gates that must clear before the build commits (dossier §4)**
- **D1** diana docs ABSENT → the product-altitude YngeniOS/Five-Guardians stack is PROVISIONAL and unused by the packaging design; owner supplies or rules out of scope. §4.1:118. **[gate]**
- **D2** AOK-OS is **Draft** (spec-023 Draft; `examples/aok/` empty; 4 BLOCKER/7 MAJOR findings) → PATH-A rests on a Draft kernel; prefer `Olamnit.Kernel` until an owner-gated kernel-verification pass + a built DPP-on-AOK example exist. §4.4:124. **[gate]**
- **D3** In-process `libAtomVM` FFI **NOT grounded** → spike a `generic_unix` `.avm` file-seam round-trip before assuming any engine→host upcall. §4.5:126. **[gate]**
- **D4** ED-6 float-decode on AtomVM **unverified** → spike `/float` bit-syntax extraction before committing the GATEWAY codec. §4.6:128. **[gate]**
- **D5** FB-M1-40 parity — Dart `heap_fcp.dart forward_to_terminal` reference line **unpinned** → pin before declaring verified. §4.7:130. **[gate]**
- **D6** Beacon PAT-01..04 line-level C# sources outside the sanctioned read set → owner adds `buildkit-beacon` to the read set, or accepts the in-`qhstate` spec-034 distillation as the contract of record. §4.3:122. **[gate]**

## Activation

`close-embed-embeddability-service-box` (b3-c1-039) is **activated**. Its deliverable — "a ratified embeddability requirements contract … with each requirement traced to the P7 dossier and marked build-bound" — is exactly section 4 above. The close must additionally:
1. Carry the **A5 store-kernel scope call** (`store_put`/`store_get` kernels vs host-owned log) to the **engineer** as an unresolved decision (never team-resolved, per FR-010).
2. Ratify the **A1 service-box contract** (the one absent near-term artifact) as the FE/BE process-boundary + host-embedding payload, reusing the frozen ED-1 envelope (A3) and StepOutcome seam (A4).
3. Record the six §D gates (D1–D6) as build-blocking preconditions handed to `build-yngenios-embeddability` (b3-c2-047).

No code work is in scope for this verify or its close — both are requirements-level; the engine-value facade (`engine.gleam`) is the delivered anchor the eventual build layers the host surface onto.
