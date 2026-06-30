# GLP → Gleam/AtomVM Baseline — Research, Verification & Reconfiguration Program

**Owner:** Gabi · **Captured:** 2026-06-26 · **Status:** feature-definition (pre-spec)
**Disposition:** wrapped under `/bk-marathon` (owner-mandated); one feature, owner pre-approved.
**Nature:** read-only research/analysis/verification + a *proposed* roadmap reconfiguration.
No roadmap/spec/code mutation of the target until the owner approves the synthesised plan.

---

## 0. One-line goal

Compress the three open epics (REPL/engine-separation · marathon · Gleam-AtomVM) into the
**shortest verified roadmap** to a **strong Gleam/AtomVM GLP** — a single combined
front-end+back-end GLP instance that faithfully replicates the Dart and C# implementations
(single instance) and, by linking such instances, their multi-instance linked behaviour.

## 1. End-state (the thing we are building a roadmap *to*)

The unit of delivery is **one combined GLP instance on Gleam + AtomVM** — front-end
(REPL/UI seam) and back-end (engine + scheduler + heap) **together in one instance**,
exactly as the Dart and C# implementations are combined today — that additionally has
**link capability** to talk to other such instances.

- **M1 — single-instance parity.** A combined Gleam/AtomVM GLP instance whose observable
  execution semantics are identical to a single Dart/C# GLP instance: HEAD/GUARD/BODY
  three-phase execution, SRSW, three-valued unification (Success/Suspend/Fail), writer-MGU
  (binds only writers, never readers, never writer↔writer), suspension on unbound readers +
  reactivation on writer-bind, deref incl. the bidirectional writer↔own-paired-reader
  self-bind ⇒ Unbound rule. Proven by the ported GLP test corpus passing on BEAM.
- **M2 — multi-instance linked parity.** Two/many combined Gleam instances linked over the
  link protocols, replicating the Dart/C# linked-distribution semantics: distributed
  unification, ground-only transport / per-hop partial-term globalization, payload wire
  format, distributed deref (w×w local pairs). Cross-runtime interop with the C# reference
  is a verification lever, not necessarily a shipped requirement (decide in P5/P8).

> Note (working hypothesis, to be *verified* not assumed): because the target is **combined
> in one instance**, much of the engine-separation epic — which was designed *C#-first to
> split* front-end from back-end into separate processes/clients (process-split, separate
> liveness host, multi-accept transport, snapshot/persistence-for-restart) — is likely
> **Optional / superseded-by-BEAM**, while the **link/wire/codec** pieces + the **Gleam
> port chain** are the **Full-Gleam core**. The pipelines must confirm or refute this.

## 2. Method (mandatory)

Use the established multi-agent, multi-stage pattern (as in LeJEPA / Yngenios / MSTACK /
Beacon / Crucible): **design (several independent approaches) → adversarial review →
deep sub-agent analysis → synthesis**, with loop-until-dry and adversarial verification.

- **Corpus is inspected directly — never memorised or summarised as a substitute.** Agents
  open the actual papers/source and cite `file:line` / page. Where a faithfulness claim is
  load-bearing, **construct a proof** (reuse the in-repo armoury: Lean / SPIN / MLIR spikes
  under `docs/research/repl-engine-separation/spikes/`).
- **Web research** is used where on-disk material is insufficient (e.g. AtomVM opcode/
  memory limits, ANTLR target availability, Miro-Samek QP/QHSM semantics) — and findings
  are then re-grounded against primary sources.
- Every analytical claim carries evidence; every recommendation is scored and ordered.

## 3. Located inputs (verified on disk, 2026-06-26)

