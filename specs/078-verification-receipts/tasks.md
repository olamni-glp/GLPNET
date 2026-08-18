<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 2b6d4f81-9e07-4a35-8c62-71d0a4e39b57
-->

# Tasks: Verification receipts and loud failure

**Feature**: `078-verification-receipts` | **Date**: 2026-08-18
**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [data-model.md](./data-model.md),
[research.md](./research.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests are IN SCOPE and mandatory.** US3 is an acceptance suite, FR-014/FR-015 require a
fault-injection test per silent-success mode, and FR-016 subjects that suite to its own invariant.
This is not a TDD preference — it is a requirement of the feature.

## Repository prefixes

This feature implements across two repositories (plan → Delivery sequencing, register block 51).
Every path is prefixed so no task is ambiguous about where it lands:

- **`bk:`** → buildkit, worktree `D:\bstdev\research\bk-wt\glpnet-lane-fixes`, branch
  `glpnet-lane/toolchain-integrity-fixes`
- **`gn:`** → glpnet, branch `078-verification-receipts`

**Wave gate (block 51):** every `gn:` task in Phase 4 onward depends on a **released** buildkit
version, never on its branch. A `gn:` task that reads buildkit source directly reintroduces the
copy-divergence FR-024 forbids.

---

## Phase 1 — Setup

- [ ] T001 Create the receipts package skeleton with SPDX header and file-id in `bk:src/buildkit_cli/receipts/__init__.py`
- [ ] T002 [P] Create the test package in `bk:tests/receipts/__init__.py`
- [ ] T003 [P] Copy the three normative schemas from `gn:specs/078-verification-receipts/contracts/*.json` into `bk:src/buildkit_cli/receipts/schemas/` and record in the module docstring that glpnet's `contracts/` is the spec-side copy of record while the packaged copy is what ships
- [ ] T004 [P] Add `.gitattributes` entries pinning `text eol=lf` on `bk:src/buildkit_cli/receipts/schemas/*.json` (research R8 — a byte-compared contract with unpinned line endings fails verification for reasons unrelated to the check)

## Phase 2 — Foundational (BLOCKS every user story)

**W1 contract. Nothing in Phase 3+ can start until this phase is green.**

- [ ] T005 Implement `Outcome` as a closed five-value enum with the `PASS`/`EMPTY` success predicate in `bk:src/buildkit_cli/receipts/model.py` (FR-006, FR-007; data-model §1)
- [ ] T006 Implement the `Receipt`, `Skip`, `Truncation` and `Override` dataclasses in `bk:src/buildkit_cli/receipts/model.py` (data-model §2, §3, §6) — pure data, no I/O
- [ ] T007 [P] Implement bounding in `bk:src/buildkit_cli/receipts/bound.py`: cap enumerations, **never** cap `*_total` counters, byte-backstop each field, emit a self-declared `Truncation` (FR-005, research R5)
- [ ] T008 [P] Implement run-id generation `<UTC ts>-<8 hex>` in `bk:src/buildkit_cli/receipts/model.py` (research R2) — generated at check START so a crashed run still has an addressable receipt
- [ ] T009 Implement `emit()` writing to `.specify/receipts/<area>/<run-id>.json` with LF endings in `bk:src/buildkit_cli/receipts/emit.py` (FR-022) — write-once per `(area, run_id)`, never mutate (constitution VI-a)
- [ ] T010 Implement schema validation of a receipt against `receipt.schema.json` in `bk:src/buildkit_cli/receipts/verify.py` (FR-004)
- [ ] T011 Implement `AdoptionManifest` load + validate in `bk:src/buildkit_cli/receipts/manifest.py` — a manifest missing any of the six areas is INVALID, not partial (FR-019, FR-020, FR-021)
- [ ] T012 Implement `ExpectedChecks` load + validate in `bk:src/buildkit_cli/receipts/manifest.py` — a run with no manifest refuses; it is not a run in which nothing was expected (FR-023)
- [ ] T013 Implement the conformance runner in `bk:src/buildkit_cli/receipts/conformance.py` — runs `contracts/conformance/vectors.json` and **emits its own result as a receipt** (FR-024)
- [ ] T014 [P] Unit tests for `Outcome`, bounding and run-id in `bk:tests/receipts/test_model.py` — assert `skipped_total` survives truncation of `skipped` (the FR-005/FR-010 tension)
- [ ] T015 [P] Conformance test asserting all 7 vectors behave as declared (2 accept, 5 reject) in `bk:tests/receipts/test_conformance.py`

**Checkpoint:** contract green. **Release a buildkit version here** — Phase 4+ pins it (block 51 W1 gate).

---

## Phase 3 — US1: A check proves it ran (P1) 🎯 MVP

**Goal:** every check emits a receipt naming the resolved target and a real examined-count; a verdict
without a receipt is refused rather than treated as a pass.

**Independent test:** run a receipt-emitting check against a known target — the receipt names that
target with a non-zero examined-count. Point the same check at a non-existent target — it does **not**
report clean.

- [ ] T016 [P] [US1] Test: a receipt records `target_resolved` as ACTUALLY resolved, not as requested, in `bk:tests/receipts/test_us1_proof.py` (FR-003)
- [ ] T017 [P] [US1] Test: a verdict with no receipt is refused as incomplete, not treated as a pass, in `bk:tests/receipts/test_us1_proof.py` (FR-008)
- [ ] T018 [P] [US1] Test: an examined-count of zero is explicit and attributed, never rendered as "clean" or "0 findings", in `bk:tests/receipts/test_us1_proof.py`
- [ ] T019 [US1] Implement `verify()` as the FR-008 consumer gate in `bk:src/buildkit_cli/receipts/verify.py` — refuse on absent receipt, malformed receipt, or an area unlisted in the adoption manifest, each with a distinct named reason (FR-008, FR-011, FR-020)
- [ ] T020 [US1] Implement unresolved-target detection in `bk:src/buildkit_cli/receipts/verify.py` — a check whose target cannot be resolved reports `UNSEARCHABLE` naming what it looked for and where (FR-011)
- [ ] T021 [US1] Wire `emit()` into the conformance runner as the first real producer in `bk:src/buildkit_cli/receipts/conformance.py` — a real emitter, not a synthetic one

**Checkpoint:** US1 independently testable and deliverable on its own.

---

## Phase 4 — US2: EMPTY, UNREAD and UNSEARCHABLE never collapse (P1)

**Goal:** the three "nothing found" situations are three named outcomes, and only `EMPTY` is a pass.

**Independent test:** drive one check into each of the three states using naturally-occurring targets
(an empty directory, a partially-consumed cursor, an absent path) — three distinct outcomes, none
reported as success.

- [ ] T022 [P] [US2] Test: a fully-examined empty target yields `EMPTY` and is a legitimate pass, in `bk:tests/receipts/test_us2_distinction.py` (FR-006)
- [ ] T023 [P] [US2] Test: items beyond the examined range yield `UNREAD`, not a pass, stating how many were left unexamined, in `bk:tests/receipts/test_us2_distinction.py`
- [ ] T024 [P] [US2] Test: an unexaminable target yields `UNSEARCHABLE` naming the reason, in `bk:tests/receipts/test_us2_distinction.py`
- [ ] T025 [P] [US2] Test: skipped items are counted with reasons and the verdict cannot be a clean pass on their behalf, in `bk:tests/receipts/test_us2_distinction.py` (FR-002)
- [ ] T026 [US2] Implement the `EMPTY` earning rule in `bk:src/buildkit_cli/receipts/verify.py` — `EMPTY` requires `examined_total == target_total`; an `EMPTY` that cannot demonstrate full examination is `UNREAD` (data-model §1)
- [ ] T027 [US2] Implement aggregate propagation in `bk:src/buildkit_cli/receipts/verify.py` — a parent cannot report clean while any child is `UNREAD` or `UNSEARCHABLE` (FR-009)
- [ ] T028 [US2] Implement count reconciliation in `bk:src/buildkit_cli/receipts/verify.py` — refuse when `examined_total + skipped_total > target_total` (FR-010)
- [ ] T029 [US2] Implement crash handling in `bk:src/buildkit_cli/receipts/emit.py` — `ended_at` null forbids `PASS`/`EMPTY`; a partial run never presents as a whole one (Edge Cases)

**Checkpoint:** US1 + US2 together make the receipt load-bearing rather than decorative.

---

## Phase 5 — US3: The guards are proven by fault injection (P2)

**Goal:** every silent-success mode is deliberately induced and asserted to refuse loudly. Without
this phase the feature is itself a verification mechanism nobody verified.

**Independent test:** run the suite — every injected fault produces a loud, named refusal; a clean
pass under injection fails the suite.

- [ ] T030 [US3] Create the fault-injection harness with one injector per witnessed instance in `bk:tests/receipts/test_fault_injection.py` (FR-014)
- [ ] T031 [P] [US3] Inject a deliberately-removed target; assert refusal, not a clean pass (instance 9/10) in `bk:tests/receipts/test_fault_injection.py`
- [ ] T032 [P] [US3] Inject a suppressed output block; assert it is detected as `UNREAD`, not read as zero findings (instance 2) in `bk:tests/receipts/test_fault_injection.py`
- [ ] T033 [P] [US3] Inject a verdict with no receipt into a consumer; assert refusal (instance 1) in `bk:tests/receipts/test_fault_injection.py`
- [ ] T034 [P] [US3] Inject a wrong working location; assert target mismatch is detected BEFORE any verdict is issued (instance 9) in `bk:tests/receipts/test_fault_injection.py`
- [ ] T035 [P] [US3] Inject a falsified examined-count exceeding the target's true size; assert the inconsistency is detected (FR-010) in `bk:tests/receipts/test_fault_injection.py`
- [ ] T036 [P] [US3] Inject a retired root; assert `UNSEARCHABLE` rather than *0 actors, empty board, exit 0* (instance 10) in `bk:tests/receipts/test_fault_injection.py`
- [ ] T037 [P] [US3] Inject an aggregate reporting success over an `UNREAD` child (instances 11, 13) in `bk:tests/receipts/test_fault_injection.py`
- [ ] T038 [P] [US3] Inject a guard that passes on its own failing case (instance 12 — the sharpest one) in `bk:tests/receipts/test_fault_injection.py`
- [ ] T039 [US3] Implement the suite's self-check so its own non-execution is loud (FR-016) in `bk:tests/receipts/test_fault_injection.py` — the suite is subject to its own invariant
- [ ] T040 [US3] Implement the `BUILDKIT_RECEIPTS_WEAKEN` hook and a test proving the suite goes RED when a guard is deliberately weakened (SC-007) in `bk:tests/receipts/test_fault_injection.py`
- [ ] T041 [US3] Implement the scoped, expiring override path in `bk:src/buildkit_cli/receipts/verify.py` — briefing, acknowledgement, rationale, scope and mandatory expiry; no indefinite override; visible in the receipt forever (FR-012)

**Checkpoint:** the feature is now self-consistent — the mechanism has been made to fail on purpose.

---

## Phase 6 — US4: The witnessed defect sites adopt receipts (P3)

**Goal:** retrofit the real sites so the historical failures cannot recur, reporting adoption honestly
including non-adoption.

**Independent test:** for each retrofitted site, reproduce its historical failure and confirm it now
surfaces loudly.

**Sequencing note (research R4):** the bash test harness carries **5 of the 13** witnessed instances
and is the only runtime that cannot import the Python emitter, so it adopts FIRST — if the contract
cannot be emitted from bash, that must surface before five Python areas are built against it.

- [ ] T042 [US4] Implement the bash emitter (`receipt_start` / `receipt_examined` / `receipt_skip` / `receipt_emit`) in `gn:test/receipts/emit.sh` — writes the same document as the Python emitter, LF endings (block 46, FR-022)
- [ ] T043 [US4] Implement harness-side assertions in `gn:test/receipts/assert.sh`
- [ ] T044 [US4] Run the conformance vectors from bash and assert 7/7 parity with the Python emitter in `gn:test/receipts/assert.sh` (FR-024) — this is what keeps two emitters honest
- [ ] T045 [US4] Emit per-section receipts keyed `(letter, slugified-title)` in `gn:test/run_all_tests.sh` — **NOT** by letter alone: `Section I` is declared twice, at lines 1653 and 2219, and letter-keying would make one receipt silently overwrite the other (research R3, register block 06)
- [ ] T046 [US4] Add the skip-guard fix in `gn:test/run_all_tests.sh` — an unsupported-platform skip is recorded as skipped with a reason, never `passed-by-skip` (instance 5)
- [ ] T047 [US4] Add a build-staleness check to Section U in `gn:test/run_all_tests.sh` — compare exe mtime against source and report `UNSEARCHABLE` on a stale binary rather than presenting a build defect as a feature defect (the 37h-stale-binary case)
- [ ] T048 [P] [US4] Author the adoption manifest enumerating all six areas with state and date in `gn:.specify/receipts/adoption-manifest.json` (FR-019)
- [ ] T049 [P] [US4] Author the expected-checks manifest for the suite run in `gn:.specify/receipts/expected-checks.json` (FR-023)
- [ ] T050 [US4] Retrofit `roadmap-sync` reconcile + import to emit receipts in `bk:src/buildkit_cli/roadmap/` (instances 4, 13)
- [ ] T051 [P] [US4] Retrofit `buildkit-3rtask` `brief`/`record-output` to emit receipts in `bk:src/buildkit_cli/threerole/` (instance 3)
- [ ] T052 [P] [US4] Retrofit `buildkit-codexreview` to emit a receipt carrying its findings count in `bk:src/buildkit_cli/codexreview/` (instances 1, 2)
- [ ] T053 [P] [US4] Retrofit the COOP poll/cursor path to emit receipts distinguishing an unread mailbox from an empty one in `bk:src/buildkit_cli/colab/` (instance 8)
- [ ] T054 [P] [US4] Retrofit the codeconv build gate to report `UNREAD` when it is compile-only (instance 6) in `gn:codeconv/src/codeconv/`
- [ ] T055 [US4] Implement the adoption report in `bk:src/buildkit_cli/receipts/manifest.py` — per-area coverage stated explicitly; an area absent from the manifest is an ERROR, printed as such, never omitted (FR-018, FR-020)

**Checkpoint:** all 13 witnessed instances have both a fault injector and a retrofitted site.

---

## Phase 7 — Polish & cross-cutting

- [ ] T056 [P] Measure SC-001: assert all 13 instances produce a loud named refusal (13 of 13) in `bk:tests/receipts/test_success_criteria.py`
- [ ] T057 [P] Measure SC-002 with FR-019's enumeration as the denominator in `bk:tests/receipts/test_success_criteria.py` (FR-021)
- [ ] T058 [P] Measure SC-004: zero outcomes render `UNREAD`/`UNSEARCHABLE` as success, by fault injection across every check in scope, in `bk:tests/receipts/test_success_criteria.py`
- [ ] T059 [P] Measure SC-005 and SC-006 in `bk:tests/receipts/test_success_criteria.py`
- [ ] T060 Implement the SC-003 blind-reader harness over 20 samples drawn from REAL receipts in `bk:tests/receipts/test_blind_reader.py` — cadence per register block 50 (recommended per-release)
- [ ] T061 [P] Document emit/consume/inject in `gn:specs/078-verification-receipts/quickstart.md` — verify every command in it actually runs (a quickstart that has never been executed is an unearned green)
- [ ] T062 Resolve register block 49 before merge — make `gn:specs/078-verification-receipts/plan.md`, `gn:.gitattributes` and `gn:.gitignore` agree on whether `.specify/receipts/` is tracked; they currently do not (plan says gitignored, the eol pin assumes tracked, and the path is in fact neither)

---

## Dependencies

```
Phase 1 Setup
    └─> Phase 2 Foundational (W1 contract)  ── RELEASE a buildkit version here
            ├─> Phase 3 US1 (P1, MVP)
            │       └─> Phase 4 US2 (P1)
            │               └─> Phase 5 US3 (P2)
            │                       └─> Phase 6 US4 (P3)
            │                               └─> Phase 7 Polish
            └─> (gn: tasks pin the RELEASED version, never the branch — block 51)
```

**Story independence.** US1 delivers standalone. US2 requires US1's receipt to exist. US3 requires
US1+US2 to have something to inject against. US4 is breadth over the mechanism and can land
incrementally, site by site.

## Parallel execution

- **Phase 2:** T007, T008 in parallel; T014, T015 in parallel once T005–T013 land.
- **Phase 3:** T016, T017, T018 in parallel (same file, distinct tests — serialise if editing conflicts).
- **Phase 4:** T022–T025 in parallel.
- **Phase 5:** T031–T038 in parallel — eight independent injectors.
- **Phase 6:** T048, T049 in parallel; T051–T054 in parallel (four different packages). T042→T045 are
  strictly ordered: the bash emitter must exist before the harness can emit.
- **Phase 7:** T056–T059 and T061 in parallel.

## Implementation strategy

**MVP = Phase 1 + Phase 2 + Phase 3.** That delivers a working receipt, an addressable location and a
consumer that refuses a verdict lacking one — converting instances 1, 2, 9, 10 and 11 from silent to
visible on its own.

**Then Phase 4** makes the receipt load-bearing rather than decorative, **Phase 5** proves it by making
it fail on purpose, and **Phase 6** applies it to the 13 real sites.

**Two ship waves (block 51):** Phases 1–5 release from buildkit (W1); Phase 6 onward is glpnet pinned
to that release (W2/W3). The marathon's single `/bk-ship 078` discharge item is satisfied by **both**
releases, not by one invocation.

## Task counts

| phase | tasks | story |
|---|---|---|
| 1 Setup | 4 | — |
| 2 Foundational | 11 | — |
| 3 US1 | 6 | P1 (MVP) |
| 4 US2 | 8 | P1 |
| 5 US3 | 12 | P2 |
| 6 US4 | 14 | P3 |
| 7 Polish | 7 | — |
| **total** | **62** | |
