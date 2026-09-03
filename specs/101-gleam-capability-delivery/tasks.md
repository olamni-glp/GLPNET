# Tasks: GLPnet Gleam Capability Delivery

**Feature:** `101-gleam-capability-delivery` · **Plan:** [plan.md](plan.md) · **Generated:** 2026-09-03

**Tests are not decoration.** Every success criterion in `spec.md` is a measurement with a named
refuter, so the tests **are** the acceptance evidence. Guard tests are written **before** the guards
they protect (C6) — this repo has shipped four checks that could not fail, one of them found inside
this feature's own parent task list.

**Format:** `[ID] [P?] [Story] Description` · **[P]** = parallelizable (different files, no dependency)

---

## Phase 1: Setup

- [x] **T001** Fix the stale self-description in `glp_gleam/gleam.toml`. It reads *"port skeleton …
      8 placeholder modules … No ported runtime semantics yet"* at v0.1.0; the tree carries ~83
      modules under `src/glp` and passes **206/206** parity. Measured 2026-09-02. A reader trusting
      it under-estimates the tree by an order of magnitude.
- [x] **T002** [P] Create `glp_gleam/src/glp/contract/` and `glp_gleam/src/glp/ring/` package
      skeletons that compile empty-but-green, so later tasks land in a building tree.
- [x] **T003** [P] Create `test/ring/` for per-ring conformance output, alongside (never replacing)
      the existing `test/parity/`.

## Phase 2: Guard tests — written FIRST, and each must be able to FAIL

- [x] **T004** [P] [US3] `test/ring/test_contract_purity.sh::test_runtime_dep_in_contract_fails_build`
      — **positive control (SC-004)**: introduce a third-party runtime dependency into the contract
      and assert the build FAILS. Must fail before C1-R exists.
- [x] **T005** [P] [US3] `...::test_admission_by_name_is_refused` — **(SC-005)** offer `glp_gleam` to
      L0 on the strength of the word "Gleam"; assert refusal **with the name quoted**. Real case: it
      is not the polyglot-L0 `kv`/`mailbox`/`network` service set.
- [x] **T006** [P] [US1] `test/ring/test_report_shape.sh::test_report_without_denominator_is_rejected`
      — **(SC-002)** a report lacking a denominator is unparseable, not merely ugly.
- [x] **T007** [P] [US1] `...::test_counts_reconcile` — **(SC-007)** `attempted = agreed + diverged +
      excused` exactly; a mismatch fails.
- [x] **T008** [P] [US1] `...::test_excused_case_without_reason_is_rejected` — **(FR-007)** a
      reasonless exclusion is indistinguishable from a case nobody ran.
- [x] **T009** [P] [US2] `test/ring/test_aggregate.sh::test_unbuilt_ring_never_reads_as_pass` —
      **positive control (SC-006)**: build ONE ring, assert the aggregate REFUSES. This is the single
      most likely way this feature could ship a lie.
- [x] **T010** [P] [US1] `test/ring/test_mutation.sh::test_weakened_guard_turns_suite_red` —
      **(SC-003)** replace a ring-placement guard with a no-op; the acceptance suite must go **RED**.
      A mutation test that stays green under a no-op is the inverse of the evidence required.
- [x] **T011** [P] [US3] `test/ring/test_platform_conditional.sh::test_vacuous_premise_is_skipped_by_name`
      — **(FR-009)** a test whose premise does not hold on this platform must **skip with a named
      reason**, never silently pass. Regression-guards the parent feature's `T005` defect.

## Phase 3: US1 — the workstation (BEAM) ring · **P1**

- [ ] **T012** [US1] Import-analyse `glp_gleam/src/glp/**` to determine which modules are runtime-free
      **today**. Measured, not assumed — this fixes the contract boundary (research R2).
- [ ] **T013** [US1] **(FR-001)** Extract the runtime-free surface into `glp/contract/` (C1). Additive: existing
      modules stay in place (Principle IV-b).
- [ ] **T014** [US1] **(FR-003)** Implement `glp/ring/beam.gleam` — the L1b realization held to the contract.
- [ ] **T015** [US1] Wire `test/ring/` to emit the C4 report shape (ring, denominator, attempted/
      agreed/diverged/excused, `not_run[]`).
- [ ] **T016** [US1] **(FR-010, SC-001)** — run the pinned corpus with **no Dart toolchain on PATH** and record
      the result. Refuter: any case that only passes with Dart present.

## Phase 4: US2 — the app (AtomVM) ring · **P2**

- [ ] **T017** [US2] Enumerate AtomVM's unsupported constructs. **NOT MEASURED today** — research R3
      leaves this open deliberately and it must not be guessed. Seed: `gleam_otp` is already excluded
      for `proc_lib`.
- [ ] **T018** [US2] Implement `glp/ring/atomvm.gleam` with a **build-time** refusal naming the
      offending construct (C3). A runtime rejection does not satisfy FR-004.
- [ ] **T019** [US2] Report host-side conformance as **UNREAD with a named reason** — the MAUI Blazor
      Hybrid host is target-side and absent here (`maui` = 0 occurrences in glpnet).
      **Do NOT synthesize a stand-in host to make a suite green.**

## Phase 5: US3 — placement evidence

- [x] **T022** [P] [US3] **(FR-005)** `test/ring/test_retention.sh::test_no_dart_or_corpus_leaves_glpnet`
      — assert the delivery set contains **no** file from `glp_runtime/`, `glp_multiagent/` or
      `programs/`. **Found by the 2026-09-03 analyze pass: FR-005 was the one requirement with no
      task.** It is a negative requirement — nothing fails if it is silently violated, which is
      exactly why it needs an explicit guard rather than trust.

- [ ] **T020** [US3] **(FR-002)** Implement C2 admission: record `subtree → ring` with **measured contract
      consumption** as evidence, and refuse name-based admission.
- [ ] **T021** [US3] Emit the source × ring coverage matrix naming **both axes** plus what was not
      read (FR-006/FR-008).

## Dependencies

```
T001..T003            → everything
T004..T011  (guards)  → BEFORE T012..T021        # tests first, and each proven able to fail
T012 → T013 → T014 → T015 → T016                 # US1, sequential
T017 → T018 → T019                               # US2, blocked on T017 which is UNMEASURED
T020 → T021                                      # US3
T022                                             # FR-005 retention guard, independent
```

## Deliberately NOT decomposed

- **Any migration into `YNGENIOS*`.** The `008` P4 gate is `REFUSE` — 2,782 of 4,782 (58.2%) of
  glpnet is still undelineated. This feature makes the capability deliverable; it moves nothing.
- **The MAUI Blazor Hybrid host itself** — target-side, absent here.
- **The QHSM-wrapped Dart reference implementation** — engineer-declared future, workstation-only,
  oracle-class work; explicitly out of scope.

## Honest limits carried from planning

- **T017 is the critical path for US2 and its input is unmeasured.** Everything downstream of it is
  blocked on a measurement nobody has taken.
- Parity is over **206 pinned cases**, not the 384-test unified suite. 100% there is not total
  semantic equivalence.