**GLP / Shapiro corpus (faithfulness ground):**
- `D:\bstdev\research\GLP\GLP\GLP_IMPLEMENTATION.pdf` — implementation paper.
- `D:\bstdev\research\GLP\GLP\GLP_ART.pdf` — language/art paper.
- `D:\bstdev\research\GLP\Art-of-GLP-2025\` — book; `formal.tex` (formal semantics),
  `chapters\{glp_core,basic_concurrent,moded_types,types,...}.tex`, `main_AofGLP.pdf`.
- Authoritative **Dart source-of-truth**: `D:\bstdev\research\GLP\GLP\glp_runtime` +
  `programs` (+ `ArtOfProlog`, `AofGLP`). (FCP primary not found locally as PDF; FCP/heap
  material is in the link-layer corpus below + may need web.)

**This repo (glpnet):**
- Engine-separation dossier + per-feature reconciliation: `docs/research/repl-engine-separation/`
  (`design-dossier.md`, `reconciliation/`, `spikes/{lean,spin,mlir}`).
- Link-layer corpus: `docs/research/multi-protocol-link-layer/corpus/` (heap-fcp, wire
  format, ground-only transport, distributed deref, madGLP correctness theorem).
- Gleam track: `docs/research/gleam-atomvm/{dossier.md,toolchain-inventory.md}` +
  `glp_gleam/src/glp/` (runtime/{terms,heap,unify,suspension} implemented; engine/compiler/
  bytecode/link/multiagent are placeholders) + `specs/031..034`.
- C# reference implementation: `out/csharp`; shipped IL codec: `csharp/glp_il_codec`
  (feature 029); FrameCodec/TcpTransport (feature 025).

**QHSM / YngeniOS (= NGENIUS = Yngenios) integration ground:**
- `D:\bstdev\research\qhstate` — QHSM / QP-framework RTOS-synthesis repo; ANTLR4 grammar
  work `specs/031-qhxm-grammar-antlr4-lexer-parser-front-end`,
  `032-qhxm-grammar-for-antlr4-c-like-surface-for-qmsm-qhsm`, `033-native-guarded-grammar`,
  `qhxm-guarded-transitions-v1`; `docs/toolchain/qhsm-suite.md`; `synthesis-os/`.
- `D:\bstdev\research\qhstate-Yngenios` — `specs/034-yngenios-microkernel-research-and-distillation-pipeline`.
- `D:\bstdev\research\qhstate-coop`.
- `D:\bstdev\tools\MSTACK\docs\diana\` (NGENIUS/DIANA candidate analysis: `MERGED-DECISION.md`,
  `REFINED-CANDIDATES.md`, `official-pdf-text.txt`) + MSTACK `ARCHITECTURE.md`, `dianna/`.
- `D:\bstdev\research\olamnit` — C# RTOS host/kernel: `specs/013-olamnit-rtos-kernel`,
  `014-rtos-kernel-core`, `005-headless-claude-code-shell-host`.

## 4. The machinery (research/analysis pipelines)

Each pipeline is a multi-agent workflow (design→review→analyse→synthesise), read-only,
emitting a verified artifact under `docs/research/glp-gleam-baseline/`.

- **P1 — Existing-epic review & realignment.** Alignment / criticality / improvement /
  ordering of every not-completed sep+marathon+gleam feature vs the combined-instance Gleam
  goal. *(Already running as a head-start: workflow `wf_fb9f56eb-0ca`; fold its output in.)*
- **P2 — Concerns register.** Adversarial sweep for risks, blockers, faithfulness gaps,
  AtomVM limits, scope traps. Loop-until-dry.
- **P3 — Opportunities register.** What becomes newly possible/cheaper on BEAM/AtomVM
  (native lightweight processes, per-process mailboxes, links/monitors, supervision,
  optional distribution, hot-code) — and what that lets us delete from the C# design.
- **P4 — Faithfulness verification (corpus + proofs).** Direct inspection of
  `GLP_IMPLEMENTATION.pdf`, `formal.tex`, the Dart runtime, the link-layer corpus →
  a precise, testable **parity specification** for M1/M2; construct proofs for the
  load-bearing invariants (SRSW preservation, unification soundness, suspension/
  reactivation, distributed deref) using the Lean/SPIN/MLIR armoury.
- **P5 — Parser & IL strategy (ANTLR + intermediate-language-vs-direct).** Verified options
  to use **ANTLR4** for GLP parsing targeting Gleam/BEAM (native target availability;
  generate-to-another-target + FFI; ANTLR→IR→Gleam), grounded by the existing qhstate
  ANTLR4 grammar work. Resolve the **core question: does the Gleam engine need an
  intermediate language/bytecode, or can it compile clauses directly to Gleam/BEAM?** —
  with a small experiment/spike each way.
- **P6 — Gleam/AtomVM implementation research.** Verified best way to implement the GLP
  engine+scheduler+heap on Gleam/AtomVM given constraints (no `gleam_otp` on AtomVM → raw
  `erlang:spawn` + `gleam_erlang` Subjects; AtomVM opcode/memory limits; binaries):
  concurrency mapping (GLP suspension/reactivation ↔ BEAM processes/messages), heap model,
  persistence. May include a small experiment.
- **P7 — QHSM / YngeniOS integration research.** How QH-state machines work (qhstate /
  QP-QHSM / guarded transitions), how to package the combined Gleam/AtomVM instance as a
  **QHSM/QHM**, and how it integrates into the **YngeniOS** microkernel — grounded by
  qhstate, qhstate-Yngenios/034, MSTACK/docs/diana, olamnit RTOS; web for QP/QHSM where
  needed.
- **P8 — Synthesis & reconfiguration.** Compress all artifacts into the shortest verified
  roadmap; produce the two target epics with **fully-scored, optimally-sequenced,
  verified** features; emit the advisory migration plan.

## 5. Two-phase marathon

- **Phase A — Build the machinery.** Stand up P2–P8 as runnable, resumable workflow scripts;
  build the shared corpus index + proof-harness wiring; define each pipeline's inputs,
  outputs, and verification gate. (P1 is already a running prototype of the pattern.)
- **Phase B — Run the machinery.** Execute the pipelines, accumulate verified artifacts,
  adversarially verify, then run P8 synthesis. Discharge gate = the two-epic reconfiguration
  proposal is verified and presented for owner approval.

## 6. Deliverables

1. Realignment analysis of all not-completed features (P1).
2. Concerns register (P2) + Opportunities register (P3).
3. **Corpus-verified faithfulness specification + proofs** for M1/M2 (P4).
4. ANTLR-integration options dossier + **IL-vs-direct-compile decision**, each with a spike (P5).
5. Gleam/AtomVM implementation-strategy dossier (P6).
6. QHSM/YngeniOS integration design (P7).
7. **Two new epics, fully scored + optimally sequenced + verified** (P8):
   - **Epic: Optional features** — everything *not* required for the full Gleam
     implementation (side items, superseded-by-BEAM, C#-split-only, deferrable spikes).
   - **Epic: Full Gleam implementation** — the shortest verified critical path to M1 then M2.
8. An advisory **migration plan** (which existing features map to which epic, re-scoped how).

## 7. Target epics & migration rule

After P8 and **owner approval only**, migrate the recombined/resynthesised feature set into
the two epics above. Until then this program **proposes**; it does not move anything.
Migration is the marathon's discharge action, behind the hard gate.

## 8. Success criteria

- Every not-completed feature has a verified disposition (aligned / realign / supersede /
  fold / cross-runtime / drop) with cited evidence.
- The Full-Gleam epic is a valid topological order with no forward dependency, fully scored,
  and each feature's faithfulness contribution is tied to a P4 parity criterion (and, where
  load-bearing, a proof).
- The IL-vs-direct question is answered with experimental evidence, not opinion.
- ANTLR integration has at least one *verified* option (built/run), not just a survey.
- QHSM/YngeniOS integration has a concrete packaging design grounded in the located repos.
- Zero target-roadmap mutations before owner approval.

## 9. Guardrails

- Read-only on the target roadmap/specs/code until owner approval (mutations = discharge gate).
- Sibling repos (GLP, qhstate*, MSTACK, olamnit) are inspected **read-only**, in place.
- Corpus inspected directly; proofs constructed for load-bearing claims.
- Safety-first: surface decision points; never resolve scope/framing forks autonomously.
