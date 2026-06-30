# Tasks: GLP → Gleam/AtomVM Baseline — Research, Verification & Reconfiguration Program

**Feature**: `036-glp-gleam-baseline-program` | **Plan**: [plan.md](./plan.md) | **Spec**: [spec.md](./spec.md)

> This is a research/reconfiguration program run under `/bk-marathon`. "Tasks" are pipeline runs
> (each a Claude Workflow honoring `contracts/pipeline-contract.md`) + scaffolding + the gated
> migration. Every task is **read-only on the target roadmap/specs/code and all sibling repos**
> until the discharge gate (T014/T015). DONE already: ED-1…ED-6 (seam architecture), P5 IL/ML
> (`pipelines/P5-il-machine-language/{DOSSIER,DECISIONS}.md`), the merge/3 verification spike
> (`spike/p5-il-merge/`). Story map: US1 two-epic reconfiguration · US2 faithfulness spec+proofs ·
> US3 parser/IL strategy · US4 impl & OS-integration · US5 concerns/opportunities.

## Phase 1: Setup (marathon Phase A — build the machinery)

- [X] T001 Author the shared corpus index at `docs/research/glp-gleam-baseline/CORPUS-INDEX.md` — locate + map every grounding source (GLP corpus `GLP_IMPLEMENTATION.pdf`/`Art-of-GLP-2025/formal.tex`/Dart `glp_runtime/`; in-repo research corpora; sibling repos read-only; the `repl-engine-separation/spikes/` armoury) with paths + what each is authoritative for.
- [X] T002 [P] Author the proof-harness wiring at `docs/research/glp-gleam-baseline/PROOF-HARNESS.md` — how to invoke the in-repo Lean/SPIN/MLIR spikes (`docs/research/repl-engine-separation/spikes/{lean,spin,mlir}`) + the exec-equivalence harness (`spike/p5-il-merge/lib/exec.dart`) for a new invariant; record reproduce commands.
- [X] T003 [P] Author the pipeline status index at `docs/research/glp-gleam-baseline/pipelines/INDEX.md` — table of P1…P8 + ANTLR-deep-dive: phase, status (mark P5 + merge/3 spike DONE), script path, artifact path, verification gate (per `contracts/pipeline-contract.md`).

## Phase 2: Foundational (marathon Phase B — blocks dispositions + synthesis) — US2

**Goal**: the testable M1/M2 parity bar + constructed proofs. **Independent test**: every criterion cites a primary source; every load-bearing invariant has a recorded proof outcome (proved/refuted/open).

- [X] T004 [US2] Run the **P4 faithfulness-proofs** pipeline → `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PARITY-BAR.md`: M1 (single-instance) + M2 (linked) testable criteria, each citing `GLP_IMPLEMENTATION.pdf`/`formal.tex`/Dart-runtime page or `file:line`. Grounding: GLP corpus + `multi-protocol-link-layer/corpus/` + `specs/034` parity list. Gate: 100% criteria primary-source-cited (FR-003).
- [X] T005 [US2] Construct proofs for the load-bearing invariants (SRSW preservation; writer-MGU binds-only-writers; three-valued unify Suspend-not-Fail; suspension/reactivation; distributed deref) via the proof harness → `pipelines/P4-faithfulness/PROOFS/` with `proved|refuted|open` per invariant. Gate: none silently skipped; any refuted/unprovable surfaced as a faithfulness risk (FR-004).

## Phase 3: US1 — decision-ready two-epic reconfiguration (P1 priority; the headline)

**Goal**: every not-completed feature dispositioned + the two scored, ordered epics. **Independent test**: 100% features dispositioned with cited evidence; Full-Gleam epic is a valid topological order, scored, each feature tied to ≥1 faithfulness criterion.

- [X] T006 [US1] Run the **corrected realignment** pipeline (replaces the failed P1) → `pipelines/P1b-realignment/DISPOSITIONS.md`: re-disposition every not-completed sep/marathon/gleam feature against the verified architecture (ED-1…ED-6) + the P4 parity bar. Grounding: the engine-sep dossier + per-feature reconciliation docs, `DECISIONS.md`, `PARITY-BAR.md`, each feature's real source. Gate: each disposition cited; **no fastest-path rubric**; the ANTLR/IL/separation cluster judged on the verified architecture, not pre-dropped (fixes the P1 failure). Depends on T004–T005.
- [X] T007 [US1] Run the **P8 synthesis** pipeline → `pipelines/P8-synthesis/RECONFIGURATION.md`: the two epics (*Optional features* / *Full Gleam implementation*), Full-Gleam fully scored + topologically ordered, each feature tied to ≥1 P4 criterion + the ED-6 obligations; + advisory migration mapping (existing-feature → epic + re-scope). Depends on T004–T006 **and** the research threads T008–T012.

