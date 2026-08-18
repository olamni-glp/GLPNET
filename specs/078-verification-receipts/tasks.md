---
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
description: "Task list for feature 078 — verification receipts and loud failure"
---

# Tasks: Verification receipts and loud failure

**Input**: Design documents from `/specs/078-verification-receipts/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: REQUIRED. The spec mandates fault-injected acceptance (US3, FR-014/015/016) — the
fault-injection suite *is* a deliverable, and it is subject to its own invariant.

**Organization**: grouped by user story. **MVP = Phases 1–5** (Setup + Foundational + US1 + US2 +
US3) proven against a purpose-built reference check — this is what the **first SHIP-TOKEN** ships
(ratified 2026-08-18). Phase 6 (US4) + the full-corpus items in Phase 7 are **post-MVP incremental**
retrofits where SC-001 (13/13) and SC-002 (100% of areas) close.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: parallelizable (different files, no dependency on incomplete tasks)
- File paths are glpnet-side; the buildkit contract is a companion change (FR-024), pinned by version.

## Path Conventions
Reference impl under `codeconv/src/codeconv/receipts/`; tests under `codeconv/tests/`; the bash
emitter under `test/receipts/`; the checked-in manifest at `.specify/receipts/adoption.json`.

---

## Phase 1: Setup (Shared Infrastructure)

- [ ] T001 Create the receipts package skeleton with module docstrings citing FRs: `codeconv/src/codeconv/receipts/__init__.py`, `outcome.py`, `receipt.py`, `consumer.py`, `manifest.py`, `override.py`, `bind.py`, `paths.py`
- [ ] T002 [P] Wire the pytest target for receipts: `codeconv/tests/conftest.py` fixtures (tmp receipts-root, run-id factory) + `codeconv/tests/faultinj/__init__.py`
- [ ] T003 [P] Define the receipts-root + expected-set path convention `<root>/<area>/<run-id>/<check-id>.receipt.json` and `<root>/<run-id>/expected.json` as constants/helpers in `codeconv/src/codeconv/receipts/paths.py` (FR-022, research D2)

---

## Phase 2: Foundational (BLOCKING — must complete before any user story)

- [ ] T004 Implement the `Outcome` enum, `is_successful()` (PASS/EMPTY only), and the worst-wins ordering `PASS≈EMPTY < UNREAD < UNSEARCHABLE < FAIL` in `codeconv/src/codeconv/receipts/outcome.py` (FR-006/007, research D4)
- [ ] T005 Implement the contract-version resolver in `codeconv/src/codeconv/receipts/bind.py`: resolve the JSON schema from the active installed buildkit version and record `contract_version` on every receipt; until the buildkit companion change lands, resolve a clearly-marked **pre-release draft** derived from `contracts/receipt-schema.design.md` (never an owned copy — FR-024, research D3). Re-pin task is T037.
- [ ] T006 Implement the `Target` and `Receipt` dataclasses + JSON (de)serialization in `codeconv/src/codeconv/receipts/receipt.py` per `data-model.md`
- [ ] T007 Implement the schema validator + reference invariants (EMPTY⇒examined==total; examined≤total when total known; UNSEARCHABLE⇒`unresolved_reason`; truncation-honesty) in `codeconv/src/codeconv/receipts/receipt.py` (FR-005/010, data-model invariants)
- [ ] T008 Implement the sidecar writer in `receipt.py`: build the path (T003), write JSON, return the verdict pointer; apply bounding — cap enumerations at declared max, always record true totals, byte-backstop any single field, set `truncated.*` when capped (FR-002/004/005/022)

---

## Phase 3: User Story 1 — A check proves it ran (P1)

**Goal**: every check emits a receipt naming the resolved target + examined-count; a verdict without a receipt is refused, and an unresolvable target is never clean.
**Independent test**: run a receipt-emitting check against a known target → receipt names it with non-zero examined-count; point it at a non-existent target → it does NOT report clean.

- [ ] T009 [US1] Implement `emit(check_id, area, target, examined_count, total_count, problems, run_id)` in `receipt.py`: classify outcome + write receipt + return pointer (FR-001/002/003)
- [ ] T010 [US1] Unresolved-target path in `emit()`: `resolved=False` ⇒ `UNSEARCHABLE`, never clean; names what was sought and where (US1 scenario 2, FR-011)
- [ ] T011 [US1] Explicit-zero rendering: `examined_count == 0` is attributed, never rendered "clean"/"0 findings" unqualified (US1 scenario 4)
- [ ] T012 [US1] Implement `consumer.read(verdict)` in `codeconv/src/codeconv/receipts/consumer.py`: refuse a verdict lacking a conforming receipt; absent/malformed ⇒ UNREAD (FR-008, contract C1/C3)
- [ ] T013 [P] [US1] Tests `codeconv/tests/test_receipt_us1.py`: receipt names target + count; non-existent target not clean; verdict-without-receipt refused; zero explicit (US1 scenarios 1–4)

**Checkpoint**: US1 independently demonstrable — earned vs unearned greens are now distinguishable.

---

## Phase 4: User Story 2 — EMPTY, UNREAD, UNSEARCHABLE never collapse (P1)

**Goal**: the three "nothing found" situations produce three distinct, named, non-collapsing outcomes; none renders as success.
**Independent test**: drive one check into each of the three states (empty dir / partial cursor / absent path) → three distinct outcomes, none a pass.

- [ ] T014 [US2] Three-way classification in `receipt.py`: EMPTY vs UNREAD vs UNSEARCHABLE from `resolved` + examined/total (FR-006, research D4)
- [ ] T015 [US2] Skipped-item accounting in `emit()`: `skipped` list with reasons + `skipped_total`; verdict never a clean pass on skipped items' behalf (US2 scenario 4, FR-002)
- [ ] T016 [US2] Partial-run handling: examined and unexamined both stated; a partial/crashed run never presents as whole (US2 scenario 5, edge: crash mid-run)
- [ ] T017 [US2] Aggregate propagation in `consumer.py`: parent = worst-of children; no clean parent while any child is UNREAD/UNSEARCHABLE (FR-009, contract C2) — closes instance 13
- [ ] T018 [P] [US2] Tests `codeconv/tests/test_receipt_us2.py`: three distinct non-success outcomes; skipped counted; partial stated; aggregate propagation (US2 scenarios 1–5)

**Checkpoint**: the receipt is now load-bearing, not decorative.

---

## Phase 5: User Story 3 — proven by fault injection (P2) — MVP PROOF

**Goal**: deliberately induce each silent-success mode against a reference check and assert a loud refusal; the suite is subject to its own invariant.
**Independent test**: run the fault-injection suite → every injected fault produces a loud named refusal; a clean pass fails the suite.

- [ ] T019 [US3] Purpose-built reference check in `codeconv/tests/faultinj/reference_check.py` — a real emitting check the MVP is proven against (research D8)
- [ ] T020 [US3] Per-run ExpectedSet + missing-check detection in `codeconv/src/codeconv/receipts/manifest.py`: `declare_expected()` + `missing_checks()`; a run with no `expected.json` refuses (FR-013/023, contract M2)
- [ ] T021 [US3] Adoption-manifest loader + absence-is-error in `manifest.py`; read `.specify/receipts/adoption.json`; unlisted area ⇒ refuse + name missing declaration (FR-019/020/021, contract M1)
- [ ] T022 [US3] Override binding in `codeconv/src/codeconv/receipts/override.py`: informed-consent shape (briefing/ack/rationale/scope/mandatory-expiry); inert outside scope/expiry; stays visible in receipt (FR-012, research D6)
- [ ] T023 [P] [US3] Fault test `codeconv/tests/faultinj/test_removed_target.py`: deliberately-removed target ⇒ UNSEARCHABLE; clean pass fails suite (US3.1)
- [ ] T024 [P] [US3] Fault test `codeconv/tests/faultinj/test_suppressed_block.py`: suppressed output block ⇒ UNREAD, not 0-findings (US3.2, instance 2)
- [ ] T025 [P] [US3] Fault test `codeconv/tests/faultinj/test_no_receipt.py`: consumer fed a receiptless verdict refuses (US3.3)
- [ ] T026 [P] [US3] Fault test `codeconv/tests/faultinj/test_wrong_dir.py`: check run from wrong working location ⇒ target mismatch detected before any verdict (US3.4, instance 9)
- [ ] T027 [P] [US3] Fault test `codeconv/tests/faultinj/test_falsified_count.py`: examined-count falsified to exceed total ⇒ rejected (US3.5)
- [ ] T028 [US3] Suite self-invariant: the fault-injection suite `check_id` is in the run's ExpectedSet so its own non-execution is loud (FR-016, US3.6)
- [ ] T029 [US3] Conformance-fixture runner in `codeconv/tests/faultinj/test_conformance.py`: run the buildkit fixture against the Python emitter (and the bash emitter once T031 lands); assert its output is itself a valid receipt (FR-024, contract F1–F4)
- [ ] T030 [US3] SC-007 guard-weakening test: deliberately weaken one guard and confirm the suite goes red (SC-007)

**Checkpoint — MVP COMPLETE**: mechanism + three-way distinction + fault-injection proof, all on the reference check. **This is the first SHIP-TOKEN increment.** STOP here for the SHIP-TOKEN before shipping.

---

## Phase 6: User Story 4 — witnessed sites adopt receipts (P3) — POST-MVP, INCREMENTAL

**Goal**: retrofit the real declared areas so historical failures cannot recur; each site independently demonstrable. Ships incrementally after the MVP; the adoption manifest reports honest partial coverage throughout.

- [ ] T031 [US4] Thin bash emitter `test/receipts/emit_receipt.sh` writing schema-conforming JSON (research D1)
- [ ] T032 [US4] test-harness retrofit in `test/run_all_tests.sh`: skip-guards emit skipped + reason via T031; an unsupported-platform link is UNREAD/skipped-qualified, never passed-by-skip (instance 5)
- [ ] T033 [US4] build-gate retrofit in `codeconv/src/codeconv/tools/codegen/buildgate.py`: a clean compile with zero tests ran ⇒ EMPTY-qualified/UNREAD, never a silent behavioural PASS (instance 6/CD-03)
- [ ] T034 [US4] roadmap-sync retrofit: `reconcile` consults `link`'s outcome; the aggregate cannot be clean over a non-success constituent (instance 13, FR-009)
- [ ] T035 [US4] coop-protocol retrofit: poll/cursor emits UNREAD on unread mail, never EMPTY (instance 8); document the coop receipt convention in `docs/receipts/coop.md`
- [ ] T036 [US4] Flip each area's `state` to `adopted` in `.specify/receipts/adoption.json` as its retrofit lands; adoption report states per-area coverage explicitly (FR-017/018)
- [ ] T037 [US4] Cross-repo coordination: re-pin `bind.py` to the released buildkit contract version (replacing the T005 draft); confirm the 3rtask + codexreview adoptions delivered by the buildkit lane; recorded as a COOP edge (FR-024)

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T038 [P] Bounded-receipt edge tests `codeconv/tests/test_bounded_receipt.py`: large target caps enumerations, true totals preserved, truncation self-declared (FR-005, edge: receipt volume)
- [ ] T039 [P] Success-criteria harness `codeconv/tests/test_success_criteria.py`: SC-001 (13/13 reproduced as faults), SC-002 (100% of areas via manifest), SC-005 (unresolvable ⇒ non-success 100%), SC-008 (identify unearned green < 5 min) — measured + reported
- [ ] T040 [US3] SC-003 blind-reader gate: a blind reader in a fresh context (no answer key) classifies 20/20 sample verdicts drawn ONLY from produced receipts; corroborated once by a blind cross-lane reader (SC-003)
- [ ] T041 [P] Validate `quickstart.md` end-to-end against the implementation; correct any drift
- [ ] T042 [P] Baseline regression: `bash test/run_all_tests.sh` green — receipts are additive, no REPL suite regression (Constitution VII)

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2, blocking)** → **US1/US2 (P1)** → **US3 (P2)** → *[MVP ship gate]* → **US4 (P3)** → **Polish**.
- T004 blocks T006/T007/T009. T005 blocks T007/T008. T006 blocks T007/T008/T009. T008 blocks T009/T020. T007 blocks T012. T009 blocks T014.
- US1 and US2 both extend the same `receipt.py`/`consumer.py` — sequence T009→T014, but their **test** tasks (T013, T018) are [P] against each other.
- US3 depends on US1+US2 landing (needs a working emitter + consumer to inject faults into).
- US4 tasks (T031–T035) are mutually [P] once the MVP mechanism exists; T037 depends on the buildkit companion landing (external).

## Parallel Opportunities

- Setup: T002, T003 in parallel.
- US3 fault tests: T023, T024, T025, T026, T027 all [P] (separate files).
- Polish: T038, T039, T041, T042 all [P].

## Implementation Strategy (MVP first)

1. **First increment (ships at first SHIP-TOKEN)**: Phases 1–5 → mechanism + three-way distinction + fault-injection proof on the reference check. Unblocks the 5 downstream features (#24/#30/#34/#35/#59).
2. **Subsequent increments**: Phase 6 retrofits one area at a time (each reproducing its historical instance), flipping adoption; Phase 7 closes SC-001/SC-002/SC-003 across the real corpus. T037 waits on the buildkit companion change.

**Total: 42 tasks** — Setup 3 · Foundational 5 · US1 5 · US2 5 · US3 12 · US4 7 · Polish 5.
