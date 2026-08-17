<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Tasks: madGLP writer-reader address-discipline closure (N/N+1 audit + residuals)

**Input**: Design documents from `specs/079-madglp-writer-reader-discipline/`
**Prerequisites**: plan.md, spec.md, research.md (R-1 scope split), data-model.md, contracts/reader-resolution.md, quickstart.md

**Tests**: Verification tasks (baseline parity + fault-injection) are included because the spec
explicitly requires them (FR-003, SC-001, SC-002). No new greenfield test scaffolding — the existing
multiagent Dart suite + REPL suite are the harness.

**Organization**: Grouped by user story. US1 (P1) is the core `heap_fcp.dart` touch and carries a
🔴 STOP-and-report gate (R-1b) and a SHIP-TOKEN requirement. US2 (P2) and US3 (P2) are independent
clean audit-closes on non-core files and may proceed in parallel with US1.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3 (maps to spec.md user stories)
- Env: `export DART=C:/src/flutter/bin/cache/dart-sdk/bin/dart.exe`

---

## Phase 1: Setup (Shared)

**Purpose**: Confirm the working context matches the audit assumptions.

- [ ] T001 Confirm on branch `079-madglp-writer-reader-discipline`, tree clean, and `export DART=C:/src/flutter/bin/cache/dart-sdk/bin/dart.exe` per `specs/079-madglp-writer-reader-discipline/quickstart.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Record the pre-change baseline (FR-003 / R-4). The behaviour-preserving guarantee
(SC-002) is measured against these counts — every story's verify step compares to them.

**⚠️ CRITICAL**: No implementation task may land before both baselines are recorded.

- [ ] T002 Record multiagent Dart baseline: `cd glp_runtime && "$DART" test test/multiagent/` — note the pass/fail count (FR-003, SC-002)
- [ ] T003 Record REPL suite baseline: `bash test/run_all_tests.sh` — note pass count; the Section T abort is the known 064 unguarded-abort (PR #158, orthogonal — not a regression)

**Checkpoint**: Baselines captured — implementation may begin.

---

## Phase 3: User Story 1 - Last convention-dependent fallback removed; broken cross-pointer fails loud (Priority: P1) 🎯 MVP

**Goal**: `pairedReaderAddr()` resolves the reader **only** via the authoritative bidirectional
cross-pointer for both unbound and bound writers, and fails loud (never `writerAddr + 1`) when the
cross-pointer is genuinely absent.

**Independent Test**: With cross-pointers intact, the full multiagent + REPL suites pass at baseline
(the authoritative path returns before the old fallback). A fault-injection that removes a
cross-pointer produces a loud diagnostic naming the writer address instead of a `+1` guess.

**🔴 Core-touch discipline (FR-008, CLAUDE.md IV-b)**: `heap_fcp.dart` is a protected core file.
Audit first; surface the diff explicitly; NEVER remove `_ClauseVar`/`_TentativeStruct`/allocation
invariants without approval. This story does not ship without a SHIP-TOKEN.

### Audit (read-only — must precede any edit)

- [ ] T004 [US1] Classify every `pairedReaderAddr` call site in `glp_runtime/lib/bytecode/runner.dart` (:411,1059,1724,1869,1880,1938,1949,2009,2036,2047,2065) as passing a bound vs unbound writer (FR-004, SC-003) — read-only, record the classification
- [ ] T005 [US1] Read `glp_runtime/lib/runtime/heap_fcp.dart` `readerForWriter` (:199, null on bound Case 3 :224) and `pairedReaderAddr` (:236, `+1` at :242); confirm whether a bound-writer cross-pointer reader accessor (R-1a) is addable without a heap-format/`_ClauseVar`/`_TentativeStruct`/allocation change (FR-008, FR-009)

### 🔴 Decision gate

- [ ] T006 [US1] **GATE**: If T005 shows R-1a needs a heap cell-format or allocation-invariant change → **STOP-and-report to Gabi + propose an FR-002 spec revision (R-1b)**; do NOT implement. Otherwise proceed to T007. (Per contracts/reader-resolution.md Escalation.)

### Implementation for User Story 1 (only past the gate)

- [ ] T007 [US1] Implement R-1a in `glp_runtime/lib/runtime/heap_fcp.dart`: add the bound-aware cross-pointer reader accessor (mirror `WriterContent.readerAddr` for the bound case), route `pairedReaderAddr` through it for bound + unbound writers, and delete the `writerAddr + 1` fallback (FR-001) — *MVP alternative if R-1a is deferred: keep the fallback but ASSERT/log when it fires on an UNBOUND writer, closing the silent-guess hazard (contracts/reader-resolution.md §MVP)*
- [ ] T008 [US1] Make `pairedReaderAddr` raise a loud, diagnosable error naming `writerAddr` when the cross-pointer cannot resolve (FR-002, SC-001) in `glp_runtime/lib/runtime/heap_fcp.dart`
- [ ] T009 [US1] Surface the `heap_fcp.dart` core diff explicitly to Gabi (FR-008) and record the SHIP-TOKEN prerequisite before any /bk-ship

### Verify User Story 1

- [ ] T010 [US1] Fault-injection check in `glp_runtime/test/multiagent/`: remove/corrupt a cross-pointer and assert `pairedReaderAddr` fails loud (naming the writer addr), never returns `+1` (SC-001)
- [ ] T011 [US1] Re-run `cd glp_runtime && "$DART" test test/multiagent/` and `bash test/run_all_tests.sh`; confirm == T002/T003 baseline (SC-002, FR-003)

**Checkpoint**: US1 done — the last silent fallback is gone; behaviour preserved at baseline. MVP reached.

---

## Phase 4: User Story 2 - three_agent_pipeline_boot false-positive verified and retired (Priority: P2)

**Goal**: Drive the globalise/send residual to a documented verdict — live defect (repro filed) or
false positive (retired) — never "unverified".

**Independent Test**: `three_agent_pipeline_boot` runs to a deterministic outcome; its status in
`docs/bug-send-globalise-localise.md` reads a definitive verdict. Independent of US1 and US3.

- [ ] T012 [US2] Run `three_agent_pipeline_boot` deterministically via `glp_runtime/test/multiagent/multiagent_glp_test.dart` and capture the outcome
- [ ] T013 [US2] Write the verdict into `docs/bug-send-globalise-localise.md`: "verified live defect (repro filed)" — filing the repro — **or** "false positive, retired" (FR-005, SC-004)

**Checkpoint**: US2 done — the test hazard is no longer ambiguous.

---

## Phase 5: User Story 3 - readerAddr renamed/re-described to match its onBind writer key (Priority: P2)

**Goal**: `GlobalSendSpawn.readerAddr` name + doc comment describe the onBind writer key it actually
holds; the Issue-1 doc header/body inconsistency is resolved. All in freely-modifiable
`lib/multiagent/` + docs.

**Independent Test**: a reader of `mad_helpers.dart` cannot mistake the field for "the reader to
watch"; all references compile and the multiagent suite is green. Independent of US1 and US2.

- [ ] T014 [US3] Read all `GlobalSendSpawn.readerAddr` references in `glp_runtime/lib/multiagent/` to confirm the field carries an onBind writer key (data-model.md R-3)
- [ ] T015 [US3] Rename the field + rewrite the doc comment at `glp_runtime/lib/multiagent/mad_helpers.dart:61-64` to onBind-writer-key wording (e.g. `onBindWriterAddr`); update all references consistently (FR-006)
- [ ] T016 [P] [US3] Correct the Issue-1 header/body "Open" vs "Fixed" inconsistency in `docs/bug-send-globalise-localise.md` (FR-007) — parallel with T015 (different file)
- [ ] T017 [US3] Re-run `cd glp_runtime && "$DART" test test/multiagent/`; confirm green after the rename (compile + baseline)

**Checkpoint**: US3 done — the field no longer lies about its contents.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T018 Run `specs/079-madglp-writer-reader-discipline/quickstart.md` end-to-end as the final validation pass
- [ ] T019 [P] Confirm SC-001..SC-005 are all met; update `spec.md` Status accordingly
- [ ] T020 Final baseline-parity re-run of both suites; stage only this feature's files (`glp_runtime/lib/runtime/heap_fcp.dart`, `glp_runtime/lib/multiagent/mad_helpers.dart`, `docs/bug-send-globalise-localise.md`, `glp_runtime/test/multiagent/*`, spec docs) — no `git add -A`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)** → **Foundational (P2, baselines)** blocks all verify comparisons.
- **US1 (Phase 3)** depends on baselines (T002/T003) and its own internal audit gate (T004→T005→T006).
  T007+ land only past the T006 gate.
- **US2 (Phase 4)** and **US3 (Phase 5)** depend only on baselines — independent of US1 and of each
  other (disjoint files: US2 = docs + test run; US3 = `mad_helpers.dart` + docs).
- **Polish (Phase 6)** depends on all stories intended for this increment.

### 🔴 Recommended execution order (plan.md risk-first, for a quick green)

Land **US3 → US2 → US1**, not strict priority order:
1. **US3** (T014–T017) — lowest risk, `lib/multiagent/` + docs only → quick green.
2. **US2** (T012–T013) — clean verdict, no core risk.
3. **US1** (T004–T011) — the core `heap_fcp.dart` touch, last, behind the STOP gate + SHIP-TOKEN.

This front-loads risk-free wins and isolates the one core-touch to a clean tree.

### Parallel Opportunities

- US2 and US3 can run fully in parallel with each other and with US1's read-only audit (T004/T005).
- T016 [P] (doc fix) runs parallel with T015 (field rename) — different files.
- No two implementation tasks touch the same file concurrently.

---

## Implementation Strategy

### MVP scope

**MVP = Setup + Foundational + US3 + US2 + US1's audit + (R-1a OR the documented R-1b STOP-report).**
US1's R-1a is the structural fix; if the T006 gate trips, the MVP is satisfied by the documented
STOP-report + FR-002 revision (the audit itself closes SC-003/SC-004/SC-005), and R-1a is deferred to
a follow-up increment.

### Incremental delivery

1. Baselines → US3 (quick green) → commit.
2. US2 verdict → commit.
3. US1 audit → gate → R-1a (surface core diff, acquire SHIP-TOKEN) → fault-injection → baseline parity.
4. Polish + final parity → /bk-analyze → (SHIP-TOKEN) → /bk-ship → /bk-close.

### Notes

- Commit after each task or logical group; commit-scoped by filename (CLAUDE.md Git Workflow).
- 🔴 Do not remove `_ClauseVar`/`_TentativeStruct`/fallback branches without approval; T006 is the
  hard gate that enforces this.
- SHIP-TOKEN is required before /bk-ship because a core file (`heap_fcp.dart`) is touched.