## Phase 4: US3 — parser & IL strategy (P5 IL DONE; ANTLR integration remains)

**Goal**: best verified ANTLR-integration option for the production grammar. **Independent test**: dossier with ≥1 option actually built/run (building on the spike).

- [X] T008 [P] [US3] Run the **ANTLR-integration deep-dive** pipeline → `pipelines/ANTLR-integration/DOSSIER.md`: production-grammar scope (full GLP — all clauses, guards, type decls, modules), the no-BEAM-target integration (parser generated in C#/Dart; engine pure-Gleam), grammar-as-verifier role. Grounding: `spike/p5-il-merge/` (the working merge.g4 + adapter), the `#12` ANTLR memo, `qhstate` `.g4` work, `glp_runtime/lib/compiler/{parser,token,ast}.dart`. Gate: ≥1 verified option extending the spike.

## Phase 5: US4 — implementation & OS-integration strategy

**Goal**: the Gleam/AtomVM impl strategy + the QHSM/YngeniOS integration design. **Independent test**: both dossiers cite their sources + give a concrete design; zero sibling-repo writes.

- [X] T009 [P] [US4] Run the **P6 Gleam/AtomVM implementation-strategy** pipeline → `pipelines/P6-gleam-impl/DOSSIER.md`: GLP→BEAM concurrency mapping (suspension/reactivation ↔ processes/messages), heap model, persistence, AtomVM constraints (no `gleam_otp`; raw `erlang:spawn`+Subjects). Grounding: `gleam-atomvm/dossier.md`, `glp_gleam/src/`, the bytecode doc, `GLP_IMPLEMENTATION.pdf`. Gate: each material claim cited.
- [X] T010 [P] [US4] Run the **P7 QHSM/YngeniOS integration** pipeline → `pipelines/P7-qhsm-yngenios/DOSSIER.md`: package the combined Gleam instance as a QHSM + integrate into the YngeniOS microkernel. Grounding (read-only): `D:\bstdev\research\qhstate`, `qhstate-Yngenios/specs/034`, `MSTACK/docs/diana`, `olamnit` RTOS; web for QP/QHSM. Gate: concrete packaging design citing the sibling repos.

## Phase 6: US5 — concerns + opportunities

**Goal**: exhaustive concerns + opportunities registers. **Independent test**: each item evidenced; discovery ran loop-until-dry.

- [X] T011 [P] [US5] Run the **P2 concerns** pipeline (loop-until-dry) → `pipelines/P2-concerns/REGISTER.md`: risks, blockers, faithfulness gaps, AtomVM limits, scope traps — each with evidence, severity, affected features.
- [X] T012 [P] [US5] Run the **P3 opportunities** pipeline → `pipelines/P3-opportunities/REGISTER.md`: BEAM/AtomVM-enabled simplifications — each naming the capability + what it lets the design delete/simplify, with evidence.

## Phase 7: Polish & Discharge Gate

- [X] T013 Completeness-critic pass over all artifacts (`docs/research/glp-gleam-baseline/pipelines/`): what is missing — a modality not run, a claim unverified, an invariant left open, a source unread; fold gaps into a final round before synthesis is final.
- [ ] T014 **DISCHARGE GATE (FR-011)** — present the P8 reconfiguration + migration plan to the owner for approval. STOP and wait; record informed consent. No live-roadmap mutation before this.
- [ ] T015 [owner-approved only] Migrate the recombined feature set into the two new epics via `buildkit-roadmap` (create *Optional features* + *Full Gleam implementation*; `add-feature` with scores + dependencies per the migration mapping). Gated strictly on T014.

## Dependencies

- Setup T001–T003 → everything.
- **Foundational T004–T005 (P4)** → T006 (realignment), T007 (synthesis); also inform T008–T012.
- Research threads **T008, T009, T010, T011, T012 run in parallel** after setup (each independent, different output files).
- T006 (realignment) after T004–T005 → feeds T007.
- **T007 (synthesis) after T004–T006 AND T008–T012.**
- T013 after T007. **T014 after T013. T015 only after T014 owner approval.**

## Parallel execution example

After T001–T005, launch the five research/realignment threads concurrently (distinct output files,
no shared writes): `T006` (realignment) ∥ `T008` (ANTLR) ∥ `T009` (Gleam-impl) ∥ `T010`
(QHSM/YngeniOS) ∥ `T011` (concerns) ∥ `T012` (opportunities). Converge at `T007` (synthesis).

## Implementation strategy

- **MVP / first value**: T001–T005 → the verified parity bar (US2) is the foundation everything
  cites; it is the smallest independently-valuable increment.
- **Incremental**: then the parallel research threads + corrected realignment, converging on the
  P8 synthesis (US1). The two-epic reconfiguration is presented at the discharge gate; **migration
  happens only on owner approval** (T014→T015). Run under the marathon for durable, restart-safe
  cross-session progress.
